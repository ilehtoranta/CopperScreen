namespace CopperDisk.Tests;

public sealed class SparseHardfileTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "CopperSparse-" + Guid.NewGuid().ToString("N"));
    public SparseHardfileTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public void DiscardPreservesLengthNeighboursAndReallocatedData()
    {
        var path = Path.Combine(_root, "disk.hdf");
        var original = Enumerable.Repeat((byte)0xA5, 1024 * 1024).ToArray();
        File.WriteAllBytes(path, original);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
        Assert.True(AmigaSparseFile.TryEnable(stream), "This platform/filesystem does not provide sparse deallocation; native sparse coverage is unavailable.");
        var before = AmigaSparseFile.GetAllocatedBytes(path);
        AmigaSparseFile.Discard(stream, 65536 + 512, 65536 * 6 - 1024);
        Assert.Equal(original.Length, stream.Length);
        var actual = new byte[original.Length]; stream.Position = 0; stream.ReadExactly(actual);
        Assert.All(actual[..131072], b => Assert.Equal(0xA5, b));
        Assert.All(actual[131072..393216], b => Assert.Equal(0, b));
        Assert.All(actual[393216..], b => Assert.Equal(0xA5, b));
        var after = AmigaSparseFile.GetAllocatedBytes(path);
        Assert.NotNull(before); Assert.NotNull(after); Assert.True(after < before);
        stream.Position = 200000; stream.Write([1, 2, 3]); stream.Flush(true);
        stream.Dispose();
        using var reopened = File.OpenRead(path); reopened.Position = 200000;
        Assert.Equal(1, reopened.ReadByte()); Assert.Equal(2, reopened.ReadByte()); Assert.Equal(3, reopened.ReadByte());
    }

    [Fact]
    public void ExistingMountRequiresOptInAndReadOnlyNeverDiscards()
    {
        var path = Path.Combine(_root, "disk.hdf"); File.WriteAllBytes(path, new byte[1024 * 1024]);
        using (var disk = AmigaHardfile.Open(new(0, path))) Assert.False(disk.SupportsDiscard);
        using (var disk = AmigaHardfile.Open(new(0, path) { SparseStorage = true }))
        {
            Assert.True(disk.SupportsDiscard);
            Assert.Throws<ArgumentOutOfRangeException>(() => disk.Discard(-512, 512));
            Assert.Throws<ArgumentOutOfRangeException>(() => disk.Discard(disk.Length - 512, 1024));
        }
        using (var disk = AmigaHardfile.Open(new(0, path, readOnly: true) { SparseStorage = true }))
        { Assert.False(disk.SupportsDiscard); Assert.Throws<UnauthorizedAccessException>(() => disk.Discard(0, 512)); }
    }

    [Fact]
    public void NewMountEnablesSparseStorageAndExplicitOptOutIsRespected()
    {
        var path = Path.Combine(_root, "new.hdf");
        using (var disk = AmigaHardfile.Open(new(0, path, createSizeBytes: 16 * 1024 * 1024)))
        {
            Assert.True(disk.SupportsDiscard);
            Assert.Equal(16 * 1024 * 1024, disk.Length);
        }
        Assert.True(AmigaSparseFile.GetAllocatedBytes(path) < 65536);
        using (var reopened = AmigaHardfile.Open(new(0, path) { SparseStorage = true })) Assert.True(reopened.SupportsDiscard);
        using var ordinary = AmigaHardfile.Open(new(0, Path.Combine(_root, "ordinary.hdf"), createSizeBytes: 1024 * 1024) { SparseStorage = false });
        Assert.False(ordinary.SupportsDiscard);
    }

    [Fact]
    public void SparseCopyRetainsZeroTailAndBytes()
    {
        var bytes = new byte[1024 * 1024]; bytes[0] = 9; bytes[300000] = 7;
        var path = Path.Combine(_root, "copy.hdf");
        using (var output = File.Create(path))
            Assert.True(AmigaSparseFile.Copy(new MemoryStream(bytes), output));
        Assert.Equal(bytes, File.ReadAllBytes(path));
        Assert.True(AmigaSparseFile.GetAllocatedBytes(path) < bytes.Length);
    }
}
