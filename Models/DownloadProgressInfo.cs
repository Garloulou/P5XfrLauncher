namespace LauncherP5XFr.Models;

public readonly record struct DownloadProgressInfo(
    long BytesReceived,
    long? TotalBytes,
    double Percentage,
    double SpeedBytesPerSecond,
    string StatusMessage
);
