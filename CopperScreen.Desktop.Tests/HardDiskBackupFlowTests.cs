using System.Reflection;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed partial class LaunchWorkflowTests
{
    [Fact]
    public Task StartupCleansAbandonedCopiesAndSkipsAnActiveOperation() => UiAsync(() =>
    {
        var data = Path.Combine(_root, "cleanup-data"); var recent = new CopperScreenRecentSession(data);
        var image = Path.Combine(_root, "existing.hdf"); File.WriteAllBytes(image, new byte[4096]);
        var backup = Path.Combine(_root, "existing.copperhdf"); File.WriteAllText(backup, "keep backup");
        var draft = new CopperScreenSettingsDraft(); draft.HardDrives.Add(new(0, image, false, 0, AmigaHardfileMountMode.RigidDiskBlock));
        recent.Save(draft, 37, true, AppContext.BaseDirectory);
        var machine = File.ReadAllBytes(Path.Combine(data, "last-machine.json")); var preferences = File.ReadAllBytes(Path.Combine(data, "recent-session.json"));
        (string Path, CopperScreenHardDiskBackupCleanup.Operation Operation) Temporary()
        {
            var id = Guid.NewGuid().ToString("N"); var path = Path.Combine(_root, ".hdf-backup-" + id + ".tmp");
            var operation = CopperScreenHardDiskBackupCleanup.Begin(data, "export", path, id)!;
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { operation.TrackRoot(file.SafeFileHandle); file.Write(new byte[4096]); }
            return (path, operation);
        }
        var abandoned = Temporary(); abandoned.Operation.Dispose(); var active = Temporary();
        try
        {
            var window = new MainWindow([], recent); window.Show();
            try
            {
                Assert.Equal(1, window.StartupHardDiskBackupCleanup.Cleaned); Assert.Equal(1, window.StartupHardDiskBackupCleanup.Deferred);
                Assert.False(File.Exists(abandoned.Path)); Assert.True(File.Exists(active.Path));
                Assert.Equal(new byte[4096], File.ReadAllBytes(image)); Assert.Equal("keep backup", File.ReadAllText(backup));
                Assert.Equal(machine, File.ReadAllBytes(Path.Combine(data, "last-machine.json"))); Assert.Equal(preferences, File.ReadAllBytes(Path.Combine(data, "recent-session.json")));
            }
            finally { window.Close(); }
        }
        finally { active.Operation.Dispose(); CopperScreenHardDiskBackupCleanup.Cleanup(data); }
        return Task.CompletedTask;
    });

    [Fact]
    public Task UnwritableRememberedMachineDoesNotCommitARestore() => UiAsync(async () =>
    {
        var original = Path.Combine(_root, "original.hdf"); File.WriteAllBytes(original, new byte[4096]);
        var data = Path.Combine(_root, "private"); var recent = new CopperScreenRecentSession(data);
        var draft = new CopperScreenSettingsDraft(); draft.HardDrives.Add(new(0, original, false, 0, AmigaHardfileMountMode.RigidDiskBlock));
        recent.Save(draft, 37, true, AppContext.BaseDirectory);
        var saved = File.ReadAllBytes(Path.Combine(data, "last-machine.json"));
        var history = File.ReadAllBytes(Path.Combine(data, "recent-session.json"));
        var backup = Path.Combine(_root, "existing.copperhdf");
        CopperScreenHardDiskBackup.Export(draft, backup, AppContext.BaseDirectory, data);
        var backupBytes = File.ReadAllBytes(backup);
        var window = new MainWindow([], recent); window.Show();
        try
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(MainWindow).GetMethod("SetWelcomeDraft", flags)!.Invoke(window, [draft]);
            var before = CopperScreenProfileStore.Serialize((CopperScreenSettingsDraft)typeof(MainWindow).GetField("_committedSettings", flags)!.GetValue(window)!, AppContext.BaseDirectory);
            using (var lease = new FileStream(Path.Combine(data, "last-machine.json"), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var error = await Record.ExceptionAsync(async () => await (Task)typeof(MainWindow).GetMethod("RestoreHardDiskBackupFromPathAsync", flags)!.Invoke(window, [backup])!);
                Assert.True(error is IOException or UnauthorizedAccessException, error?.ToString() ?? "Restore ignored an unwritable saved-machine file.");
            }
            Assert.Equal(before, CopperScreenProfileStore.Serialize((CopperScreenSettingsDraft)typeof(MainWindow).GetField("_committedSettings", flags)!.GetValue(window)!, AppContext.BaseDirectory));
            Assert.Equal(saved, File.ReadAllBytes(Path.Combine(data, "last-machine.json")));
            Assert.Equal(history, File.ReadAllBytes(Path.Combine(data, "recent-session.json")));
            Assert.Equal(backupBytes, File.ReadAllBytes(backup)); Assert.Equal(new byte[4096], File.ReadAllBytes(original));
            Assert.Empty(Directory.GetFileSystemEntries(Path.Combine(data, "HardDiskRestores")));
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task BackupReportsInvalidPartitionFieldsBeforeOpeningThePicker() => UiAsync(async () =>
    {
        var draft = new CopperScreenSettingsDraft();
        draft.HardDrives.Add(CopperScreenHardfileSettings.Ffs(0, "selected.hdf", true, "DH0:2:32:4:58"));
        var window = new MainWindow([], new CopperScreenRecentSession(_root)); window.Show();
        try
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(MainWindow).GetMethod("SetWelcomeDraft", flags)!.Invoke(window, [draft]);
            var rows = (StackPanel)typeof(MainWindow).GetField("_hardfileRows", flags)!.GetValue(window)!;
            var cylinder = rows.GetLogicalDescendants().OfType<TextBox>().Single(c => Avalonia.Automation.AutomationProperties.GetName(c) == "First cylinder");
            cylinder.Text = "invalid cylinder";
            await (Task)typeof(MainWindow).GetMethod("ExportHardDiskBackupAsync", flags)!.Invoke(window, null)!;
            Assert.NotNull(typeof(MainWindow).GetField("_settingsStartupError", flags)!.GetValue(window));
            Assert.False((bool)typeof(MainWindow).GetField("_applyingSettings", flags)!.GetValue(window)!);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task HardDiskSettingsBackupAndRestoreRememberNewCopiesAndPreserveWriteProtection() => UiAsync(async () =>
    {
        var original = Path.Combine(_root, "working.hdf");
        var bytes = Enumerable.Range(0, 4096).Select(i => (byte)(i * 31)).ToArray(); File.WriteAllBytes(original, bytes);
        var draft = new CopperScreenSettingsDraft();
        draft.HardDrives.Add(new(0, original, false, 0, AmigaHardfileMountMode.RigidDiskBlock));
        var window = new MainWindow([], new CopperScreenRecentSession(Path.Combine(_root, "private"))); window.Show();
        try
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(MainWindow).GetMethod("SetWelcomeDraft", flags)!.Invoke(window, [draft]);
            typeof(MainWindow).GetMethod("ShowSettingsWindow", flags)!.Invoke(window, null);
            typeof(MainWindow).GetMethod("SelectSettingsPage", flags)!.Invoke(window, [3]);
            var settings = (Window)typeof(MainWindow).GetField("_settingsWindow", flags)!.GetValue(window)!;
            var labels = settings.GetLogicalDescendants().OfType<Button>().Select(b => b.Content as string).ToArray();
            Assert.Contains("Export hard-disk backup…", labels); Assert.Contains("Restore hard-disk backup…", labels);
            var backup = Path.Combine(_root, "export.copperhdf");
            var selected = (CopperScreenSettingsDraft)typeof(MainWindow).GetMethod("ReadSettingsDraft", flags)!.Invoke(window, null)!;
            await (Task)typeof(MainWindow).GetMethod("ExportHardDiskBackupToPathAsync", flags)!.Invoke(window, [selected, backup])!;
            await (Task)typeof(MainWindow).GetMethod("RestoreHardDiskBackupFromPathAsync", flags)!.Invoke(window, [backup])!;
            var restored = (CopperScreenSettingsDraft)typeof(MainWindow).GetField("_settingsDraft", flags)!.GetValue(window)!;
            var drive = Assert.Single(restored.HardDrives);
            Assert.NotEqual(original, drive.Path); Assert.False(drive.ReadOnly); Assert.Equal(bytes, File.ReadAllBytes(drive.Path));
            var remembered = new CopperScreenRecentSession(Path.Combine(_root, "private")).LoadMachine(AppContext.BaseDirectory)!;
            Assert.Equal(drive, Assert.Single(remembered.HardDrives));
            Assert.Equal(bytes, File.ReadAllBytes(original));
        }
        finally { window.Close(); }
    });
}
