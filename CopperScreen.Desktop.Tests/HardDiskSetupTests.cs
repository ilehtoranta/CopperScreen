using CopperDisk;
using CopperScreen;
using System.Buffers.Binary;

namespace CopperScreen.Tests;

public sealed class HardDiskSetupTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "copperscreen-setup-" + Guid.NewGuid().ToString("N"));
    public HardDiskSetupTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public void FreeRangeSearchHandlesGapsAndFullDisks()
    {
        var layout = new AmigaRdbSetupLayout(2, 32, 1, 100, [new("DH0", 1, 20, 0x444F5301), new("DH1", 40, 100, 0x444F5301)]);
        Assert.Equal((21u, 39u), CopperScreenHardDiskSetup.FirstFreeRange(layout));
        Assert.Null(CopperScreenHardDiskSetup.FirstFreeRange(layout with { Partitions = [new("DH0", 1, 100, 0x444F5301)] }));
    }
    [Theory]
    [InlineData(0, 512u)] [InlineData(0, 1024u)] [InlineData(0, 2048u)] [InlineData(0, 4096u)]
    [InlineData(1, 512u)] [InlineData(1, 1024u)] [InlineData(1, 2048u)] [InlineData(1, 4096u)]
    [InlineData(2, 512u)] [InlineData(2, 1024u)] [InlineData(2, 2048u)] [InlineData(2, 4096u)]
    [InlineData(3, 512u)] [InlineData(3, 1024u)] [InlineData(3, 2048u)] [InlineData(3, 4096u)]
    public void CreatesEachFilesystemAndPreservesSourceWhenAddingAnotherPartition(int filesystem, uint blockSize)
    {
        var fs = (CopperScreenHardDiskSetup.FileSystem)filesystem;
        var source = Path.Combine(_root, "source.hdf");
        CopperScreenHardDiskSetup.Create(source, 16 * 1048576, null, "DH0", 1, 255, fs, "ÄWork", true, blockSize: blockSize);
        var before = File.ReadAllBytes(source);
        var first = Assert.Single(CopperScreenHardDiskSetup.Inspect(source).Partitions);
        Assert.Equal(CopperScreenHardDiskSetup.DosType(fs), first.DosType);
        Assert.Equal(blockSize, first.BlockSize);
        Assert.Equal(128u, BinaryPrimitives.ReadUInt32BigEndian(before.AsSpan(512 + 132)));
        Assert.Equal(blockSize / 512, BinaryPrimitives.ReadUInt32BigEndian(before.AsSpan(512 + 144)));
        var destination = Path.Combine(_root, "copy.hdf");
        CopperScreenHardDiskSetup.Create(destination, 0, source, "DH1", 256, 511, fs, "Data", false, blockSize: blockSize);
        Assert.Equal(before, File.ReadAllBytes(source));
        Assert.Equal(2, CopperScreenHardDiskSetup.Inspect(destination).Partitions.Count);
        var after = File.ReadAllBytes(destination);
        Assert.Equal(before.AsSpan(32768, 255 * 32768).ToArray(), after.AsSpan(32768, 255 * 32768).ToArray());
        AmigaRdbImage.Validate(destination);
        Assert.DoesNotContain(Directory.GetFiles(_root), f => f.EndsWith(".tmp"));
    }

    [Theory]
    [InlineData("1.5", "GiB", 1610612736L)]
    [InlineData("8", "MiB", 8388608L)]
    public void SizesAcceptMiBAndGiBWithoutAllocatingAnImage(string text, string unit, long bytes) =>
        Assert.Equal(bytes, CopperScreenHardDiskSetup.ParseSize(text, unit));

    [Fact]
    public void PlanningFindsAContiguousGapAndRoundsWithoutTouchingExistingPartitions()
    {
        var layout = new AmigaRdbSetupLayout(2, 32, 1, 100, [new("DH0", 21, 40, 0x444F5301)]);
        var plan = CopperScreenHardDiskSetup.PlanPartition(layout, 1048576 + 1000, 4096);
        Assert.Equal(new CopperScreenHardDiskSetup.PartitionRange(41, 72, 1048576), plan);
        Assert.Equal(new CopperScreenHardDiskSetup.PartitionRange(1, 20, 20 * 32768), CopperScreenHardDiskSetup.PlanPartition(layout, null));
        Assert.Throws<ArgumentException>(() => CopperScreenHardDiskSetup.PlanPartition(layout, 3 * 1048576));
        Assert.Throws<ArgumentException>(() => CopperScreenHardDiskSetup.PlanPartition(layout, null, first: 20, last: 22));
        Assert.Throws<ArgumentException>(() => CopperScreenHardDiskSetup.PlanPartition(layout, null, first: 0, last: 20));
        Assert.Throws<ArgumentException>(() => CopperScreenHardDiskSetup.PlanPartition(layout, null, first: 90, last: uint.MaxValue));
        Assert.Equal(plan, CopperScreenHardDiskSetup.PlanPartition(layout, null, 4096, 41, 72));
    }

    [Fact]
    public void PlanningAlignsCustomGeometryAndRejectsSubCylinderSizes()
    {
        var layout = new AmigaRdbSetupLayout(1, 3, 1, 100, []);
        Assert.Equal(new CopperScreenHardDiskSetup.PartitionRange(1, 8, 12288), CopperScreenHardDiskSetup.PlanPartition(layout, 13000, 4096));
        Assert.Throws<ArgumentException>(() => CopperScreenHardDiskSetup.PlanPartition(layout, 10000, 4096));
        Assert.Throws<ArgumentException>(() => CopperScreenHardDiskSetup.PlanPartition(layout, null, 4096, 1, 3));
        Assert.Equal(8 * 1048576, CopperScreenHardDiskSetup.AlignNewCapacity(8 * 1048576 + 1000));
        Assert.Throws<OverflowException>(() => CopperScreenHardDiskSetup.ParseSize(long.MaxValue.ToString(), "GiB"));
    }

    [Theory]
    [InlineData(256u)] [InlineData(1000u)] [InlineData(8192u)]
    public void UnsupportedBlockSizesDoNotPublishFiles(uint blockSize)
    {
        var path = Path.Combine(_root, "invalid.hdf");
        Assert.Throws<ArgumentException>(() => CopperScreenHardDiskSetup.Create(path, 8 * 1048576, null, "DH0", null, null,
            CopperScreenHardDiskSetup.FileSystem.Ffs, "Work", false, blockSize: blockSize));
        Assert.Empty(Directory.GetFiles(_root));
    }

    [Fact]
    public void OverlapCancellationAndExistingDestinationNeverPublishOrChangeSource()
    {
        var source = Path.Combine(_root, "source.hdf");
        CopperScreenHardDiskSetup.Create(source, 16 * 1048576, null, "DH0", 1, 255, CopperScreenHardDiskSetup.FileSystem.Ffs, "System", true);
        var before = File.ReadAllBytes(source); var destination = Path.Combine(_root, "copy.hdf");
        Assert.Throws<ArgumentException>(() => CopperScreenHardDiskSetup.Create(destination, 0, source, "DH1", 100, 511, CopperScreenHardDiskSetup.FileSystem.Ffs, "Work", false));
        Assert.False(File.Exists(destination));
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Assert.Throws<OperationCanceledException>(() => CopperScreenHardDiskSetup.Create(destination, 0, source, "DH1", 256, 511, CopperScreenHardDiskSetup.FileSystem.Cpx, "Work", false, cancel.Token));
        Assert.False(File.Exists(destination));
        File.WriteAllText(destination, "keep");
        Assert.Throws<IOException>(() => CopperScreenHardDiskSetup.Create(destination, 0, source, "DH1", 256, 511, CopperScreenHardDiskSetup.FileSystem.Sfs, "Work", false));
        Assert.Equal("keep", File.ReadAllText(destination)); Assert.Equal(before, File.ReadAllBytes(source));
        Assert.DoesNotContain(Directory.GetFiles(_root), f => f.EndsWith(".tmp"));
    }

    [Theory]
    [InlineData("")] [InlineData("Bad:label")] [InlineData("Bad/name")] [InlineData("Work😀")]
    public void InvalidLabelsDoNotCreateFiles(string label)
    {
        var destination = Path.Combine(_root, "bad.hdf");
        Assert.Throws<ArgumentException>(() => CopperScreenHardDiskSetup.Create(destination, 8 * 1048576, null, "DH0", null, null, CopperScreenHardDiskSetup.FileSystem.Ffs, label, false));
        Assert.Empty(Directory.GetFiles(_root));
    }
}
