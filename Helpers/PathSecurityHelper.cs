using System;
using System.IO;

namespace LauncherP5XFr.Helpers;

public static class PathSecurityHelper
{
    /// <summary>
    /// Protects against Zip Slip vulnerability by ensuring the target extraction path is within the destination directory.
    /// </summary>
    public static string GetSafeExtractionPath(string destinationDirectory, string entryFullName)
    {
        var fullDestDir = Path.GetFullPath(destinationDirectory);
        // Normalize entry separators to OS directory separator
        var safeEntryName = entryFullName.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

        // Remove any leading separator
        if (safeEntryName.StartsWith(Path.DirectorySeparatorChar))
        {
            safeEntryName = safeEntryName.TrimStart(Path.DirectorySeparatorChar);
        }

        var combinedPath = Path.Combine(fullDestDir, safeEntryName);
        var fullTarget = Path.GetFullPath(combinedPath);

        if (!fullTarget.StartsWith(fullDestDir, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Tentative d'extraction non autorisée hors du dossier cible : '{entryFullName}'");
        }

        return fullTarget;
    }

    /// <summary>
    /// Checks if a directory is writable by attempting to create and delete a temporary file.
    /// </summary>
    public static bool IsDirectoryWritable(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
            return false;

        var testFile = Path.Combine(directoryPath, $".perm_test_{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
