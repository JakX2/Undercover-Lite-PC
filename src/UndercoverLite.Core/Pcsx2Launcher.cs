using System.Diagnostics;

namespace UndercoverLite.Core;

public static class Pcsx2Launcher
{
    public static void Validate(LauncherSettings settings, bool launchingGame)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (string.IsNullOrWhiteSpace(settings.Pcsx2Executable) ||
            !File.Exists(settings.Pcsx2Executable) ||
            !string.Equals(Path.GetExtension(settings.Pcsx2Executable), ".exe", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Select a valid PCSX2 Windows executable (pcsx2-qt.exe).");
        }

        if (launchingGame && (string.IsNullOrWhiteSpace(settings.GameImage) ||
                              !File.Exists(settings.GameImage)))
        {
            throw new InvalidOperationException("Select your own PS2 game image first.");
        }
    }

    /// <summary>ArgumentList prevents spaces/quotes in user paths from becoming extra flags.</summary>
    public static ProcessStartInfo CreateStartInfo(
        LauncherSettings settings, string pcsx2DataDirectory, bool launchingGame)
    {
        Validate(settings, launchingGame);
        var exe = Path.GetFullPath(settings.Pcsx2Executable);
        var startInfo = new ProcessStartInfo
        {
            FileName = exe,
            WorkingDirectory = Path.GetDirectoryName(exe)!,
            UseShellExecute = false
        };
        if (settings.IsolatePcsx2Data)
        {
            startInfo.ArgumentList.Add("-datapath");
            startInfo.ArgumentList.Add(Path.GetFullPath(pcsx2DataDirectory));
        }
        if (launchingGame)
        {
            startInfo.ArgumentList.Add(settings.HideEmulatorWindow ? "-nogui" : "-batch");
            startInfo.ArgumentList.Add(settings.Fullscreen ? "-fullscreen" : "-nofullscreen");
            startInfo.ArgumentList.Add(settings.FastBoot ? "-fastboot" : "-slowboot");
            startInfo.ArgumentList.Add("--");
            startInfo.ArgumentList.Add(Path.GetFullPath(settings.GameImage));
        }
        return startInfo;
    }

    public static Process Start(
        LauncherSettings settings, string pcsx2DataDirectory, bool launchingGame)
    {
        var info = CreateStartInfo(settings, pcsx2DataDirectory, launchingGame);
        if (settings.IsolatePcsx2Data)
            Directory.CreateDirectory(pcsx2DataDirectory);
        return Process.Start(info) ?? throw new InvalidOperationException("PCSX2 did not start.");
    }
}