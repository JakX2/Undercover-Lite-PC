using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace UndercoverLite.Core;

/// <summary>
/// Read-only inspection of saved PCSX2 2.8.2 global and serial+ELF-CRC
/// per-game internal-resolution settings. A verified ISO serial does NOT
/// identify the loaded executable CRC; the saved result is never "live".
/// </summary>
public static class Pcsx2GameGraphicsAudit
{
    public enum State
    {
        SharedProfile, UnverifiedDisc, ConfigurationMissing, CustomFolder,
        NoMatchingGameIni, MultipleCrcs, UniqueGameIni, OverridePresent,
        InvalidConfiguration
    }

    public sealed record Report(
        State Status, string? VerifiedSerial, float? GlobalScale, float? GameScale,
        int CandidateCount, string Summary, string NextStep)
    {
        public bool HasPossibleDifferentOverride =>
            Status == State.OverridePresent && GameScale != GlobalScale;
    }

    private static readonly Regex CrcName = new(
        @"^[0-9A-Fa-f]{8}\.ini$", RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(200));
    private static readonly Regex SerialName = new(
        @"^[A-Z]{4}-[0-9]{5}$", RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(200));

    private static Report Result(State state, string? serial, float? global, float? game,
        string message, string next, int count = 0) =>
        new(state, serial, global, game, count, message, next);

    private static void Guard(string path)
    {
        if ((File.Exists(path) || Directory.Exists(path)) &&
            (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Linked PCSX2 configuration locations cannot be inspected.");
    }

    private static string ReadSafeIni(string path)
    {
        Guard(path);
        var info = new FileInfo(path);
        if (info.Length > 2_097_152) throw new InvalidDataException("Oversized PCSX2 config.");
        var bytes = File.ReadAllBytes(path);
        if (bytes.Any(b => b == 0)) throw new InvalidDataException("Binary INI file.");
        return new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
    }

    // Parse only the indicated key. Reject duplicates and malformed values,
    // do not inspect other settings or attempt to canonicalize arbitrary INI.
    private static string? Value(string text, string sectionName, string keyName)
    {
        var section = "";
        string? foundValue = null;
        bool found = false;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith(';')) continue;
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line[1..^1].Trim();
                continue;
            }
            if (!section.Equals(sectionName, StringComparison.OrdinalIgnoreCase)) continue;
            int eq = line.IndexOf('=');
            if (eq < 0 || !line[..eq].Trim().Equals(keyName, StringComparison.OrdinalIgnoreCase))
                continue;
            if (found) throw new InvalidDataException("Duplicate relevant INI key.");
            found = true;
            foundValue = line[(eq + 1)..].Trim();
        }
        return foundValue;
    }

    private static float? ScaleValue(string text)
    {
        var raw = Value(text, "EmuCore/GS", "upscale_multiplier");
        if (raw is null) return null;
        if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var scale) ||
            !float.IsFinite(scale) || scale <= 0f)
            throw new InvalidDataException("Invalid internal-resolution scale.");
        return scale;
    }

    public static Report Inspect(LauncherSettings settings, string pcsx2DataDirectory)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.IsolatePcsx2Data)
            return Result(State.SharedProfile, null, null, null,
                "Shared/non-isolated PCSX2 configuration cannot be inspected by this tool.",
                "Use the isolated profile or check Undercover Game Properties manually.");

        var disc = Ps2DiscIdentifier.Inspect(settings.GameImage);
        if (disc.Recognition != DiscRecognition.Undercover ||
            disc.Serial is null || !SerialName.IsMatch(disc.Serial))
            return Result(State.UnverifiedDisc, null, null, null,
                "Undercover ISO serial is not verified; a game-specific graphics INI cannot be matched safely.",
                "Select an uncompressed Undercover PS2 ISO, or inspect PCSX2 Game Properties manually.");

        var serial = disc.Serial;
        try
        {
            var path = Pcsx2GraphicsAdapter.FindExistingIni(pcsx2DataDirectory);
            if (path is null)
                return Result(State.ConfigurationMissing, serial, null, null,
                    "No isolated PCSX2.ini was found. Initial emulator setup may be incomplete.",
                    "Complete PCSX2 setup first. No config or directory was created.");

            Guard(Path.GetFullPath(pcsx2DataDirectory));
            var dataRoot = Path.GetDirectoryName(Path.GetDirectoryName(path)!)!;
            Guard(dataRoot);
            Guard(Path.GetDirectoryName(path)!);
            var global = Pcsx2GraphicsAdapter.Inspect(settings, pcsx2DataDirectory).CurrentScale;
            var globalIni = ReadSafeIni(path);
            var folderValue = Value(globalIni, "Folders", "GameSettings");
            if (folderValue is not null &&
                !folderValue.Equals("gamesettings", StringComparison.OrdinalIgnoreCase))
                return Result(State.CustomFolder, serial, global, null,
                    "PCSX2 has a customized GameSettings directory. No arbitrary folders were scanned.",
                    "Check the GameSettings folder setting directly inside PCSX2.");

            var folder = Path.Combine(dataRoot, "gamesettings");
            if (!Directory.Exists(folder))
                return Result(State.NoMatchingGameIni, serial, global, null,
                    $"Saved global internal resolution: {global:0.##}×. No game-settings directory exists.",
                    "A separate per-game override cannot be ruled out; check PCSX2 Game Properties.");
            Guard(folder);

            // No ELF CRC is computed from the ISO in this tool. A unique
            // serial-specific file is *evidence*, not proof of a loaded CRC.
            var candidates = Directory.EnumerateFiles(folder, serial + "_*.ini",
                    SearchOption.TopDirectoryOnly)
                .Where(f => Path.GetFileName(f).StartsWith(serial + "_", StringComparison.OrdinalIgnoreCase) &&
                            CrcName.IsMatch(Path.GetFileName(f)[(serial.Length + 1)..]))
                .Take(2).ToArray();
            if (candidates.Length == 0)
                return Result(State.NoMatchingGameIni, serial, global, null,
                    $"Saved global internal resolution: {global:0.##}×. " +
                    "No matching serial-and-CRC game config was found.",
                    "PCSX2 may also use a legacy CRC-only config. Verify the game's actual graphics settings.");
            if (candidates.Length > 1)
                return Result(State.MultipleCrcs, serial, global, null,
                    "Multiple per-game configurations match this disc serial but use different ELF CRCs.",
                    "Open the matching Undercover game entry in PCSX2. No configuration was guessed.",
                    candidates.Length);

            var gameIni = ReadSafeIni(candidates[0]);
            var overrideScale = ScaleValue(gameIni);
            if (overrideScale is null)
                return Result(State.UniqueGameIni, serial, global, null,
                    $"Saved global internal resolution: {global:0.##}×. " +
                    "The unique serial-specific game INI has no upscale_multiplier override.",
                    "PCSX2 may still load a legacy CRC-only file or different running settings; check in-game rendering.",
                    1);

            var changesValue = overrideScale.Value != global;
            return Result(State.OverridePresent, serial, global, overrideScale,
                $"Saved global internal resolution: {global:0.##}×. " +
                $"Serial-specific game INI resolution: {overrideScale:0.##}×. " +
                (changesValue
                    ? "These saved values differ, so the game-specific override may supersede the global setting."
                    : "The saved values agree; an override still exists.") +
                " Live PCSX2 settings and the active ELF CRC are not verified.",
                changesValue
                    ? "Open Undercover Game Properties > Graphics. Review the game's Internal Resolution override."
                    : "The saved values agree; test actual rendering in Undercover to confirm.",
                1);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or
                                   InvalidDataException or ArgumentException)
        {
            return Result(State.InvalidConfiguration, serial, null, null,
                "The isolated emulator or per-game graphics configuration is ambiguous, malformed or unreadable.",
                "Inspect PCSX2 settings directly; no files were changed.");
        }
    }
}
