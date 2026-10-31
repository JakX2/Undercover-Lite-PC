using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace UndercoverLite.Core;

/// <summary>A user-created texture replacement pack, never distributed with game textures.</summary>
public sealed class GlyphPackManifest
{
    public int SchemaVersion { get; set; } = 1;
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string GameSerial { get; set; } = "";
    public string InputProfile { get; set; } = "";
    public string Description { get; set; } = "";
}

public sealed record GlyphPackPlan(
    string Name, string Serial, string DestinationDirectory,
    int NewFiles, int IdenticalFiles, int ConflictingFiles, IReadOnlyList<string> FileNames);

/// <summary>
/// Installs user-supplied PNG/DDS replacement images to an isolated PCSX2 data folder.
/// It never edits PCSX2 settings, overwrites existing images, or touches original ISOs.
/// </summary>
public static class GlyphPackManager
{
    // PCSX2 dump filenames contain TEX0/CLUT hashes, an optional subregion, and GS bits.
    // Restrict imports to conventional hash-style filenames rather than arbitrary files.
    private static readonly Regex TextureName = new(
        @"^[0-9a-f]{1,16}(?:-[0-9a-f]{1,16})?(?:-r\d+(?:x\d+)?)?-[0-9a-f]{8}(?:-mip\d+)?\.(?:png|dds)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(150));
    private static readonly Regex SerialName = new(
        @"^(?:SLUS|SLES|SLPM)-\d{5}$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(150));
    private static readonly Regex PackId = new(
        @"^[a-z0-9][a-z0-9_-]{1,63}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(150));

    public static string GetGameTextureDirectory(string pcsx2DataDirectory, string gameSerial)
    {
        if (string.IsNullOrWhiteSpace(pcsx2DataDirectory))
            throw new ArgumentException("PCSX2 data directory is required.", nameof(pcsx2DataDirectory));
        if (string.IsNullOrWhiteSpace(gameSerial) || !SerialName.IsMatch(gameSerial))
            throw new ArgumentException("A valid PS2 game serial is required.", nameof(gameSerial));
        return Path.Combine(Path.GetFullPath(pcsx2DataDirectory), "textures", gameSerial.ToUpperInvariant());
    }

    public static GlyphPackPlan Inspect(string packDirectory, string expectedSerial, string pcsx2DataDirectory)
    {
        if (string.IsNullOrWhiteSpace(packDirectory) || !Directory.Exists(packDirectory))
            throw new DirectoryNotFoundException("Select an existing glyph pack folder.");
        var pack = Path.GetFullPath(packDirectory);
        RejectLink(pack);
        var manifestFile = Path.Combine(pack, "glyph-pack.json");
        if (!File.Exists(manifestFile))
            throw new InvalidDataException("glyph-pack.json was not found in the selected folder.");
        RejectLink(manifestFile);
        var manifest = JsonSerializer.Deserialize<GlyphPackManifest>(File.ReadAllText(manifestFile),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ??
            throw new InvalidDataException("The glyph pack manifest is empty.");
        if (manifest.SchemaVersion != 1 || !PackId.IsMatch(manifest.Id ?? "") ||
            string.IsNullOrWhiteSpace(manifest.Name) || manifest.Name.Length > 120)
            throw new InvalidDataException("Unsupported glyph-pack manifest schema, ID, or name.");
        var destinationGameDir = GetGameTextureDirectory(pcsx2DataDirectory, expectedSerial);
        if (!string.Equals(manifest.GameSerial, expectedSerial, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"This pack targets {manifest.GameSerial}, not your verified game {expectedSerial}. Region-specific hashes cannot be assumed interchangeable.");
        var source = Path.Combine(pack, "replacements");
        if (!Directory.Exists(source))
            throw new InvalidDataException("The pack must have a replacements folder.");
        RejectLink(source);
        if (Directory.EnumerateDirectories(source).Any())
            throw new InvalidDataException("Nested replacement folders are not supported in this prototype.");

        var files = Directory.EnumerateFiles(source, "*", SearchOption.TopDirectoryOnly).ToArray();
        if (files.Length > 10000)
            throw new InvalidDataException("This pack contains too many files for the prototype installer.");
        var dest = Path.Combine(destinationGameDir, "replacements");
        var names = new List<string>(files.Length);
        int identical = 0, conflicts = 0;
        foreach (var file in files)
        {
            RejectLink(file);
            var name = Path.GetFileName(file);
            if (!TextureName.IsMatch(name))
                throw new InvalidDataException($"Unexpected texture filename: {name}. Use PCSX2's original dump filename.");
            names.Add(name);
            var target = Path.Combine(dest, name);
            if (File.Exists(target))
            {
                RejectLink(target);
                if (SameFileContents(file, target)) identical++;
                else conflicts++;
            }
        }
        if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Count)
            throw new InvalidDataException("The pack contains filenames that collide on Windows.");
        return new GlyphPackPlan(manifest.Name, expectedSerial.ToUpperInvariant(), dest,
            files.Length - identical - conflicts, identical, conflicts, names);
    }

    public static GlyphPackPlan Install(string packDirectory, string expectedSerial, string pcsx2DataDirectory)
    {
        var plan = Inspect(packDirectory, expectedSerial, pcsx2DataDirectory);
        if (plan.FileNames.Count == 0)
            throw new InvalidDataException("This pack contains no textures yet. Capture and prepare the replacement images first.");
        if (plan.ConflictingFiles != 0)
            throw new IOException($"Refusing to overwrite {plan.ConflictingFiles} existing texture(s). Back them up or remove them manually first.");
        if (plan.NewFiles == 0) return plan; // already installed
        var source = Path.Combine(Path.GetFullPath(packDirectory), "replacements");
        Directory.CreateDirectory(plan.DestinationDirectory);
        var installed = new List<string>();
        try
        {
            foreach (var name in plan.FileNames)
            {
                var target = Path.Combine(plan.DestinationDirectory, name);
                var file = Path.Combine(source, name);
                if (File.Exists(target))
                {
                    if (!SameFileContents(file, target))
                        throw new IOException($"Destination changed during installation: {name}");
                    continue;
                }
                var stage = Path.Combine(plan.DestinationDirectory, ".undercoverlite-" + Guid.NewGuid().ToString("N") + ".tmp");
                try
                {
                    File.Copy(file, stage, overwrite: false);
                    File.Move(stage, target, overwrite: false);
                    installed.Add(target);
                }
                finally
                {
                    if (File.Exists(stage)) File.Delete(stage);
                }
            }
        }
        catch
        {
            // Undo only files this installation created. Never touch pre-existing replacements.
            foreach (var path in installed)
            {
                try { File.Delete(path); } catch (IOException) { /* best effort; report original exception */ }
            }
            throw;
        }
        return plan;
    }

    private static bool SameFileContents(string a, string b)
    {
        if (new FileInfo(a).Length != new FileInfo(b).Length) return false;
        using var first = File.OpenRead(a);
        using var second = File.OpenRead(b);
        return SHA256.HashData(first).SequenceEqual(SHA256.HashData(second));
    }

    private static void RejectLink(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Symbolic links and reparse points are not supported in glyph packs.");
    }
}