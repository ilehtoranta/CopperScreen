using System;
using System.Globalization;

namespace CopperDisk;

/// <summary>Explicit 512-byte DOS1 partition settings for the bounded CopperStart boot path.</summary>
public static class AmigaFfsPartition
{
    /// <summary>Parse DEVICE:HEADS:BLOCKS-PER-TRACK:FIRST-CYLINDER:LAST-CYLINDER (inclusive).</summary>
    public static AmigaHardfilePartitionMetadata ParseGeometry(string value)
    {
        var parts = value.Split(':');
        if (parts.Length != 5) throw new ArgumentException("FFS geometry requires DEVICE:HEADS:BLOCKS-PER-TRACK:FIRST-CYLINDER:LAST-CYLINDER, for example DH0:2:32:4:58.");
        uint Number(int index) => uint.TryParse(parts[index], NumberStyles.None, CultureInfo.InvariantCulture, out var n)
            ? n : throw new ArgumentException("FFS geometry requires nonnegative decimal cylinder and track values.");
        var partition = new AmigaHardfilePartitionMetadata
        {
            DeviceName = parts[0], TableSize = 16, DosType = 0x444F5301,
            SizeBlockLongs = 128, SectorsPerBlock = 1,
            Surfaces = Number(1), BlocksPerTrack = Number(2), LowCylinder = Number(3), HighCylinder = Number(4),
            ReservedBlocks = 2, MaxTransfer = 0x1FE00, Mask = 0xFFFFFFFE,
        };
        GetByteRange(partition);
        return partition;
    }

    /// <summary>Validate 32-bit FFS block keys and a host-addressable TD64 byte range.</summary>
    public static (long Start, long Length) GetByteRange(AmigaHardfilePartitionMetadata? p)
    {
        if (p is null || p.DosType != 0x444F5301 || (p.SizeBlockLongs ?? 128) != 128 || (p.SectorsPerBlock ?? 1) != 1)
            throw new ArgumentException("CopperStart requires an explicit FFS (DOS1) partition with 512-byte blocks.");
        if (string.IsNullOrWhiteSpace(p.DeviceName) || p.DeviceName.IndexOfAny([':', '/', '\\']) >= 0)
            throw new ArgumentException("Enter the partition device name without a colon, for example DH0.");
        if (p.Surfaces is null or 0 || p.BlocksPerTrack is null or 0 || p.LowCylinder is null || p.HighCylinder is null || p.HighCylinder < p.LowCylinder)
            throw new ArgumentException("Enter heads, blocks per track, and inclusive first and last cylinders for the formatted FFS partition.");
        if ((p.TableSize ?? 16) < 16 || (p.MaxTransfer ?? 0x200000) < 512)
            throw new ArgumentException("FFS needs an environment vector through DOS type and a maximum transfer of at least 512 bytes.");
        // Bound products before multiplication. FFS keys count blocks, not bytes;
        // the backing stream uses signed 64-bit byte positions.
        var cylinder = (ulong)p.Surfaces.Value * p.BlocksPerTrack.Value;
        var cylinders = (ulong)p.HighCylinder.Value - p.LowCylinder.Value + 1;
        if (cylinders > uint.MaxValue / cylinder)
            throw new ArgumentException("The FFS partition exceeds its 32-bit block count.");
        if ((ulong)p.HighCylinder.Value + 1 > ((ulong)long.MaxValue / 512) / cylinder)
            throw new ArgumentException("The FFS partition exceeds the backing file's 64-bit byte range.");
        var start = (long)((ulong)p.LowCylinder.Value * cylinder * 512);
        var length = (long)(((ulong)p.HighCylinder.Value - p.LowCylinder.Value + 1) * cylinder * 512);
        var reserved = p.ReservedBlocks ?? 2;
        if (reserved == 0 || (ulong)reserved + 1 >= (ulong)length / 512)
            throw new ArgumentException("The FFS partition must contain its reserved blocks and filesystem data.");
        return (start, length);
    }
}
