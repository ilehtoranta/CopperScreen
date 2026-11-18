using CopperDisk;
using CopperDisk.FileSystem;

namespace CopperScreen.Tests;

public sealed partial class DiskImageOperationsTests
{
    [Theory]
    [InlineData(0, false)] [InlineData(1, false)] [InlineData(2, false)] [InlineData(3, false)]
    [InlineData(4, false)] [InlineData(5, false)] [InlineData(6, false)] [InlineData(7, false)]
    [InlineData(0, true)] [InlineData(1, true)] [InlineData(2, true)] [InlineData(3, true)]
    [InlineData(4, true)] [InlineData(5, true)] [InlineData(6, true)] [InlineData(7, true)]
    public void ImageSavesUseSparseWorkingFilesAndPreserveImportedZeroAndNonzeroBytes(int format, bool dms)
    {
        var path = dms ? DmsImage(format) : Blank("Sparse", format);
        if (!dms) Seed(path, format);
        var originalLength = new FileInfo(path).Length;
        var before = AmigaSparseFile.GetAllocatedBytes(path); Assert.NotNull(before);
        var host = Path.Combine(_root, "Incoming"); Directory.CreateDirectory(host);
        var zeros = new byte[220017]; File.WriteAllBytes(Path.Combine(host, "Zeros"), zeros);
        var nonzero = Enumerable.Range(0, 70001).Select(i => (byte)(i * 17 + i / 71)).ToArray();
        File.WriteAllBytes(Path.Combine(host, "Data"), nonzero);
        var stageSeen = false;
        using (var image = DiskImageSession.Open(path))
        {
            var volume = image.Volumes[0];
            image.Import(volume, volume.Root, [host], progress: new SparseSaveProgress(_ =>
            {
                var stages = Directory.GetFiles(_root, ".copperscreen-import-*"); Assert.Single(stages);
                if (OperatingSystem.IsWindows()) Assert.True(File.GetAttributes(stages[0]).HasFlag(FileAttributes.SparseFile));
                stageSeen = true;
            }));
            Assert.True(stageSeen);
            volume = image.Volumes[0]; image.CreateDirectory(volume, volume.Root, "Again");
        }
        if (!dms)
        {
            Assert.Equal(originalLength, new FileInfo(path).Length);
            if (OperatingSystem.IsWindows()) Assert.True(File.GetAttributes(path).HasFlag(FileAttributes.SparseFile));
            var after = AmigaSparseFile.GetAllocatedBytes(path); Assert.NotNull(after); Assert.True(after < before);
        }
        using var reopened = DiskImageSession.Open(path); var v = reopened.Volumes[0];
        var entries = v.ReadDirectory(v.Root); Assert.Contains(entries, e => e.Name == "Again");
        var files = v.ReadDirectory(entries.Single(e => e.Name == "Incoming"));
        using var output = new MemoryStream(); v.CopyFileTo(files.Single(e => e.Name == "Zeros"), output); Assert.Equal(zeros, output.ToArray());
        output.SetLength(0); v.CopyFileTo(files.Single(e => e.Name == "Data"), output); Assert.Equal(nonzero, output.ToArray());
        output.SetLength(0); v.CopyFileTo(v.ReadDirectory(entries.Single(e => e.Name == "Tree")).Single(e => e.Name == "CON"), output); Assert.Equal(Payload, output.ToArray());
        Assert.DoesNotContain(Directory.GetFiles(_root), p => Path.GetFileName(p).StartsWith(".copperscreen-import-") || Path.GetFileName(p).StartsWith(".copper-sparse-"));
    }
    private sealed class SparseSaveProgress(Action<DiskProgress> action) : IProgress<DiskProgress>
    { public void Report(DiskProgress value) => action(value); }
}
