namespace CopperDisk.Tests;

public sealed class AmigaSectorImageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "copper-sector-image-" + Guid.NewGuid().ToString("N"));
    public AmigaSectorImageTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);
    private string Save(byte[] data) { var path = Path.Combine(_root, Guid.NewGuid() + ".img"); File.WriteAllBytes(path, data); return path; }

    [Fact]
    public void RawImageIsAWriterExcludedBoundedViewWithoutFilesystemInterpretation()
    {
        var data = new byte[901120]; data[1300] = 123; var path = Save(data);
        using (var image = AmigaSectorImage.Open(path))
        {
            var volume = Assert.Single(image.Volumes); Assert.Null(volume.UnsupportedReason); Assert.Null(volume.DosType);
            var bytes = new byte[4]; volume.Read(1300, bytes); Assert.Equal(123, bytes[0]);
            Assert.Throws<ArgumentOutOfRangeException>(() => volume.Read(-1, bytes));
            Assert.Throws<ArgumentOutOfRangeException>(() => volume.Read(volume.Length - 3, bytes));
            Assert.Throws<IOException>(() => new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite));
        }
        Assert.Equal(data, File.ReadAllBytes(path));
    }

    [Fact]
    public void RdbExtentsRetainOffsetsGeometryAndOpaqueDosTypes()
    {
        var f = new AmigaFileSystemFixture(); var path = Save(f.Rdb(true));
        using var image = AmigaSectorImage.Open(path); Assert.Equal(2, image.Volumes.Count);
        var first = image.Volumes[0]; var second = image.Volumes[1];
        Assert.Equal(44L * 512, first.ByteOffset); Assert.Equal(1760L * 512, first.Length);
        Assert.Equal(1804L * 512, second.ByteOffset); Assert.Equal(0x53465300u, second.DosType);
        Assert.Null(second.UnsupportedReason); // A storage view does not decide filesystem support.
        var bytes = new byte[512]; first.Read(0, bytes); Assert.Equal(f.Bytes[..512], bytes);
        Assert.Throws<ArgumentOutOfRangeException>(() => first.Read(first.Length, bytes));
    }

    [Fact]
    public void ClosingTheImageReleasesItsWriterLockAndInvalidatesViews()
    {
        var path = Save(new byte[901120]); var image = AmigaSectorImage.Open(path); var volume = image.Volumes[0]; image.Dispose();
        Assert.Throws<ObjectDisposedException>(() => volume.Read(0, new byte[4]));
        using var writer = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None);
    }
}
