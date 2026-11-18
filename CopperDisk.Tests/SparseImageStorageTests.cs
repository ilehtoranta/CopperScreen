using Xunit.Abstractions;

namespace CopperDisk.Tests;

public sealed class SparseImageStorageTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "CopperSparseSave-" + Guid.NewGuid().ToString("N"));
    private readonly ITestOutputHelper output;
    public SparseImageStorageTests(ITestOutputHelper output) { this.output = output; Directory.CreateDirectory(root); }
    public void Dispose() => Directory.Delete(root, true);

    [Theory]
    [InlineData("adf")] [InlineData("hdf")] [InlineData("dms")]
    public void SparseSectorClonePreservesAllBytesAndPartialZeroTail(string extension)
    {
        var sectors = new byte[901120]; "DOS\x01"u8.CopyTo(sectors);
        sectors[65535] = 7; sectors[131073] = 9;
        var original = extension == "dms" ? DmsFixture.Create(sectors) : sectors;
        var source = Path.Combine(root, "Source." + extension); File.WriteAllBytes(source, original);
        var target = Path.Combine(root, "Stage");
        using (var image = extension == "dms" ? AmigaSectorImage.OpenDms(source) : AmigaSectorImage.Open(source))
        using (var stage = new FileStream(target, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read))
        {
            Assert.True(image.CopyToSparse(stage), "Sparse host storage is unavailable on the test filesystem.");
            Assert.Equal(sectors.Length, stage.Length); stage.Flush(true);
            var allocated = AmigaSparseFile.GetAllocatedBytes(target);
            Assert.NotNull(allocated); Assert.True(allocated < sectors.Length / 2);
            output.WriteLine($"{extension}: logical={stage.Length}, allocated={allocated}");
            var actual = new byte[sectors.Length]; stage.Position = 0; stage.ReadExactly(actual); Assert.Equal(sectors, actual);
            Assert.Throws<ArgumentException>(() => image.CopyToSparse(stage));
        }
        Assert.Equal(original, File.ReadAllBytes(source));
        Assert.Equal(sectors, File.ReadAllBytes(target));
    }

    [Fact]
    public void DenseFallbackPreservesCompleteZeroTailWithoutMarkingSparse()
    {
        var bytes = new byte[1024 * 1024 + 31]; bytes[73] = 0x90;
        var path = Path.Combine(root, "Dense");
        using (var target = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read))
        {
            Assert.False(AmigaSparseFile.Copy(new MemoryStream(bytes), target, default, sparseStorage: false));
            var position = target.Position;
            AmigaSparseFile.CompactZeros(target, false);
            Assert.Equal(position, target.Position); Assert.Equal(bytes.Length, target.Length);
        }
        Assert.Equal(bytes, File.ReadAllBytes(path));
        if (OperatingSystem.IsWindows()) Assert.False(File.GetAttributes(path).HasFlag(FileAttributes.SparseFile));
    }

    [Fact]
    public void ZeroCompactionReclaimsGuestStyleWritesAndRetainsNonzeroBoundaryUnits()
    {
        var bytes = Enumerable.Repeat((byte)0xa5, 2 * 1024 * 1024 + 31).ToArray();
        Array.Clear(bytes, 65536 + 13, 20 * 65536 - 26);
        var path = Path.Combine(root, "Working"); File.WriteAllBytes(path, bytes);
        using var file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
        Assert.True(AmigaSparseFile.TryEnable(file));
        var before = AmigaSparseFile.GetAllocatedBytes(path); Assert.NotNull(before);
        file.Position = 123; AmigaSparseFile.CompactZeros(file, true);
        Assert.Equal(123, file.Position); Assert.Equal(bytes.Length, file.Length);
        var after = AmigaSparseFile.GetAllocatedBytes(path); Assert.NotNull(after); Assert.True(after < before - 65536);
        output.WriteLine($"compaction: logical={file.Length}, before={before}, after={after}");
        var actual = new byte[bytes.Length]; file.Position = 0; file.ReadExactly(actual); Assert.Equal(bytes, actual);
        file.Position = 300000; file.Write([1, 3, 5]); bytes[300000] = 1; bytes[300001] = 3; bytes[300002] = 5;
        AmigaSparseFile.CompactZeros(file, true);
        file.Position = 0; file.ReadExactly(actual); Assert.Equal(bytes, actual);
    }

    [Fact]
    public void LongZeroRunsAndTailWritesAboveFourGiBRemainExact()
    {
        var path = Path.Combine(root, "Large.hdf");
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read);
        Assert.True(AmigaSparseFile.TryEnable(file));
        const long tail = 0x1_0000_0000L + 17, length = 0x1_0000_0000L + 65536 + 512;
        file.SetLength(length); file.WriteByte(9); file.Position = tail; file.Write([7, 8, 9]);
        file.Position = 123; AmigaSparseFile.CompactZeros(file, true);
        Assert.Equal(123, file.Position); Assert.Equal(length, file.Length);
        file.Position = tail - 1; var bytes = new byte[5]; file.ReadExactly(bytes); Assert.Equal(new byte[] { 0, 7, 8, 9, 0 }, bytes);
        file.Position = length - 512; file.ReadExactly(new byte[512]); Assert.Equal(length, file.Position);
        var allocated = AmigaSparseFile.GetAllocatedBytes(path); Assert.NotNull(allocated); Assert.True(allocated < 1024 * 1024);
        output.WriteLine($"large: logical={length}, allocated={allocated}");
    }

    [Fact]
    public void CancellationStopsCopiesAndRestoresCompactionPosition()
    {
        var bytes = new byte[3 * 65536]; bytes[0] = 9;
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        var copy = Path.Combine(root, "Cancelled");
        using (var target = new FileStream(copy, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.ThrowsAny<OperationCanceledException>(() => AmigaSparseFile.Copy(new MemoryStream(bytes), target, cancelled.Token));
            Assert.Equal(0, target.Length);
        }
        var path = Path.Combine(root, "Working"); File.WriteAllBytes(path, bytes);
        using var duringRead = new CancellationTokenSource();
        using (var file = new CancelReadFile(path, duringRead))
        {
            Assert.True(AmigaSparseFile.TryEnable(file)); file.Position = 31;
            Assert.ThrowsAny<OperationCanceledException>(() => AmigaSparseFile.CompactZeros(file, true, duringRead.Token));
            Assert.Equal(31, file.Position); Assert.Equal(bytes.Length, file.Length);
        }
        Assert.Equal(bytes, File.ReadAllBytes(path));
        Assert.DoesNotContain(Directory.GetFiles(root), p => Path.GetFileName(p).StartsWith(".copper-sparse-"));
    }
    private sealed class CancelReadFile(string path, CancellationTokenSource source)
        : FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read)
    {
        public override int Read(Span<byte> buffer)
        { var count = base.Read(buffer); source.Cancel(); return count; }
    }
}
