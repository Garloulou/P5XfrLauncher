using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LauncherP5XFr.Models;

namespace LauncherP5XFr.Services;

public interface ISelfUpdateService
{
    Version CurrentVersion { get; }
    Task<(bool isUpdateAvailable, GitHubReleaseDto? release, string message)> CheckLauncherUpdateAsync(CancellationToken ct = default);
    Task ApplyLauncherUpdateAsync(GitHubReleaseDto release, IProgress<DownloadProgressInfo> progress, CancellationToken ct = default);
}

public sealed class SelfUpdateService : ISelfUpdateService
{
    private static readonly Lazy<SelfUpdateService> _instance = new(() => new SelfUpdateService());
    public static SelfUpdateService Instance => _instance.Value;

    private readonly HttpClient _httpClient;
    private readonly IConfigService _configService;
    private readonly ILoggerService _logger;

    public Version CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);

    public SelfUpdateService() : this(ConfigService.Instance, LoggerService.Instance)
    {
    }

    public SelfUpdateService(IConfigService configService, ILoggerService logger)
    {
        _configService = configService;
        _logger = logger;

        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "LauncherP5XFr");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
    }

    public async Task<(bool isUpdateAvailable, GitHubReleaseDto? release, string message)> CheckLauncherUpdateAsync(CancellationToken ct = default)
    {
        var repo = _configService.Config.LauncherRepo?.Trim();
        if (string.IsNullOrWhiteSpace(repo) || repo.Contains("[OWNER/REPO]"))
        {
            return (false, null, "Dépôt GitHub du launcher non configuré.");
        }

        try
        {
            var url = $"https://api.github.com/repos/{repo}/releases/latest";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrWhiteSpace(_configService.Config.GitHubToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _configService.Config.GitHubToken.Trim());
            }

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return (false, null, $"Réponse GitHub : {response.StatusCode}");
            }

            var contentStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var release = await JsonSerializer.DeserializeAsync(contentStream, AppJsonContext.Default.GitHubReleaseDto, ct).ConfigureAwait(false);

            if (release == null)
            {
                return (false, null, "Impossible de lire la release.");
            }

            var remoteTag = release.TagName.TrimStart('v', 'V');
            if (Version.TryParse(remoteTag, out var remoteVer))
            {
                var isNewer = remoteVer > CurrentVersion;
                return (isNewer, release, isNewer ? $"Nouvelle version {release.TagName} disponible !" : "Le launcher est à jour.");
            }

            return (false, release, "Le launcher est à jour.");
        }
        catch (HttpRequestException ex)
        {
            _logger.Warn($"Auto-update launcher : impossible de joindre GitHub ({ex.Message})");
            return (false, null, "Mode hors-ligne : vérification du launcher ignorée.");
        }
        catch (Exception ex)
        {
            _logger.Error("Erreur lors de la vérification de mise à jour du launcher.", ex);
            return (false, null, ex.Message);
        }
    }

    public async Task ApplyLauncherUpdateAsync(GitHubReleaseDto release, IProgress<DownloadProgressInfo> progress, CancellationToken ct = default)
    {
        // 1. Locate the .exe asset
        var exeAsset = release.Assets.FirstOrDefault(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
        if (exeAsset == null)
        {
            throw new FileNotFoundException("Aucun exécutable (.exe) trouvé parmi les assets de la release du launcher.");
        }

        var currentProcess = Process.GetCurrentProcess();
        var currentExePath = currentProcess.MainModule?.FileName;
        if (string.IsNullOrEmpty(currentExePath) || !File.Exists(currentExePath))
        {
            throw new InvalidOperationException("Impossible de déterminer le chemin d'accès de l'exécutable actuel.");
        }

        var tempNewExe = Path.Combine(Path.GetTempPath(), $"LauncherP5XFr_new_{Guid.NewGuid():N}.exe");

        // 2. Download new exe
        _logger.Info($"Téléchargement du nouvel exécutable : {exeAsset.BrowserDownloadUrl}");
        using (var response = await _httpClient.GetAsync(exeAsset.BrowserDownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
        {
            response.EnsureSuccessStatusCode();
            var totalBytes = exeAsset.Size;
            using var contentStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var fileStream = new FileStream(tempNewExe, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            var buffer = new byte[81920];
            long bytesReceived = 0;
            int bytesRead;
            var sw = Stopwatch.StartNew();

            while ((bytesRead = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct).ConfigureAwait(false);
                bytesReceived += bytesRead;
                var pct = totalBytes > 0 ? (bytesReceived * 100.0 / totalBytes) : 0;
                var speed = bytesReceived / Math.Max(sw.Elapsed.TotalSeconds, 0.001);
                progress.Report(new DownloadProgressInfo(bytesReceived, totalBytes, pct, speed, "Téléchargement de la mise à jour..."));
            }
        }

        // 3. Rename current running exe to .old, and place new exe in its place
        var oldExePath = currentExePath + ".old";
        if (File.Exists(oldExePath))
        {
            try
            {
                File.Delete(oldExePath);
            }
            catch
            {
                // If locked, choose a randomized old name
                oldExePath = currentExePath + $".{Guid.NewGuid():N}.old";
            }
        }

        _logger.Info($"Renommage de l'exécutable courant : '{currentExePath}' -> '{oldExePath}'");
        File.Move(currentExePath, oldExePath);

        _logger.Info($"Installation du nouvel exécutable : '{tempNewExe}' -> '{currentExePath}'");
        File.Move(tempNewExe, currentExePath);

        // 4. Start new process and terminate current process
        _logger.Info("Relance du launcher mis à jour...");
        Process.Start(new ProcessStartInfo
        {
            FileName = currentExePath,
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = true
        });

        Environment.Exit(0);
    }
}
