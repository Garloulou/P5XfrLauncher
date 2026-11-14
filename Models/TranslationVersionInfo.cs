using System;

namespace LauncherP5XFr.Models;

public sealed class TranslationVersionInfo
{
    public string Tag { get; set; } = "";

    public string ReleaseName { get; set; } = "";

    public string ReleaseNotes { get; set; } = "";

    public DateTimeOffset? PublishedAt { get; set; }

    public DateTimeOffset InstalledAt { get; set; } = DateTimeOffset.UtcNow;

    public string? Sha256 { get; set; }

    public string? CommitSha { get; set; }
}
