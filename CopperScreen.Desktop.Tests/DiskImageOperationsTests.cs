using CopperScreen;
using CopperStart.FileSystem;

namespace CopperScreen.Tests;

public sealed partial class DiskImageOperationsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "copper-operations-" + Guid.NewGuid().ToString("N"));
    private static readonly byte[] Payload = Enumerable.Range(0, 100013).Select(i => (byte)(i * 19 + i / 71)).ToArray();
    public DiskImageOperationsTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    private string Blank(string name, int format)
    {
        var path = Path.Combine(_root, name + (format < 6 && format % 2 == 0 ? ".adf" : ".hdf"));
        using (var disk = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
        {
            disk.SetLength(format < 6 && format % 2 == 0 ? 901120 : 2 * 1024 * 1024);
            CopperScreenFileSystemFormatter.Format(disk, 0, disk.Length, format < 6 ? CopperScreenHardDiskSetup.FileSystem.Ffs
                : format == 6 ? CopperScreenHardDiskSetup.FileSystem.Sfs : CopperScreenHardDiskSetup.FileSystem.Sfs2, name, default);
        }
        if (format < 6) DiskImageImportTests.SetClassicVariant(path, format);
        return path;
    }
    private void Seed(string path, int format)
    {
        using var disk = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using var session = new FileSystemSession(new Source(disk), new((uint)(disk.Length / 512), 512, format < 6 ? 2u : 1u), new() { AllowWrites = true });
        var dir = session.CreateDirectory(session.Root, "Tree"); session.CreateDirectory(dir, "Empty");
        // A valid Amiga name that cannot be staged through a Windows host directory.
        var file = session.CopyFileFrom(dir, "CON", new MemoryStream(Payload));
        session.CopyFileFrom(dir, "Zero", new MemoryStream());
        foreach (var e in new[] { dir, file })
        {
            session.SetModified(e, new DateTime(2020, 5, 6, 7, 8, 9));
            session.SetComment(e, "Amiga metadata"); session.SetProtection(e, 0x40);
        }
    }
    [Fact]
    public void ImageTransfersAndRenameUseGuestNameComparison()
    {
        var a = Blank("Source", 0); var b = Blank("Target", 0);
        using (var disk = new FileStream(a, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        using (var s = new FileSystemSession(new Source(disk), new((uint)(disk.Length / 512)), new() { AllowWrites = true }))
        {
            s.CopyFileFrom(s.Root, "café", new MemoryStream([1]));
            s.CopyFileFrom(s.Root, "CAFÉ", new MemoryStream([2]));
        }
        using var source = CopperScreenDiskImage.Open(a); using var target = CopperScreenDiskImage.Open(b);
        var sv = source.Volumes[0]; var tv = target.Volumes[0];
        target.CopyFromImage(tv, tv.Root, sv, sv.ReadDirectory(sv.Root));
        tv = target.Volumes[0]; Assert.Equal(2, tv.ReadDirectory(tv.Root).Count);
        var entries = tv.ReadDirectory(tv.Root); var lower = entries.Single(e => e.Name == "café");
        target.Rename(tv, tv.Root, lower, "CAFé"); // DOS0 folds ASCII, but keeps Latin-1 é and É distinct.
        tv = target.Volumes[0]; Assert.Contains(tv.ReadDirectory(tv.Root), e => e.Name == "CAFé");
        Assert.Contains(tv.ReadDirectory(tv.Root), e => e.Name == "CAFÉ");
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    public void ImagesCopyDirectlyAcrossFormatsAndKeepTheSourceAndMetadata(int format)
    {
        var a = Blank("Source", format); var b = Blank("Target", (format + 1) % 8); Seed(a, format);
        var before = File.ReadAllBytes(a);
        using (var source = CopperScreenDiskImage.Open(a))
        using (var target = CopperScreenDiskImage.Open(b))
        {
            var sv = source.Volumes[0]; var tv = target.Volumes[0];
            var result = target.CopyFromImage(tv, tv.Root, sv, sv.ReadDirectory(sv.Root)); Assert.Equal(2, result.Files);
            tv = target.Volumes[0]; var tree = Assert.Single(tv.ReadDirectory(tv.Root));
            Assert.Equal(0x40u, tree.Protection); Assert.Equal("Amiga metadata", tree.Comment);
            Assert.Equal(new DateTime(2020, 5, 6, 7, 8, 9), tree.Modified);
            var children = tv.ReadDirectory(tree); Assert.Equal(3, children.Count);
            Assert.Empty(tv.ReadDirectory(children.Single(e => e.Name == "Empty")));
            var file = children.Single(e => e.Name == "CON");
            Assert.Equal(0x40u, file.Protection); Assert.Equal("Amiga metadata", file.Comment); Assert.Equal(tree.Modified, file.Modified);
            using var output = new MemoryStream(); tv.CopyFileTo(file, output); Assert.Equal(Payload, output.ToArray());
        }
        Assert.Equal(before, File.ReadAllBytes(a));
        Assert.DoesNotContain(Directory.GetFileSystemEntries(_root), p => Path.GetFileName(p).StartsWith(".copperscreen"));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    public void ImageDirectoryEditsPublishCleanlyAndFailuresLeaveTheImageUnchanged(int format)
    {
        var path = Blank("Edits", format); Seed(path, format);
        using var image = CopperScreenDiskImage.Open(path);
        var v = image.Volumes[0]; image.CreateDirectory(v, v.Root, "Added");
        v = image.Volumes[0]; var added = v.ReadDirectory(v.Root).Single(e => e.Name == "Added");
        image.Rename(v, v.Root, added, "ADDED");
        v = image.Volumes[0]; Assert.Contains(v.ReadDirectory(v.Root), e => e.Name == "ADDED");
        var before = File.ReadAllBytes(path);
        Assert.ThrowsAny<Exception>(() => image.Rename(v, v.Root, v.ReadDirectory(v.Root).Single(e => e.Name == "ADDED"), "Tree"));
        Assert.Equal(before, File.ReadAllBytes(path));
        _ = v.Root;
        Assert.ThrowsAny<Exception>(() => image.CreateDirectory(v, v.Root, "../bad")); Assert.Equal(before, File.ReadAllBytes(path));
        _ = v.Root;
        Assert.ThrowsAny<OperationCanceledException>(() => image.Delete(v, v.Root, v.ReadDirectory(v.Root), new CancellationToken(true)));
        Assert.Equal(before, File.ReadAllBytes(path));
        _ = v.Root; image.Delete(v, v.Root, v.ReadDirectory(v.Root));
        v = image.Volumes[0]; Assert.Empty(v.ReadDirectory(v.Root));
        using var remounted = CopperScreenDiskImage.Open(path); Assert.Empty(remounted.Volumes[0].ReadDirectory(remounted.Volumes[0].Root));
    }

    [Theory]
    [InlineData("conflict")] [InlineData("cancel")] [InlineData("protected")]
    public void FailedCopiesAndDeletesNeverPublishPartialChanges(string failure)
    {
        var a = Blank("Source", 1); var b = Blank("Target", 7); Seed(a, 1);
        using var source = CopperScreenDiskImage.Open(a); using var target = CopperScreenDiskImage.Open(b);
        var sv = source.Volumes[0]; var tv = target.Volumes[0];
        if (failure == "conflict") { target.CopyFromImage(tv, tv.Root, sv, sv.ReadDirectory(sv.Root)); tv = target.Volumes[0]; _ = tv.Root; }
        var original = File.ReadAllBytes(b);
        if (failure == "protected")
        {
            // Native protection failures are already qualified by service tests; a read-only host image is refused before staging.
            File.SetAttributes(b, FileAttributes.ReadOnly);
            try { Assert.Throws<NotSupportedException>(() => target.CreateDirectory(tv, tv.Root, "No")); }
            finally { File.SetAttributes(b, FileAttributes.Normal); }
        }
        else
        {
            using var cancel = new CancellationTokenSource();
            Assert.ThrowsAny<Exception>(() => target.CopyFromImage(tv, tv.Root, sv, sv.ReadDirectory(sv.Root), cancel.Token,
                failure == "cancel" ? new CancelProgress(cancel) : null));
        }
        Assert.Equal(original, File.ReadAllBytes(b));
        Assert.DoesNotContain(Directory.GetFileSystemEntries(_root), p => Path.GetFileName(p).StartsWith(".copperscreen"));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    public void AProtectedEntryRollsBackEarlierDeletionsInTheSelection(int format)
    {
        var path = Blank("Protected", format);
        using (var disk = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        using (var s = new FileSystemSession(new Source(disk), new((uint)(disk.Length / 512), 512, format < 6 ? 2u : 1u), new() { AllowWrites = true }))
        {
            var entry = s.CopyFileFrom(s.Root, "Protected", new MemoryStream([1])); s.SetProtection(entry, 1);
            s.CopyFileFrom(s.Root, "Before", new MemoryStream([2]));
        }
        var before = File.ReadAllBytes(path);
        using var image = CopperScreenDiskImage.Open(path); var v = image.Volumes[0]; var entries = v.ReadDirectory(v.Root);
        // The deletion stack removes Before first, then encounters the protected entry.
        var error = Assert.Throws<FileSystemServiceException>(() => image.Delete(v, v.Root,
            [entries.Single(e => e.Name == "Protected"), entries.Single(e => e.Name == "Before")]));
        Assert.Equal(222u, error.Status); Assert.Contains("delete-protected", error.Message);
        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.Equal(2, v.ReadDirectory(v.Root).Count);
        Assert.DoesNotContain(Directory.GetFileSystemEntries(_root), p => Path.GetFileName(p).StartsWith(".copperscreen"));
    }
    private sealed class CancelProgress(CancellationTokenSource source) : IProgress<DiskExtractionProgress>
    { public void Report(DiskExtractionProgress progress) => source.Cancel(); }
    private sealed class Source(FileStream file) : IWritableBlockSource
    {
        public long Length => file.Length;
        public void Read(long offset, Span<byte> target) { file.Position = offset; file.ReadExactly(target); }
        public void Write(long offset, ReadOnlySpan<byte> data) { file.Position = offset; file.Write(data); }
        public void Flush() => file.Flush(true);
    }
}
