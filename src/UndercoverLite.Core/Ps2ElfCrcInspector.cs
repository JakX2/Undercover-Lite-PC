using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;

namespace UndercoverLite.Core;

/// <summary>
/// Computes exactly PCSX2 2.8.2 ElfObject::GetCRC(): XOR of the ELF file's
/// little-endian u32 words; trailing 1-3 bytes do not participate.
/// Reads only a verified uncompressed ISO9660 PS2 image, its BOOT2 path and ELF.
/// No extraction to disk, writes, BIOS access, emulator startup or SHA hashing.
/// </summary>
public static class Ps2ElfCrcInspector
{
    public enum Status { Verified, NotSupported, NotFound, InvalidIso, InvalidElf, ReadError }

    public sealed record Result(Status State, string? Serial, uint? Crc, string Message)
    {
        public string? Hex => Crc.HasValue ? Crc.Value.ToString("X8") : null;
    }

    private const int Sector = 2048;
    private const int MaxDirectory = 4 * 1024 * 1024;
    private const int MaxSystemCnf = 64 * 1024;
    private const int MaxElfBytes = 256 * 1024 * 1024;
    private static readonly Regex BootPath = new(
        @"^\s*BOOT2\s*=\s*cdrom0?:[\\/]+(?<path>[A-Za-z0-9_\-./\\]+)(?:;\d+)?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));

    private readonly record struct Entry(long Offset, int Length, bool IsDirectory);

    private static Result Fail(Status status, string message, string? serial = null) =>
        new(status, serial, null, message);

    public static Result Inspect(string isoPath)
    {
        var identification = Ps2DiscIdentifier.Inspect(isoPath);
        if (!identification.IsUndercover || identification.Serial is null)
            return Fail(Status.NotSupported,
                "An uncompressed, ISO9660 Undercover PS2 image with a verified BOOT2 serial is required.");
        var serial = identification.Serial;
        try
        {
            using var file = new FileStream(isoPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (file.Length < 17L * Sector) return Fail(Status.InvalidIso, "Image is too short.", serial);
            var pvd = new byte[Sector];
            file.Position = 16L * Sector;
            file.ReadExactly(pvd);
            if (pvd[0] != 1 || pvd[6] != 1 || !pvd.AsSpan(1, 5).SequenceEqual("CD001"u8))
                return Fail(Status.InvalidIso, "No supported ISO9660 primary volume descriptor.", serial);
            if (!TryRecord(pvd.AsSpan(156), file.Length, out var root) || !root.IsDirectory)
                return Fail(Status.InvalidIso, "Invalid ISO9660 root directory.", serial);

            var cnfEntry = Find(file, root, "SYSTEM.CNF");
            if (!cnfEntry.HasValue || cnfEntry.Value.IsDirectory ||
                cnfEntry.Value.Length is < 1 or > MaxSystemCnf)
                return Fail(Status.InvalidIso, "Missing or invalid SYSTEM.CNF file.", serial);
            var cnfBytes = new byte[cnfEntry.Value.Length];
            file.Position = cnfEntry.Value.Offset;
            file.ReadExactly(cnfBytes);
            string cnf = Encoding.ASCII.GetString(cnfBytes);
            string? boot = null;
            foreach (string line in cnf.Split('\n'))
            {
                var match = BootPath.Match(line.Trim().TrimEnd('\r'));
                if (!match.Success) continue;
                if (boot is not null)
                    return Fail(Status.InvalidIso, "Multiple BOOT2 executable paths are ambiguous.", serial);
                boot = match.Groups["path"].Value.Replace('\\', '/');
            }
            if (string.IsNullOrWhiteSpace(boot))
                return Fail(Status.NotFound, "No supported CDROM BOOT2 executable path.", serial);

            var segments = boot.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length is < 1 or > 8 ||
                segments.Any(part => part.Length is < 1 or > 64 ||
                    part is "." or ".." || part.Contains(';') ||
                    part.Any(ch => !char.IsAsciiLetterOrDigit(ch) && ch is not ('_' or '-' or '.'))))
                return Fail(Status.InvalidIso, "Unsafe or unsupported BOOT2 path.", serial);

            Entry current = root;
            for (int n = 0; n < segments.Length; n++)
            {
                var entry = Find(file, current, segments[n]);
                if (!entry.HasValue)
                    return Fail(Status.NotFound, "BOOT2 executable could not be found inside the ISO.", serial);
                if (n + 1 < segments.Length && !entry.Value.IsDirectory)
                    return Fail(Status.InvalidIso, "BOOT2 parent is not an ISO directory.", serial);
                current = entry.Value;
            }
            if (current.IsDirectory || current.Length is < 53 or > MaxElfBytes)
                return Fail(Status.InvalidElf, "BOOT2 executable has an invalid size.", serial);

            var header = new byte[16];
            file.Position = current.Offset;
            file.ReadExactly(header);
            if (!(header[0] == 0x7f && header[1] == (byte)'E' &&
                  header[2] == (byte)'L' && header[3] == (byte)'F' &&
                  header[4] == 1 && header[5] == 1))
                return Fail(Status.InvalidElf, "BOOT2 file is not a little-endian 32-bit PS2 ELF.", serial);

            // Exact PCSX2 ElfObject::GetCRC behavior, which is NOT the CRC32 polynomial:
            // for each full little-endian u32 in the *entire* ELF file, XOR into result.
            // A bounded streaming read avoids allocating up to 256 MiB.
            uint xor = 0;
            file.Position = current.Offset;
            int remaining = current.Length;
            var buffer = new byte[64 * 1024];
            while (remaining >= 4)
            {
                int take = Math.Min(buffer.Length, remaining & ~3);
                file.ReadExactly(buffer.AsSpan(0, take));
                for (int i = 0; i < take; i += 4)
                    xor ^= BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(i, 4));
                remaining -= take;
            }
            return new(Status.Verified, serial, xor,
                $"Verified PS2 ELF XOR CRC {xor:X8} for Undercover serial {serial} (ISO bytes; no emulator state checked).");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or
                                   InvalidDataException or OverflowException)
        {
            return Fail(Status.ReadError,
                "The ISO executable metadata or ELF could not be read safely.", serial);
        }
    }

    private static bool TryRecord(ReadOnlySpan<byte> bytes, long fileSize, out Entry entry)
    {
        entry = default;
        if (bytes.Length < 34 || bytes[0] < 34 || bytes[0] > bytes.Length ||
            33 + bytes[32] > bytes[0])
            return false;
        uint sector = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(2, 4));
        uint length = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(10, 4));
        long start = (long)sector * Sector;
        if (length > int.MaxValue || start > fileSize || length > fileSize - start)
            return false;
        entry = new(start, (int)length, (bytes[25] & 2) != 0);
        return true;
    }

    private static Entry? Find(FileStream file, Entry dir, string target)
    {
        if (!dir.IsDirectory || dir.Length is < 1 or > MaxDirectory)
            throw new InvalidDataException("Invalid or oversized directory.");
        var sector = new byte[Sector];
        var count = (dir.Length + Sector - 1) / Sector;
        for (int s = 0; s < count; s++)
        {
            int available = Math.Min(Sector, dir.Length - s * Sector);
            file.Position = dir.Offset + (long)s * Sector;
            file.ReadExactly(sector.AsSpan(0, available));
            int index = 0;
            while (index < available)
            {
                int len = sector[index];
                if (len == 0) break;
                if (len < 34 || index + len > available)
                    throw new InvalidDataException("Invalid ISO9660 directory entry.");
                var record = sector.AsSpan(index, len);
                if (!TryRecord(record, file.Length, out var entry))
                    throw new InvalidDataException("Bad directory record bounds.");
                int nameLength = record[32];
                string filename = Encoding.ASCII.GetString(record.Slice(33, nameLength));
                var cut = filename.IndexOf(';');
                if (cut >= 0) filename = filename[..cut];
                if (filename.Equals(target, StringComparison.OrdinalIgnoreCase))
                    return entry;
                index += len;
            }
        }
        return null;
    }
}
