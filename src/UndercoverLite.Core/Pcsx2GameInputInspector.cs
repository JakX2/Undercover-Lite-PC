using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace UndercoverLite.Core;

/// <summary>
/// Read-only inspection of a *saved* PCSX2 2.8.2 per-game input-profile selection.
/// PCSX2 VMManager uses &lt;SERIAL&gt;_&lt;ELF-CRC&gt;.ini in GameSettings and
/// [EmuCore] InputProfileName. This is not a live controller/runtime query.
/// </summary>
public static class Pcsx2GameInputInspector
{
    public enum Kind
    {
        UnverifiedDisc, ConfigurationMissing, UnsupportedFolder, NoGameFile,
        AmbiguousCrc, InvalidFile, SharedBindings, SelectedProfile
    }

    public sealed record Result(
        Kind Status, string? DiscSerial, string? SelectedProfile,
        bool? SelectedProfileFileExists, bool? RecognizedProfileMatchesSource,
        int CandidateCount, string Summary, string NextStep)
    {
        public bool HasProvenSelection => Status is Kind.SharedBindings or Kind.SelectedProfile;
    }

    private static readonly Regex ValidSerial = new(
        @"^[A-Z]{4}-[0-9]{5}$", RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(200));
    private static readonly Regex GameFileSuffix = new(
        @"^[0-9a-fA-F]{8}\.ini$", RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(200));
    private const int MaxIniBytes = 2 * 1024 * 1024;

    private static Result Report(Kind kind, string? serial, string summary, string next,
        string? profile = null, bool? fileExists = null, bool? sourceMatches = null, int candidates = 0)
        => new(kind, serial, profile, fileExists, sourceMatches, candidates, summary, next);

    private static void VerifyFile(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Linked or redirected config files cannot be inspected safely.");
        if (new FileInfo(path).Length > MaxIniBytes)
            throw new InvalidDataException("PCSX2 configuration file is too large.");
    }

    private static string ReadIni(string path)
    {
        VerifyFile(path);
        var bytes = File.ReadAllBytes(path);
        if (bytes.Any(b => b == 0))
            throw new InvalidDataException("PCSX2 INI is not a UTF-8 text file.");
        return new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
    }

    /// <summary>Returns null when the key is absent; rejects duplicate values.</summary>
    private static string? ReadValue(string text, string wantedSection, string wantedKey)
    {
        string section = "";
        string? result = null;
        bool found = false;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#')) continue;
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line[1..^1].Trim();
                continue;
            }
            if (!section.Equals(wantedSection, StringComparison.OrdinalIgnoreCase)) continue;
            var equal = line.IndexOf('=');
            if (equal < 0 || !line[..equal].Trim().Equals(wantedKey, StringComparison.OrdinalIgnoreCase))
                continue;
            if (found) throw new InvalidDataException("Duplicated PCSX2 configuration key.");
            found = true;
            result = line[(equal + 1)..].Trim();
        }
        return result;
    }

    private static bool? CheckKnownProfile(string name, string content)
    {
        string? expected = null;
        if (name.Equals(UndercoverKeyboardProfile.Name, StringComparison.OrdinalIgnoreCase))
            expected = UndercoverKeyboardProfile.RenderIni();
        else
        {
            foreach (SteeringProfiles.Strength strength in Enum.GetValues<SteeringProfiles.Strength>())
            {
                foreach (bool menu in new[] { false, true })
                {
                    var choice = new SteeringProfiles.Choice(strength, menu);
                    if (choice.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    {
                        expected = SteeringProfiles.RenderIni(choice);
                        break;
                    }
                }
                if (expected is not null) break;
            }
        }
        return expected is null ? null :
            string.Equals(expected.Replace("\r\n", "\n"), content.Replace("\r\n", "\n"),
                StringComparison.Ordinal);
    }

    public static Result Inspect(LauncherSettings settings, string pcsx2DataDirectory)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.IsolatePcsx2Data)
            return Report(Kind.UnsupportedFolder, null,
                "The launcher uses a shared PCSX2 profile; this inspector only reads isolated data.",
                "Switch to isolated PCSX2 data or review Undercover Game Properties manually.");

        var disc = Ps2DiscIdentifier.Inspect(settings.GameImage);
        if (disc.Recognition != DiscRecognition.Undercover || disc.Serial is null ||
            !ValidSerial.IsMatch(disc.Serial))
            return Report(Kind.UnverifiedDisc, null,
                "The configured image's Undercover PS2 serial has not been verified.",
                "Select an uncompressed Undercover PS2 ISO to match its saved game settings. " +
                "Compressed images cannot currently be matched safely.");

        string serial = disc.Serial;
        string? ini;
        try { ini = Pcsx2GraphicsAdapter.FindExistingIni(pcsx2DataDirectory); }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        {
            return Report(Kind.UnsupportedFolder, serial,
                "Multiple or unreadable emulator settings locations were found.",
                "Inspect the actual PCSX2 Data Directory and use its Game Properties manually.");
        }
        if (ini is null)
            return Report(Kind.ConfigurationMissing, serial,
                "PCSX2.ini has not been found in the isolated emulator data folder.",
                "Run PCSX2 setup first; no files were created by this check.");

        try
        {
            var dataRoot = Path.GetDirectoryName(Path.GetDirectoryName(ini)!)!;
            var config = ReadIni(ini);
            var customGameFolder = ReadValue(config, "Folders", "GameSettings");
            var customInputFolder = ReadValue(config, "Folders", "InputProfiles");
            if ((customGameFolder is not null &&
                 !customGameFolder.Equals("gamesettings", StringComparison.OrdinalIgnoreCase)) ||
                (customInputFolder is not null &&
                 !customInputFolder.Equals("inputprofiles", StringComparison.OrdinalIgnoreCase)))
                return Report(Kind.UnsupportedFolder, serial,
                    "PCSX2 has custom GameSettings or InputProfiles folder settings.",
                    "Inspect the configured folders in PCSX2; this tool intentionally does not scan arbitrary paths.");

            string directory = Path.Combine(dataRoot, "gamesettings");
            if (!Directory.Exists(directory))
                return Report(Kind.NoGameFile, serial,
                    "No PCSX2 per-game settings directory exists for this isolated profile.",
                    "Open Undercover Game Properties to create/check a per-game input selection.");
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                return Report(Kind.UnsupportedFolder, serial,
                    "The PCSX2 game-settings directory is redirected or linked.",
                    "Verify the actual game settings location in PCSX2.");

            // Only filenames of exactly <VERIFIED_SERIAL>_<EIGHT-HEX-CRC>.ini.
            // Never attribute a legacy CRC-only INI to a disc without its ELF CRC.
            var matches = Directory.EnumerateFiles(directory, serial + "_*.ini",
                    SearchOption.TopDirectoryOnly)
                .Where(p =>
                    Path.GetFileName(p).StartsWith(serial + "_", StringComparison.OrdinalIgnoreCase) &&
                    GameFileSuffix.IsMatch(Path.GetFileName(p)[(serial.Length + 1)..]))
                .Take(2).ToList();
            if (matches.Count == 0)
                return Report(Kind.NoGameFile, serial,
                    "No serial-and-CRC per-game INI matches this verified Undercover edition.",
                    "PCSX2 may use Shared settings, a legacy CRC-only file, or a different folder. " +
                    "Check Undercover Game Properties manually.");
            if (matches.Count > 1)
                return Report(Kind.AmbiguousCrc, serial,
                    "Multiple game-setting INIs match this disc serial but have different ELF CRCs.",
                    "Choose the correct Undercover entry in PCSX2's game list; this tool will not guess.",
                    candidates: matches.Count);

            var gameText = ReadIni(matches[0]);
            var profile = ReadValue(gameText, "EmuCore", "InputProfileName");
            if (string.IsNullOrWhiteSpace(profile))
                return Report(Kind.SharedBindings, serial,
                    "The unique matching per-game INI has no InputProfileName; PCSX2 selects Shared bindings.",
                    "Select an UndercoverLite input profile in Undercover Game Properties if using keyboard glyphs.",
                    candidates: 1);

            // Validate to avoid interpreting paths as profile names.
            if (profile.Length > 128 || profile.Contains('/') || profile.Contains('\\') ||
                profile.Contains("..", StringComparison.Ordinal) ||
                profile.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_')))
                return Report(Kind.InvalidFile, serial,
                    "A per-game InputProfileName contains invalid characters.",
                    "Inspect Undercover Game Properties; this tool does not open unsafe profile paths.",
                    candidates: 1);

            var profilePath = Path.Combine(dataRoot, "inputprofiles", profile + ".ini");
            if (!File.Exists(profilePath))
                return Report(Kind.SelectedProfile, serial,
                    $"The saved Undercover game settings select {profile}, but its input-profile file is missing.",
                    "Reinstall that profile or select Shared in Undercover Game Properties.",
                    profile, false, null, 1);
            var profileText = ReadIni(profilePath);
            var matching = CheckKnownProfile(profile, profileText);
            string note = matching switch
            {
                true => "It matches the original Undercover Lite generated binding table.",
                false => "It differs from the Undercover Lite generated binding table.",
                null => "It is a custom/unknown profile; its keys were not verified."
            };
            return Report(Kind.SelectedProfile, serial,
                $"The unique saved Undercover game settings select {profile}. {note} " +
                "This is a DISK configuration read, not a live emulator confirmation.",
                "Close PCSX2 before changes; inspect the in-game controls and glyph theme together.",
                profile, true, matching, 1);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or
                                   InvalidDataException or ArgumentException)
        {
            return Report(Kind.InvalidFile, serial,
                "PCSX2 per-game configuration could not be read or validated safely.",
                "Inspect Undercover Game Properties manually; no files have been modified.");
        }
    }
}
