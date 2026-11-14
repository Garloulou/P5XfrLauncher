using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LauncherP5XFr.Helpers;
using LauncherP5XFr.Models;

namespace LauncherP5XFr.Services;

public interface IBepInExInstallService
{
    bool IsBepInExInstalled(string? gameDir);
    Task<(bool success, string message)> InstallBepInExAsync(
        string gameDir,
        IProgress<DownloadProgressInfo>? progress = null,
        CancellationToken ct = default);
}

public sealed class BepInExInstallService : IBepInExInstallService
{
    private static readonly Lazy<BepInExInstallService> _instance = new(() => new BepInExInstallService());
    public static BepInExInstallService Instance => _instance.Value;

    private readonly HttpClient _httpClient;
    private readonly IConfigService _configService;
    private readonly ILoggerService _logger;

    public BepInExInstallService() : this(ConfigService.Instance, LoggerService.Instance)
    {
    }

    public BepInExInstallService(IConfigService configService, ILoggerService logger)
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

    public bool IsBepInExInstalled(string? gameDir)
    {
        if (string.IsNullOrWhiteSpace(gameDir) || !Directory.Exists(gameDir))
            return false;

        // Check in root gameDir
        var rootWinhttp = File.Exists(Path.Combine(gameDir, "winhttp.dll")) || File.Exists(Path.Combine(gameDir, "winhttp.dll.disabled"));
        var rootBepInEx = Directory.Exists(Path.Combine(gameDir, "BepInEx", "core"));

        if (rootWinhttp && rootBepInEx)
            return true;

        // Check in client/pc if present
        var clientPcDir = Path.Combine(gameDir, "client", "pc");
        if (Directory.Exists(clientPcDir))
        {
            var pcWinhttp = File.Exists(Path.Combine(clientPcDir, "winhttp.dll")) || File.Exists(Path.Combine(clientPcDir, "winhttp.dll.disabled"));
            var pcBepInEx = Directory.Exists(Path.Combine(clientPcDir, "BepInEx", "core"));
            if (pcWinhttp && pcBepInEx)
                return true;
        }

        return false;
    }

    public async Task<(bool success, string message)> InstallBepInExAsync(
        string gameDir,
        IProgress<DownloadProgressInfo>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(gameDir) || !Directory.Exists(gameDir))
        {
            return (false, "Dossier du jeu non configuré ou introuvable.");
        }

        var repo = string.IsNullOrWhiteSpace(_configService.Config.BepInExRepo)
            ? "BepInEx/BepInEx"
            : _configService.Config.BepInExRepo.Trim();

        _logger.Info($"Recherche de la dernière version de BepInEx sur https://github.com/{repo}...");
        progress?.Report(new DownloadProgressInfo(0, 0, 0, 0, "Recherche de la dernière version de BepInEx..."));

        try
        {
            var url = $"https://api.github.com/repos/{repo}/releases/latest";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            ApplyAuthenticationHeaders(request);

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var err = $"Échec de connexion à GitHub ({response.StatusCode}).";
                _logger.Warn(err);
                return (false, err);
            }

            var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var release = await JsonSerializer.DeserializeAsync(stream, AppJsonContext.Default.GitHubReleaseDto, ct).ConfigureAwait(false);

            if (release == null || release.Assets.Count == 0)
            {
                return (false, "Aucune release ou asset BepInEx valide trouvé sur GitHub.");
            }

            // Find win_x64 asset
            var asset = release.Assets.FirstOrDefault(a => Regex.IsMatch(a.Name, @"(?i)BepInEx_(?:win_)?x64_.*\.zip"))
                     ?? release.Assets.FirstOrDefault(a => Regex.IsMatch(a.Name, @"(?i)BepInEx.*x64.*\.zip"));

            if (asset == null)
            {
                return (false, $"Impossible de trouver l'archive BepInEx x64 Windows dans la release {release.TagName}.");
            }

            _logger.Info($"Asset BepInEx sélectionné : {asset.Name} ({asset.Size / (1024 * 1024.0):F2} Mo)");

            var tempZip = Path.Combine(Path.GetTempPath(), $"bepinex_install_{Guid.NewGuid():N}.zip");

            try
            {
                // Download
                progress?.Report(new DownloadProgressInfo(0, asset.Size, 0, 0, $"Téléchargement de {asset.Name}..."));
                await DownloadAssetWithProgressAsync(asset.BrowserDownloadUrl, asset.Size, tempZip, progress, ct).ConfigureAwait(false);

                // Extract to game directory
                progress?.Report(new DownloadProgressInfo(asset.Size, asset.Size, 100, 0, "Extraction de BepInEx dans le dossier du jeu..."));
                _logger.Info($"Extraction de BepInEx dans '{gameDir}'...");
                ExtractZipSafely(tempZip, gameDir);

                // If client/pc subdirectory exists with Unity player, duplicate proxy or place configuration
                var clientPcDir = Path.Combine(gameDir, "client", "pc");
                if (Directory.Exists(clientPcDir) && File.Exists(Path.Combine(clientPcDir, "UnityPlayer.dll")))
                {
                    try
                    {
                        var winhttpSrc = Path.Combine(gameDir, "winhttp.dll");
                        var winhttpDst = Path.Combine(clientPcDir, "winhttp.dll");
                        if (File.Exists(winhttpSrc) && !File.Exists(winhttpDst))
                        {
                            File.Copy(winhttpSrc, winhttpDst, true);
                        }

                        var doorstopSrc = Path.Combine(gameDir, "doorstop_config.ini");
                        var doorstopDst = Path.Combine(clientPcDir, "doorstop_config.ini");
                        if (File.Exists(doorstopSrc) && !File.Exists(doorstopDst))
                        {
                            // Doorstop in client/pc points to BepInEx in root or client/pc
                            File.Copy(doorstopSrc, doorstopDst, true);
                        }
                    }
                    catch (Exception syncEx)
                    {
                        _logger.Warn($"Note: Synchronisation client/pc : {syncEx.Message}");
                    }
                }

                _logger.Info($"BepInEx {release.TagName} installé avec succès dans '{gameDir}'.");
                progress?.Report(new DownloadProgressInfo(asset.Size, asset.Size, 100, 0, $"BepInEx ({release.TagName}) installé avec succès !"));

                return (true, $"BepInEx {release.TagName} installé avec succès !");
            }
            finally
            {
                if (File.Exists(tempZip))
                {
                    try { File.Delete(tempZip); } catch { }
                }
            }
        }
        catch (HttpRequestException httpEx)
        {
            _logger.Error("Erreur réseau lors du téléchargement de BepInEx.", httpEx);
            return (false, "Erreur réseau : impossible de joindre GitHub. Vérifiez votre connexion Internet.");
        }
        catch (Exception ex)
        {
            _logger.Error("Erreur lors de l'installation de BepInEx.", ex);
            return (false, $"Erreur : {ex.Message}");
        }
    }

    private async Task DownloadAssetWithProgressAsync(
        string downloadUrl,
        long totalBytes,
        string destinationFilePath,
        IProgress<DownloadProgressInfo>? progress,
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
                progress?.Report(new DownloadProgressInfo(bytesReceived, totalBytes, pct, speed, status));
            }
        }
    }

    private static void ExtractZipSafely(string zipPath, string targetDir)
    {
        using var archive = ZipFile.OpenRead(zipPath);

        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name) && (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\")))
            {
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
}
