using System.Buffers.Binary;
using System.Security.Cryptography;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed class DiskImageImportTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "copper-import-" + Guid.NewGuid().ToString("N"));
    public DiskImageImportTests() => Directory.CreateDirectory(_root);
    public void Dispose()
    {
        var image = Path.Combine(_root, "Disk.img"); if (File.Exists(image)) File.SetAttributes(image, FileAttributes.Normal);
        Directory.Delete(_root, true);
    }

    private string Blank(CopperScreenHardDiskSetup.FileSystem fs = CopperScreenHardDiskSetup.FileSystem.Ffs, bool raw = false, bool ofs = false, uint blockSize = 512)
    {
        var path = Path.Combine(_root, "Disk.img");
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        file.SetLength(raw ? 901120 : 4096 * blockSize);
        CopperScreenFileSystemFormatter.Format(file, 0, file.Length, fs, "Import", default, blockSize);
        if (ofs) { file.Position = 0; file.Write(new byte[] { 0x44, 0x4F, 0x53, 0 }); }
        return path;
    }
    private string Tree(int bytes = 140013)
    {
        var source = Path.Combine(_root, "Host", "Tree"); Directory.CreateDirectory(Path.Combine(source, "Nested", "Empty"));
        File.WriteAllBytes(Path.Combine(source, "Nested", "Payload"), Enumerable.Range(0, bytes).Select(i => (byte)(i * 13 + i / 71)).ToArray());
        File.WriteAllBytes(Path.Combine(source, "Zero"), []); File.WriteAllBytes(Path.Combine(source, "Tree.info"), [1, 5, 9]);
        File.SetLastWriteTime(Path.Combine(source, "Nested", "Payload"), new DateTime(2020, 5, 6, 7, 8, 9));
        return source;
    }
    // Independent generated-media producer: upgrade a blank DOS1 image without
    // depending on CopperFS's new formatter/cache writer for the input fixture.
    internal static void SetClassicVariant(string path, int dos, uint blockSize = 512)
    {
        var data = File.ReadAllBytes(path); var blocks = data.Length / (int)blockSize;
        var root = (blocks + 1) / 2; var tail = (int)blockSize - 512;
        void Put(int block, int offset, uint value) => BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(block * (int)blockSize + offset), value);
        uint Word(int block, int offset) => BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(block * (int)blockSize + offset));
        void Seal(int block, int at)
        {
            Put(block, at, 0); uint sum = 0;
            for (var i = 0; i < blockSize; i += 4) sum = unchecked(sum + Word(block, i));
            Put(block, at, unchecked(0u - sum));
        }
        Put(0, 0, 0x444F5300u + (uint)dos);
        if (dos >= 4)
        {
            var bits = (blockSize - 4) * 8; var maps = ((uint)blocks - 3) / bits + 1;
            var cache = root + 1 + (int)maps;
            Put(cache, 0, 33); Put(cache, 4, (uint)cache); Put(cache, 8, (uint)root); Seal(cache, 20);
            Put(root, tail + 504, (uint)cache); Seal(root, 20);
            var bit = (uint)cache - 2; var map = (int)Word(root, tail + 316 + (int)(bit / bits) * 4);
            var at = 4 + (int)(bit % bits / 32) * 4;
            Put(map, at, Word(map, at) & ~(1u << (int)(bit % 32))); Seal(map, 0);
        }
        File.WriteAllBytes(path, data);
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    public void GuestImportRoundTripsNestedTreesAndMetadataForEachWritableFormat(int format)
    {
        var path = Blank(format is 2 or 3 ? format == 2 ? CopperScreenHardDiskSetup.FileSystem.Sfs : CopperScreenHardDiskSetup.FileSystem.Sfs2 : CopperScreenHardDiskSetup.FileSystem.Ffs,
            raw: format is < 2 or >= 4, ofs: format == 0);
        if (format >= 4) SetClassicVariant(path, format - 2);
        var source = Tree(); var expected = File.ReadAllBytes(Path.Combine(source, "Nested", "Payload"));
        if (Environment.GetEnvironmentVariable("COPPERSCREEN_IMPORT_FIXTURES") is { } fixtures)
        { Directory.CreateDirectory(fixtures); File.Copy(path, Path.Combine(fixtures, $"blank-{format}.img"), true); }
        using (var image = CopperScreenDiskImage.Open(path))
        {
            var volume = image.Volumes[0]; var root = volume.Root; Assert.Null(volume.ImportUnavailableReason);
            var result = image.Import(volume, root, [source]); Assert.Equal(3, result.Files);
            var updated = image.Volumes[0]; var tree = updated.ReadDirectory(updated.Root).Single(e => e.Name == "Tree");
            var host = Path.Combine(_root, "Extracted"); Directory.CreateDirectory(host);
            CopperScreenDiskExtraction.Extract(updated, [tree], host);
            Assert.Equal(expected, File.ReadAllBytes(Path.Combine(host, "Tree", "Nested", "Payload")));
            Assert.Empty(File.ReadAllBytes(Path.Combine(host, "Tree", "Zero")));
            Assert.True(Directory.Exists(Path.Combine(host, "Tree", "Nested", "Empty")));
            Assert.Equal(new byte[] { 1, 5, 9 }, File.ReadAllBytes(Path.Combine(host, "Tree", "Tree.info")));
            Assert.Equal(new DateTime(2020, 5, 6, 7, 8, 9), File.GetLastWriteTime(Path.Combine(host, "Tree", "Nested", "Payload")));
            Assert.Throws<ArgumentException>(() => updated.ReadDirectory(root));
            Assert.Throws<IOException>(() => new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite));
        }
        using var reopened = CopperScreenDiskImage.Open(path); Assert.Single(reopened.Volumes[0].ReadDirectory(reopened.Volumes[0].Root));
        Assert.Equal(expected, File.ReadAllBytes(Path.Combine(source, "Nested", "Payload"))); AssertNoStage();
    }

    [Theory]
    [InlineData("conflict")] [InlineData("invalid-name")] [InlineData("disk-full")] [InlineData("cancelled")]
    public void FailedOrCancelledImportNeverPublishesPartialImageChanges(string failure)
    {
        var path = Blank(raw: true); var source = Tree(failure == "disk-full" ? 1000000 : 140013);
        var empty = Path.Combine(_root, "Before"); File.WriteAllBytes(empty, [7]);
        using var image = CopperScreenDiskImage.Open(path); var volume = image.Volumes[0]; _ = volume.Root;
        if (failure == "conflict") { image.Import(volume, volume.Root, [source]); volume = image.Volumes[0]; _ = volume.Root; }
        if (failure == "invalid-name") File.WriteAllText(Path.Combine(source, new string('a', 31)), "cannot fit");
        var before = SHA256.HashData(File.ReadAllBytes(path)); using var cancel = new CancellationTokenSource();
        var progress = failure == "cancelled" ? new CancelOnProgress(cancel) : null;
        var error = Record.Exception(() => image.Import(volume, volume.Root, [empty, source], cancel.Token, progress));
        if (failure == "cancelled") Assert.IsType<OperationCanceledException>(error);
        else if (failure == "invalid-name") Assert.IsType<ArgumentException>(error);
        else if (failure == "disk-full") Assert.IsType<CopperStart.FileSystem.FileSystemServiceException>(error);
        else Assert.IsType<IOException>(error);
        Assert.Equal(before, SHA256.HashData(File.ReadAllBytes(path))); AssertNoStage();
        if (failure == "cancelled") Assert.True(cancel.IsCancellationRequested);
    }
    private sealed class CancelOnProgress(CancellationTokenSource cancel) : IProgress<DiskExtractionProgress>
    { public void Report(DiskExtractionProgress value) => cancel.Cancel(); }

    [Fact]
    public void ReadOnlyImageRefusesImportAndKeepsTheSourceUnchanged()
    {
        var path = Blank(); var source = Tree(); File.SetAttributes(path, FileAttributes.ReadOnly);
        var before = File.ReadAllBytes(path);
        using var image = CopperScreenDiskImage.Open(path); var v = image.Volumes[0]; _ = v.Root;
        Assert.Contains("read-only", v.ImportUnavailableReason);
        Assert.Throws<NotSupportedException>(() => image.Import(v, v.Root, [source])); Assert.Equal(before, File.ReadAllBytes(path));
        AssertNoStage();
    }

    [Fact]
    public void RdbImportChangesOnlyTheSelectedPartitionAndCanTargetNestedDirectories()
    {
        var path = Path.Combine(_root, "Disk.img");
        var raw = new CopperDisk.Tests.AmigaFileSystemFixture().Rdb(true);
        File.WriteAllBytes(path, raw);
        using (var file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            CopperScreenFileSystemFormatter.Format(file, 44 * 512, 1760 * 512, CopperScreenHardDiskSetup.FileSystem.Ffs, "First", default);
            CopperScreenFileSystemFormatter.Format(file, 1804 * 512, 1760 * 512, CopperScreenHardDiskSetup.FileSystem.Sfs, "Second", default);
        }
        var before = File.ReadAllBytes(path); var source = Tree();
        using var image = CopperScreenDiskImage.Open(path); var v = image.Volumes[1]; _ = v.Root;
        image.Import(v, v.Root, [source]);
        var after = File.ReadAllBytes(path); Assert.Equal(before[..(1804 * 512)], after[..(1804 * 512)]);
        v = image.Volumes[1]; var tree = v.ReadDirectory(v.Root).Single(e => e.Name == "Tree");
        var nested = v.ReadDirectory(tree).Single(e => e.Name == "Nested");
        var extra = Path.Combine(_root, "Extra"); File.WriteAllBytes(extra, [4, 2]); image.Import(v, nested, [extra]);
        v = image.Volumes[1]; tree = v.ReadDirectory(v.Root).Single(e => e.Name == "Tree"); nested = v.ReadDirectory(tree).Single(e => e.Name == "Nested");
        Assert.Contains(v.ReadDirectory(nested), e => e.Name == "Extra"); AssertNoStage();
    }
    private void AssertNoStage() => Assert.DoesNotContain(Directory.GetFileSystemEntries(_root), p => Path.GetFileName(p).StartsWith(".copperscreen-import-"));

    [Theory]
    [InlineData(4, "disk-full")] [InlineData(5, "disk-full")]
    [InlineData(4, "cancelled")] [InlineData(5, "cancelled")]
    [InlineData(4, "cache-checksum")] [InlineData(5, "cache-checksum")]
    public void CachedVariantFailuresNeverPublishChanges(int dos, string failure)
    {
        var path = Blank(raw: true); SetClassicVariant(path, dos);
        if (failure == "cache-checksum")
        {
            var bytes = File.ReadAllBytes(path); var cache = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(880 * 512 + 504));
            bytes[cache * 512 + 20] ^= 1; File.WriteAllBytes(path, bytes);
        }
        var source = Tree(failure == "disk-full" ? 1000000 : 140013); var before = File.ReadAllBytes(path);
        using var image = CopperScreenDiskImage.Open(path); var v = image.Volumes[0]; _ = v.Root;
        using var cancel = new CancellationTokenSource();
        var error = Record.Exception(() => image.Import(v, v.Root, [source], cancel.Token,
            failure == "cancelled" ? new CancelOnProgress(cancel) : null));
        if (failure == "cancelled") Assert.IsType<OperationCanceledException>(error);
        else Assert.IsType<CopperStart.FileSystem.FileSystemServiceException>(error);
        Assert.Equal(before, File.ReadAllBytes(path)); AssertNoStage();
    }

    [Fact]
    public void PublicationSharingFailureLeavesTheOriginalImageIntactAndReopensStorage()
    {
        var path = Blank(); var source = Tree(); var before = File.ReadAllBytes(path);
        using var image = CopperScreenDiskImage.Open(path); var v = image.Volumes[0]; _ = v.Root;
        using (var blocker = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.Throws<IOException>(() => image.Import(v, v.Root, [source]));
        Assert.Equal(before, File.ReadAllBytes(path)); AssertNoStage();
        v = image.Volumes[0]; Assert.Empty(v.ReadDirectory(v.Root));
        image.Import(v, v.Root, [source]); v = image.Volumes[0]; Assert.Single(v.ReadDirectory(v.Root));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void LargerRdbFilesystemBlocksRemainWithinTheDeclaredExtent(bool smart)
    {
        var fixture = new CopperDisk.Tests.AmigaFileSystemFixture(blockSize: 1024);
        var bytes = fixture.Rdb(); var path = Path.Combine(_root, "Disk.img");
        if (smart)
        {
            ServiceImageFixture.Put(bytes, 512 + 192, 0x53465302);
            var part = bytes.AsSpan(512, 256).ToArray(); ServiceImageFixture.Seal(part, 8); part.CopyTo(bytes, 512);
        }
        File.WriteAllBytes(path, bytes);
        using (var disk = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            CopperScreenFileSystemFormatter.Format(disk, 44 * 512, 1760 * 1024, smart ? CopperScreenHardDiskSetup.FileSystem.Sfs2 : CopperScreenHardDiskSetup.FileSystem.Ffs, "Large", default, 1024);
        var prefix = File.ReadAllBytes(path)[..(44 * 512)]; var source = Tree();
        using var image = CopperScreenDiskImage.Open(path); var v = image.Volumes[0]; _ = v.Root;
        image.Import(v, v.Root, [source]); v = image.Volumes[0]; Assert.Single(v.ReadDirectory(v.Root));
        Assert.Equal(prefix, File.ReadAllBytes(path)[..(44 * 512)]);
    }

    [Theory]
    [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void InternationalRdbVolumesImportUsingTheirLargerFilesystemBlocks(int dos)
    {
        var partition = Path.Combine(_root, "Partition.img");
        using (var file = new FileStream(partition, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
        {
            file.SetLength(1760 * 1024);
            CopperScreenFileSystemFormatter.Format(file, 0, file.Length, CopperScreenHardDiskSetup.FileSystem.Ffs, "Large", default, 1024);
        }
        SetClassicVariant(partition, dos, 1024);
        var bytes = new CopperDisk.Tests.AmigaFileSystemFixture(blockSize: 1024).Rdb();
        File.ReadAllBytes(partition).CopyTo(bytes, 44 * 512); ServiceImageFixture.Put(bytes, 512 + 192, 0x444F5300u + (uint)dos);
        var part = bytes.AsSpan(512, 256).ToArray(); ServiceImageFixture.Seal(part, 8); part.CopyTo(bytes, 512);
        var path = Path.Combine(_root, "Disk.img"); File.WriteAllBytes(path, bytes); var source = Tree();
        using var image = CopperScreenDiskImage.Open(path); var v = image.Volumes[0]; _ = v.Root;
        image.Import(v, v.Root, [source]); v = image.Volumes[0]; Assert.Single(v.ReadDirectory(v.Root));
        var extracted = Path.Combine(_root, "Extracted"); Directory.CreateDirectory(extracted);
        CopperScreenDiskExtraction.Extract(v, v.ReadDirectory(v.Root), extracted);
        Assert.Equal(File.ReadAllBytes(Path.Combine(source, "Nested", "Payload")), File.ReadAllBytes(Path.Combine(extracted, "Tree", "Nested", "Payload")));
        Assert.Equal(bytes[..(44 * 512)], File.ReadAllBytes(path)[..(44 * 512)]); AssertNoStage();
    }
}
