using System.Globalization;
using System.Text;

namespace UndercoverLite.Core;

/// <summary>
/// Opt-in alternate PCSX2 input profiles for the exact same Undercover PS2
/// controller, varying only keyboard stick magnitude and optional extra menu keys.
/// Uses PCSX2 2.8.2's LLeftScale/LRightScale per-binding settings and repeated
/// INI keys for multiple host bindings. Does not patch the running emulator.
/// </summary>
public static class SteeringProfiles
{
    public enum Strength { Gentle, Balanced, Full }

    public sealed record Choice(Strength Steering, bool MenuShortcuts)
    {
        public string Name => $"UndercoverLite-Steer{Percent(Steering)}" + (MenuShortcuts ? "-Menu" : "");
        public string FileName => Name + ".ini";
    }

    public sealed record Preview(string Destination, UndercoverKeyboardProfile.InstallStatus Status);

    public static int Percent(Strength value) => value switch
    {
        Strength.Gentle => 65,
        Strength.Balanced => 85,
        Strength.Full => 100,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    public static string RenderIni(Choice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);
        var strength = (Percent(choice.Steering) / 100.0).ToString("0.00", CultureInfo.InvariantCulture);
        var ini = UndercoverKeyboardProfile.RenderIni();
        const string needle = "AxisScale = 1.330000";
        if (!ini.Contains(needle, StringComparison.Ordinal))
            throw new InvalidOperationException("Underlying 0.9 profile layout changed.");
        // Keep regular PCSX2 analog sensitivity at 1.0. Use the game's specific
        // two horizontal PS2 half-axis binds for keyboard steering strength.
        ini = ini.Replace(needle,
            "AxisScale = 1.000000\nLLeftScale = " + strength +
            "\nLRightScale = " + strength, StringComparison.Ordinal);
        if (choice.MenuShortcuts)
        {
            // PCSX2 2.8.2 INISettingsInterface::GetStringList accepts repeated
            // INI keys; InputManager adds each as an independent button binding.
            // Both keys remain active while driving, not only in game menus!
            ini = ini.Replace("Cross = Keyboard/W\n",
                "Cross = Keyboard/W\nCross = Keyboard/Return\n",
                StringComparison.Ordinal);
            ini = ini.Replace("Triangle = Keyboard/Space\n",
                "Triangle = Keyboard/Space\nTriangle = Keyboard/Backspace\n",
                StringComparison.Ordinal);
        }
        return ini.Replace(
            "# Undercover Lite PC | Driving-first PS2 controller input profile",
            "# Undercover Lite PC | Steering preset " + Percent(choice.Steering) +
            "%, extra menu keys: " + choice.MenuShortcuts,
            StringComparison.Ordinal);
    }

    private static void Guard(string path)
    {
        if ((File.Exists(path) || Directory.Exists(path)) &&
            (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Cannot install a steering profile through a symlink/reparse point.");
    }

    public static Preview Inspect(string pcsx2DataDirectory, Choice choice)
    {
        var directory = UndercoverKeyboardProfile.GetProfileDirectory(pcsx2DataDirectory);
        var destination = Path.Combine(directory, choice.FileName);
        Guard(Path.GetFullPath(pcsx2DataDirectory));
        Guard(Path.GetDirectoryName(directory)!);
        Guard(directory);
        Guard(destination);
        if (!File.Exists(destination)) return new(destination, UndercoverKeyboardProfile.InstallStatus.New);
        var existing = File.ReadAllText(destination).Replace("\r\n", "\n");
        return new(destination, string.Equals(existing, RenderIni(choice), StringComparison.Ordinal)
            ? UndercoverKeyboardProfile.InstallStatus.AlreadyInstalled
            : UndercoverKeyboardProfile.InstallStatus.Conflict);
    }

    public static Preview Install(LauncherSettings settings, string pcsx2DataDirectory, Choice choice)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.IsolatePcsx2Data)
            throw new InvalidOperationException("Steering profile installation requires isolated PCSX2 data.");
        var inspection = Inspect(pcsx2DataDirectory, choice);
        if (inspection.Status == UndercoverKeyboardProfile.InstallStatus.Conflict)
            throw new IOException("A different profile already exists with this name; nothing was overwritten.");
        if (inspection.Status == UndercoverKeyboardProfile.InstallStatus.AlreadyInstalled)
            return inspection;

        var folder = Path.GetDirectoryName(inspection.Destination)!;
        Directory.CreateDirectory(folder);
        Guard(folder);
        var temp = Path.Combine(folder, ".undercoverlite-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(temp, RenderIni(choice), new UTF8Encoding(false));
            File.Move(temp, inspection.Destination, overwrite: false);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        return Inspect(pcsx2DataDirectory, choice);
    }
}

/// <summary>
/// Bounded, read-only hotkey collision guidance. The default Space -> TogglePause
/// and Escape -> OpenPauseMenu are verified in PCSX2 2.8.2 Pad::SetDefaultHotkeyConfig.
/// A missing INI or unknown custom key is not treated as evidence that no conflict exists.
/// </summary>
public static class Pcsx2HotkeyAudit
{
    public sealed record Finding(string Action, string Description);
    public sealed record Result(string Source, IReadOnlyList<Finding> Findings);

    public static Result Scan(string pcsx2DataDirectory)
    {
        if (string.IsNullOrWhiteSpace(pcsx2DataDirectory))
            throw new ArgumentException("PCSX2 data directory is required.", nameof(pcsx2DataDirectory));
        var root = Path.GetFullPath(pcsx2DataDirectory);
        var candidates = new[]
        {
            Path.Combine(root, "PCSX2", "inis", "PCSX2.ini"),
            Path.Combine(root, "inis", "PCSX2.ini")
        };
        var path = candidates.FirstOrDefault(File.Exists);
        if (path is null)
            return new("Default / settings unavailable",
                [new("Space / handbrake",
                    "PCSX2 2.8.2 defaults Space to TogglePause. " +
                    "Confirm in PCSX2 > Settings > Hotkeys before using this profile."),
                 new("Escape / menu",
                    "PCSX2 defaults Escape to its own pause menu. Avoid using Escape as an in-game PS2 button.")]);

        var found = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // Never read arbitrary huge or user-controlled configuration files.
        var info = new FileInfo(path);
        if (info.Length > 2 * 1024 * 1024)
            return new("PCSX2.ini too large to inspect",
                [new("Hotkeys", "Cannot safely inspect hotkey settings; verify Space and Escape in PCSX2.")]);
        string section = "";
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith(';')) continue;
            if (line.StartsWith('[') && line.EndsWith(']')) { section = line[1..^1]; continue; }
            if (!section.Equals("Hotkeys", StringComparison.OrdinalIgnoreCase)) continue;
            var equals = line.IndexOf('=');
            if (equals < 0) continue;
            var key = line[..equals].Trim();
            if (key is "TogglePause" or "OpenPauseMenu")
                found[key] = line[(equals + 1)..].Trim();
        }

        var findings = new List<Finding>();
        if (!found.TryGetValue("TogglePause", out var toggle))
            findings.Add(new("Space / handbrake", "TogglePause not found; the PCSX2 default may still be Space."));
        else if (toggle.Equals("Keyboard/Space", StringComparison.OrdinalIgnoreCase))
            findings.Add(new("Space / handbrake", "Conflict confirmed: Space is also bound to PCSX2 TogglePause."));
        else if (toggle.Contains("Keyboard/Space", StringComparison.OrdinalIgnoreCase))
            findings.Add(new("Space / handbrake", "Space appears in a PCSX2 TogglePause binding. Inspect the hotkey."));
        else findings.Add(new("Space / handbrake", "TogglePause has a custom binding; no direct Space hotkey detected."));

        if (!found.TryGetValue("OpenPauseMenu", out var pause))
            findings.Add(new("Escape / menu", "OpenPauseMenu not found; the PCSX2 default may still be Escape."));
        else if (pause.Contains("Keyboard/Escape", StringComparison.OrdinalIgnoreCase))
            findings.Add(new("Escape / menu", "Escape opens PCSX2's pause menu and is unsuitable for PS2 Back."));
        else findings.Add(new("Escape / menu", "OpenPauseMenu has a custom binding."));

        return new("Local PCSX2 Hotkeys section inspected", findings);
    }
}
