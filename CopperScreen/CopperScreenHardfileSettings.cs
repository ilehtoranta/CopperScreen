
namespace CopperScreen;

internal sealed record CopperScreenHardfileSettings(
	int Unit,
	string Path,
	bool ReadOnly,
	long CreateSizeBytes,
	AmigaHardfileMountMode Mode = AmigaHardfileMountMode.Auto,
	AmigaHardfilePartitionMetadata? Partition = null)
{
    internal static CopperScreenHardfileSettings Ffs(int unit, string path, bool readOnly, string geometry)
    {
        var p = CopperDisk.AmigaFfsPartition.ParseGeometry(geometry);
        return new(unit, path, readOnly, 0, AmigaHardfileMountMode.Partition, new()
        {
            DeviceName = p.DeviceName, TableSize = p.TableSize, DosType = p.DosType,
            SizeBlockLongs = p.SizeBlockLongs, SectorsPerBlock = p.SectorsPerBlock,
            Surfaces = p.Surfaces, BlocksPerTrack = p.BlocksPerTrack,
            LowCylinder = p.LowCylinder, HighCylinder = p.HighCylinder,
            ReservedBlocks = p.ReservedBlocks, MaxTransfer = p.MaxTransfer, Mask = p.Mask,
        });
    }

    internal void ValidateCopperStart(bool checkImage)
    {
        if (Unit != 0 || Mode is not (AmigaHardfileMountMode.Partition or AmigaHardfileMountMode.RigidDiskBlock) || CreateSizeBytes != 0)
            throw new NotSupportedException("CopperStart supports one existing hard disk image on unit 0. Select RDB for partitions stored in the image, or Partition for explicit FFS geometry. Image creation and automatic layout detection are not supported.");
        if (Mode == AmigaHardfileMountMode.RigidDiskBlock)
        {
            if (checkImage) CopperDisk.AmigaRdbImage.Validate(Path);
            return;
        }
        var (start, length) = CopperDisk.AmigaFfsPartition.GetByteRange(CopperScreenLightweightSession.ConvertPartition(Partition));
        if (checkImage)
        {
            var info = new FileInfo(Path);
            if (!info.Exists) throw new FileNotFoundException("Choose an existing formatted FFS hard disk image.", Path);
            if (info.Length % 512 != 0 || info.Length < start + length)
                throw new InvalidOperationException("The hard disk image must contain the entire configured FFS partition and have a size divisible by 512 bytes.");
        }
    }
}
