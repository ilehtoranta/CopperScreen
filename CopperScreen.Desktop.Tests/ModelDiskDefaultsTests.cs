using Avalonia.Controls;
using CopperMod.Amiga.Lightweight;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed partial class LaunchWorkflowTests
{
    [Fact]
    public Task ModelSwitchesRestoreDisksAndRomTogetherAcrossRestart() => Ui(() =>
    {
        var paths = Enumerable.Range(0, 3).Select(i => Path.Combine(_root, $"disk-{i}.adf")).ToArray();
        var roms = Enumerable.Range(0, 3).Select(i => DefaultRomFixture(i, "rom-" + i)).ToArray();
        var window = new MainWindow([], new CopperScreenRecentSession(_root)); window.Show();
        try
        {
            for (var i = 0; i < 3; i++)
            {
                Named<ComboBox>(window, "Start configuration").SelectedIndex = i;
                Assert.All(MenuDraft(window).DriveDiskPaths, Assert.Null);
                var draft = MenuDraft(window);
                draft.KickstartRomPath = roms[i];
                draft.FloppyDriveCount = 2;
                draft.DriveDiskPaths[0] = paths[i];
                draft.DriveDiskPaths[1] = paths[i] + ".zip::disk.adf";
                draft.DriveWriteProtected[0] = false;
                draft.FloppyDriveTypes[1] = LightweightFloppyDriveType.HighDensity;
                SetMenuDraft(window, draft);
            }
            Named<ComboBox>(window, "Start configuration").SelectedIndex = 3;
            Assert.All(MenuDraft(window).DriveDiskPaths, Assert.Null);
            foreach (var i in new[] { 0, 1, 2 })
            {
                Named<ComboBox>(window, "Start configuration").SelectedIndex = i;
                Assert.Equal(paths[i], MenuDraft(window).DriveDiskPaths[0]);
                Assert.Equal(roms[i], MenuDraft(window).KickstartRomPath);
            }
        }
        finally { window.Close(); }
        var reopened = new MainWindow([], new CopperScreenRecentSession(_root)); reopened.Show();
        try
        {
            foreach (var i in new[] { 1, 2, 0 })
            {
                Named<ComboBox>(reopened, "Start configuration").SelectedIndex = i;
                var draft = MenuDraft(reopened);
                Assert.Equal(paths[i], draft.DriveDiskPaths[0]);
                Assert.Equal(paths[i] + ".zip::disk.adf", draft.DriveDiskPaths[1]);
                Assert.Equal(roms[i], draft.KickstartRomPath);
                Assert.Equal(2, draft.FloppyDriveCount);
                Assert.False(draft.DriveWriteProtected[0]);
                Assert.Equal(LightweightFloppyDriveType.HighDensity, draft.FloppyDriveTypes[1]);
            }
        }
        finally { reopened.Close(); }
    });

    [Fact]
    public Task DiskSelectionAndEjectionAreRememberedBeforeBoot() => UiAsync(async () =>
    {
        var disk = Path.Combine(_root, "selected.adf"); File.WriteAllBytes(disk, new byte[901120]);
        var window = new MainWindow([], new CopperScreenRecentSession(_root)); window.Show();
        try
        {
            await window.InsertDriveDiskAsync(0, disk);
            var restored = new CopperScreenRecentSession(_root).RestoreDisks(CopperScreenLaunchWorkflow.SelectStartChoice(new(), 0));
            Assert.Equal(disk, restored.DriveDiskPaths[0]);
            Click(Button(window, "Remove floppy disk"));
            restored = new CopperScreenRecentSession(_root).RestoreDisks(CopperScreenLaunchWorkflow.SelectStartChoice(new(), 0));
            Assert.Null(restored.DriveDiskPaths[0]);
            Named<ComboBox>(window, "Start configuration").SelectedIndex = 1;
            Named<ComboBox>(window, "Start configuration").SelectedIndex = 0;
            Assert.Null(MenuDraft(window).DriveDiskPaths[0]);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void ModelHardDisksPreserveGeometryAndWriteSettings()
    {
        var recent = new CopperScreenRecentSession(_root);
        var draft = CopperScreenLaunchWorkflow.SelectStartChoice(new(), 2);
        draft.HardDrives.Add(CopperScreenHardfileSettings.Ffs(0, Path.Combine(_root, "native.hdf"), false, "DH0:2:32:0:255"));
        recent.RememberDisks(draft, AppContext.BaseDirectory);
        var restored = new CopperScreenRecentSession(_root).RestoreDisks(CopperScreenLaunchWorkflow.SelectStartChoice(new(), 2));
        Assert.Equal(draft.HardDrives[0], Assert.Single(restored.HardDrives));
        Assert.Empty(recent.RestoreDisks(CopperScreenLaunchWorkflow.SelectStartChoice(new(), 1)).HardDrives);
    }
}
