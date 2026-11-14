using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LauncherP5XFr.Helpers;
using LauncherP5XFr.Models;

namespace LauncherP5XFr.Services;

public interface IGitHubUpdateService
{
    TranslationVersionInfo? GetLocalTranslationVersion(string? gameDir);
    Task<(bool isUpdateAvailable, GitHubReleaseDto? release, string? statusMessage)> CheckTranslationUpdateAsync(string? gameDir, CancellationToken ct = default);
    Task UpdateTranslationAsync(string gameDir, GitHubReleaseDto release, IProgress<DownloadProgressInfo> progress, CancellationToken ct = default);
    Task RollbackTranslationAsync(string gameDir);
    bool HasBackup(string? gameDir);
}

public sealed class GitHubUpdateService : IGitHubUpdateService
{
    private static readonly Lazy<GitHubUpdateService> _instance = new(() => new GitHubUpdateService());
    public static GitHubUpdateService Instance => _instance.Value;

    private readonly HttpClient _httpClient;
    private readonly IConfigService _configService;
    private readonly ILoggerService _logger;

    private GitHubReleaseDto? _cachedRelease;

    public GitHubUpdateService() : this(ConfigService.Instance, LoggerService.Instance)
    {
    }

    public GitHubUpdateService(IConfigService configService, ILoggerService logger)
    {
        _configService = configService;
        _logger = logger;

        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "LauncherP5XFr");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
    }

    private void ApplyAuthenticationHeaders(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(_configService.Config.GitHubToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _configService.Config.GitHubToken.Trim());
        }
    }

    public TranslationVersionInfo? GetLocalTranslationVersion(string? gameDir)
    {
        if (string.IsNullOrWhiteSpace(gameDir) || !Directory.Exists(gameDir))
            return null;

        var versionFile = Path.Combine(gameDir, "translation_version.json");
        if (!File.Exists(versionFile))
            return null;

        try
        {
            var json = File.ReadAllText(versionFile);
            return JsonSerializer.Deserialize(json, AppJsonContext.Default.TranslationVersionInfo);
        }
        catch (Exception ex)
        {
            _logger.Warn($"Erreur lors de la lecture de translation_version.json : {ex.Message}");
            return null;
        }
    }

    public async Task<(bool isUpdateAvailable, GitHubReleaseDto? release, string? statusMessage)> CheckTranslationUpdateAsync(
        string? gameDir,
        CancellationToken ct = default)
    {
        var repo = !string.IsNullOrWhiteSpace(_configService.Config.TranslationRepo) && !_configService.Config.TranslationRepo.Contains("[OWNER/REPO]")
            ? _configService.Config.TranslationRepo.Trim()
            : AppConstants.DefaultTranslationRepo;

        var localVersion = GetLocalTranslationVersion(gameDir);

        try
        {
            var url = $"https://api.github.com/repos/{repo}/releases/latest";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            ApplyAuthenticationHeaders(request);

            if (!string.IsNullOrEmpty(_configService.Config.LastETagTranslation) && _cachedRelease != null)
            {
                request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(_configService.Config.LastETagTranslation));
            }

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                _logger.Info("GitHub API: 304 Not Modified. La release en cache est à jour.");
                if (_cachedRelease != null)
                {
                    var isNewer = IsReleaseNewer(_cachedRelease.TagName, localVersion?.Tag);
                    return (isNewer, _cachedRelease, isNewer ? "Nouvelle mise à jour disponible !" : "Traduction à jour.");
                }
            }

            if (response.StatusCode == HttpStatusCode.Forbidden || (int)response.StatusCode == 429)
            {
                _logger.Warn("GitHub API : Limite de requêtes atteinte (Rate Limit).");
                return (false, _cachedRelease, "Limite de requêtes GitHub atteinte. Configurez un token dans les paramètres ou réessayez plus tard.");
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.Warn($"Aucune release trouvée pour le dépôt '{repo}'.");
                return (false, null, "Aucune release publiée sur ce dépôt GitHub.");
            }

            response.EnsureSuccessStatusCode();

            if (response.Headers.ETag != null)
            {
                _configService.Config.LastETagTranslation = response.Headers.ETag.Tag;
                _configService.Save();
            }

            var contentStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var release = await JsonSerializer.DeserializeAsync(contentStream, AppJsonContext.Default.GitHubReleaseDto, ct).ConfigureAwait(false);

            if (release == null)
            {
                return (false, null, "Réponse GitHub invalide.");
            }

            _cachedRelease = release;

            var isUpdate = IsReleaseNewer(release.TagName, localVersion?.Tag);
            var status = isUpdate 
                ? $"Mise à jour disponible : {release.TagName}" 
                : (localVersion != null ? "Traduction à jour." : "Traduction non installée.");

            return (isUpdate, release, status);
        }
        catch (HttpRequestException ex)
        {
            _logger.Warn($"Mode hors-ligne : Impossible de contacter GitHub ({ex.Message}).");
            return (false, _cachedRelease, "Mode hors-ligne : Impossible de joindre GitHub.");
        }
        catch (TaskCanceledException)
        {
            return (false, _cachedRelease, "Vérification annulée ou délai dépassé.");
        }
        catch (Exception ex)
        {
            _logger.Error("Erreur lors de la vérification de mise à jour GitHub.", ex);
            return (false, _cachedRelease, $"Erreur : {ex.Message}");
        }
    }

    private static bool IsReleaseNewer(string remoteTag, string? localTag)
    {
        if (string.IsNullOrWhiteSpace(remoteTag))
            return false;
        if (string.IsNullOrWhiteSpace(localTag))
            return true;

        var cleanRemote = remoteTag.TrimStart('v', 'V');
        var cleanLocal = localTag.TrimStart('v', 'V');

        if (Version.TryParse(cleanRemote, out var remoteVer) && Version.TryParse(cleanLocal, out var localVer))
        {
            return remoteVer > localVer;
        }

        return !string.Equals(remoteTag, localTag, StringComparison.OrdinalIgnoreCase);
    }

    public async Task UpdateTranslationAsync(
        string gameDir,
        GitHubReleaseDto release,
        IProgress<DownloadProgressInfo> progress,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(gameDir) || !Directory.Exists(gameDir))
        {
            throw new DirectoryNotFoundException("Dossier du jeu introuvable.");
        }

        // 1. Select matching asset
        var pattern = !string.IsNullOrWhiteSpace(_configService.Config.TranslationAssetPattern) 
            ? _configService.Config.TranslationAssetPattern 
            : AppConstants.DefaultTranslationAssetPattern;
        var asset = SelectMatchingAsset(release.Assets, pattern);
        if (asset == null)
        {
            throw new FileNotFoundException($"Aucun asset correspondant au motif '{pattern}' n'a été trouvé dans la release {release.TagName}.");
        }

        var tempZipPath = Path.Combine(Path.GetTempPath(), $"p5x_trans_{Guid.NewGuid():N}.zip");
        string? backupPath = null;

        try
        {
            // 2. Download asset
            _logger.Info($"Téléchargement de la traduction : {asset.Name} ({asset.Size / (1024 * 1024.0):F2} Mo)");
            await DownloadAssetWithProgressAsync(asset.BrowserDownloadUrl, asset.Size, tempZipPath, progress, ct).ConfigureAwait(false);

            // 3. Verify SHA256 if available
            progress.Report(new DownloadProgressInfo(asset.Size, asset.Size, 100, 0, "Vérification de l'intégrité SHA-256..."));
            var computedHash = ComputeSha256(tempZipPath);
            var expectedHash = ExtractSha256FromRelease(release, asset.Name);

            if (!string.IsNullOrEmpty(expectedHash))
            {
                if (!string.Equals(computedHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Échec de la vérification d'intégrité : le hash calculé ({computedHash}) ne correspond pas au hash attendu ({expectedHash}).");
                }
                _logger.Info($"Vérification SHA256 réussie : {computedHash}");
            }
            else
            {
                _logger.Info($"SHA256 calculé pour le fichier : {computedHash} (aucun hash spécifié dans la release)");
            }

            // 4. Create Backup before extraction
            progress.Report(new DownloadProgressInfo(asset.Size, asset.Size, 100, 0, "Création d'une sauvegarde de sécurité..."));
            backupPath = CreateTranslationBackup(gameDir);

            // 5. Atomic Extraction
            progress.Report(new DownloadProgressInfo(asset.Size, asset.Size, 100, 0, "Extraction des fichiers de traduction..."));
            ExtractZipSafely(tempZipPath, gameDir);

            // 6. Write version info
            var versionInfo = new TranslationVersionInfo
            {
                Tag = release.TagName,
                ReleaseName = release.Name ?? release.TagName,
                ReleaseNotes = release.Body ?? "",
                PublishedAt = release.PublishedAt,
                InstalledAt = DateTimeOffset.UtcNow,
                Sha256 = computedHash
            };

            var versionFile = Path.Combine(gameDir, "translation_version.json");
            var versionJson = JsonSerializer.Serialize(versionInfo, AppJsonContext.Default.TranslationVersionInfo);
            File.WriteAllText(versionFile, versionJson);

            // 7. Cleanup old backups (keep last 3)
            CleanupOldBackups(gameDir, 3);

            _logger.Info($"Mise à jour de la traduction vers {release.TagName} effectuée avec succès.");
            progress.Report(new DownloadProgressInfo(asset.Size, asset.Size, 100, 0, "Mise à jour terminée avec succès !"));
        }
        catch (Exception ex)
        {
            _logger.Error("Échec lors de la mise à jour de la traduction. Tentative de rollback automatique...", ex);

            if (!string.IsNullOrEmpty(backupPath) && Directory.Exists(backupPath))
            {
                try
                {
                    progress.Report(new DownloadProgressInfo(0, 0, 0, 0, "Erreur détectée. Rollback automatique en cours..."));
                    RestoreFromBackup(gameDir, backupPath);
                    _logger.Info("Rollback automatique terminé.");
                }
                catch (Exception rollbackEx)
                {
                    _logger.Error("Échec critique lors du rollback automatique.", rollbackEx);
                }
            }

            throw;
        }
        finally
        {
            if (File.Exists(tempZipPath))
            {
                try
                {
                    File.Delete(tempZipPath);
                }
                catch
                {
                    // Ignore
                }
            }
        }
    }

    private GitHubAssetDto? SelectMatchingAsset(List<GitHubAssetDto> assets, string pattern)
    {
        if (assets.Count == 0)
            return null;

        try
        {
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);
            var match = assets.FirstOrDefault(a => regex.IsMatch(a.Name));
            if (match != null)
                return match;
        }
        catch
        {
            // Fallback if regex is invalid
        }

        // Default: prefer asset containing 'translation' and ending in .zip, or first zip
        return assets.FirstOrDefault(a => a.Name.Contains("translation", StringComparison.OrdinalIgnoreCase) && a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            ?? assets.FirstOrDefault(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
    }

    private async Task DownloadAssetWithProgressAsync(
        string downloadUrl,
        long totalBytes,
        string destinationFilePath,
        IProgress<DownloadProgressInfo> progress,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
        ApplyAuthenticationHeaders(request);

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var contentStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var fileStream = new FileStream(destinationFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

        var buffer = new byte[81920];
        long bytesReceived = 0;
        int bytesRead;
        var stopwatch = Stopwatch.StartNew();
        var lastReport = DateTime.UtcNow;

        while ((bytesRead = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct).ConfigureAwait(false);
            bytesReceived += bytesRead;

            if (DateTime.UtcNow - lastReport > TimeSpan.FromMilliseconds(100))
            {
                lastReport = DateTime.UtcNow;
                var speed = bytesReceived / Math.Max(stopwatch.Elapsed.TotalSeconds, 0.001);
                var pct = totalBytes > 0 ? (bytesReceived * 100.0 / totalBytes) : 0;
                var status = $"Téléchargement : {bytesReceived / (1024 * 1024.0):F1} / {totalBytes / (1024 * 1024.0):F1} Mo ({speed / (1024 * 1024.0):F1} Mo/s)";
                progress.Report(new DownloadProgressInfo(bytesReceived, totalBytes, pct, speed, status));
            }
        }
    }

    private static string ComputeSha256(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha256.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string? ExtractSha256FromRelease(GitHubReleaseDto release, string assetName)
    {
        if (string.IsNullOrWhiteSpace(release.Body))
            return null;

        // Search for SHA256 in release body (e.g. "sha256: abc...123" or "translation.zip: abc...123")
        var match = Regex.Match(release.Body, $@"{Regex.Escape(assetName)}[:\s]+([0-9a-fA-F]{{64}})", RegexOptions.IgnoreCase);
        if (match.Success)
            return match.Groups[1].Value.ToLowerInvariant();

        var genericMatch = Regex.Match(release.Body, @"(?:sha256|hash)[:\s]+([0-9a-fA-F]{{64}})", RegexOptions.IgnoreCase);
        if (genericMatch.Success)
            return genericMatch.Groups[1].Value.ToLowerInvariant();

        return null;
    }

    private string CreateTranslationBackup(string gameDir)
    {
        var backupRoot = Path.Combine(gameDir, "backups");
        Directory.CreateDirectory(backupRoot);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupDir = Path.Combine(backupRoot, $"BepInEx_backup_{timestamp}");
        Directory.CreateDirectory(backupDir);

        // Backup BepInEx folder
        var bepInExDir = Path.Combine(gameDir, "BepInEx");
        if (Directory.Exists(bepInExDir))
        {
            CopyDirectory(bepInExDir, Path.Combine(backupDir, "BepInEx"));
        }

        // Backup DLLs and configs
        CopyFileIfExists(Path.Combine(gameDir, "winhttp.dll"), Path.Combine(backupDir, "winhttp.dll"));
        CopyFileIfExists(Path.Combine(gameDir, "winhttp.dll.disabled"), Path.Combine(backupDir, "winhttp.dll.disabled"));
        CopyFileIfExists(Path.Combine(gameDir, "doorstop_config.ini"), Path.Combine(backupDir, "doorstop_config.ini"));
        CopyFileIfExists(Path.Combine(gameDir, "translation_version.json"), Path.Combine(backupDir, "translation_version.json"));

        _logger.Info($"Sauvegarde créée dans '{backupDir}'");
        return backupDir;
    }

    private static void ExtractZipSafely(string zipPath, string targetDir)
    {
        using var archive = ZipFile.OpenRead(zipPath);

        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name) && (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\")))
            {
                // Directory entry
                var safeDirPath = PathSecurityHelper.GetSafeExtractionPath(targetDir, entry.FullName);
                Directory.CreateDirectory(safeDirPath);
                continue;
            }

            var safeFilePath = PathSecurityHelper.GetSafeExtractionPath(targetDir, entry.FullName);
            var parentDir = Path.GetDirectoryName(safeFilePath);
            if (!string.IsNullOrEmpty(parentDir))
            {
                Directory.CreateDirectory(parentDir);
            }

            entry.ExtractToFile(safeFilePath, overwrite: true);
        }
    }

    public bool HasBackup(string? gameDir)
    {
        if (string.IsNullOrWhiteSpace(gameDir) || !Directory.Exists(gameDir))
            return false;

        var backupRoot = Path.Combine(gameDir, "backups");
        if (!Directory.Exists(backupRoot))
            return false;

        return Directory.GetDirectories(backupRoot, "BepInEx_backup_*").Length > 0;
    }

    public Task RollbackTranslationAsync(string gameDir)
    {
        var backupRoot = Path.Combine(gameDir, "backups");
        if (!Directory.Exists(backupRoot))
        {
            throw new DirectoryNotFoundException("Aucun dossier de sauvegarde trouvé.");
        }

        var backups = Directory.GetDirectories(backupRoot, "BepInEx_backup_*")
            .OrderByDescending(d => d)
            .ToList();

        if (backups.Count == 0)
        {
            throw new FileNotFoundException("Aucune sauvegarde de traduction disponible pour restaurer.");
        }

        var latestBackup = backups[0];
        _logger.Info($"Restauration de la traduction depuis : {latestBackup}");
        RestoreFromBackup(gameDir, latestBackup);
        return Task.CompletedTask;
    }

    private static void RestoreFromBackup(string gameDir, string backupDir)
    {
        // Restore BepInEx directory
        var sourceBepInEx = Path.Combine(backupDir, "BepInEx");
        var destBepInEx = Path.Combine(gameDir, "BepInEx");
        if (Directory.Exists(sourceBepInEx))
        {
            if (Directory.Exists(destBepInEx))
            {
                Directory.Delete(destBepInEx, true);
            }
            CopyDirectory(sourceBepInEx, destBepInEx);
        }

        // Restore individual files
        CopyFileIfExists(Path.Combine(backupDir, "winhttp.dll"), Path.Combine(gameDir, "winhttp.dll"), true);
        CopyFileIfExists(Path.Combine(backupDir, "winhttp.dll.disabled"), Path.Combine(gameDir, "winhttp.dll.disabled"), true);
        CopyFileIfExists(Path.Combine(backupDir, "doorstop_config.ini"), Path.Combine(gameDir, "doorstop_config.ini"), true);
        CopyFileIfExists(Path.Combine(backupDir, "translation_version.json"), Path.Combine(gameDir, "translation_version.json"), true);
    }

    private static void CleanupOldBackups(string gameDir, int keepCount)
    {
        try
        {
            var backupRoot = Path.Combine(gameDir, "backups");
            if (!Directory.Exists(backupRoot))
                return;

            var dirs = Directory.GetDirectories(backupRoot, "BepInEx_backup_*")
                .OrderByDescending(d => d)
                .Skip(keepCount);

            foreach (var oldDir in dirs)
            {
                Directory.Delete(oldDir, true);
            }
        }
        catch
        {
            // Best effort
        }
    }

    private static void CopyFileIfExists(string source, string dest, bool overwrite = false)
    {
        if (File.Exists(source))
        {
            File.Copy(source, dest, overwrite);
        }
    }

    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var targetFile = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, targetFile, true);
        }

        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            var targetSubDir = Path.Combine(targetDir, Path.GetFileName(subDir));
            CopyDirectory(subDir, targetSubDir);
        }
    }
}
