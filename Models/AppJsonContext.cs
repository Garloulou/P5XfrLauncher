using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LauncherP5XFr.Models;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AppConfig))]
[JsonSerializable(typeof(TranslationVersionInfo))]
[JsonSerializable(typeof(GitHubReleaseDto))]
[JsonSerializable(typeof(List<GitHubReleaseDto>))]
[JsonSerializable(typeof(GitHubAssetDto))]
[JsonSerializable(typeof(List<GitHubAssetDto>))]
[JsonSerializable(typeof(GitHubCommitDto))]
[JsonSerializable(typeof(List<GitHubCommitDto>))]
internal sealed partial class AppJsonContext : JsonSerializerContext
{
}
