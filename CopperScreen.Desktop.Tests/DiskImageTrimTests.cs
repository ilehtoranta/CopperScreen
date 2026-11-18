using CopperDisk;
using CopperDisk.FileSystem;
using CopperDisk.Tests;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed partial class DiskImageOperationsTests
{
    [Theory]
    [InlineData(0, false)] [InlineData(1, false)] [InlineData(2, false)] [InlineData(3, false)]
    [InlineData(4, false)] [InlineData(5, false)] [InlineData(6, false)] [InlineData(7, false)]
    [InlineData(0, true)] [InlineData(1, true)] [InlineData(2, true)] [InlineData(3, true)]
    [InlineData(4, true)] [InlineData(5, true)] [InlineData(6, true)] [InlineData(7, true)]
    public void DeletingNonzeroFilesAndTreesReclaimsStorageAndPreservesOtherFiles(int format, bool dms)
    {
        var path = dms ? DmsImage(format) : Blank("Trim", format);
        if (!dms) Seed(path, format);
        var trash = Path.Combine(_root, "Trash"); var tree = Path.Combine(_root, "RemovedTree");
        Directory.CreateDirectory(Path.Combine(tree, "Empty"));
        var data = new byte[320017]; new Random(1511).NextBytes(data); File.WriteAllBytes(trash, data);
        File.WriteAllBytes(Path.Combine(tree, "Nested"), data[..66001]);
        using var image = DiskImageSession.Open(path); var v = image.Volumes[0];
        image.Import(v, v.Root, [trash, tree]);
        var before = dms ? new FileInfo(path).Length : AmigaSparseFile.GetAllocatedBytes(path)!.Value;
        var logical = new FileInfo(path).Length;
        v = image.Volumes[0]; var entries = v.ReadDirectory(v.Root);
        image.Delete(v, v.Root, entries.Where(e => e.Name is "Trash" or "RemovedTree").ToArray());
        var after = dms ? new FileInfo(path).Length : AmigaSparseFile.GetAllocatedBytes(path)!.Value;
        Assert.True(after < before, $"Expected reclaim for format {format}, DMS={dms}: {before} -> {after}.");
        if (!dms) Assert.Equal(logical, new FileInfo(path).Length);
        v = image.Volumes[0]; VerifyTree(v, Assert.Single(v.ReadDirectory(v.Root)));
        Assert.DoesNotContain(Directory.GetFiles(_root), p => Path.GetFileName(p).StartsWith(".copperscreen-import-"));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public void PartitionRelativeTrimPreservesRdbAndEveryByteOutsideTheSelectedPartition(int selected)
    {
        var path = Path.Combine(_root, "TrimPartitions.hdf"); File.WriteAllBytes(path, new AmigaFileSystemFixture().Rdb(true));
        using (var disk = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            CopperScreenFileSystemFormatter.Format(disk, 44 * 512, 1760 * 512, CopperScreenHardDiskSetup.FileSystem.Ffs, "First", default);
            CopperScreenFileSystemFormatter.Format(disk, 1804 * 512, 1760 * 512, CopperScreenHardDiskSetup.FileSystem.Sfs, "Second", default);
        }
        var trash = Path.Combine(_root, "Trash"); var data = new byte[300013]; new Random(1511).NextBytes(data); File.WriteAllBytes(trash, data);
        using var image = DiskImageSession.Open(path); var v = image.Volumes[selected]; image.Import(v, v.Root, [trash]);
        v = image.Volumes[selected]; var before = File.ReadAllBytes(path); var allocation = AmigaSparseFile.GetAllocatedBytes(path)!.Value;
        var start = (int)v.Range.ByteOffset; var end = start + (int)v.Range.Length;
        image.Delete(v, v.Root, v.ReadDirectory(v.Root));
        var after = File.ReadAllBytes(path);
        Assert.Equal(before[..start], after[..start]); Assert.Equal(before[end..], after[end..]);
        Assert.True(AmigaSparseFile.GetAllocatedBytes(path)!.Value < allocation);
        foreach (var volume in image.Volumes) Assert.Empty(volume.ReadDirectory(volume.Root));
    }
}
