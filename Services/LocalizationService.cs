using System;
using System.Collections.Generic;
using LauncherP5XFr.Models;

namespace LauncherP5XFr.Services;

public interface ILocalizationService
{
    string CurrentLanguage { get; set; }
    string GetString(string key);
    event Action? LanguageChanged;
}

public sealed class LocalizationService : ILocalizationService
{
    private static readonly Lazy<LocalizationService> _instance = new(() => new LocalizationService());
    public static LocalizationService Instance => _instance.Value;

    private readonly IConfigService _configService;
    private string _currentLanguage;

    public event Action? LanguageChanged;

    private static readonly Dictionary<string, string> FrenchStrings = new()
    {
        ["AppTitle"] = "Persona 5: The Phantom X - Launcher FR",
        ["NavHome"] = "Accueil",
        ["NavTranslation"] = "Traduction",
        ["NavSettings"] = "Paramètres",
        ["NavAbout"] = "À propos",
        ["PlayButton"] = "JOUER",
        ["TranslationToggle"] = "Traduction française (BepInEx)",
        ["StatusActive"] = "Active",
        ["StatusInactive"] = "Inactive",
        ["StatusUpToDate"] = "À jour",
        ["StatusUpdateAvailable"] = "Mise à jour disponible",
        ["StatusNotInstalled"] = "Non installée",
        ["VersionJP"] = "Version Japon (JP)",
        ["VersionEN"] = "Version Global (EN)",
        ["CheckUpdates"] = "Rechercher les mises à jour",
        ["UpdateNow"] = "Mettre à jour maintenant",
        ["Rollback"] = "Restaurer version précédente",
        ["AutoUpdateOnLaunch"] = "Mettre à jour automatiquement au lancement",
        ["OpenGameFolder"] = "Ouvrir le dossier du jeu",
        ["GamePath"] = "Dossier du jeu",
        ["Browse"] = "Parcourir",
        ["DetectAuto"] = "Détection automatique",
        ["Save"] = "Enregistrer"
    };

    private static readonly Dictionary<string, string> EnglishStrings = new()
    {
        ["AppTitle"] = "Persona 5: The Phantom X - FR Launcher",
        ["NavHome"] = "Home",
        ["NavTranslation"] = "Translation",
        ["NavSettings"] = "Settings",
        ["NavAbout"] = "About",
        ["PlayButton"] = "PLAY",
        ["TranslationToggle"] = "French Translation (BepInEx)",
        ["StatusActive"] = "Active",
        ["StatusInactive"] = "Inactive",
        ["StatusUpToDate"] = "Up to date",
        ["StatusUpdateAvailable"] = "Update available",
        ["StatusNotInstalled"] = "Not installed",
        ["VersionJP"] = "Japan Client (JP)",
        ["VersionEN"] = "Global Client (EN)",
        ["CheckUpdates"] = "Check for updates",
        ["UpdateNow"] = "Update now",
        ["Rollback"] = "Rollback previous version",
        ["AutoUpdateOnLaunch"] = "Auto-update translation on launch",
        ["OpenGameFolder"] = "Open game folder",
        ["GamePath"] = "Game directory",
        ["Browse"] = "Browse",
        ["DetectAuto"] = "Auto Detect",
        ["Save"] = "Save"
    };

    public LocalizationService() : this(ConfigService.Instance)
    {
    }

    public LocalizationService(IConfigService configService)
    {
        _configService = configService;
        _currentLanguage = _configService.Config.Language ?? "fr-FR";
    }

    public string CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (_currentLanguage != value)
            {
                _currentLanguage = value;
                _configService.Config.Language = value;
                _configService.Save();
                LanguageChanged?.Invoke();
            }
        }
    }

    public string GetString(string key)
    {
        var dict = _currentLanguage.StartsWith("en", StringComparison.OrdinalIgnoreCase) 
            ? EnglishStrings 
            : FrenchStrings;

        if (dict.TryGetValue(key, out var val))
            return val;

        if (FrenchStrings.TryGetValue(key, out var frVal))
            return frVal;

        return key;
    }
}
