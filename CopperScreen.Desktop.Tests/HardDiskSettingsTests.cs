using System.Reflection;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed partial class LaunchWorkflowTests
{
    [Fact]
    public Task RdbLayoutUsesTheImageWithoutParsingHiddenPartitionFields() => UiAsync(async () =>
    {
        var window = new MainWindow([], new CopperScreenRecentSession(_root)); window.Show();
        try
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var draft = new CopperScreenSettingsDraft { KickstartSource = CopperScreenKickstartSource.CopperStart };
            draft.HardDrives.Add(CopperScreenHardfileSettings.Ffs(0, "disk.hdf", true, "DH0:2:32:4:58"));
            typeof(MainWindow).GetField("_settingsDraft", flags)!.SetValue(window, draft);
            typeof(MainWindow).GetMethod("RefreshHardfileSettings", flags)!.Invoke(window, null);
            var rows = (StackPanel)typeof(MainWindow).GetField("_hardfileRows", flags)!.GetValue(window)!;
            var controls = rows.GetLogicalDescendants().ToArray();
            var mode = controls.OfType<ComboBox>().Single(c => AutomationProperties.GetName(c) == "Image layout");
            var cylinder = controls.OfType<TextBox>().Single(c => AutomationProperties.GetName(c) == "First cylinder");
            cylinder.Text = "invalid hidden geometry"; mode.SelectedItem = "RDB";
            var size = controls.OfType<TextBox>().Single(c => AutomationProperties.GetName(c) == "Create size (bytes)");
            await Until(() => !size.IsEnabled);
            Assert.Equal("0", size.Text);
            Assert.Contains(cylinder.GetLogicalAncestors().OfType<Control>(), parent => !parent.IsVisible);
            typeof(MainWindow).GetMethod("ReadHardfileSettings", flags)!.Invoke(window, [draft]);
            var drive = Assert.Single(draft.HardDrives);
            Assert.Equal(AmigaHardfileMountMode.RigidDiskBlock, drive.Mode); Assert.Null(drive.Partition);
            Assert.True(drive.ReadOnly); Assert.Equal(0, drive.CreateSizeBytes);
            cylinder.Text = "4";
            Click(controls.OfType<Button>().Single(c => c.Content as string == "Use FFS partition settings"));
            typeof(MainWindow).GetMethod("ReadHardfileSettings", flags)!.Invoke(window, [draft]);
            Assert.Equal(AmigaHardfileMountMode.Partition, draft.HardDrives[0].Mode);
            Assert.Equal(58u, draft.HardDrives[0].Partition!.HighCylinder);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task HardDiskFormPreservesExplicitGeometryWhenChangingProtectionAndAppliesFfsDefaults() => UiAsync(async () =>
    {
        var window = new MainWindow([], new CopperScreenRecentSession(_root));
        window.Show();
        try
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var draft = new CopperScreenSettingsDraft { KickstartSource = CopperScreenKickstartSource.CopperStart };
            var drive = CopperScreenHardfileSettings.Ffs(0, "selected.hdf", false, "Work:2:32:4:58");
            draft.HardDrives.Add(drive with { Partition = drive.Partition! with { BootPriority = -5, NumBuffers = 17 } });
            typeof(MainWindow).GetField("_settingsDraft", flags)!.SetValue(window, draft);
            typeof(MainWindow).GetMethod("RefreshHardfileSettings", flags)!.Invoke(window, null);
            var rows = (StackPanel)typeof(MainWindow).GetField("_hardfileRows", flags)!.GetValue(window)!;
            var controls = rows.GetLogicalDescendants().ToArray();
            var path = controls.OfType<TextBox>().Single(c => AutomationProperties.GetName(c) == "HDF path");
            path.Text = "another.hdf";
            var protection = controls.OfType<CheckBox>().Single();
            await Until(() => protection.IsChecked == true);
            Assert.True(protection.IsChecked);
            protection.IsChecked = false;
            typeof(MainWindow).GetMethod("ReadHardfileSettings", flags)!.Invoke(window, [draft]);
            Assert.Equal(drive.Partition! with { BootPriority = -5, NumBuffers = 17 }, Assert.Single(draft.HardDrives).Partition);
            Assert.Equal("another.hdf", draft.HardDrives[0].Path);
            Assert.False(draft.HardDrives[0].ReadOnly);
            Click(controls.OfType<Button>().Single(c => c.Content as string == "Use FFS partition settings"));
            typeof(MainWindow).GetMethod("ReadHardfileSettings", flags)!.Invoke(window, [draft]);
            Assert.Equal(0x444F5301u, draft.HardDrives[0].Partition!.DosType);
            Assert.Equal(58u, draft.HardDrives[0].Partition!.HighCylinder);
            Assert.Equal(0xFFFFFFFEu, draft.HardDrives[0].Partition!.Mask);
        }
        finally { window.Close(); }
    });
}
