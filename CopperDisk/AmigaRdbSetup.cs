using System.Buffers.Binary;
using System.Text;

namespace CopperDisk;

/// <summary>A partition extent, including partitions disabled for mounting.</summary>
public sealed record AmigaRdbSetupPartition(string DeviceName, uint FirstCylinder, uint LastCylinder, uint DosType);

/// <summary>Validated geometry for the bounded 512-byte RDB setup editor.</summary>
public sealed record AmigaRdbSetupLayout(uint Heads, uint SectorsPerTrack, uint FirstCylinder,
    uint LastCylinder, IReadOnlyList<AmigaRdbSetupPartition> Partitions);

/// <summary>Offline RDB setup. Mutations belong on an unpublished copy, never on a mounted image.</summary>
public static class AmigaRdbSetup
{
    private const uint End = uint.MaxValue;
    private static uint U(byte[] b, int offset) => BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(offset));
    private static void Put(byte[] b, int offset, uint value) => BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(offset), value);
    private static byte[] Read(Stream disk, uint block)
    {
        if ((ulong)block * 512 + 512 > (ulong)disk.Length) throw new InvalidDataException("RDB block is outside the image.");
        disk.Position = (long)block * 512;
        var data = new byte[512]; disk.ReadExactly(data); return data;
    }
    private static void Check(byte[] b, uint magic, uint minimumWords = 64)
    {
        var words = U(b, 4);
        if (U(b, 0) != magic || words < minimumWords || words > 128) throw new InvalidDataException("Unsupported or damaged RDB metadata.");
        uint sum = 0; for (var at = 0; at < words * 4; at += 4) sum = unchecked(sum + U(b, at));
        if (sum != 0) throw new InvalidDataException("RDB metadata checksum is invalid.");
    }
    private static void Seal(byte[] b)
    {
        Put(b, 8, 0); uint sum = 0;
        for (var at = 0; at < U(b, 4) * 4; at += 4) sum = unchecked(sum + U(b, at));
        Put(b, 8, unchecked(0u - sum));
    }
    private static void Write(Stream disk, uint block, byte[] data)
    { Seal(data); disk.Position = (long)block * 512; disk.Write(data); }

    private sealed record Parsed(uint Block, byte[] Header, AmigaRdbSetupLayout Layout);
    private static Parsed Parse(Stream disk)
    {
        if (!disk.CanSeek || !disk.CanRead || disk.Length < 512 || disk.Length % 512 != 0)
            throw new InvalidDataException("Choose a sector-aligned RDB image.");
        for (uint block = 0; block < Math.Min(16L, disk.Length / 512); block++)
        {
            var b = Read(disk, block);
            if (U(b, 0) != 0x5244534B) continue;
            Check(b, 0x5244534B);
            if (U(b, 16) != 512) throw new NotSupportedException("Partition setup currently requires 512-byte RDB blocks.");
            var heads = U(b, 72); var sectors = U(b, 68); var low = U(b, 136); var high = U(b, 140);
            var cylinder = (ulong)heads * sectors;
            if (heads == 0 || sectors == 0 || cylinder > uint.MaxValue || U(b, 144) != cylinder ||
                high < low || high >= U(b, 64) || ((ulong)high + 1) * cylinder > (ulong)disk.Length / 512 ||
                U(b, 128) > block || U(b, 132) < block || U(b, 128) > U(b, 132) || U(b, 132) - U(b, 128) > 65535 ||
                (ulong)U(b, 132) >= (ulong)low * cylinder || U(b, 152) < block || U(b, 152) > U(b, 132))
                throw new InvalidDataException("Unsupported or inconsistent RDB geometry/reserved area.");
            var partitions = new List<AmigaRdbSetupPartition>();
            var next = U(b, 28); var seen = new HashSet<uint> { block };
            while (next != End)
            {
                if (partitions.Count >= 128 || !seen.Add(next) || next < U(b, 128) || next > U(b, 152))
                    throw new InvalidDataException("Invalid RDB partition chain.");
                var part = Read(disk, next); Check(part, 0x50415254);
                if (U(part, 128) < 16 || U(part, 128) > 95 || U(part, 132) != 128 ||
                    U(part, 140) != heads || U(part, 148) != sectors || U(part, 144) == 0 ||
                    U(part, 164) < low || U(part, 168) > high || U(part, 168) < U(part, 164) || part[36] is 0 or > 31)
                    throw new InvalidDataException("Unsupported or inconsistent partition geometry.");
                var p = new AmigaRdbSetupPartition(Encoding.Latin1.GetString(part, 37, part[36]), U(part, 164), U(part, 168), U(part, 192));
                if (partitions.Any(old => p.FirstCylinder <= old.LastCylinder && old.FirstCylinder <= p.LastCylinder))
                    throw new InvalidDataException("Existing RDB partitions overlap.");
                partitions.Add(p); next = U(part, 16);
            }
            // Every referenced metadata block must precede the declared high-water
            // mark before a zero block after that mark can be used for a new PART.
            void ValidateChain(uint head, uint magic, uint minimumWords)
            {
                while (head != End)
                {
                    if (seen.Count >= 1024 || !seen.Add(head) || head < U(b, 128) || head > U(b, 152))
                        throw new InvalidDataException("Invalid or unsupported RDB metadata chain/high-water mark.");
                    var item = Read(disk, head); Check(item, magic, minimumWords);
                    if (magic == 0x46534844) ValidateChain(U(item, 72), 0x4C534547, 5);
                    head = U(item, 16);
                }
            }
            ValidateChain(U(b, 24), 0x42414442, 6);
            ValidateChain(U(b, 32), 0x46534844, 20);
            ValidateChain(U(b, 36), 0x4C534547, 5);
            return new(block, b, new(heads, sectors, low, high, partitions.AsReadOnly()));
        }
        throw new InvalidDataException("No RDB was found. Existing partition images cannot be repartitioned here.");
    }

    /// <summary>Read strict geometry; malformed chains and unsupported layouts fail closed.</summary>
    public static AmigaRdbSetupLayout ReadLayout(Stream disk) => Parse(disk).Layout;

    /// <summary>Initialize an empty, sector-aligned unpublished image with a standard RDB.</summary>
    public static AmigaRdbSetupLayout Initialize(Stream disk)
    {
        if (!disk.CanWrite || !disk.CanSeek || disk.Length < 8 * 1024 * 1024 || disk.Length % 32768 != 0 || disk.Length / 32768 > uint.MaxValue)
            throw new ArgumentException("New RDB images need at least 8 MiB and whole 32 KiB cylinders.");
        // Caller must supply a newly created image. Never silently replace a partition table.
        disk.Position = 0; var prefix = new byte[32768]; disk.ReadExactly(prefix);
        if (prefix.AsSpan().ContainsAnyExcept((byte)0)) throw new InvalidDataException("Initialize requires a blank new image.");
        var b = new byte[512]; Put(b, 0, 0x5244534B); Put(b, 4, 64); Put(b, 12, 7); Put(b, 16, 512);
        Put(b, 24, End); Put(b, 28, End); Put(b, 32, End); Put(b, 36, End);
        Put(b, 64, (uint)(disk.Length / 32768)); Put(b, 68, 32); Put(b, 72, 2);
        Put(b, 128, 0); Put(b, 132, 63); Put(b, 136, 1); Put(b, 140, (uint)(disk.Length / 32768 - 1));
        Put(b, 144, 64); Put(b, 152, 0); Write(disk, 0, b);
        return ReadLayout(disk);
    }

    /// <summary>Add one partition in free cylinders. Existing partition blocks and contents are retained.</summary>
    public static AmigaHardfilePartitionMetadata AddPartition(Stream disk, string device, uint first, uint last, uint dosType, bool bootable)
    {
        var parsed = Parse(disk); var layout = parsed.Layout; var b = parsed.Header;
        if (!disk.CanWrite) throw new UnauthorizedAccessException("The staging image must be writable.");
        if (string.IsNullOrWhiteSpace(device) || device.Length > 31 || device.Any(c => !char.IsAsciiLetterOrDigit(c)))
            throw new ArgumentException("Use 1–31 ASCII letters or digits for the device name, for example DH0.");
        if (layout.Partitions.Count >= 128 || layout.Partitions.Any(p => string.Equals(p.DeviceName, device, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Choose a unique device name; at most 128 partitions are supported.");
        if (first < layout.FirstCylinder || last > layout.LastCylinder || last < first ||
            layout.Partitions.Any(p => first <= p.LastCylinder && p.FirstCylinder <= last))
            throw new ArgumentException("Choose whole free cylinders outside existing partitions and RDB metadata.");
        var slot = (ulong)U(b, 152) + 1;
        while (slot <= U(b, 132) && Read(disk, (uint)slot).AsSpan().ContainsAnyExcept((byte)0)) slot++;
        if (slot > U(b, 132)) throw new InvalidDataException("No unused RDB metadata block remains.");
        var p = new byte[512]; Put(p, 0, 0x50415254); Put(p, 4, 64); Put(p, 12, 7);
        Put(p, 16, U(b, 28)); Put(p, 20, bootable ? 1u : 0); p[36] = (byte)device.Length;
        Encoding.ASCII.GetBytes(device).CopyTo(p, 37);
        Put(p, 128, 16); Put(p, 132, 128); Put(p, 140, layout.Heads); Put(p, 144, 1);
        Put(p, 148, layout.SectorsPerTrack); Put(p, 152, 2); Put(p, 164, first); Put(p, 168, last);
        Put(p, 172, 30); Put(p, 176, 1); Put(p, 180, 0x1FE00); Put(p, 184, 0xFFFFFFFE); Put(p, 192, dosType);
        Write(disk, (uint)slot, p); Put(b, 28, (uint)slot); Put(b, 152, (uint)slot); Write(disk, parsed.Block, b);
        return new() { DeviceName = device, TableSize = 16, SizeBlockLongs = 128, Surfaces = layout.Heads,
            SectorsPerBlock = 1, BlocksPerTrack = layout.SectorsPerTrack, ReservedBlocks = 2,
            LowCylinder = first, HighCylinder = last, NumBuffers = 30, BufferMemoryType = 1,
            MaxTransfer = 0x1FE00, Mask = 0xFFFFFFFE, BootPriority = 0, DosType = dosType };
    }
}
