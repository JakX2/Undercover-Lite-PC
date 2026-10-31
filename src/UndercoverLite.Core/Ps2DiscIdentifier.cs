using System.Text;
using System.Text.RegularExpressions;

namespace UndercoverLite.Core;

public enum DiscRecognition
{
    Undercover,
    DifferentGame,
    Unknown,
    NotInspected
}

/// <summary>Non-destructive, bounded inspection of the ISO9660 SYSTEM.CNF entry in a PS2 .iso.</summary>
public sealed record DiscInspection(DiscRecognition Recognition, string Message, string? Serial = null)
{
    public bool IsUndercover => Recognition == DiscRecognition.Undercover;
}

public static class Ps2DiscIdentifier
{
    private const int SectorBytes = 2048;
    private const int MaxRootDirectoryBytes = 4 * 1024 * 1024;
    private const int MaxSystemCnfBytes = 64 * 1024;
    private static readonly HashSet<string> UndercoverSerials = new(StringComparer.OrdinalIgnoreCase)
    {
        "SLUS-21801", // North America
        "SLES-55349", "SLES-55350", "SLES-55351", "SLES-55352", "SLES-55353", // PAL
        "SLPM-55127", "SLPM-55244" // Japan
    };
    private static readonly Regex BootSerial = new(
        @"(?<![A-Z0-9])([A-Z]{4})[_-](\d{3})\.(\d{2})(?!\d)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));

    public static DiscInspection Inspect(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            return new(DiscRecognition.Unknown, "Game image was not found.");

        if (!string.Equals(Path.GetExtension(imagePath), ".iso", StringComparison.OrdinalIgnoreCase))
            return new(DiscRecognition.NotInspected,
                "Only uncompressed ISO images can be identified here. Compressed images can still launch through PCSX2.");

        try
        {
            using var stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length < 17L * SectorBytes)
                return new(DiscRecognition.Unknown, "ISO is too short for a valid ISO9660 volume descriptor.");

            Span<byte> descriptor = stackalloc byte[SectorBytes];
            stream.Position = 16L * SectorBytes;
            stream.ReadExactly(descriptor);
            if (descriptor[0] != 1 || descriptor[6] != 1 ||
                !descriptor.Slice(1, 5).SequenceEqual("CD001"u8))
                return new(DiscRecognition.Unknown, "ISO9660 primary volume descriptor was not found.");

            ReadOnlySpan<byte> root = descriptor.Slice(156);
            if (!TryDirectoryEntry(root, stream.Length, out long rootOffset, out int rootSize) ||
                rootSize > MaxRootDirectoryBytes)
                return new(DiscRecognition.Unknown, "ISO9660 root directory has invalid bounds.");

            var sector = new byte[SectorBytes];
            for (int sectorStart = 0; sectorStart < rootSize; sectorStart += SectorBytes)
            {
                int available = Math.Min(SectorBytes, rootSize - sectorStart);
                stream.Position = rootOffset + sectorStart;
                stream.ReadExactly(sector.AsSpan(0, available));
                int offset = 0;
                while (offset < available)
                {
                    int length = sector[offset];
                    if (length == 0) break; // next logical directory sector
                    if (length < 34 || length > available - offset)
                        return new(DiscRecognition.Unknown, "ISO9660 directory contains an invalid record.");
                    ReadOnlySpan<byte> entry = sector.AsSpan(offset, length);
                    int nameLength = entry[32];
                    if (33 + nameLength > length)
                        return new(DiscRecognition.Unknown, "ISO9660 filename record is truncated.");
                    string name = Encoding.ASCII.GetString(entry.Slice(33, nameLength));
                    if (name.Split(';', 2)[0].Equals("SYSTEM.CNF", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!TryDirectoryEntry(entry, stream.Length, out long cnfOffset, out int cnfSize) ||
                            cnfSize <= 0 || cnfSize > MaxSystemCnfBytes)
                            return new(DiscRecognition.Unknown, "SYSTEM.CNF has invalid bounds.");
                        var content = new byte[cnfSize];
                        stream.Position = cnfOffset;
                        stream.ReadExactly(content);
                        return IdentifySystemCnf(Encoding.ASCII.GetString(content));
                    }
                    offset += length;
                }
            }
            return new(DiscRecognition.Unknown, "SYSTEM.CNF was not found in the ISO root directory.");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return new(DiscRecognition.Unknown, "Could not read ISO: " + e.Message);
        }
    }

    /// <summary>Public for isolated tests. Does not rely on the ISO filename.</summary>
    public static DiscInspection IdentifySystemCnf(string cnf)
    {
        if (string.IsNullOrEmpty(cnf))
            return new(DiscRecognition.Unknown, "SYSTEM.CNF does not contain a PS2 boot serial.");
        // Only consider a BOOT2 line; unrelated filenames do not determine game identity.
        foreach (var line in cnf.Split('\n'))
        {
            var clean = line.Trim().TrimEnd('\r');
            if (!clean.StartsWith("BOOT2", StringComparison.OrdinalIgnoreCase)) continue;
            int equals = clean.IndexOf('=');
            if (equals < 0 || !clean[..equals].Trim().Equals("BOOT2", StringComparison.OrdinalIgnoreCase))
                continue;
            var match = BootSerial.Match(clean[(equals + 1)..]);
            if (!match.Success) break;
            string serial = $"{match.Groups[1].Value.ToUpperInvariant()}-{match.Groups[2].Value}{match.Groups[3].Value}";
            if (UndercoverSerials.Contains(serial))
                return new(DiscRecognition.Undercover, $"Need for Speed: Undercover (PS2), serial {serial}.", serial);
            return new(DiscRecognition.DifferentGame, $"This image reports PS2 serial {serial}, which is not in the known Undercover list.", serial);
        }
        return new(DiscRecognition.Unknown, "No recognizable BOOT2 disc serial was found in SYSTEM.CNF.");
    }

    private static bool TryDirectoryEntry(ReadOnlySpan<byte> record, long imageSize, out long offset, out int size)
    {
        offset = 0;
        size = 0;
        if (record.Length < 34 || record[0] < 34) return false;
        uint sector = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(record.Slice(2, 4));
        uint length = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(record.Slice(10, 4));
        long byteOffset = (long)sector * SectorBytes;
        if (length > int.MaxValue || byteOffset > imageSize || length > imageSize - byteOffset)
            return false;
        offset = byteOffset;
        size = (int)length;
        return true;
    }
}