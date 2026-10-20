using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed class HardDiskBackupTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "copper-hdf-backup-" + Guid.NewGuid().ToString("N"));
    public HardDiskBackupTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);
    private string PathOf(string name) => Path.Combine(_root, name);
    private CopperScreenSettingsDraft Draft()
    {
        var draft = new CopperScreenSettingsDraft { Id = "personal", DisplayName = "My disks", Description = "Keep all settings", KickstartSource = CopperScreenKickstartSource.CopperStart, KickstartRomPath = PathOf("external.rom") };
        for (var unit = 0; unit < 2; unit++)
        {
            var path = PathOf($"source-{unit}.hdf");
            File.WriteAllBytes(path, Enumerable.Range(0, 8192).Select(i => (byte)(i * 17 + unit)).ToArray());
            draft.HardDrives.Add(unit == 0 ? new(unit, path, false, 0, AmigaHardfileMountMode.RigidDiskBlock)
                : CopperScreenHardfileSettings.Ffs(unit, path, true, "Work:2:32:4:58") with { Partition = new() { DeviceName = "Work", BootPriority = -7, NumBuffers = 57, Surfaces = 2, BlocksPerTrack = 32, LowCylinder = 4, HighCylinder = 58, DosType = 0x444F5301 } });
        }
        return draft;
    }
    [Fact]
    public void RoundTripPreservesEveryImageByteAndConfigurationAndRebasesPathsIntoNewCopies()
    {
        var draft = Draft(); var backup = PathOf("backup.copperhdf");
        var saved = CopperScreenProfileStore.Serialize(draft, AppContext.BaseDirectory);
        CopperScreenHardDiskBackup.Export(draft, backup, AppContext.BaseDirectory, PathOf("private"));
        var bytes = File.ReadAllBytes(backup);
        var restored = CopperScreenHardDiskBackup.Restore(backup, PathOf("reinstalled"), AppContext.BaseDirectory);
        for (var i = 0; i < 2; i++)
        {
            Assert.NotEqual(draft.HardDrives[i].Path, restored.HardDrives[i].Path);
            Assert.Equal(File.ReadAllBytes(draft.HardDrives[i].Path), File.ReadAllBytes(restored.HardDrives[i].Path));
            Assert.Equal(draft.HardDrives[i] with { Path = restored.HardDrives[i].Path }, restored.HardDrives[i]);
        }
        var comparable = restored.Clone(); comparable.HardDrives.Clear(); comparable.HardDrives.AddRange(draft.HardDrives);
        Assert.Equal(saved, CopperScreenProfileStore.Serialize(comparable, AppContext.BaseDirectory));
        Assert.Equal(bytes, File.ReadAllBytes(backup));
        var second = CopperScreenHardDiskBackup.Restore(backup, PathOf("reinstalled"), AppContext.BaseDirectory);
        Assert.NotEqual(restored.HardDrives[0].Path, second.HardDrives[0].Path);
        Assert.True(File.Exists(restored.SourcePath));
    }
    [Fact]
    public void ExportRefusesExistingDestinationAndActiveWriterAndPrivateDataDestination()
    {
        var draft = Draft(); var backup = PathOf("backup.copperhdf");
        File.WriteAllText(backup, "existing backup");
        Assert.Throws<IOException>(() => CopperScreenHardDiskBackup.Export(draft, backup, AppContext.BaseDirectory, PathOf("private")));
        Assert.Equal("existing backup", File.ReadAllText(backup));
        using var writer = new FileStream(draft.HardDrives[1].Path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
        Assert.Throws<IOException>(() => CopperScreenHardDiskBackup.Export(draft, PathOf("locked.copperhdf"), AppContext.BaseDirectory, PathOf("private")));
        Assert.False(File.Exists(PathOf("locked.copperhdf")));
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
        Assert.Throws<IOException>(() => CopperScreenHardDiskBackup.Export(draft, PathOf("private/backup.copperhdf"), AppContext.BaseDirectory, PathOf("private")));
    }
    [Theory]
    [InlineData("archive")]
    [InlineData("image")]
    [InlineData("second-image")]
    public void TruncatedBackupsPreserveAnExistingRestoreAndSavedSettings(string truncation)
    {
        var draft = Draft(); var backup = PathOf("valid.copperhdf");
        CopperScreenHardDiskBackup.Export(draft, backup, AppContext.BaseDirectory, PathOf("private"));
        var existing = CopperScreenHardDiskBackup.Restore(backup, PathOf("restored"), AppContext.BaseDirectory);
        var data = PathOf("restored");
        new CopperScreenRecentSession(data).Save(existing, 37, true, AppContext.BaseDirectory);
        var before = Directory.GetFiles(data, "*", SearchOption.AllDirectories).ToDictionary(p => Path.GetRelativePath(data, p), File.ReadAllBytes);
        var damaged = PathOf("truncated.copperhdf"); File.Copy(backup, damaged);
        if (truncation == "archive")
        {
            using var file = new FileStream(damaged, FileMode.Open, FileAccess.Write); file.SetLength(file.Length - 32);
        }
        else
        {
            using var zip = ZipFile.Open(damaged, ZipArchiveMode.Update);
            var name = truncation == "second-image" ? "disks/unit-1.hdf" : "disks/unit-0.hdf";
            var entry = zip.GetEntry(name)!; entry.Delete();
            using var writer = zip.CreateEntry(name).Open(); writer.Write(new byte[512]);
        }
        var input = File.ReadAllBytes(damaged);
        Assert.Throws<InvalidDataException>(() => CopperScreenHardDiskBackup.Restore(damaged, data, AppContext.BaseDirectory));
        Assert.Equal(before.Keys.Order(), Directory.GetFiles(data, "*", SearchOption.AllDirectories).Select(p => Path.GetRelativePath(data, p)).Order());
        foreach (var file in before) Assert.Equal(file.Value, File.ReadAllBytes(Path.Combine(data, file.Key)));
        Assert.Equal(input, File.ReadAllBytes(damaged));
        Assert.Single(Directory.GetDirectories(Path.Combine(data, "HardDiskRestores")));
    }

    [Fact]
    public void FailedMachineSelectionSaveRemovesOnlyItsNewRestoreAndPreservesExistingCopies()
    {
        var draft = Draft(); var backup = PathOf("valid.copperhdf");
        CopperScreenHardDiskBackup.Export(draft, backup, AppContext.BaseDirectory, PathOf("private"));
        var existing = CopperScreenHardDiskBackup.Restore(backup, PathOf("data"), AppContext.BaseDirectory);
        new CopperScreenRecentSession(PathOf("data")).Save(existing, 37, true, AppContext.BaseDirectory);
        var before = Directory.GetFiles(PathOf("data"), "*", SearchOption.AllDirectories).ToDictionary(p => p, File.ReadAllBytes);
        var input = File.ReadAllBytes(backup);
        Assert.Throws<IOException>(() => CopperScreenHardDiskBackup.Restore(backup, PathOf("data"), AppContext.BaseDirectory,
            _ => throw new IOException("Machine selection could not be saved.")));
        Assert.Equal(before.Keys.Order(), Directory.GetFiles(PathOf("data"), "*", SearchOption.AllDirectories).Order());
        foreach (var file in before) Assert.Equal(file.Value, File.ReadAllBytes(file.Key));
        Assert.Equal(input, File.ReadAllBytes(backup));
    }

    [Theory]
    [InlineData("payload")]
    [InlineData("profile")]
    [InlineData("version")]
    [InlineData("traversal")]
    [InlineData("duplicate")]
    public void CorruptOrUnexpectedBackupNeverPublishesARestoreOrChangesExistingImages(string corruption)
    {
        var draft = Draft(); var backup = PathOf("backup.copperhdf");
        CopperScreenHardDiskBackup.Export(draft, backup, AppContext.BaseDirectory, PathOf("private"));
        using (var zip = ZipFile.Open(backup, ZipArchiveMode.Update))
        {
            if (corruption is "payload" or "profile" or "version")
            {
                var name = corruption == "payload" ? "disks/unit-0.hdf" : corruption == "profile" ? "profile.json" : "manifest.json";
                var entry = zip.GetEntry(name)!; byte[] data;
                using (var reader = entry.Open()) { using var memory = new MemoryStream(); reader.CopyTo(memory); data = memory.ToArray(); }
                if (corruption == "version") { var json = JsonNode.Parse(data)!; json["Version"] = 999; data = Encoding.UTF8.GetBytes(json.ToJsonString()); }
                else data[0] ^= 1;
                entry.Delete(); using var stream = zip.CreateEntry(name).Open(); stream.Write(data);
            }
            else { using var stream = zip.CreateEntry(corruption == "traversal" ? "../escaped.hdf" : "profile.json").Open(); stream.WriteByte(0); }
        }
        var existing = File.ReadAllBytes(draft.HardDrives[0].Path); var damagedBackup = File.ReadAllBytes(backup);
        Assert.Throws<InvalidDataException>(() => CopperScreenHardDiskBackup.Restore(backup, PathOf("restored"), AppContext.BaseDirectory));
        Assert.Equal(existing, File.ReadAllBytes(draft.HardDrives[0].Path));
        Assert.Equal(damagedBackup, File.ReadAllBytes(backup));
        Assert.False(File.Exists(PathOf("escaped.hdf")));
        var restoreRoot = PathOf("restored/HardDiskRestores");
        Assert.True(!Directory.Exists(restoreRoot) || !Directory.EnumerateFileSystemEntries(restoreRoot).Any());
    }
}
