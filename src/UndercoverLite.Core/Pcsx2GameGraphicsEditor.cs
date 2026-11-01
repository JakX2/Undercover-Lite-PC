using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace UndercoverLite.Core;

/// <summary>
/// 0.17: An opt-in editor for an EXISTING, exact SERIAL_ELFCRC.ini belonging to
/// the verified Undercover ISO. Never creates a game settings file, edits a
/// legacy CRC-only file, or touches shared profiles. This is a saved PCSX2
/// setting, not a running-emulator or native game configuration.
/// </summary>
public static class Pcsx2GameGraphicsEditor
{
    public sealed record Snapshot(
        string FilePath, string FileHash, string GlobalConfigHash,
        string Serial, string ElfCrc, float? GameScale, float GlobalScale);

    private const int MaxIniBytes = 2 * 1024 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    private static void GuardPath(string path)
    {
        // Check the entire existing path chain, not just the leaf. Symlinked
        // emulator roots/directories could otherwise escape the isolated data.
        var current = Path.GetFullPath(path);
        while (current is not null)
        {
            if ((File.Exists(current) || Directory.Exists(current)) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Redirected PCSX2 configuration paths cannot be edited.");
            var parent = Path.GetDirectoryName(current);
            if (parent is null || parent == current) break;
            current = parent;
        }
    }

    private static byte[] ReadSafe(string path)
    {
        GuardPath(path);
        var size = new FileInfo(path).Length;
        if (size is < 1 or > MaxIniBytes)
            throw new InvalidDataException("PCSX2 settings file is empty or too large.");
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length > MaxIniBytes || bytes.Any(b => b == 0))
            throw new InvalidDataException("PCSX2 configuration contains unsupported bytes.");
        _ = StrictUtf8.GetString(bytes);
        return bytes;
    }

    private static string Hash(byte[] content) => Convert.ToHexString(SHA256.HashData(content));

    // Only a standard GameSettings location is acceptable. Refuse repeated
    // Folders sections/keys rather than trusting one arbitrary interpretation.
    private static void CheckDefaultGameFolder(string config)
    {
        var inFolders = false;
        var seenFolders = false;
        var seenKey = false;
        foreach (var raw in config.Split('\n'))
        {
            var line = raw.Trim().TrimStart('\uFEFF');
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#')) continue;
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                inFolders = line[1..^1].Trim().Equals("Folders", StringComparison.OrdinalIgnoreCase);
                if (inFolders && seenFolders)
                    throw new InvalidDataException("Duplicate PCSX2 Folders sections.");
                seenFolders |= inFolders;
                continue;
            }
            if (!inFolders) continue;
            int equals = line.IndexOf('=');
            if (equals < 0 || !line[..equals].Trim().Equals("GameSettings", StringComparison.OrdinalIgnoreCase))
                continue;
            if (seenKey) throw new InvalidDataException("Duplicate GameSettings folder values.");
            seenKey = true;
            if (!line[(equals + 1)..].Trim().Equals("gamesettings", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("A customized PCSX2 GameSettings folder is not supported.");
        }
    }

    private static (float? Value, int Section, int Key, string Newline) ParseGame(string text)
    {
        int sectionIndex = -1, keyIndex = -1;
        float? value = null;
        bool inGs = false;
        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim().TrimStart('\uFEFF');
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#')) continue;
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                inGs = line[1..^1].Trim().Equals("EmuCore/GS", StringComparison.OrdinalIgnoreCase);
                if (inGs)
                {
                    if (sectionIndex >= 0)
                        throw new InvalidDataException("Duplicate per-game graphics sections.");
                    sectionIndex = i;
                }
                continue;
            }
            if (!inGs) continue;
            var eq = line.IndexOf('=');
            if (eq < 0 || !line[..eq].Trim().Equals("upscale_multiplier", StringComparison.OrdinalIgnoreCase))
                continue;
            if (keyIndex >= 0) throw new InvalidDataException("Duplicate game upscale_multiplier keys.");
            var rawScale = line[(eq + 1)..].Trim();
            if (!float.TryParse(rawScale, NumberStyles.Float, CultureInfo.InvariantCulture, out var scale) ||
                !float.IsFinite(scale) || scale <= 0)
                throw new InvalidDataException("Invalid saved game resolution multiplier.");
            keyIndex = i;
            value = scale;
        }
        return (value, sectionIndex, keyIndex,
            text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n");
    }

    public static Snapshot Inspect(LauncherSettings settings, string pcsx2DataDirectory)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.IsolatePcsx2Data)
            throw new InvalidOperationException("The game-specific graphics editor requires isolated PCSX2 data.");
        var elf = Ps2ElfCrcInspector.Inspect(settings.GameImage);
        if (elf.State != Ps2ElfCrcInspector.Status.Verified || elf.Serial is null || elf.Hex is null)
            throw new InvalidDataException("Select an uncompressed Undercover ISO with a verified BOOT2 ELF CRC.");

        var global = Pcsx2GraphicsAdapter.Inspect(settings, pcsx2DataDirectory);
        GuardPath(global.FilePath);
        var globalBytes = ReadSafe(global.FilePath);
        CheckDefaultGameFolder(StrictUtf8.GetString(globalBytes));
        var dataRoot = Path.GetDirectoryName(Path.GetDirectoryName(global.FilePath)!)!;
        var folder = Path.Combine(dataRoot, "gamesettings");
        GuardPath(folder);
        if (!Directory.Exists(folder))
            throw new FileNotFoundException("Open Undercover Game Properties in PCSX2 first to create the game-specific settings.");
        var target = Path.Combine(folder, elf.Serial + "_" + elf.Hex + ".ini");
        if (!File.Exists(target))
            throw new FileNotFoundException(
                "No exact SERIAL_CRC game settings file exists. Create a per-game override in PCSX2 Game Properties first. Legacy CRC-only files are never edited.");
        var bytes = ReadSafe(target);
        var parsed = ParseGame(StrictUtf8.GetString(bytes));
        return new(target, Hash(bytes), Hash(globalBytes), elf.Serial, elf.Hex, parsed.Value, global.CurrentScale);
    }

    public static string Apply(LauncherSettings settings, string pcsx2DataDirectory,
        Snapshot expected, int multiplier)
    {
        ArgumentNullException.ThrowIfNull(expected);
        if (!Pcsx2GraphicsAdapter.AllowedMultipliers.Contains(multiplier))
            throw new ArgumentOutOfRangeException(nameof(multiplier), "Use 1x, 2x, 3x, 4x or 6x.");

        // Revalidate ISO CRC, exact config path, folder layout and both hashes.
        // No stale preview may write to a changed game or emulator profile.
        var live = Inspect(settings, pcsx2DataDirectory);
        if (!string.Equals(live.FilePath, expected.FilePath, StringComparison.OrdinalIgnoreCase) ||
            live.Serial != expected.Serial || live.ElfCrc != expected.ElfCrc ||
            live.FileHash != expected.FileHash || live.GlobalConfigHash != expected.GlobalConfigHash)
            throw new IOException("The ISO or PCSX2 settings changed since preview. Refresh before applying.");

        var original = ReadSafe(live.FilePath);
        if (Hash(original) != expected.FileHash)
            throw new IOException("Per-game settings changed during the update. Nothing was written.");
        var text = StrictUtf8.GetString(original);
        var parsed = ParseGame(text);
        if (parsed.Value == multiplier) return ""; // Identical explicit value: no-op.

        var lines = text.Split('\n').ToList();
        var replacement = "upscale_multiplier = " + multiplier.ToString("0.0", CultureInfo.InvariantCulture);
        var carriage = parsed.Newline == "\r\n" ? "\r" : "";
        if (parsed.Key >= 0)
        {
            var old = lines[parsed.Key];
            var whitespace = old[..(old.Length - old.TrimStart().Length)];
            lines[parsed.Key] = whitespace + replacement + (old.EndsWith('\r') ? "\r" : "");
        }
        else if (parsed.Section >= 0)
            lines.Insert(parsed.Section + 1, replacement + carriage);
        else
        {
            if (lines.Count > 0 && lines[^1].Length != 0) lines.Add("");
            lines.Add("[EmuCore/GS]" + carriage);
            lines.Add(replacement + carriage);
            lines.Add("");
        }

        var newText = string.Join("\n", lines);
        var newBytes = new UTF8Encoding(false).GetBytes(newText);
        var suffix = ".undercoverlite-" +
            DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) +
            "-" + Guid.NewGuid().ToString("N")[..8];
        var backup = live.FilePath + suffix + ".bak";
        var stage = live.FilePath + suffix + ".tmp";
        try
        {
            File.WriteAllBytes(backup, original);
            File.WriteAllBytes(stage, newBytes);
            // Recheck the original before atomically replacing it.
            if (Hash(ReadSafe(live.FilePath)) != expected.FileHash)
                throw new IOException("Per-game settings changed before replacement; the original is intact.");
            File.Move(stage, live.FilePath, overwrite: true);
            return backup;
        }
        finally
        {
            if (File.Exists(stage)) File.Delete(stage);
        }
    }
}
