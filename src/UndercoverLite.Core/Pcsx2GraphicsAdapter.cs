using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace UndercoverLite.Core;

/// <summary>
/// Conservative adapter for PCSX2 Qt 2.8.2's isolated GLOBAL GS setting:
/// [EmuCore/GS] upscale_multiplier = float. Does NOT modify CRC-specific game
/// overrides, auto-create PCSX2.ini, or write to external/shared PCSX2 profiles.
/// Backups are retained and concurrent edits are rejected.
/// </summary>
public static class Pcsx2GraphicsAdapter
{
    public sealed record Snapshot(
        string FilePath, string Hash, float CurrentScale, bool HasExplicitSetting);

    public static readonly int[] AllowedMultipliers = [1, 2, 3, 4, 6];

    public static string? FindExistingIni(string pcsx2DataDirectory)
    {
        if (string.IsNullOrWhiteSpace(pcsx2DataDirectory))
            throw new ArgumentException("An isolated PCSX2 data directory is required.", nameof(pcsx2DataDirectory));
        var root = Path.GetFullPath(pcsx2DataDirectory);
        var nested = Path.Combine(root, "PCSX2", "inis", "PCSX2.ini");
        var direct = Path.Combine(root, "inis", "PCSX2.ini");
        if (File.Exists(nested) && File.Exists(direct))
            throw new InvalidDataException("Both nested and direct PCSX2.ini files exist. Select the actual PCSX2 data location manually; nothing changed.");
        return File.Exists(nested) ? nested : File.Exists(direct) ? direct : null;
    }

    private static void Guard(string path)
    {
        if ((File.Exists(path) || Directory.Exists(path)) &&
            (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Reparse points are not supported for automatic graphics editing.");
    }

    private static byte[] ReadSafe(string path)
    {
        Guard(path);
        Guard(Path.GetDirectoryName(path)!);
        Guard(Path.GetDirectoryName(Path.GetDirectoryName(path)!)!);
        var file = new FileInfo(path);
        if (file.Length is < 1 or > 2_097_152)
            throw new InvalidDataException("PCSX2.ini is empty or too large for a safe graphics edit.");
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length > 2_097_152 || bytes.Any(b => b == 0))
            throw new InvalidDataException("Unsupported PCSX2.ini encoding or content.");
        // PCSX2 writes UTF-8 via SimpleIni. Retain UTF-8 BOM if the file has one.
        _ = new UTF8Encoding(false, true).GetString(bytes);
        return bytes;
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    // Preserves unrelated sections, whitespace and comments. Refuses ambiguous
    // duplicated GS sections / keys instead of guessing which takes effect.
    private static (float Current, bool Explicit, int Section, int Key, string Newline) Parse(string text)
    {
        string section = "";
        int targetSection = -1, keyIndex = -1;
        float value = 1.0f;
        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim().TrimStart('\uFEFF');
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith(';')) continue;
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line[1..^1].Trim();
                if (section.Equals("EmuCore/GS", StringComparison.OrdinalIgnoreCase))
                {
                    if (targetSection >= 0)
                        throw new InvalidDataException("Duplicate EmuCore/GS sections in PCSX2.ini.");
                    targetSection = i;
                }
                continue;
            }
            if (section.Equals("EmuCore/GS", StringComparison.OrdinalIgnoreCase))
            {
                int equal = line.IndexOf('=');
                if (equal < 0) continue;
                if (line[..equal].Trim().Equals("upscale_multiplier", StringComparison.OrdinalIgnoreCase))
                {
                    if (keyIndex >= 0)
                        throw new InvalidDataException("Duplicate upscale_multiplier settings in PCSX2.ini.");
                    string raw = line[(equal + 1)..].Trim();
                    if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
                        !float.IsFinite(value) || value <= 0)
                        throw new InvalidDataException("PCSX2 has an invalid internal resolution multiplier. No settings changed.");
                    keyIndex = i;
                }
            }
        }
        return (value, keyIndex >= 0, targetSection, keyIndex,
            text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n");
    }

    public static Snapshot Inspect(LauncherSettings settings, string pcsx2DataDirectory)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.IsolatePcsx2Data)
            throw new InvalidOperationException("Graphics editing is available only with isolated PCSX2 data.");
        string path = FindExistingIni(pcsx2DataDirectory) ??
            throw new FileNotFoundException("PCSX2.ini does not exist yet. Run the PCSX2 first-time setup before changing graphics.");
        Guard(Path.GetFullPath(pcsx2DataDirectory));
        byte[] bytes = ReadSafe(path);
        string text = new UTF8Encoding(false, true).GetString(bytes);
        var parsed = Parse(text);
        return new(path, Hash(bytes), parsed.Current, parsed.Explicit);
    }

    public static string Apply(LauncherSettings settings, string pcsx2DataDirectory, Snapshot expected, int multiplier)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(expected);
        if (!AllowedMultipliers.Contains(multiplier))
            throw new ArgumentOutOfRangeException(nameof(multiplier), "Use one of the tested UI values: 1, 2, 3, 4 or 6.");
        if (!settings.IsolatePcsx2Data)
            throw new InvalidOperationException("Refusing to modify a shared/non-isolated PCSX2 configuration.");
        var currentPath = FindExistingIni(pcsx2DataDirectory);
        if (currentPath is null ||
            !string.Equals(Path.GetFullPath(currentPath), Path.GetFullPath(expected.FilePath),
                StringComparison.OrdinalIgnoreCase))
            throw new IOException("PCSX2.ini location changed since the preview. No settings were written.");

        byte[] original = ReadSafe(currentPath);
        if (!string.Equals(Hash(original), expected.Hash, StringComparison.Ordinal))
            throw new IOException("PCSX2.ini was modified since the preview. Refresh before applying.");
        var encoding = new UTF8Encoding(false, true);
        var text = encoding.GetString(original);
        var parsed = Parse(text);
        if (parsed.Current == multiplier && parsed.Explicit)
            return ""; // No-op, no unnecessary backup.
        var lines = text.Split('\n').ToList();
        var replacement = "upscale_multiplier = " + multiplier.ToString("0.0", CultureInfo.InvariantCulture);
        if (parsed.Key >= 0)
        {
            // Preserve indentation and inline comments if straightforward.
            var old = lines[parsed.Key];
            string prefix = old[..(old.Length - old.TrimStart().Length)];
            lines[parsed.Key] = prefix + replacement + (old.EndsWith('\r') ? "\r" : "");
        }
        else if (parsed.Section >= 0)
        {
            int insert = parsed.Section + 1;
            lines.Insert(insert, replacement + (parsed.Newline == "\r\n" ? "\r" : ""));
        }
        else
        {
            if (lines.Count > 0 && lines[^1].Length != 0)
                lines.Add("");
            lines.Add("[EmuCore/GS]" + (parsed.Newline == "\r\n" ? "\r" : ""));
            lines.Add(replacement + (parsed.Newline == "\r\n" ? "\r" : ""));
            lines.Add("");
        }
        string rewritten = string.Join("\n", lines);
        if (original.Length >= 3 && original[0] == 0xEF && original[1] == 0xBB && original[2] == 0xBF &&
            !rewritten.StartsWith('\uFEFF')) rewritten = "\uFEFF" + rewritten;
        var newBytes = new UTF8Encoding(false).GetBytes(rewritten);
        var backup = currentPath + ".undercoverlite-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
                     + "-" + Guid.NewGuid().ToString("N")[..8] + ".bak";
        var stage = currentPath + ".undercoverlite-" + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            // Snapshot backup must be completed before replacing any live config.
            File.WriteAllBytes(backup, original);
            File.WriteAllBytes(stage, newBytes);
            if (!string.Equals(Hash(ReadSafe(currentPath)), expected.Hash, StringComparison.Ordinal))
                throw new IOException("PCSX2.ini changed during update. Original file is intact; backup retained.");
            File.Move(stage, currentPath, overwrite: true);
            return backup;
        }
        finally
        {
            if (File.Exists(stage)) File.Delete(stage);
        }
    }
}
