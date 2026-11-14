using System;
using System.IO;

namespace LauncherP5XFr.Helpers;

public static class AppEnvironment
{
    private static string? _appDirectory;

    public static string AppDirectory
    {
        get
        {
            if (_appDirectory != null)
                return _appDirectory;

            try
            {
                var processPath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(processPath))
                {
                    var dir = Path.GetDirectoryName(processPath);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    {
                        _appDirectory = dir;
                        return _appDirectory;
                    }
                }
            }
            catch
            {
                // Fallback below
            }

            _appDirectory = AppContext.BaseDirectory;
            return _appDirectory;
        }
    }

    public static string? GetAssetPath(string fileName)
    {
        var dirs = new[]
        {
            Path.Combine(AppDirectory, "Assets"),
            Path.Combine(AppContext.BaseDirectory, "Assets"),
            Path.Combine(Directory.GetCurrentDirectory(), "Assets"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets")
        };

        foreach (var dir in dirs)
        {
            if (!Directory.Exists(dir)) continue;
            var fullPath = Path.Combine(dir, fileName);
            if (File.Exists(fullPath))
                return fullPath;
        }

        return null;
    }
}
