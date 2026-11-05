using System;

namespace CopperDisk;

/// <summary>Explicit partition geometry for CopperStart's Full filesystem ROM pair.</summary>
public static class AmigaFileSystemPartition
{
    /// <summary>Validate DOS0-DOS5, classic SFS or SFS2 geometry and return its backing-file byte range.</summary>
    public static (long Start, long Length) GetByteRange(AmigaHardfilePartitionMetadata? p)
    {
        if (p is null || !(p.DosType >= 0x444F5300 && p.DosType <= 0x444F5305 || p.DosType is 0x53465300 or 0x53465302))
            throw new ArgumentException("Choose an OFS, FFS (DOS0-DOS5) or SFS/SFS2 partition type.");
        if (string.IsNullOrWhiteSpace(p.DeviceName) || p.DeviceName.IndexOfAny([':', '/', '\\']) >= 0)
            throw new ArgumentException("Enter the partition device name without a colon, for example DH0.");
        if (p.Surfaces is null or 0 || p.BlocksPerTrack is null or 0 || p.LowCylinder is null ||
            p.HighCylinder is null || p.HighCylinder < p.LowCylinder || (p.TableSize ?? 16) < 16)
            throw new ArgumentException("Enter heads, sectors per track, inclusive cylinders and an environment vector through DOS type.");
        var words = p.SizeBlockLongs ?? 128;
        var perBlock = p.SectorsPerBlock ?? 1;
        var sectorBytes = (ulong)words * 4;
        var blockBytes = sectorBytes * perBlock;
        if (sectorBytes < 512 || sectorBytes > uint.MaxValue || (sectorBytes & (sectorBytes - 1)) != 0 ||
            perBlock == 0 || blockBytes > uint.MaxValue / 8 || (blockBytes & (blockBytes - 1)) != 0 ||
            (p.MaxTransfer ?? 0x200000) < sectorBytes)
            throw new ArgumentException("Use power-of-two filesystem blocks of at least 512 bytes and a maximum transfer covering a device sector.");
        var cylinder = (ulong)p.Surfaces.Value * p.BlocksPerTrack.Value;
        var cylinders = (ulong)p.HighCylinder.Value - p.LowCylinder.Value + 1;
        if (cylinders > ulong.MaxValue / cylinder)
            throw new ArgumentException("The partition sector count overflows.");
        var sectors = cylinders * cylinder;
        if (sectors % perBlock != 0 || sectors / perBlock > uint.MaxValue)
            throw new ArgumentException("The partition must contain whole filesystem blocks within the 32-bit block-key range.");
        if ((ulong)p.HighCylinder.Value + 1 > (ulong)long.MaxValue / sectorBytes / cylinder)
            throw new ArgumentException("The partition exceeds the backing file's 64-bit byte range.");
        var reserved = p.ReservedBlocks ?? 2;
        if (reserved == 0 || (ulong)reserved + 1 >= sectors / perBlock)
            throw new ArgumentException("The partition must contain its reserved blocks and filesystem data.");
        return ((long)((ulong)p.LowCylinder.Value * cylinder * sectorBytes), (long)(sectors * sectorBytes));
    }
}
