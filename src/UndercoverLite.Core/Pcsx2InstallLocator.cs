namespace UndercoverLite.Core;

/// <summary>
/// Finds a separately extracted PCSX2 Qt build near the launcher.
/// No downloads, installation, directory traversal, or emulator files are bundled.
/// </summary>
public static class Pcsx2InstallLocator
{
    private static readonly string[] RelativeCandidates =
    [
        "pcsx2-qt.exe",
        Path.Combine("PCSX2", "pcsx2-qt.exe"),
        Path.Combine("pcsx2", "pcsx2-qt.exe"),
        Path.Combine("pcsx2-v2.8.2-windows-x64-Qt", "pcsx2-qt.exe"),
        Path.Combine("emulator", "pcsx2-qt.exe")
    ];

    /// <summary>Returns a local path, not a verified emulator version.</summary>
    public static string? FindNextToLauncher(string launcherDirectory)
    {
        if (string.IsNullOrWhiteSpace(launcherDirectory))
            throw new ArgumentException("Launcher directory must not be empty.", nameof(launcherDirectory));

        var root = Path.GetFullPath(launcherDirectory);
        foreach (var candidate in RelativeCandidates)
        {
            var fullPath = Path.Combine(root, candidate);
            if (File.Exists(fullPath))
                return fullPath;
        }
        return null;
    }
}