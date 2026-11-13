using CopperDisk;
using CopperScreen;

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
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void CreatesEachFilesystemAndPreservesSourceWhenAddingAnotherPartition(int filesystem)
    {
        var fs = (CopperScreenHardDiskSetup.FileSystem)filesystem;
        var source = Path.Combine(_root, "source.hdf");
        CopperScreenHardDiskSetup.Create(source, 16 * 1048576, null, "DH0", 1, 255, fs, "ÄWork", true);
        var before = File.ReadAllBytes(source);
        var first = Assert.Single(CopperScreenHardDiskSetup.Inspect(source).Partitions);
        Assert.Equal(CopperScreenHardDiskSetup.DosType(fs), first.DosType);
        var destination = Path.Combine(_root, "copy.hdf");
        CopperScreenHardDiskSetup.Create(destination, 0, source, "DH1", 256, 511, fs, "Data", false);
        Assert.Equal(before, File.ReadAllBytes(source));
        Assert.Equal(2, CopperScreenHardDiskSetup.Inspect(destination).Partitions.Count);
        var after = File.ReadAllBytes(destination);
        Assert.Equal(before.AsSpan(32768, 255 * 32768).ToArray(), after.AsSpan(32768, 255 * 32768).ToArray());
        AmigaRdbImage.Validate(destination);
        Assert.DoesNotContain(Directory.GetFiles(_root), f => f.EndsWith(".tmp"));
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
