using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using LauncherP5XFr.Models;
using LauncherP5XFr.Services;

namespace LauncherP5XFr.Services;

public interface IGameLauncherService
{
    void LaunchGame(bool enableTranslation);
    string GetSteamAppId(string? gameDir);
}

public sealed class GameLauncherService : IGameLauncherService
{
    private static readonly Lazy<GameLauncherService> _instance = new(() => new GameLauncherService());
    public static GameLauncherService Instance => _instance.Value;

    private readonly IGameLocator _gameLocator;
    private readonly IModToggleService _modToggleService;
    private readonly IConfigService _configService;
    private readonly ILoggerService _logger;

    public GameLauncherService() : this(GameLocator.Instance, ModToggleService.Instance, ConfigService.Instance, LoggerService.Instance)
    {
    }

    public GameLauncherService(IGameLocator gameLocator, IModToggleService modToggleService, IConfigService configService, ILoggerService logger)
    {
        _gameLocator = gameLocator;
        _modToggleService = modToggleService;
        _configService = configService;
        _logger = logger;
    }

    public string GetSteamAppId(string? gameDir)
    {
        if (!string.IsNullOrWhiteSpace(_configService.Config.SteamAppId))
        {
            return _configService.Config.SteamAppId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(gameDir) && Directory.Exists(gameDir))
        {
            var candidateFiles = new[]
            {
                Path.Combine(gameDir, "steam_appid.txt"),
                Path.Combine(gameDir, "client", "pc", "steam_appid.txt")
            };

            foreach (var file in candidateFiles)
            {
                if (File.Exists(file))
                {
                    try
                    {
                        var id = File.ReadAllText(file).Trim();
                        if (!string.IsNullOrEmpty(id) && long.TryParse(id, out _))
                        {
                            return id;
                        }
                    }
                    catch { }
                }
            }
        }

        return AppConstants.DefaultSteamAppId;
    }

    public void LaunchGame(bool enableTranslation)
    {
        var gameDir = _gameLocator.GetGameDirectory();
        if (string.IsNullOrWhiteSpace(gameDir) || !Directory.Exists(gameDir))
        {
            throw new DirectoryNotFoundException("Le dossier du jeu n'est pas configuré ou n'existe pas. Veuillez le définir dans les paramètres.");
        }

        var exePath = _gameLocator.GetExecutablePath();
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            throw new FileNotFoundException($"L'exécutable du jeu est introuvable dans '{gameDir}'. Assurez-vous d'avoir sélectionné le bon dossier de jeu.");
        }

        // 1. Anti double-launch
        if (_gameLocator.IsGameRunning())
        {
            throw new InvalidOperationException("Le jeu est déjà en cours d'exécution. Veuillez fermer l'instance existante avant de relancer.");
        }

        // 2. Apply translation toggle if installed
        if (_modToggleService.IsTranslationInstalled(gameDir))
        {
            _modToggleService.SetTranslationActive(gameDir, enableTranslation);
            if (enableTranslation)
            {
                EnsureBepInExConsoleConfig(gameDir);
            }
        }
        else if (enableTranslation)
        {
            _logger.Warn("La traduction est activée mais aucun fichier BepInEx n'est détecté dans le dossier du jeu.");
        }

        // 3. Launch via Steam rungameid if enabled
        if (_configService.Config.LaunchViaSteam)
        {
            var appId = GetSteamAppId(gameDir);
            var steamUri = $"steam://rungameid/{appId}";

            _logger.Info($"Démarrage du jeu via Steam : {steamUri} (Traduction : {(enableTranslation ? "ACTIVÉE" : "DÉSACTIVÉE")})");

            try
            {
                var steamStartInfo = new ProcessStartInfo
                {
                    FileName = steamUri,
                    UseShellExecute = true
                };

                Process.Start(steamStartInfo);
                _logger.Info($"Commande Steam rungame envoyée avec succès pour l'AppID {appId}.");
                return;
            }
            catch (Exception steamEx)
            {
                _logger.Warn($"Échec de l'envoi de la commande Steam ({steamEx.Message}). Bascule sur le lancement direct...");
            }
        }

        // 4. Fallback to direct process launch
        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            WorkingDirectory = gameDir,
            UseShellExecute = true,
            Arguments = _configService.Config.CustomArguments ?? ""
        };

        _logger.Info($"Démarrage direct de l'exécutable : {exePath} (Traduction : {(enableTranslation ? "ACTIVÉE" : "DÉSACTIVÉE")})");

        try
        {
            var process = Process.Start(startInfo);
            if (process == null)
            {
                throw new InvalidOperationException("Échec du démarrage du processus du jeu (Process.Start a renvoyé null).");
            }

            _logger.Info($"Jeu lancé avec succès en mode direct (PID: {process.Id}).");
        }
        catch (Exception ex)
        {
            _logger.Error($"Erreur lors du lancement direct du jeu : {ex.Message}", ex);
            throw;
        }
    }

    private void EnsureBepInExConsoleConfig(string gameDir)
    {
        try
        {
            var targetDir = GameLocator.GetGameClientDirectory(gameDir);
            var configDir = Path.Combine(targetDir, "BepInEx", "config");
            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }

            var cfgPath = Path.Combine(configDir, "BepInEx.cfg");
            var enabled = _configService.Config.EnableBepInExConsole;

            if (File.Exists(cfgPath))
            {
                var content = File.ReadAllText(cfgPath);
                if (content.Contains("[Logging.Console]"))
                {
                    var regex = new Regex(@"(\[Logging\.Console\][\s\S]*?Enabled\s*=\s*)(true|false)", RegexOptions.IgnoreCase);
                    if (regex.IsMatch(content))
                    {
                        content = regex.Replace(content, $"${{1}}{(enabled ? "true" : "false")}", 1);
                        File.WriteAllText(cfgPath, content);
                        _logger.Info($"Configuration console BepInEx mise à jour (Enabled = {enabled}) dans '{cfgPath}'");
                        return;
                    }
                }
            }

            var newConfig = $@"[Logging.Console]
Enabled = {(enabled ? "true" : "false")}
ShiftWindowPosition = false
StandardOutType = Everything

[Logging.Disk]
LogLevels = All
WriteUnityLog = true
";
            File.WriteAllText(cfgPath, newConfig);
            _logger.Info($"Fichier BepInEx.cfg créé avec console {(enabled ? "ACTIVÉE" : "DÉSACTIVÉE")} dans '{cfgPath}'");
        }
        catch (Exception ex)
        {
            _logger.Warn($"Note: Impossible de configurer la console BepInEx : {ex.Message}");
        }
    }
}
