using System.Security.Cryptography;
using System.Text.Json;

namespace UndercoverLite.Core;

/// <summary>
/// Opt-in management of PCSX2 replacement files. Changes only textures created
/// by this manager, or identical files the user explicitly chooses to adopt.
/// Not a live texture switcher: the game must be stopped.
/// </summary>
public static class ManagedGlyphThemes
{
    private const string StateFile = ".undercoverlite-glyph-state.json";
    private const long MaxBytes = 64L * 1024 * 1024;
    private const int MaxFiles = 512;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public sealed record PreviewResult(
        string PackName, string Serial, string Destination, int NewFiles,
        int ReplaceManaged, int RemoveManaged, int IdenticalManaged,
        int IdenticalUnmanaged, int ForeignConflicts, int ModifiedManaged)
    {
        public bool CanApply => ForeignConflicts == 0 && ModifiedManaged == 0;
    }

    public sealed record ChangeResult(string Name, int Changed);

    private sealed class State
    {
        public int Version { get; set; } = 1;
        public string Serial { get; set; } = "";
        public string TextureRoot { get; set; } = "";
        public string Name { get; set; } = "";
        public Dictionary<string, string> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed record Context(string Serial, string Root, string Game, string Replacements, string StatePath);

    private static Context Locate(string serial, string data, string? textureSearch)
    {
        var game = GlyphPackManager.GetGameTextureDirectory(data, serial, textureSearch);
        var root = Path.GetDirectoryName(game)!;
        var c = new Context(serial.ToUpperInvariant(), root, game,
            Path.Combine(game, "replacements"), Path.Combine(game, StateFile));
        Guard(c.Root); Guard(c.Game); Guard(c.Replacements); Guard(c.StatePath);
        return c;
    }

    private static void Guard(string path)
    {
        if ((File.Exists(path) || Directory.Exists(path)) &&
            (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Reparse points are not supported for managed glyphs: " + path);
    }

    private static string Hash(string file)
    {
        using var stream = File.OpenRead(file);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static bool Equal(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static bool SafeName(string n) =>
        n.Length is > 4 and < 180 && Path.GetFileName(n) == n &&
        !n.Contains('/') && !n.Contains('\\') &&
        (n.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
         n.EndsWith(".dds", StringComparison.OrdinalIgnoreCase));

    private static State? Read(Context c)
    {
        if (!File.Exists(c.StatePath)) return null;
        Guard(c.StatePath);
        var s = JsonSerializer.Deserialize<State>(File.ReadAllText(c.StatePath), Json)
            ?? throw new InvalidDataException("The managed glyph state file is empty.");
        if (s.Version != 1 || s.Serial != c.Serial || !Equal(s.TextureRoot, c.Root) ||
            s.Files is null || s.Files.Count > MaxFiles ||
            s.Files.Keys.Any(n => !SafeName(n)) ||
            s.Files.Values.Any(h => h.Length != 64 || !h.All(Uri.IsHexDigit)))
            throw new InvalidDataException("The managed glyph state does not match this serial and texture root.");
        s.Files = new Dictionary<string, string>(s.Files, StringComparer.OrdinalIgnoreCase);
        return s;
    }

    private static Dictionary<string, string> Pack(string folder, Context c, out string name)
    {
        // Existing inspector verifies manifest, game serial, source filenames and symlinks.
        var inspected = GlyphPackManager.Inspect(folder, c.Serial, c.Root, c.Root);
        if (inspected.FileNames.Count is 0 or > MaxFiles)
            throw new InvalidDataException("A managed pack must contain 1 to 512 images.");
        name = inspected.Name;
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var length = 0L;
        foreach (var file in inspected.FileNames)
        {
            var source = Path.Combine(folder, "replacements", file);
            length = checked(length + new FileInfo(source).Length);
            if (length > MaxBytes)
                throw new InvalidDataException("Managed packs cannot exceed 64 MiB.");
            result.Add(file, Hash(source));
        }
        return result;
    }

    private static bool OwnedUnchanged(Context c, State s, string name)
    {
        var target = Path.Combine(c.Replacements, name);
        Guard(target);
        return File.Exists(target) && Equal(Hash(target), s.Files[name]);
    }

    public static string? ActivePack(string serial, string data, string? search = null) =>
        Read(Locate(serial, data, search))?.Name;

    public static PreviewResult Preview(string packFolder, string serial, string data, string? search = null)
    {
        var c = Locate(serial, data, search);
        var old = Read(c);
        var incoming = Pack(packFolder, c, out var name);
        int add = 0, replace = 0, remove = 0, same = 0, adopt = 0, conflict = 0, modified = 0;
        foreach (var existing in old?.Files.Keys ?? Array.Empty<string>())
        {
            if (!OwnedUnchanged(c, old!, existing)) modified++;
            if (!incoming.ContainsKey(existing)) remove++;
        }
        foreach (var (file, digest) in incoming)
        {
            var dest = Path.Combine(c.Replacements, file);
            Guard(dest);
            if (old?.Files.ContainsKey(file) == true)
            {
                if (Equal(old.Files[file], digest)) same++;
                else replace++;
            }
            else if (!File.Exists(dest)) add++;
            else if (Equal(Hash(dest), digest)) adopt++;
            else conflict++;
        }
        return new PreviewResult(name, c.Serial, c.Replacements,
            add, replace, remove, same, adopt, conflict, modified);
    }

    private static void WriteState(Context c, State? state)
    {
        if (state is null)
        {
            if (File.Exists(c.StatePath)) File.Delete(c.StatePath);
            return;
        }
        var temp = c.StatePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(state, Json));
            File.Move(temp, c.StatePath, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private static void WriteImage(string destination, byte[] bytes, bool replace)
    {
        var temp = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temp, bytes);
            File.Move(temp, destination, overwrite: replace);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public static ChangeResult Activate(string packFolder, string serial, string data,
        string? search = null, bool adoptIdenticalUnmanaged = false)
    {
        var c = Locate(serial, data, search);
        var plan = Preview(packFolder, serial, data, search);
        if (!plan.CanApply)
            throw new IOException("Cannot activate: foreign conflicting or modified managed textures exist.");
        if (plan.IdenticalUnmanaged > 0 && !adoptIdenticalUnmanaged)
            throw new InvalidOperationException("Adoption requires explicit confirmation for pre-existing identical glyph files.");

        var nextHashes = Pack(packFolder, c, out var packName);
        var old = Read(c);
        var oldNames = old?.Files.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = oldNames.Union(nextHashes.Keys, StringComparer.OrdinalIgnoreCase).ToArray();

        // Stage images and original snapshots in memory before modifying disk.
        // 64 MiB per side prevents unbounded memory use.
        var staged = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        var before = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        long previousBytes = 0;
        foreach (var name in names)
        {
            var path = Path.Combine(c.Replacements, name);
            Guard(path);
            if (!File.Exists(path)) continue;
            if (oldNames.Contains(name))
            {
                if (!OwnedUnchanged(c, old!, name))
                    throw new IOException("A managed texture changed while preparing activation: " + name);
            }
            else if (!nextHashes.TryGetValue(name, out var expected) || !Equal(Hash(path), expected) ||
                !adoptIdenticalUnmanaged)
                throw new IOException("An unmanaged texture cannot be overwritten or adopted: " + name);
            var bytes = File.ReadAllBytes(path);
            previousBytes = checked(previousBytes + bytes.LongLength);
            if (previousBytes > MaxBytes)
                throw new InvalidDataException("Existing managed textures exceed the 64 MiB safety limit.");
            before.Add(name, bytes);
        }

        foreach (var (name, digest) in nextHashes)
        {
            var bytes = File.ReadAllBytes(Path.Combine(packFolder, "replacements", name));
            if (!Equal(Convert.ToHexString(SHA256.HashData(bytes)), digest))
                throw new IOException("The selected glyph pack changed during preparation: " + name);
            staged.Add(name, bytes);
        }
        var state = new State { Serial = c.Serial, TextureRoot = c.Root, Name = packName, Files = nextHashes };
        Directory.CreateDirectory(c.Replacements);
        var changed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var removed in oldNames.Except(nextHashes.Keys, StringComparer.OrdinalIgnoreCase))
            {
                File.Delete(Path.Combine(c.Replacements, removed));
                changed.Add(removed);
            }
            foreach (var (name, digest) in nextHashes)
            {
                var dest = Path.Combine(c.Replacements, name);
                Guard(dest);
                if (File.Exists(dest) && Equal(Hash(dest), digest)) continue;
                // Never overwrite an unknown file that appeared since the snapshot.
                if (!before.ContainsKey(name) && File.Exists(dest))
                    throw new IOException("An unknown glyph appeared during activation: " + name);
                WriteImage(dest, staged[name], replace: before.ContainsKey(name));
                changed.Add(name);
            }
            WriteState(c, state);
            return new ChangeResult(packName, changed.Count);
        }
        catch
        {
            // Recover only paths actually modified by this operation.
            foreach (var item in changed)
            {
                var path = Path.Combine(c.Replacements, item);
                try
                {
                    if (before.TryGetValue(item, out var bytes)) WriteImage(path, bytes, replace: true);
                    else if (File.Exists(path)) File.Delete(path);
                }
                catch (IOException) { /* best-effort rollback; preserve original exception */ }
            }
            throw;
        }
    }

    public static ChangeResult Deactivate(string serial, string data, string? search = null)
    {
        var c = Locate(serial, data, search);
        var state = Read(c);
        if (state is null) return new ChangeResult("None", 0);
        var saved = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        long bytes = 0;
        foreach (var name in state.Files.Keys)
        {
            if (!OwnedUnchanged(c, state, name))
                throw new IOException("A managed glyph was edited or removed; refusing to delete it: " + name);
            var content = File.ReadAllBytes(Path.Combine(c.Replacements, name));
            bytes = checked(bytes + content.LongLength);
            if (bytes > MaxBytes) throw new InvalidDataException("Managed glyphs exceed the rollback size limit.");
            saved.Add(name, content);
        }
        var removed = new List<string>();
        try
        {
            foreach (var name in state.Files.Keys)
            {
                File.Delete(Path.Combine(c.Replacements, name));
                removed.Add(name);
            }
            WriteState(c, null);
            return new ChangeResult(state.Name, removed.Count);
        }
        catch
        {
            foreach (var name in removed)
            {
                try { WriteImage(Path.Combine(c.Replacements, name), saved[name], replace: true); }
                catch (IOException) { /* best-effort rollback */ }
            }
            throw;
        }
    }
}
