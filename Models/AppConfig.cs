using System.Text.Json.Serialization;

namespace LauncherP5XFr.Models;

public sealed class AppConfig
{
    public string? GamePath { get; set; } = "";

    public string GameExeName { get; set; } = "P5X.exe";

    public bool TranslationEnabled { get; set; } = true;

    public bool UseDoorstopConfigToggle { get; set; } = false; // false = rename winhttp.dll, true = modify doorstop_config.ini

    public string Theme { get; set; } = "Default"; // Default (System), Light, Dark

    public string Language { get; set; } = "fr-FR"; // fr-FR, en-US

    public bool LaunchViaSteam { get; set; } = true;

    public string? CustomArguments { get; set; } = "";

    // Optional overrides for advanced users (ignored if null)
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SteamAppId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TranslationRepo { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LauncherRepo { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BepInExRepo { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TranslationAssetPattern { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? GitHubToken { get; set; }

    [JsonIgnore]
    public string? LastETagTranslation { get; set; }

    [JsonIgnore]
    public string? LastETagLauncher { get; set; }
}
