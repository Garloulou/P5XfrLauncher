using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace LauncherP5XFr.Helpers;

public static class SteamHelper
{
    public static List<string> GetSteamLibraryFolders()
    {
        var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            // 1. Check Steam Registry paths
            var steamPath = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")?.GetValue("SteamPath") as string;
            if (string.IsNullOrEmpty(steamPath))
            {
                steamPath = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam")?.GetValue("InstallPath") as string;
            }

            if (!string.IsNullOrEmpty(steamPath) && Directory.Exists(steamPath))
            {
                libraries.Add(Path.GetFullPath(steamPath));

                var libraryFoldersVdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
                if (File.Exists(libraryFoldersVdf))
                {
                    ParseLibraryFoldersVdf(libraryFoldersVdf, libraries);
                }
            }
        }
        catch
        {
            // Ignore registry read errors
        }

        // 2. Add typical default locations if they exist
        var defaultLocations = new[]
        {
            @"C:\Program Files (x86)\Steam",
            @"C:\Program Files\Steam",
            @"D:\Steam",
            @"D:\SteamLibrary",
            @"E:\SteamLibrary",
            @"F:\SteamLibrary"
        };

        foreach (var loc in defaultLocations)
        {
            if (Directory.Exists(loc))
            {
                libraries.Add(Path.GetFullPath(loc));
            }
        }

        return new List<string>(libraries);
    }

    private static void ParseLibraryFoldersVdf(string vdfPath, HashSet<string> libraries)
    {
        try
        {
            var content = File.ReadAllText(vdfPath);
            // Matches "path" "\t\t" "D:\\SteamLibrary"
            var regex = new Regex(@"""path""\s+""([^""]+)""", RegexOptions.IgnoreCase);
            var matches = regex.Matches(content);

            foreach (Match match in matches)
            {
                if (match.Groups.Count > 1)
                {
                    var rawPath = match.Groups[1].Value.Replace(@"\\", @"\");
                    if (Directory.Exists(rawPath))
                    {
                        libraries.Add(Path.GetFullPath(rawPath));
                    }
                }
            }
        }
        catch
        {
            // Best effort
        }
    }
}
