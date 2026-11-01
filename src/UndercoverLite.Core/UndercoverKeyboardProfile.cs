using System.Text;

namespace UndercoverLite.Core;

/// <summary>
/// Installs an opt-in, game-specific PCSX2 Qt controller profile.
/// Never edits PCSX2.ini, shared bindings, game settings, BIOS or game images.
/// The user must explicitly select this profile in their Undercover Game Properties.
/// </summary>
public static class UndercoverKeyboardProfile
{
    public const string Name = "UndercoverLite-DrivingFirst";
    public const string FileName = Name + ".ini";

    public enum InstallStatus { New, AlreadyInstalled, Conflict }

    public sealed record Preview(string ProfileDirectory, string DestinationFile, InstallStatus Status);

    public static string GetProfileDirectory(string pcsx2DataDirectory)
    {
        if (string.IsNullOrWhiteSpace(pcsx2DataDirectory))
            throw new ArgumentException("PCSX2 data directory is required.", nameof(pcsx2DataDirectory));
        var data = Path.GetFullPath(pcsx2DataDirectory);
        var nested = Path.Combine(data, "PCSX2", "inputprofiles");
        var direct = Path.Combine(data, "inputprofiles");
        // Mirrors the texture path observation from the isolated 2.8.2 instance.
        if (Directory.Exists(nested) || Directory.Exists(Path.Combine(data, "PCSX2")))
            return nested;
        if (Directory.Exists(direct)) return direct; // Older user-defined profiles.
        return nested;
    }

    // Keys follow the verified 2008 PS2 control scheme and the driving-first
    // texture art. These are emulator input mappings, not context-aware prompts.
    // PCSX2 key names and Pad1 field names follow its Qt INI format.
    private static readonly (string Ps2, string PcKey)[] Bindings =
    [
        ("Up", "Up"), ("Down", "Down"), ("Left", "Left"), ("Right", "Right"),
        ("Cross", "W"),            // Accelerate / Accept in menu
        ("Circle", "S"),           // Brake / Defaults in some menus
        ("Triangle", "Space"),     // Handbrake / Back in menu
        ("Square", "X"),           // Speedbreaker / Delete in name entry
        ("L1", "Q"), ("R1", "E"), // Shift down / shift up
        ("L2", "C"), ("R2", "Shift"),
        ("Start", "F"), ("Select", "R"),
        ("R3", "V"), ("L3", "Z"),
        ("LLeft", "A"), ("LRight", "D"),
        ("RUp", "I"), ("RDown", "K"), ("RLeft", "J"), ("RRight", "L")
    ];

    public static IReadOnlyList<(string Ps2, string PcKey)> GetBindings() => Bindings;

    public static string RenderIni()
    {
        var lines = new List<string>
        {
            "# Undercover Lite PC | Driving-first PS2 controller input profile",
            "# Install only when PCSX2 is closed; assign this profile to Undercover",
            "# in the game's Properties > Summary > Input Profile dropdown.",
            "# A static Cross = W also means W = Accept in game menus.",
            "[InputSources]",
            "Keyboard = true",
            "",
            "[Pad1]",
            "Type = DualShock2",
            "AxisScale = 1.330000"
        };
        foreach (var (ps2, pc) in Bindings)
            lines.Add(ps2 + " = Keyboard/" + pc);
        lines.Add("");
        lines.Add("[Pad2]");
        lines.Add("Type = None");
        lines.Add("");
        return string.Join("\n", lines);
    }

    private static void RejectReparse(string path)
    {
        if ((File.Exists(path) || Directory.Exists(path)) &&
            (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Cannot write a profile through a symbolic link or reparse point.");
    }

    public static Preview Inspect(string pcsx2DataDirectory)
    {
        var dir = GetProfileDirectory(pcsx2DataDirectory);
        var dest = Path.Combine(dir, FileName);
        RejectReparse(Path.GetFullPath(pcsx2DataDirectory));
        RejectReparse(Path.GetDirectoryName(dir)!);
        RejectReparse(dir);
        RejectReparse(dest);
        if (!File.Exists(dest)) return new(dir, dest, InstallStatus.New);
        var expected = RenderIni().Replace("\r\n", "\n");
        // Existing user files, even if named alike, never get overwritten.
        var actual = File.ReadAllText(dest).Replace("\r\n", "\n");
        return new(dir, dest, string.Equals(expected, actual, StringComparison.Ordinal)
            ? InstallStatus.AlreadyInstalled : InstallStatus.Conflict);
    }

    public static Preview Install(LauncherSettings settings, string pcsx2DataDirectory)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.IsolatePcsx2Data)
            throw new InvalidOperationException(
                "Only the isolated Undercover Lite PCSX2 profile is supported. " +
                "Use PCSX2's controller settings manually for an external emulator profile.");
        var preview = Inspect(pcsx2DataDirectory);
        if (preview.Status == InstallStatus.Conflict)
            throw new IOException("A different file already exists at the profile path. " +
                "Nothing was overwritten. Back it up or choose a different profile manually.");
        if (preview.Status == InstallStatus.AlreadyInstalled) return preview;

        Directory.CreateDirectory(preview.ProfileDirectory);
        RejectReparse(preview.ProfileDirectory);
        var stage = Path.Combine(preview.ProfileDirectory,
            ".undercoverlite-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(stage, RenderIni(), new UTF8Encoding(false));
            File.Move(stage, preview.DestinationFile, overwrite: false);
        }
        finally { if (File.Exists(stage)) File.Delete(stage); }
        return Inspect(pcsx2DataDirectory);
    }
}
