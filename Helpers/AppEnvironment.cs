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
}
