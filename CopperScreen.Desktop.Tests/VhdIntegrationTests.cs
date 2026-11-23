using CopperDisk;
using CopperDisk.FileSystem;
using CopperDisk.Tests;

namespace CopperScreen.Tests;

public sealed class VhdIntegrationTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("CopperDesktopVhd-").FullName;
    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void RdbVhdCanBeBrowsedBackedUpAndCopiedForPartitioning()
    {
        const int capacity = 8 * 1024 * 1024;
        var raw = Path.Combine(_root, "source.hdf");
        CopperScreenHardDiskSetup.Create(raw, capacity, null, "DH0", 1, 7,
            CopperScreenHardDiskSetup.FileSystem.Ffs, "Work", true);
        var path = Path.Combine(_root, "disk.vhd");
        var bytes = VhdFixture.Dynamic(capacity, firstBlock: File.ReadAllBytes(raw)[..(512 * 1024)]);
        File.WriteAllBytes(path, bytes);

        using (var image = DiskImageSession.Open(path))
        {
            var volume = Assert.Single(image.Volumes);
            volume.Mount();
            Assert.Equal("Work", volume.DisplayName);
            Assert.True(image.IsReadOnly);
        }

        var draft = new CopperScreenSettingsDraft();
        draft.HardDrives.Add(new(0, path, true, 0, AmigaHardfileMountMode.RigidDiskBlock));
        var backup = Path.Combine(_root, "backup.copperhdf");
        CopperScreenHardDiskBackup.Export(draft, backup, AppContext.BaseDirectory, Path.Combine(_root, "private"));
        var restored = CopperScreenHardDiskBackup.Restore(backup, Path.Combine(_root, "restored"), AppContext.BaseDirectory);
        var restoredPath = Assert.Single(restored.HardDrives).Path;
        Assert.Equal(bytes, File.ReadAllBytes(restoredPath));
        AmigaRdbImage.Validate(restoredPath);

        var copy = Path.Combine(_root, "partitioned-copy.hdf");
        CopperScreenHardDiskSetup.Create(copy, 0, path, "DH1", 8, 255,
            CopperScreenHardDiskSetup.FileSystem.Ffs, "Data", false);
        Assert.Equal(capacity, new FileInfo(copy).Length);
        Assert.Equal(2, CopperScreenHardDiskSetup.Inspect(copy).Partitions.Count);
        Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void CopperStartValidatesAnExplicitPartitionAgainstTheVhdCapacity()
    {
        var path = Path.Combine(_root, "partition.vhd");
        File.WriteAllBytes(path, VhdFixture.Dynamic(8 * 1024 * 1024));
        CopperScreenHardfileSettings.Ffs(0, path, true, "DH0:2:32:4:58").ValidateCopperStart(checkImage: true);
        Assert.Throws<InvalidOperationException>(() => CopperScreenHardfileSettings.Ffs(0, path, true, "DH0:2:32:4:256")
            .ValidateCopperStart(checkImage: true));
    }
}
