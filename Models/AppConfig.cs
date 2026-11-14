using System.Text.Json.Serialization;

namespace LauncherP5XFr.Models;

public sealed class AppConfig
{
    public string? GamePath { get; set; }

    public string GameExeName { get; set; } = "P5X.exe";

    public bool TranslationEnabled { get; set; } = true;

    public bool UseDoorstopConfigToggle { get; set; } = false; // false = rename winhttp.dll, true = modify doorstop_config.ini

    public string Theme { get; set; } = "Default"; // Default (System), Light, Dark

    public string Language { get; set; } = "fr-FR"; // fr-FR, en-US

    public string? CustomArguments { get; set; } = "";

    // Steam Launch Options
    public bool LaunchViaSteam { get; set; } = true;

    public string SteamAppId { get; set; } = "3061570";

    // Optional fields for repository integration
    public string? TranslationRepo { get; set; } = "";

    public string? LauncherRepo { get; set; } = "";

    public string BepInExRepo { get; set; } = "BepInEx/BepInEx";

    public bool AutoInstallBepInEx { get; set; } = true;

    public string? TranslationAssetPattern { get; set; } = "";

    public string? GitHubToken { get; set; }

    public string? LastETagTranslation { get; set; }

    public string? LastETagLauncher { get; set; }
}
