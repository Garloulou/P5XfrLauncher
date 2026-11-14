using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LauncherP5XFr.Helpers;
using LauncherP5XFr.Models;

namespace LauncherP5XFr.Services;

public interface IGameLocator
{
    string? GetGameDirectory();
    string? GetExecutablePath();
    bool IsValidGameDirectory(string? directoryPath, out string detectedExe);
    bool IsGameRunning();
    void AutoDetectGameInstallation();
}

public sealed class GameLocator : IGameLocator
{
    private static readonly Lazy<GameLocator> _instance = new(() => new GameLocator());
    public static GameLocator Instance => _instance.Value;

    private readonly IConfigService _configService;
    private readonly ILoggerService _logger;

    private static readonly string[] CommonFolderNames =
    {
        "Persona 5 The Phantom X",
        "Persona 5: The Phantom X",
        "P5X",
        "Persona5ThePhantomX",
        "P5X_Global"
    };

    public GameLocator() : this(ConfigService.Instance, LoggerService.Instance)
    {
    }

    public GameLocator(IConfigService configService, ILoggerService logger)
    {
        _configService = configService;
        _logger = logger;
    }

    public string? GetGameDirectory()
    {
        return _configService.Config.GamePath;
    }

    public string? GetExecutablePath()
    {
        var dir = GetGameDirectory();
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            return null;

        if (IsValidGameDirectory(dir, out var exeName))
        {
            return Path.Combine(dir, exeName);
        }

        return null;
    }

    public bool IsValidGameDirectory(string? directoryPath, out string detectedExe)
    {
        detectedExe = string.Empty;
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
            return false;

        var configuredExe = _configService.Config.GameExeName;

        // 1. Check configured exe
        if (!string.IsNullOrWhiteSpace(configuredExe))
        {
            var configuredPath = Path.Combine(directoryPath, configuredExe);
            if (File.Exists(configuredPath))
            {
                detectedExe = configuredExe;
                return true;
            }
        }

        // 2. Candidate executable names
        var candidates = new[]
        {
            "P5X.exe",
            "Persona 5 The Phantom X.exe",
            "P5X-Win64-Shipping.exe"
        };

        foreach (var candidate in candidates)
        {
            var path = Path.Combine(directoryPath, candidate);
            if (File.Exists(path))
            {
                detectedExe = candidate;
                return true;
            }
        }

        // 3. Fallback: Search for any .exe with matching Unity Data folder
        try
        {
            var exeFiles = Directory.GetFiles(directoryPath, "*.exe", SearchOption.TopDirectoryOnly);
            foreach (var exe in exeFiles)
            {
                var nameWithoutExt = Path.GetFileNameWithoutExtension(exe);
                var dataDir = Path.Combine(directoryPath, nameWithoutExt + "_Data");
                if (Directory.Exists(dataDir) &&
                    !nameWithoutExt.Contains("CrashHandler", StringComparison.OrdinalIgnoreCase) &&
                    !nameWithoutExt.Contains("UnityCrash", StringComparison.OrdinalIgnoreCase))
                {
                    detectedExe = Path.GetFileName(exe);
                    return true;
                }
            }
        }
        catch
        {
            // Ignore directory search exceptions
        }

        return false;
    }

    public bool IsGameRunning()
    {
        var exePath = GetExecutablePath();
        if (string.IsNullOrEmpty(exePath))
            return false;

        var processName = Path.GetFileNameWithoutExtension(exePath);
        var processes = Process.GetProcessesByName(processName);
        if (processes.Length > 0)
        {
            foreach (var p in processes)
            {
                try
                {
                    var pPath = p.MainModule?.FileName;
                    if (!string.IsNullOrEmpty(pPath) && string.Equals(pPath, exePath, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                catch
                {
                    return true;
                }
            }
        }

        return false;
    }

    public void AutoDetectGameInstallation()
    {
        if (!string.IsNullOrWhiteSpace(_configService.Config.GamePath) &&
            IsValidGameDirectory(_configService.Config.GamePath, out _))
        {
            return;
        }

        var detected = SearchPossibleLocations();
        if (!string.IsNullOrEmpty(detected))
        {
            _configService.Config.GamePath = detected;
            _logger.Info($"Installation P5X détectée automatiquement : {detected}");
            _configService.Save();
        }
    }

    private string? SearchPossibleLocations()
    {
        var searchRoots = new List<string>();

        // 1. Steam Libraries
        var steamLibraries = SteamHelper.GetSteamLibraryFolders();
        foreach (var lib in steamLibraries)
        {
            var common = Path.Combine(lib, "steamapps", "common");
            if (Directory.Exists(common))
            {
                searchRoots.Add(common);
            }
        }

        // 2. Default installation folders
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        searchRoots.Add(localAppData);
        searchRoots.Add(programFiles);
        if (!string.IsNullOrEmpty(programFilesX86))
            searchRoots.Add(programFilesX86);

        // 3. Drive roots
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
        {
            searchRoots.Add(Path.Combine(drive.RootDirectory.FullName, "Games"));
            searchRoots.Add(drive.RootDirectory.FullName);
        }

        foreach (var root in searchRoots)
        {
            if (!Directory.Exists(root))
                continue;

            foreach (var folder in CommonFolderNames)
            {
                var candidate = Path.Combine(root, folder);
                if (Directory.Exists(candidate) && IsValidGameDirectory(candidate, out _))
                {
                    return candidate;
                }
            }
        }

        return null;
    }
}
