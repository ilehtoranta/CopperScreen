using CopperDisk.Tests;
using CopperScreen;
using CopperStart.FileSystem;

namespace CopperScreen.Tests;

public sealed partial class DiskImageOperationsTests
{
    [Theory]
    [InlineData(6)] [InlineData(7)]
    public void NativeSmartDirectoryMovesSucceedWhenThereIsNoRoomForACopy(int format)
    {
        var path = Blank("NearlyFull", format); Seed(path, format);
        using (var disk = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        using (var s = new FileSystemSession(new Source(disk), new((uint)(disk.Length / 512), 512, 1), new() { AllowWrites = true }))
        {
            s.CreateDirectory(s.Root, "Destination");
            s.CopyFileFrom(s.Root, "Filler", new MemoryStream(new byte[1_900_000]));
        }
        using var image = CopperScreenDiskImage.Open(path); var v = image.Volumes[0]; var entries = v.ReadDirectory(v.Root);
        var tree = entries.Single(e => e.Name == "Tree"); var destination = entries.Single(e => e.Name == "Destination");
        var before = File.ReadAllBytes(path);
        var full = Assert.Throws<FileSystemServiceException>(() => image.CopyFromImage(v, destination, v, [tree]));
        Assert.Equal(221u, full.Status); Assert.Equal(before, File.ReadAllBytes(path));
        image.MoveFromImage(v, destination, v, v.Root, [tree]);
        v = image.Volumes[0]; entries = v.ReadDirectory(v.Root); Assert.DoesNotContain(entries, e => e.Name == "Tree");
        VerifyTree(v, Assert.Single(v.ReadDirectory(entries.Single(e => e.Name == "Destination"))));
        using var remount = CopperScreenDiskImage.Open(path); var fresh = remount.Volumes[0];
        VerifyTree(fresh, Assert.Single(fresh.ReadDirectory(fresh.ReadDirectory(fresh.Root).Single(e => e.Name == "Destination"))));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    public void SameVolumeMovesPreserveContentsAndMetadata(int format)
    {
        var path = Blank("Move", format); Seed(path, format);
        using var image = CopperScreenDiskImage.Open(path); var v = image.Volumes[0]; image.CreateDirectory(v, v.Root, "Destination");
        v = image.Volumes[0]; var entries = v.ReadDirectory(v.Root); var tree = entries.Single(e => e.Name == "Tree");
        image.MoveFromImage(v, entries.Single(e => e.Name == "Destination"), v, v.Root, [tree]);
        v = image.Volumes[0]; var destination = Assert.Single(v.ReadDirectory(v.Root)); Assert.Equal("Destination", destination.Name);
        VerifyTree(v, Assert.Single(v.ReadDirectory(destination)));
        var children = v.ReadDirectory(v.ReadDirectory(destination).Single());
        image.MoveFromImage(v, v.Root, v, v.ReadDirectory(destination).Single(), children);
        v = image.Volumes[0]; Assert.Equal(4, v.ReadDirectory(v.Root).Count);
        using var output = new MemoryStream(); v.CopyFileTo(v.ReadDirectory(v.Root).Single(e => e.Name == "CON"), output); Assert.Equal(Payload, output.ToArray());
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    public void MovesAcrossImagesCopyBeforeRemovingTheSource(int format)
    {
        var a = Blank("Source", format); var b = Blank("Target", (format + 1) % 8); Seed(a, format);
        using var source = CopperScreenDiskImage.Open(a); using var target = CopperScreenDiskImage.Open(b);
        var sv = source.Volumes[0]; var tv = target.Volumes[0];
        target.MoveFromImage(tv, tv.Root, sv, sv.Root, sv.ReadDirectory(sv.Root));
        sv = source.Volumes[0]; tv = target.Volumes[0]; Assert.Empty(sv.ReadDirectory(sv.Root));
        VerifyTree(tv, Assert.Single(tv.ReadDirectory(tv.Root)));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    public void ProtectionChangesAreStagedAndApplyMasksToEachEntry(int format)
    {
        var path = Blank("Protection", format); Seed(path, format);
        using var image = CopperScreenDiskImage.Open(path); var v = image.Volumes[0]; var tree = Assert.Single(v.ReadDirectory(v.Root));
        image.Protect(v, v.Root, [tree], 1, 1);
        v = image.Volumes[0]; tree = Assert.Single(v.ReadDirectory(v.Root)); Assert.Equal(0x41u, tree.Protection);
        var before = File.ReadAllBytes(path);
        Assert.Throws<FileSystemServiceException>(() => image.Delete(v, v.Root, [tree])); Assert.Equal(before, File.ReadAllBytes(path));
        image.Protect(v, v.Root, [tree], 1, 0);
        v = image.Volumes[0]; tree = Assert.Single(v.ReadDirectory(v.Root)); var children = v.ReadDirectory(tree);
        image.Protect(v, tree, children, 128, 128);
        v = image.Volumes[0]; tree = Assert.Single(v.ReadDirectory(v.Root)); children = v.ReadDirectory(tree);
        Assert.Equal(0xC0u, children.Single(e => e.Name == "CON").Protection);
        Assert.Equal(0x80u, children.Single(e => e.Name == "Zero").Protection);
        Assert.Equal(0x40u, tree.Protection); // no implicit recursive changes
        before = File.ReadAllBytes(path);
        Assert.ThrowsAny<OperationCanceledException>(() => image.Protect(v, tree, children, 255, 0, new(true)));
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    private string PartitionedMoveImage()
    {
        var path = Path.Combine(_root, "Partitions.hdf"); File.WriteAllBytes(path, new AmigaFileSystemFixture().Rdb(true));
        using (var disk = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            CopperScreenFileSystemFormatter.Format(disk, 44 * 512, 1760 * 512, CopperScreenHardDiskSetup.FileSystem.Ffs, "First", default);
            CopperScreenFileSystemFormatter.Format(disk, 1804 * 512, 1760 * 512, CopperScreenHardDiskSetup.FileSystem.Sfs, "Second", default);
        }
        return path;
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CrossPartitionMovesCommitBothPartitionsTogether(bool reverse)
    {
        var path = PartitionedMoveImage(); var tree = Path.Combine(_root, "Tree");
        Directory.CreateDirectory(Path.Combine(tree, "Empty")); File.WriteAllBytes(Path.Combine(tree, "Data"), Payload);
        using var image = CopperScreenDiskImage.Open(path); var si = reverse ? 1 : 0; var ti = 1 - si;
        var source = image.Volumes[si]; image.Import(source, source.Root, [tree]); source = image.Volumes[si]; var target = image.Volumes[ti];
        var rdb = File.ReadAllBytes(path)[..(44 * 512)];
        image.MoveFromImage(target, target.Root, source, source.Root, source.ReadDirectory(source.Root));
        source = image.Volumes[si]; target = image.Volumes[ti]; Assert.Empty(source.ReadDirectory(source.Root));
        var copied = Assert.Single(target.ReadDirectory(target.Root)); var children = target.ReadDirectory(copied); Assert.Equal(2, children.Count);
        using var output = new MemoryStream(); target.CopyFileTo(children.Single(e => e.Name == "Data"), output); Assert.Equal(Payload, output.ToArray());
        Assert.Equal(rdb, File.ReadAllBytes(path)[..(44 * 512)]);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void ProtectedSourceRollsBackSameImageButRetainsSeparateImageCopy(bool shared)
    {
        var a = shared ? PartitionedMoveImage() : Blank("Source", 1); var b = shared ? a : Blank("Target", 6);
        var file = Path.Combine(_root, "Data"); File.WriteAllBytes(file, Payload);
        using var sourceImage = CopperScreenDiskImage.Open(a); var source = sourceImage.Volumes[0]; sourceImage.Import(source, source.Root, [file]);
        source = sourceImage.Volumes[0]; sourceImage.Protect(source, source.Root, source.ReadDirectory(source.Root), 1, 1);
        source = sourceImage.Volumes[0]; using var targetOwner = shared ? sourceImage.Retain() : CopperScreenDiskImage.Open(b);
        var target = targetOwner.Volumes[shared ? 1 : 0]; var original = File.ReadAllBytes(a);
        var error = Assert.ThrowsAny<IOException>(() => targetOwner.MoveFromImage(target, target.Root, source, source.Root, source.ReadDirectory(source.Root)));
        Assert.Equal(original, File.ReadAllBytes(a)); Assert.Single(source.ReadDirectory(source.Root));
        target = targetOwner.Volumes[shared ? 1 : 0];
        if (shared) Assert.Empty(target.ReadDirectory(target.Root));
        else { Assert.Contains("Destination files are retained", error.Message); Assert.Single(target.ReadDirectory(target.Root)); }
    }

    [Fact]
    public void CancelledCrossPartitionMoveDiscardsBothPartitionChanges()
    {
        var path = PartitionedMoveImage(); var file = Path.Combine(_root, "Data"); File.WriteAllBytes(file, Payload);
        using var image = CopperScreenDiskImage.Open(path); var source = image.Volumes[0]; image.Import(source, source.Root, [file]);
        source = image.Volumes[0]; var target = image.Volumes[1]; var before = File.ReadAllBytes(path);
        using var cancel = new CancellationTokenSource();
        Assert.ThrowsAny<OperationCanceledException>(() => image.MoveFromImage(target, target.Root, source, source.Root,
            source.ReadDirectory(source.Root), cancel.Token, new CancelProgress(cancel)));
        Assert.Equal(before, File.ReadAllBytes(path)); Assert.Empty(target.ReadDirectory(target.Root));
        Assert.Single(source.ReadDirectory(source.Root));
    }

    [Theory]
    [InlineData("self")] [InlineData("descendant")] [InlineData("same")] [InlineData("conflict")] [InlineData("cancel")]
    public void InvalidSameVolumeMovesLeaveTheOriginalUntouched(string failure)
    {
        var path = Blank("Move", 1); Seed(path, 1); using var image = CopperScreenDiskImage.Open(path); var v = image.Volumes[0];
        image.CreateDirectory(v, v.Root, "Destination"); v = image.Volumes[0]; var roots = v.ReadDirectory(v.Root);
        var tree = roots.Single(e => e.Name == "Tree"); var destination = roots.Single(e => e.Name == "Destination");
        if (failure == "conflict")
        {
            image.CopyFromImage(v, destination, v, [tree]); v = image.Volumes[0]; roots = v.ReadDirectory(v.Root);
            tree = roots.Single(e => e.Name == "Tree"); destination = roots.Single(e => e.Name == "Destination");
        }
        if (failure == "self") destination = tree;
        if (failure == "descendant") destination = v.ReadDirectory(tree).Single(e => e.Name == "Empty");
        if (failure == "same") destination = v.Root;
        var before = File.ReadAllBytes(path);
        Assert.ThrowsAny<Exception>(() => image.MoveFromImage(v, destination, v, v.Root, [tree], new(failure == "cancel")));
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void ChangedHostSourcesAndUnexpectedChildrenAreNotRemoved()
    {
        var tree = Path.Combine(_root, "Tree"); Directory.CreateDirectory(tree); var file = Path.Combine(tree, "File"); File.WriteAllBytes(file, [1]);
        var snapshot = new CopperScreenHostMove([tree], default);
        Assert.Throws<IOException>(() => File.WriteAllBytes(file, [2])); // source locked throughout copying
        snapshot.Dispose(); File.WriteAllBytes(file, [2]);
        Assert.Throws<IOException>(() => snapshot.Remove(default)); Assert.Equal(new byte[] { 2 }, File.ReadAllBytes(file));
        using var changed = new CopperScreenHostMove([tree], default); File.WriteAllBytes(Path.Combine(tree, "New"), [3]);
        Assert.Throws<IOException>(() => changed.Remove(default)); Assert.True(File.Exists(file)); changed.Dispose();
        snapshot = new CopperScreenHostMove([tree], default); snapshot.Remove(default); Assert.False(Directory.Exists(tree));
    }
}
