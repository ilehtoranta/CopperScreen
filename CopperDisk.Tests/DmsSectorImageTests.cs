namespace CopperDisk.Tests;

public sealed class DmsSectorImageTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "CopperDms-" + Guid.NewGuid().ToString("N"));
    public DmsSectorImageTests() => Directory.CreateDirectory(root);
    public void Dispose() => Directory.Delete(root, true);

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public void DecodedSectorsAreExactBoundedAndProtectedFromConcurrentWriters(byte mode)
    {
        var sectors = new byte[901120];
        for (var i = 0; i < 125001; i++) sectors[i] = (byte)(i * 19 + i / 71);
        var bytes = DmsFixture.Create(sectors, mode);
        var path = Path.Combine(root, "Disk.dms"); File.WriteAllBytes(path, bytes);
        using (var image = AmigaSectorImage.OpenDms(path))
        {
            var volume = Assert.Single(image.Volumes); Assert.Equal(sectors.Length, volume.Length);
            var actual = new byte[sectors.Length]; volume.Read(0, actual); Assert.Equal(sectors, actual);
            var tail = new byte[37]; volume.Read(volume.Length - tail.Length, tail); Assert.Equal(sectors[^37..], tail);
            volume.Read(volume.Length, []);
            Assert.Throws<ArgumentOutOfRangeException>(() => volume.Read(-1, new byte[1]));
            Assert.Throws<ArgumentOutOfRangeException>(() => volume.Read(volume.Length, new byte[1]));
            Assert.Null(image.WriteUnavailableReason);
            using var stage = new MemoryStream(); image.CopyTo(stage); Assert.Equal(sectors, stage.ToArray());
            Assert.Throws<IOException>(() => new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.Read));
        }
        Assert.Equal(bytes, File.ReadAllBytes(path));
        using var writableAfterDispose = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [Theory]
    [InlineData("crc")] [InlineData("incomplete")] [InlineData("encrypted")] [InlineData("hd")] [InlineData("truncated")]
    public void InvalidArchivesFailAndReleaseTheirSource(string failure)
    {
        var bytes = DmsFixture.Create(new byte[901120], cylinders: failure == "incomplete" ? 79 : 80,
            flags: failure == "encrypted" ? (ushort)2 : failure == "hd" ? (ushort)16 : (ushort)0);
        if (failure == "crc") bytes[76] ^= 1;
        if (failure == "truncated") bytes = bytes[..^1];
        var path = Path.Combine(root, "Bad.dms"); File.WriteAllBytes(path, bytes);
        Assert.Throws<InvalidDataException>(() => AmigaSectorImage.OpenDms(path));
        Assert.Equal(bytes, File.ReadAllBytes(path));
        using var unlocked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }
}
