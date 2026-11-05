using CopperDisk;

namespace CopperDisk.Tests;

public sealed class AmigaFileSystemPartitionTests
{
    private static AmigaHardfilePartitionMetadata Partition(uint type, uint sectorsPerBlock = 1) => new()
    {
        DeviceName = "DH0", DosType = type, SizeBlockLongs = 128, SectorsPerBlock = sectorsPerBlock,
        Surfaces = 2, BlocksPerTrack = 32, LowCylinder = 4, HighCylinder = 58,
    };

    [Theory]
    [InlineData(0x444F5300u)]
    [InlineData(0x444F5301u)]
    [InlineData(0x444F5305u)]
    [InlineData(0x53465300u)]
    [InlineData(0x53465302u)]
    public void FileSystemBlocksDoNotChangeDeviceCylinderOffsets(uint type)
    {
        Assert.Equal((4L * 64 * 512, 55L * 64 * 512), AmigaFileSystemPartition.GetByteRange(Partition(type)));
        Assert.Equal((4L * 64 * 512, 55L * 64 * 512), AmigaFileSystemPartition.GetByteRange(Partition(type, 2)));
    }

    [Fact]
    public void ClassicFfsContractStillRejectsOtherFormats()
        => Assert.Throws<ArgumentException>(() => AmigaFfsPartition.GetByteRange(Partition(0x53465300)));

    [Fact]
    public void FilesystemBlockAlignmentIsRelativeToThePartition()
    {
        var partition = new AmigaHardfilePartitionMetadata { DeviceName = "DH0", DosType = 0x444F5301,
            Surfaces = 1, BlocksPerTrack = 1, LowCylinder = 1, HighCylinder = 8, SectorsPerBlock = 2 };
        Assert.Equal((512L, 4096L), AmigaFileSystemPartition.GetByteRange(partition));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(3u)]
    [InlineData(uint.MaxValue)]
    public void InvalidBlockGeometryIsRejected(uint sectorsPerBlock)
        => Assert.Throws<ArgumentException>(() => AmigaFileSystemPartition.GetByteRange(Partition(0x53465300, sectorsPerBlock)));
}
