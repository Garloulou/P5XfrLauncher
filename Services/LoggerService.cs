using System;
using System.IO;

namespace LauncherP5XFr.Services;

public interface ILoggerService
{
    void Info(string message);
    void Warn(string message);
    void Error(string message, Exception? ex = null);
    void Debug(string message);
    string GetLogFilePath();
    event Action<string>? LogMessageReceived;
}

public sealed class LoggerService : ILoggerService
{
    private static readonly Lazy<LoggerService> _instance = new(() => new LoggerService());
    public static LoggerService Instance => _instance.Value;

    private readonly object _lock = new();
    private readonly string _logFilePath;
    private const long MaxLogSizeBytes = 5 * 1024 * 1024; // 5 MB

    public event Action<string>? LogMessageReceived;

    public LoggerService()
    {
        var logDir = Path.Combine(Helpers.AppEnvironment.AppDirectory, "logs");
        Directory.CreateDirectory(logDir);
        _logFilePath = Path.Combine(logDir, "launcher.log");

        // Rotate log if it exceeds max size
        RotateLogIfNeeded();
    }

    public string GetLogFilePath() => _logFilePath;

    public void Info(string message) => Log("INFO", message);
    public void Warn(string message) => Log("WARN", message);
    public void Debug(string message) => Log("DEBUG", message);

    public void Error(string message, Exception? ex = null)
    {
        var formatted = ex != null ? $"{message} | Exception: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}" : message;
        Log("ERROR", formatted);
    }

    private void Log(string level, string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";

        lock (_lock)
        {
            try
            {
                File.AppendAllText(_logFilePath, line + Environment.NewLine);
            }
            catch
            {
                // Best effort
            }
        }

        try
        {
            LogMessageReceived?.Invoke(line);
        }
        catch
        {
            // Ignore UI listener exceptions
        }
    }

    private void RotateLogIfNeeded()
    {
        try
        {
            if (File.Exists(_logFilePath))
            {
                var fileInfo = new FileInfo(_logFilePath);
                if (fileInfo.Length > MaxLogSizeBytes)
                {
                    var backupFile = _logFilePath + ".old";
                    if (File.Exists(backupFile))
                        File.Delete(backupFile);
                    File.Move(_logFilePath, backupFile);
                }
            }
        }
        catch
        {
            // Ignore rotation errors
        }
    }
}
