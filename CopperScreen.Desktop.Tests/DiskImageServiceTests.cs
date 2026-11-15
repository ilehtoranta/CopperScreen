using CopperDisk.Tests;
using CopperScreen;
namespace CopperScreen.Tests;

public sealed class DiskImageServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "copper-filesystem-" + Guid.NewGuid().ToString("N"));
    public DiskImageServiceTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);
    private string Save(byte[] data) { var path = Path.Combine(_root, Guid.NewGuid() + ".adf"); File.WriteAllBytes(path, data); return path; }

    internal static byte[] MixedRdb(bool sfs2, ServiceImageFixture smart)
    {
        var bytes = new AmigaFileSystemFixture().Rdb(true);
        smart.Bytes(1760).CopyTo(bytes, 1804 * 512);
        ServiceImageFixture.Put(bytes, 2 * 512 + 192, sfs2 ? 0x53465302u : 0x53465300u);
        // PART checksum is over 64 words.
        var partition = bytes.AsSpan(2 * 512, 256).ToArray(); ServiceImageFixture.Seal(partition, 8);
        partition.CopyTo(bytes, 2 * 512);
        return bytes;
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SmartVolumesBrowseAndExtractFragmentedFilesThroughThePackagedRom(bool sfs2)
    {
        var fixture = ServiceImageFixture.Smart(sfs2); var bytes = fixture.Bytes(); var path = Save(bytes);
        var host = Path.Combine(_root, "host"); Directory.CreateDirectory(host);
        using (var image = CopperScreenDiskImage.Open(path))
        {
            var v = Assert.Single(image.Volumes); Assert.False(v.IsMounted);
            var root = v.Root; Assert.True(v.IsMounted); Assert.Equal("Fixture", root.Name);
            Assert.Equal(sfs2 ? "SFS2" : "SFS", v.FileSystemName);
            var entries = v.ReadDirectory(root); Assert.Equal(new[] { "Docs", "Empty" }, entries.Select(e => e.Name));
            Assert.Empty(v.ReadDirectory(entries[1]));
            var files = v.ReadDirectory(entries[0]); var file = files.Single(e => e.Name == "Payload");
            Assert.Equal("Comment", file.Comment); Assert.Equal(0x40u, file.Protection); Assert.NotNull(file.Modified);
            for (var i = 0; i < 3; i++)
            {
                using var output = new MemoryStream(); v.CopyFileTo(file, output); Assert.Equal(fixture.Expected, output.ToArray());
            }
            var copied = CopperScreenDiskExtraction.Extract(v, entries, host);
            Assert.Equal(2, copied.Files); Assert.Equal(fixture.Expected, File.ReadAllBytes(Path.Combine(host, "Docs", "Payload")));
            Assert.Empty(File.ReadAllBytes(Path.Combine(host, "Docs", "EmptyFile")));
            Assert.True(Directory.Exists(Path.Combine(host, "Empty")));
        }
        Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RdbPartitionSessionsRemainIndependentUntilExplicitlyClosed(bool sfs2)
    {
        var fixture = ServiceImageFixture.Smart(sfs2); var bytes = MixedRdb(sfs2, fixture); var path = Save(bytes);
        using (var image = CopperScreenDiskImage.Open(path))
        {
            Assert.All(image.Volumes, v => Assert.False(v.IsMounted));
            var ffs = image.Volumes[0]; var smart = image.Volumes[1];
            var oldRoot = ffs.Root; Assert.True(ffs.IsMounted); Assert.False(smart.IsMounted);
            Assert.Equal("Fixture", smart.Root.Name); Assert.True(ffs.IsMounted);
            var docs = smart.ReadDirectory(smart.Root).Single(e => e.Name == "Docs");
            using var output = new MemoryStream(); smart.CopyFileTo(smart.ReadDirectory(docs).Single(e => e.Name == "Payload"), output);
            Assert.Equal(fixture.Expected, output.ToArray());
            Assert.Equal(3, ffs.ReadDirectory(oldRoot).Count);
            ffs.Close(); Assert.True(smart.IsMounted);
            Assert.Equal("Fixture", ffs.Root.Name); Assert.True(smart.IsMounted);
            Assert.Throws<ArgumentException>(() => ffs.ReadDirectory(oldRoot));
            Assert.Equal(3, ffs.ReadDirectory(ffs.Root).Count);
        }
        Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SmartDirectoryEnumerationCrossesGuestBatches(bool sfs2)
    {
        using var image = CopperScreenDiskImage.Open(Save(ServiceImageFixture.SmartMany(sfs2).Bytes()));
        var v = image.Volumes[0]; var entries = v.ReadDirectory(v.Root);
        Assert.Equal(142, entries.Count); Assert.Equal(140, entries.Count(e => e.Name.StartsWith("Item")));
        Assert.Equal(entries.Count, entries.Select(e => e.Name).Distinct().Count());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void DirtySmartVolumesAreRefusedAndLinksRemainMetadataOnly(bool sfs2)
    {
        var dirty = ServiceImageFixture.Smart(sfs2); ServiceImageFixture.Put(dirty.Blocks[5], 12, 1);
        ServiceImageFixture.Seal(dirty.Blocks[5], 4, sfs2 ? 2u : 1u);
        using (var image = CopperScreenDiskImage.Open(Save(dirty.Bytes())))
        {
            Assert.ThrowsAny<IOException>(() => image.Volumes[0].Mount());
            Assert.NotNull(image.Volumes[0].UnsupportedReason); Assert.False(image.Volumes[0].IsMounted);
        }
        using var links = CopperScreenDiskImage.Open(Save(ServiceImageFixture.Smart(sfs2, link: true).Bytes()));
        var v = links.Volumes[0]; var link = v.ReadDirectory(v.Root).Single(e => e.IsLink);
        Assert.Equal("Link", link.Name);
        Assert.Throws<NotSupportedException>(() => v.ReadDirectory(link));
        using var output = new MemoryStream(); Assert.Throws<NotSupportedException>(() => v.CopyFileTo(link, output));
        Assert.Equal(0, output.Length);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void BrowsesHashChainsAndStreamsExtendedFilesForEveryClassicVariant(int dos)
    {
        var fixture = new AmigaFileSystemFixture(dos);
        var path = Save(fixture.Bytes);
        using (var image = CopperScreenDiskImage.Open(path))
        {
            var volume = Assert.Single(image.Volumes); Assert.Null(volume.UnsupportedReason);
            Assert.Equal("Fixture", volume.Root.Name);
            var entries = volume.ReadDirectory(volume.Root);
            Assert.Equal(new[] { "Tools", "EmptyFile", "ReadMe" }, entries.Select(e => e.Name));
            var file = entries.Single(e => e.Name == "ReadMe");
            Assert.Equal("Amiga comment", file.Comment); Assert.Equal(0x40u, file.Protection);
            Assert.Equal(new DateTime(1978, 1, 2, 0, 2, 0).AddMilliseconds(60), file.Modified);
            using var output = new MemoryStream(); volume.CopyFileTo(file, output);
            Assert.Equal(fixture.Content, output.ToArray());
            var nested = volume.ReadDirectory(entries[0]);
            Assert.Empty(volume.ReadDirectory(nested.Single(e => e.Name == "Empty")));
            output.SetLength(0); volume.CopyFileTo(nested.Single(e => e.Name == "Nested.bin"), output);
            Assert.Equal(new byte[] { 1, 3, 5, 7 }, output.ToArray());
            output.SetLength(0); volume.CopyFileTo(entries.Single(e => e.Name == "EmptyFile"), output); Assert.Equal(0, output.Length);
            Assert.Throws<IOException>(() => new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite));
        }
        Assert.Equal(fixture.Bytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public void RdbDiscoversOffsetVolumeAndKeepsUnsupportedPartitionVisible(int dos)
    {
        var f = new AmigaFileSystemFixture(dos);
        using var image = CopperScreenDiskImage.Open(Save(f.Rdb(true)));
        Assert.Equal(2, image.Volumes.Count);
        var volume = image.Volumes[0]; Assert.Equal("DH0", volume.DeviceName); Assert.Null(volume.UnsupportedReason);
        using var output = new MemoryStream(); volume.CopyFileTo(volume.ReadDirectory(volume.Root).Single(e => e.Name == "ReadMe"), output);
        Assert.Equal(f.Content, output.ToArray());
        Assert.ThrowsAny<IOException>(() => image.Volumes[1].Mount());
    }

    [Fact]
    public void HighDensityUsesActualMiddleRatherThanBootCodeRootHint()
    {
        var f = new AmigaFileSystemFixture(blocks: 3520);
        using var image = CopperScreenDiskImage.Open(Save(f.Bytes));
        Assert.Equal("Fixture", image.Volumes[0].Root.Name);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public void RdbUsesFilesystemBlockSizeAndPartitionRelativeKeys(int dos)
    {
        var f = new AmigaFileSystemFixture(dos, blockSize: 1024, contentBlocks: 203);
        using var image = CopperScreenDiskImage.Open(Save(f.Rdb())); var v = image.Volumes[0];
        Assert.Null(v.UnsupportedReason); Assert.Equal(1024u, v.BlockSize);
        using var output = new MemoryStream(); v.CopyFileTo(v.ReadDirectory(v.Root).Single(e => e.Name == "ReadMe"), output);
        Assert.Equal(f.Content, output.ToArray());
    }

    [Theory]
    [InlineData("checksum")] [InlineData("hash-cycle")] [InlineData("parent")]
    [InlineData("out-of-range")] [InlineData("type")]
    public void RejectsDamagedDirectoryMetadata(string damage)
    {
        var f = new AmigaFileSystemFixture();
        if (damage == "checksum") f.Bytes[10 * 512 + 20] ^= 1;
        else
        {
            if (damage == "hash-cycle") f.Put(20, 496, 10);
            if (damage == "parent") f.Put(20, 500, 20);
            if (damage == "out-of-range") f.Put(20, 496, 1760);
            if (damage == "type") f.Put(20, 508, 99);
            f.Fix(20);
        }
        using var image = CopperScreenDiskImage.Open(Save(f.Bytes)); var v = image.Volumes[0];
        var error = Record.Exception(() => v.ReadDirectory(v.Root));
        Assert.True(error is IOException or InvalidDataException, error?.ToString());
    }

    [Theory]
    [InlineData(0, "checksum")] [InlineData(0, "sequence")] [InlineData(0, "data-pointer")]
    [InlineData(1, "data-pointer")] [InlineData(1, "extension")] [InlineData(1, "length")]
    public void RejectsBrokenFileChains(int dos, string damage)
    {
        var f = new AmigaFileSystemFixture(dos);
        if (damage == "checksum") f.Bytes[100 * 512 + 24] ^= 1;
        if (damage == "sequence") { f.Put(100, 8, 99); f.Fix(100); }
        if (damage == "data-pointer") { f.Put(10, 308, 1760); f.Fix(10); }
        if (damage == "extension") { f.Put(10, 504, 10); f.Fix(10); }
        if (damage == "length") { f.Put(10, 324, uint.MaxValue); f.Fix(10); }
        using var image = CopperScreenDiskImage.Open(Save(f.Bytes)); var v = image.Volumes[0];
        using var output = new MemoryStream();
        Assert.ThrowsAny<IOException>(() => v.CopyFileTo(v.ReadDirectory(v.Root).Single(e => e.Name == "ReadMe"), output));
    }

    [Fact]
    public void OfsReadsTheCanonicalPointerTableRatherThanRedundantNextFields()
    {
        var f = new AmigaFileSystemFixture(0); f.Put(100, 16, 100); f.Fix(100);
        using var image = CopperScreenDiskImage.Open(Save(f.Bytes)); var v = image.Volumes[0];
        using var output = new MemoryStream();
        v.CopyFileTo(v.ReadDirectory(v.Root).Single(e => e.Name == "ReadMe"), output);
        Assert.Equal(f.Content, output.ToArray());
    }

    [Fact]
    public void DamagedRootAndNonDosImagesAreExplainedWithoutBrowsing()
    {
        var f = new AmigaFileSystemFixture(); f.Bytes[f.Root * 512 + 20] ^= 1;
        using var badRoot = CopperScreenDiskImage.Open(Save(f.Bytes));
        Assert.ThrowsAny<IOException>(() => badRoot.Volumes[0].Mount());
        using var custom = CopperScreenDiskImage.Open(Save(new byte[901120]));
        Assert.Throws<NotSupportedException>(() => custom.Volumes[0].Mount());
    }

    [Fact]
    public void ReaderHonorsCancellationAndVolumeOwnership()
    {
        var path = Save(new AmigaFileSystemFixture().Bytes);
        using var a = CopperScreenDiskImage.Open(path); using var b = CopperScreenDiskImage.Open(path);
        var va = a.Volumes[0]; var vb = b.Volumes[0];
        Assert.Throws<ArgumentException>(() => va.ReadDirectory(vb.Root));
        Assert.Throws<OperationCanceledException>(() => va.ReadDirectory(va.Root, new CancellationToken(true)));
    }

    private sealed class CancelOnWrite(CancellationTokenSource source) : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count)
        {
            base.Write(buffer, offset, count); source.Cancel();
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public void CancellationInterruptsStreamingInsideALargeFile(int dos)
    {
        var f = new AmigaFileSystemFixture(dos);
        using var image = CopperScreenDiskImage.Open(Save(f.Bytes)); var v = image.Volumes[0];
        using var cancel = new CancellationTokenSource(); using var output = new CancelOnWrite(cancel);
        var file = v.ReadDirectory(v.Root).Single(e => e.Name == "ReadMe");
        Assert.Throws<OperationCanceledException>(() => v.CopyFileTo(file, output, cancel.Token));
        Assert.InRange(output.Length, 1, 65536);
    }
}
