using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LauncherP5XFr.Models;

public sealed class GitHubReleaseDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = "";

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("draft")]
    public bool Draft { get; set; }

    [JsonPropertyName("prerelease")]
    public bool Prerelease { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("published_at")]
    public DateTimeOffset? PublishedAt { get; set; }

    [JsonPropertyName("assets")]
    public List<GitHubAssetDto> Assets { get; set; } = new();
}

public sealed class GitHubAssetDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = "";

    [JsonPropertyName("content_type")]
    public string? ContentType { get; set; }
}

public sealed class GitHubCommitDto
{
    [JsonPropertyName("sha")]
    public string Sha { get; set; } = "";

    [JsonPropertyName("commit")]
    public GitHubCommitDetailsDto? Commit { get; set; }
}

public sealed class GitHubCommitDetailsDto
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
}
