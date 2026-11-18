using CopperDisk.FileSystem;
using CopperDisk.Tests;

namespace CopperScreen.Tests;

public sealed partial class DiskImageOperationsTests
{
    private string DmsImage(int dos)
    {
        var source = Blank("DmsSeed" + dos, dos, floppy: true); Seed(source, dos);
        var path = Path.Combine(_root, $"ReadOnly{dos}.dms");
        File.WriteAllBytes(path, DmsFixture.Create(File.ReadAllBytes(source)));
        return path;
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    public void DmsBrowsesAndExtractsWithoutChangingArchive(int dos)
    {
        var path = DmsImage(dos); var before = File.ReadAllBytes(path);
        using (var image = DiskImageSession.Open(path))
        {
            Assert.False(image.IsReadOnly);
            var volume = Assert.Single(image.Volumes); var tree = Assert.Single(volume.ReadDirectory(volume.Root));
            var children = volume.ReadDirectory(tree); Assert.Equal(3, children.Count);
            Assert.Empty(volume.ReadDirectory(children.Single(e => e.Name == "Empty")));
            var file = children.Single(e => e.Name == "CON");
            Assert.Equal("Amiga metadata", file.Comment); Assert.Equal(0x40u, file.Protection);
            using (var input = volume.OpenRead(file, offset: 65537))
            using (var output = new MemoryStream()) { input.CopyTo(output); Assert.Equal(Payload[65537..], output.ToArray()); }
            var host = Path.Combine(_root, "Export"); Directory.CreateDirectory(host);
            DiskTransfer.Extract(volume, [tree], host);
            Assert.Equal(Payload, File.ReadAllBytes(Path.Combine(host, "Tree", ".copperimage-v1-434F4E")));
            Assert.True(Directory.Exists(Path.Combine(host, "Tree", "Empty")));
            Assert.Empty(File.ReadAllBytes(Path.Combine(host, "Tree", "Zero")));
            Assert.Throws<IOException>(() => DiskTransfer.Extract(volume, [tree], host));
            Assert.True(volume.Capabilities.Operations.HasFlag(DiskVolumeOperations.Import));
        }
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    public void DmsMutationsReencodeAndRemountCleanlyThroughTheGuest(int dos)
    {
        var path = DmsImage(dos);
        var host = Path.Combine(_root, "Host"); Directory.CreateDirectory(Path.Combine(host, "Nested", "Empty"));
        var data = Enumerable.Range(0, 120005).Select(i => (byte)(i * 23 + i / 67)).ToArray();
        File.WriteAllBytes(Path.Combine(host, "Nested", "Data.bin"), data);
        using (var image = DiskImageSession.Open(path))
        {
            var v = image.Volumes[0]; var stale = v.Root; var generation = image.Generation;
            image.CreateDirectory(v, v.Root, "Destination");
            Assert.NotEqual(generation, image.Generation);
            Assert.Throws<InvalidOperationException>(() => v.ReadDirectory(stale));
            v = image.Volumes[0]; var destination = v.ReadDirectory(v.Root).Single(e => e.Name == "Destination");
            image.Import(v, destination, [host]);
            v = image.Volumes[0]; destination = v.ReadDirectory(v.Root).Single(e => e.Name == "Destination");
            image.CopyFromImage(v, destination, v, [v.ReadDirectory(v.Root).Single(e => e.Name == "Tree")]);
            v = image.Volumes[0]; destination = v.ReadDirectory(v.Root).Single(e => e.Name == "Destination");
            var imported = v.ReadDirectory(destination).Single(e => e.Name == "Host");
            image.MoveFromImage(v, v.Root, v, destination, [imported]);
            v = image.Volumes[0]; imported = v.ReadDirectory(v.Root).Single(e => e.Name == "Host");
            image.Rename(v, v.Root, imported, "Renamed");
            v = image.Volumes[0]; imported = v.ReadDirectory(v.Root).Single(e => e.Name == "Renamed");
            image.Protect(v, v.Root, [imported], 255, 0x40);
            v = image.Volumes[0]; imported = v.ReadDirectory(v.Root).Single(e => e.Name == "Renamed");
            Assert.Equal(0x40u, imported.Protection);
            var nested = v.ReadDirectory(imported).Single();
            using (var input = v.OpenRead(v.ReadDirectory(nested).Single(e => e.Name == "Data.bin")))
            using (var output = new MemoryStream()) { input.CopyTo(output); Assert.Equal(data, output.ToArray()); }
            image.Delete(v, v.Root, [imported]);
        }
        using var reopened = DiskImageSession.Open(path);
        var volume = reopened.Volumes[0]; var entries = volume.ReadDirectory(volume.Root);
        Assert.DoesNotContain(entries, e => e.Name is "Host" or "Renamed");
        var copy = volume.ReadDirectory(entries.Single(e => e.Name == "Destination")).Single(); Assert.Equal("Tree", copy.Name);
        using var extracted = new MemoryStream(); volume.CopyFileTo(volume.ReadDirectory(copy).Single(e => e.Name == "CON"), extracted);
        Assert.Equal(Payload, extracted.ToArray());
        Assert.DoesNotContain(Directory.GetFileSystemEntries(_root), p => Path.GetFileName(p).StartsWith(".copperscreen-import-"));
    }

    [Theory]
    [InlineData("conflict")] [InlineData("cancel")] [InlineData("full")] [InlineData("publish")] [InlineData("reader")]
    public void FailedDmsSavesKeepTheOriginalArchiveAndCleanBothStages(string failure)
    {
        var path = DmsImage(1); var original = File.ReadAllBytes(path);
        using var image = DiskImageSession.Open(path); var v = image.Volumes[0];
        var host = Path.Combine(_root, failure == "conflict" ? "Tree" : "Incoming"); Directory.CreateDirectory(host);
        File.WriteAllBytes(Path.Combine(host, "Data"), new byte[failure == "full" ? 901120 : 150003]);
        using var cancelled = new CancellationTokenSource();
        using var reader = failure == "reader" ? v.OpenRead(v.ReadDirectory(v.ReadDirectory(v.Root).Single()).Single(e => e.Name == "CON")) : null;
        var progress = new DmsProgress(_ =>
        {
            if (failure == "cancel") cancelled.Cancel();
            if (failure == "publish") File.SetAttributes(path, FileAttributes.ReadOnly);
        });
        try { Assert.ThrowsAny<Exception>(() => image.Import(v, v.Root, [host], cancelled.Token, progress)); }
        finally { File.SetAttributes(path, FileAttributes.Normal); }
        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.DoesNotContain(Directory.GetFileSystemEntries(_root), p => Path.GetFileName(p).StartsWith(".copperscreen-import-"));
        reader?.Dispose();
        v = image.Volumes[0]; Assert.Single(v.ReadDirectory(v.Root));
        image.CreateDirectory(v, v.Root, "Recovered");
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void DmsWithAuxiliaryRecordsOrHostReadOnlyAttributeRefusesMutations(bool auxiliary)
    {
        var path = DmsImage(0);
        if (auxiliary) File.WriteAllBytes(path, DmsFixture.WithAuxiliary(File.ReadAllBytes(path)));
        else File.SetAttributes(path, FileAttributes.ReadOnly);
        var original = File.ReadAllBytes(path);
        try
        {
            using var image = DiskImageSession.Open(path); var v = image.Volumes[0]; var tree = Assert.Single(v.ReadDirectory(v.Root));
            Assert.True(image.IsReadOnly); Assert.False(v.Capabilities.Operations.HasFlag(DiskVolumeOperations.Import));
            Assert.Throws<NotSupportedException>(() => image.CreateDirectory(v, v.Root, "Refused"));
            Assert.Throws<NotSupportedException>(() => image.Delete(v, v.Root, [tree]));
            Assert.Equal(original, File.ReadAllBytes(path));
        }
        finally { File.SetAttributes(path, FileAttributes.Normal); }
    }
    private sealed class DmsProgress(Action<DiskProgress> action) : IProgress<DiskProgress>
    { public void Report(DiskProgress value) => action(value); }

    [Theory]
    [InlineData(true, false)] [InlineData(false, true)] [InlineData(true, true)]
    public void MovesBetweenDmsAndRawImagesPublishDestinationThenRemoveSource(bool sourceDms, bool targetDms)
    {
        var sourcePath = sourceDms ? DmsImage(0) : Blank("RawSource", 0);
        if (!sourceDms) Seed(sourcePath, 0);
        var targetPath = targetDms ? DmsImage(1) : Blank("RawTarget", 1);
        using var source = DiskImageSession.Open(sourcePath); using var target = DiskImageSession.Open(targetPath);
        var tv = target.Volumes[0]; target.CreateDirectory(tv, tv.Root, "Destination");
        tv = target.Volumes[0]; var destination = tv.ReadDirectory(tv.Root).Single(e => e.Name == "Destination");
        var sv = source.Volumes[0]; target.MoveFromImage(tv, destination, sv, sv.Root, sv.ReadDirectory(sv.Root));
        sv = source.Volumes[0]; Assert.Empty(sv.ReadDirectory(sv.Root));
        tv = target.Volumes[0]; destination = tv.ReadDirectory(tv.Root).Single(e => e.Name == "Destination");
        var tree = Assert.Single(tv.ReadDirectory(destination));
        using var output = new MemoryStream(); tv.CopyFileTo(tv.ReadDirectory(tree).Single(e => e.Name == "CON"), output);
        Assert.Equal(Payload, output.ToArray());
    }

    [Fact]
    public void DmsMoveRetainsDestinationIfSourceRemovalBecomesUnavailable()
    {
        var sourcePath = DmsImage(0); var targetPath = Blank("Destination", 1); var original = File.ReadAllBytes(sourcePath);
        using var source = DiskImageSession.Open(sourcePath); using var target = DiskImageSession.Open(targetPath);
        var sv = source.Volumes[0]; var tv = target.Volumes[0];
        try
        {
            Assert.Throws<DiskMoveIncompleteException>(() => target.MoveFromImage(tv, tv.Root, sv, sv.Root, sv.ReadDirectory(sv.Root),
                progress: new DmsProgress(_ => File.SetAttributes(sourcePath, FileAttributes.ReadOnly))));
            Assert.Equal(original, File.ReadAllBytes(sourcePath));
            tv = target.Volumes[0]; Assert.Equal("Tree", Assert.Single(tv.ReadDirectory(tv.Root)).Name);
        }
        finally { File.SetAttributes(sourcePath, FileAttributes.Normal); }
    }
}
