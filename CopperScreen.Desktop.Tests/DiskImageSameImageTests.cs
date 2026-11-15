using CopperDisk.Tests;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed partial class DiskImageOperationsTests
{
    private static void VerifyTree(DiskImageVolume volume, DiskImageEntry tree)
    {
        Assert.Equal(0x40u, tree.Protection); Assert.Equal("Amiga metadata", tree.Comment);
        var children = volume.ReadDirectory(tree); Assert.Equal(3, children.Count);
        Assert.Empty(volume.ReadDirectory(children.Single(e => e.Name == "Empty")));
        var file = children.Single(e => e.Name == "CON");
        Assert.Equal(tree.Modified, file.Modified); Assert.Equal(0x40u, file.Protection); Assert.Equal("Amiga metadata", file.Comment);
        using var bytes = new MemoryStream(); volume.CopyFileTo(file, bytes); Assert.Equal(Payload, bytes.ToArray());
        using var zero = new MemoryStream(); volume.CopyFileTo(children.Single(e => e.Name == "Zero"), zero); Assert.Equal(0, zero.Length);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    public void CopiesWithinOneVolumeReadTheOriginalAndPreserveTreesAndMetadata(int format)
    {
        var path = Blank("SameImage", format); Seed(path, format);
        using var image = CopperScreenDiskImage.Open(path); var v = image.Volumes[0];
        image.CreateDirectory(v, v.Root, "Destination");
        v = image.Volumes[0]; var roots = v.ReadDirectory(v.Root);
        var original = roots.Single(e => e.Name == "Tree"); var destination = roots.Single(e => e.Name == "Destination");
        var result = image.CopyFromImage(v, destination, v, [original]); Assert.Equal(2, result.Files);
        Assert.Throws<InvalidOperationException>(() => v.ReadDirectory(original));
        v = image.Volumes[0]; roots = v.ReadDirectory(v.Root);
        VerifyTree(v, roots.Single(e => e.Name == "Tree"));
        destination = roots.Single(e => e.Name == "Destination"); VerifyTree(v, Assert.Single(v.ReadDirectory(destination)));
        Assert.DoesNotContain(Directory.GetFileSystemEntries(_root), p => Path.GetFileName(p).StartsWith(".copperscreen"));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CrossPartitionCopiesPreserveTheRdbAndTheSourcePartition(bool reverse)
    {
        var path = Path.Combine(_root, "Partitions.hdf"); File.WriteAllBytes(path, new AmigaFileSystemFixture().Rdb(true));
        using (var disk = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            CopperScreenFileSystemFormatter.Format(disk, 44 * 512, 1760 * 512, CopperScreenHardDiskSetup.FileSystem.Ffs, "First", default);
            CopperScreenFileSystemFormatter.Format(disk, 1804 * 512, 1760 * 512, CopperScreenHardDiskSetup.FileSystem.Sfs, "Second", default);
        }
        var tree = Path.Combine(_root, "Tree"); Directory.CreateDirectory(Path.Combine(tree, "Empty")); File.WriteAllBytes(Path.Combine(tree, "Payload"), Payload);
        using var image = CopperScreenDiskImage.Open(path); var sourceIndex = reverse ? 1 : 0; var targetIndex = 1 - sourceIndex;
        var source = image.Volumes[sourceIndex]; image.Import(source, source.Root, [tree]);
        source = image.Volumes[sourceIndex]; var target = image.Volumes[targetIndex];
        var entries = source.ReadDirectory(source.Root); var targetRoot = target.Root;
        Assert.True(source.IsMounted); Assert.True(target.IsMounted);
        var before = File.ReadAllBytes(path); var offset = (int)target.Range.ByteOffset; var length = (int)target.Range.Length;
        image.CopyFromImage(target, targetRoot, source, entries);
        var after = File.ReadAllBytes(path);
        Assert.Equal(before[..offset], after[..offset]); Assert.Equal(before[(offset + length)..], after[(offset + length)..]);
        foreach (var volume in image.Volumes)
        {
            var copied = Assert.Single(volume.ReadDirectory(volume.Root));
            var children = volume.ReadDirectory(copied); Assert.Equal(2, children.Count);
            using var output = new MemoryStream(); volume.CopyFileTo(children.Single(e => e.Name == "Payload"), output); Assert.Equal(Payload, output.ToArray());
        }
    }

    [Theory]
    [InlineData("same-directory")] [InlineData("self")] [InlineData("descendant")] [InlineData("conflict")] [InlineData("cancel")]
    public void UnsafeOrFailedSameImageCopiesNeverPublishChanges(string failure)
    {
        var path = Blank("Safe", 1); Seed(path, 1);
        using var image = CopperScreenDiskImage.Open(path); var v = image.Volumes[0]; image.CreateDirectory(v, v.Root, "Destination");
        v = image.Volumes[0]; var entries = v.ReadDirectory(v.Root); var source = entries.Single(e => e.Name == "Tree");
        var destination = entries.Single(e => e.Name == "Destination");
        if (failure == "conflict")
        {
            image.CopyFromImage(v, destination, v, [source]); v = image.Volumes[0]; entries = v.ReadDirectory(v.Root);
            source = entries.Single(e => e.Name == "Tree"); destination = entries.Single(e => e.Name == "Destination");
        }
        if (failure == "same-directory") destination = v.Root;
        if (failure == "self") destination = source;
        if (failure == "descendant") destination = v.ReadDirectory(source).Single(e => e.Name == "Empty");
        var before = File.ReadAllBytes(path); using var cancel = new CancellationTokenSource();
        Assert.ThrowsAny<Exception>(() => image.CopyFromImage(v, destination, v, [source], cancel.Token,
            failure == "cancel" ? new CancelProgress(cancel) : null));
        Assert.Equal(before, File.ReadAllBytes(path)); VerifyTree(v, source);
        Assert.DoesNotContain(Directory.GetFileSystemEntries(_root), p => Path.GetFileName(p).StartsWith(".copperscreen"));
    }

    [Fact]
    public void ReleasingOneImageReferenceKeepsTheOtherReaderProtected()
    {
        var path = Blank("Lifetime", 1); using var image = CopperScreenDiskImage.Open(path); var retained = image.Retain();
        image.Dispose(); Assert.Empty(retained.Volumes[0].ReadDirectory(retained.Volumes[0].Root));
        Assert.Throws<IOException>(() => new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None));
        retained.Dispose();
        using var writable = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }
}
