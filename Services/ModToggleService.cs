using System;
using System.IO;
using System.Text.RegularExpressions;
using LauncherP5XFr.Helpers;
using LauncherP5XFr.Models;

namespace LauncherP5XFr.Services;

public interface IModToggleService
{
    bool IsTranslationInstalled(string? gameDir);
    bool IsTranslationActive(string? gameDir);
    void SetTranslationActive(string? gameDir, bool active);
    bool VerifyGameFolderPermissions(string? gameDir, out string errorMessage);
}

public sealed class ModToggleService : IModToggleService
{
    private static readonly Lazy<ModToggleService> _instance = new(() => new ModToggleService());
    public static ModToggleService Instance => _instance.Value;

    private readonly IConfigService _configService;
    private readonly ILoggerService _logger;

    public ModToggleService() : this(ConfigService.Instance, LoggerService.Instance)
    {
    }

    public ModToggleService(IConfigService configService, ILoggerService logger)
    {
        _configService = configService;
        _logger = logger;
    }

    public bool IsTranslationInstalled(string? gameDir)
    {
        if (string.IsNullOrWhiteSpace(gameDir) || !Directory.Exists(gameDir))
            return false;

        var winhttpActive = Path.Combine(gameDir, "winhttp.dll");
        var winhttpDisabled = Path.Combine(gameDir, "winhttp.dll.disabled");
        var bepInExDir = Path.Combine(gameDir, "BepInEx");
        var doorstopConfig = Path.Combine(gameDir, "doorstop_config.ini");

        return File.Exists(winhttpActive) || File.Exists(winhttpDisabled) || Directory.Exists(bepInExDir) || File.Exists(doorstopConfig);
    }

    public bool IsTranslationActive(string? gameDir)
    {
        if (string.IsNullOrWhiteSpace(gameDir) || !Directory.Exists(gameDir))
            return false;

        var winhttpActive = Path.Combine(gameDir, "winhttp.dll");
        var winhttpDisabled = Path.Combine(gameDir, "winhttp.dll.disabled");

        if (_configService.Config.UseDoorstopConfigToggle)
        {
            var doorstopConfig = Path.Combine(gameDir, "doorstop_config.ini");
            if (File.Exists(doorstopConfig))
            {
                var content = File.ReadAllText(doorstopConfig);
                var match = Regex.Match(content, @"^\s*enabled\s*=\s*(true|false)", RegexOptions.IgnoreCase | RegexOptions.Multiline);
                if (match.Success)
                {
                    return string.Equals(match.Groups[1].Value, "true", StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        // Primary check: winhttp.dll must exist and not be disabled
        if (File.Exists(winhttpActive))
        {
            return true;
        }

        if (File.Exists(winhttpDisabled))
        {
            return false;
        }

        return false;
    }

    public void SetTranslationActive(string? gameDir, bool active)
    {
        if (string.IsNullOrWhiteSpace(gameDir) || !Directory.Exists(gameDir))
        {
            throw new DirectoryNotFoundException("Le dossier du jeu spécifié est introuvable.");
        }

        if (!PathSecurityHelper.IsDirectoryWritable(gameDir))
        {
            throw new UnauthorizedAccessException("Le dossier du jeu est protégé en écriture. Lancez le launcher en tant qu'administrateur ou modifiez les autorisations du dossier.");
        }

        var winhttpActive = Path.Combine(gameDir, "winhttp.dll");
        var winhttpDisabled = Path.Combine(gameDir, "winhttp.dll.disabled");
        var doorstopConfig = Path.Combine(gameDir, "doorstop_config.ini");

        _logger.Info($"Application du statut traduction : {(active ? "ACTIVÉE" : "DÉSACTIVÉE")} dans '{gameDir}'");

        try
        {
            ApplyToggleToFolder(gameDir, active);

            var clientPcDir = Path.Combine(gameDir, "client", "pc");
            if (Directory.Exists(clientPcDir))
            {
                ApplyToggleToFolder(clientPcDir, active);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Échec lors du changement d'état de la traduction : {ex.Message}", ex);
            throw new InvalidOperationException($"Impossible de modifier l'état de BepInEx : {ex.Message}", ex);
        }
    }

    private void ApplyToggleToFolder(string targetDir, bool active)
    {
        var winhttpActive = Path.Combine(targetDir, "winhttp.dll");
        var winhttpDisabled = Path.Combine(targetDir, "winhttp.dll.disabled");
        var doorstopConfig = Path.Combine(targetDir, "doorstop_config.ini");

        if (active)
        {
            // Restore winhttp.dll if disabled
            if (File.Exists(winhttpDisabled) && !File.Exists(winhttpActive))
            {
                File.Move(winhttpDisabled, winhttpActive);
                _logger.Info($"Fichier winhttp.dll.disabled restauré en winhttp.dll dans '{targetDir}'");
            }

            // Update doorstop_config.ini
            UpdateDoorstopConfig(doorstopConfig, true);
        }
        else
        {
            // Disable winhttp.dll
            if (File.Exists(winhttpActive))
            {
                if (File.Exists(winhttpDisabled))
                {
                    File.Delete(winhttpDisabled);
                }
                File.Move(winhttpActive, winhttpDisabled);
                _logger.Info($"Fichier winhttp.dll renommé en winhttp.dll.disabled dans '{targetDir}'");
            }

            // Update doorstop_config.ini
            UpdateDoorstopConfig(doorstopConfig, false);
        }
    }

    public bool VerifyGameFolderPermissions(string? gameDir, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(gameDir) || !Directory.Exists(gameDir))
        {
            errorMessage = "Dossier du jeu non défini ou inexistant.";
            return false;
        }

        if (!PathSecurityHelper.IsDirectoryWritable(gameDir))
        {
            errorMessage = "Le dossier du jeu se trouve dans un emplacement protégé (ex: Program Files) et n'est pas accessible en écriture directe.";
            return false;
        }

        return true;
    }

    private void UpdateDoorstopConfig(string doorstopConfigPath, bool enabled)
    {
        try
        {
            if (!File.Exists(doorstopConfigPath))
                return;

            var content = File.ReadAllText(doorstopConfigPath);
            var pattern = @"(^\s*enabled\s*=\s*)(true|false)";
            var replacement = $"$1{(enabled ? "true" : "false")}";

            if (Regex.IsMatch(content, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline))
            {
                var newContent = Regex.Replace(content, pattern, replacement, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                File.WriteAllText(doorstopConfigPath, newContent);
                _logger.Info($"doorstop_config.ini mis à jour avec enabled={(enabled ? "true" : "false")}");
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"Impossible de mettre à jour doorstop_config.ini : {ex.Message}");
        }
    }
}
