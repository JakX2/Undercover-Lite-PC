namespace UndercoverLite.Core;

/// <summary>
/// Merge only fields this settings screen actually edits into the most recently
/// loaded settings object. This preserves glyph paths and PC Input Lab preferences
/// saved by other windows while the hub is open.
/// </summary>
public static class SettingsHubPatch
{
    public static bool HasChanges(LauncherSettings baseline, LauncherSettings edited)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(edited);
        return baseline.Fullscreen != edited.Fullscreen ||
            baseline.FastBoot != edited.FastBoot ||
            baseline.HideEmulatorWindow != edited.HideEmulatorWindow ||
            baseline.IsolatePcsx2Data != edited.IsolatePcsx2Data ||
            baseline.EnableCompanionHotkey != edited.EnableCompanionHotkey ||
            !string.Equals(baseline.Pcsx2Executable, edited.Pcsx2Executable, StringComparison.Ordinal) ||
            !string.Equals(baseline.GameImage, edited.GameImage, StringComparison.Ordinal) ||
            !string.Equals(baseline.TextureSearchDirectory, edited.TextureSearchDirectory, StringComparison.Ordinal);
    }

    /// <summary>
    /// Mutates only intended edited fields on the freshly loaded target. Never
    /// replaces the whole LauncherSettings instance or nested PC prompt layout.
    /// </summary>
    public static LauncherSettings Merge(LauncherSettings current, LauncherSettings baseline, LauncherSettings edited)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(edited);
        if (edited.Fullscreen != baseline.Fullscreen) current.Fullscreen = edited.Fullscreen;
        if (edited.FastBoot != baseline.FastBoot) current.FastBoot = edited.FastBoot;
        if (edited.HideEmulatorWindow != baseline.HideEmulatorWindow)
            current.HideEmulatorWindow = edited.HideEmulatorWindow;
        if (edited.IsolatePcsx2Data != baseline.IsolatePcsx2Data)
            current.IsolatePcsx2Data = edited.IsolatePcsx2Data;
        if (edited.EnableCompanionHotkey != baseline.EnableCompanionHotkey)
            current.EnableCompanionHotkey = edited.EnableCompanionHotkey;
        if (!string.Equals(edited.Pcsx2Executable, baseline.Pcsx2Executable, StringComparison.Ordinal))
            current.Pcsx2Executable = edited.Pcsx2Executable;
        if (!string.Equals(edited.GameImage, baseline.GameImage, StringComparison.Ordinal))
            current.GameImage = edited.GameImage;
        if (!string.Equals(edited.TextureSearchDirectory, baseline.TextureSearchDirectory, StringComparison.Ordinal))
            current.TextureSearchDirectory = edited.TextureSearchDirectory;
        return current;
    }
}
