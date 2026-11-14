using System.Reflection;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed partial class LaunchWorkflowTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task DiskSetupCreatesAFormattedImageAndAddsAPartitionInACopy(bool cpx) => UiAsync(async () =>
    {
        var window = new MainWindow([], new CopperScreenRecentSession(_root)); window.Show();
        try
        {
            Click(Button(window, "Configure Amiga…")); SettingsCall(window, "SelectSettingsPage", 3);
            var owner = SettingsField<Window>(window, "_settingsWindow");
            SettingsNamed<TabControl>(owner, "Storage tabs").SelectedIndex = 1;
            owner.UpdateLayout();
            var actions = owner.GetLogicalDescendants().OfType<Button>().Distinct().ToArray();
            Assert.DoesNotContain(actions, b => b.Content is "Create formatted disk…");
            var createButton = actions.Single(b => b.Content is "Create disk image…");
            Assert.True(createButton.IsEffectivelyVisible); Assert.True(createButton.IsEnabled);
            Click(createButton);
            await Until(() => owner.OwnedWindows.Any(w => w.Title == "Create disk image"));
            var dialog = owner.OwnedWindows.Single(w => w.Title == "Create disk image");
            var source = Path.Combine(_root, "formatted.hdf");
            SettingsNamed<TextBox>(dialog, "New image file").Text = source;
            Assert.Equal("FFS", SettingsNamed<ComboBox>(dialog, "Filesystem").SelectedItem);
            SettingsNamed<TextBox>(dialog, "Disk capacity").Text = "16";
            var advanced = dialog.GetLogicalDescendants().OfType<Expander>().Single(e => e.Header is "Advanced geometry");
            Assert.False(advanced.IsExpanded);
            dialog.UpdateLayout();
            Assert.False(SettingsNamed<TextBox>(dialog, "First cylinder").IsEnabled);
            dialog.GetLogicalDescendants().OfType<CheckBox>().Single(c => c.Content is "Use available space").IsChecked = false;
            SettingsNamed<TextBox>(dialog, "Partition size").Text = "0.0078125";
            SettingsNamed<ComboBox>(dialog, "Partition size unit").SelectedItem = "GiB";
            SettingsNamed<ComboBox>(dialog, "Filesystem block size").SelectedItem = "4 KiB";
            if (cpx)
            {
                SettingsNamed<ComboBox>(dialog, "Filesystem").SelectedItem = "CopperNova (CNFS)";
                Click(dialog.GetLogicalDescendants().OfType<Button>().Single(b => b.Content is "Create image"));
                Assert.False(File.Exists(source));
                dialog.GetLogicalDescendants().OfType<CheckBox>().Single(c => c.Content is "I want to create an experimental CopperNova (CNFS) volume").IsChecked = true;
            }
            Capture(dialog, cpx ? "harddisk-format-cpx" : "harddisk-format-ffs");
            Click(dialog.GetLogicalDescendants().OfType<Button>().Single(b => b.Content is "Create image"));
            await Until(() => !dialog.IsVisible);
            var drive = Assert.Single(MenuDraft(window).HardDrives);
            Assert.Equal(source, drive.Path); Assert.True(drive.ReadOnly);
            Assert.Equal(AmigaHardfileMountMode.RigidDiskBlock, drive.Mode); Assert.Equal(0, drive.CreateSizeBytes);
            var before = File.ReadAllBytes(source);
            var addButton = owner.GetLogicalDescendants().OfType<Button>().Distinct().Single(b => b.Content is "Add partition and format…");
            Assert.True(addButton.IsEffectivelyVisible); Assert.True(addButton.IsEnabled);
            Click(addButton);
            await Until(() => owner.OwnedWindows.Any(w => w.Title == "Add partition and format"));
            dialog = owner.OwnedWindows.Single(w => w.Title == "Add partition and format");
            var copy = Path.Combine(_root, "partitioned.hdf");
            SettingsNamed<TextBox>(dialog, "New image file").Text = copy;
            Assert.DoesNotContain("Unformatted", SettingsNamed<ComboBox>(dialog, "Filesystem").Items);
            Assert.False(dialog.GetLogicalDescendants().OfType<Expander>().Single(e => e.Header is "Advanced geometry").IsExpanded);
            Assert.Equal("DH1", SettingsNamed<TextBox>(dialog, "New partition device").Text);
            Assert.Equal(4096u, Assert.Single(CopperScreenHardDiskSetup.Inspect(source).Partitions).BlockSize);
            Capture(dialog, "harddisk-add-partition");
            Click(dialog.GetLogicalDescendants().OfType<Button>().Single(b => b.Content is "Save partitioned copy"));
            await Until(() => !dialog.IsVisible);
            Assert.Equal(copy, Assert.Single(MenuDraft(window).HardDrives).Path);
            Assert.Equal(2, CopperScreenHardDiskSetup.Inspect(copy).Partitions.Count);
            Assert.Equal(257u, CopperScreenHardDiskSetup.Inspect(copy).Partitions.Single(p => p.DeviceName == "DH1").FirstCylinder);
            Assert.True(before.AsSpan().SequenceEqual(File.ReadAllBytes(source)));
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task AdvancedPartitionRangeRejectsOverlapAndPreservesTheSource() => UiAsync(async () =>
    {
        var source = Path.Combine(_root, "advanced-source.hdf");
        CopperScreenHardDiskSetup.Create(source, 16 * 1048576, null, "DH0", 1, 128, CopperScreenHardDiskSetup.FileSystem.Ffs, "Work", false);
        var before = File.ReadAllBytes(source);
        var window = new MainWindow([], new CopperScreenRecentSession(_root)); window.Show();
        try
        {
            var draft = MenuDraft(window); draft.HardDrives.Add(new(0, source, true, 0, AmigaHardfileMountMode.RigidDiskBlock));
            SetMenuDraft(window, draft);
            Click(Button(window, "Configure Amiga…"));
            var owner = SettingsField<Window>(window, "_settingsWindow");
            var open = (Task)typeof(MainWindow).GetMethod("OpenHardDiskSetupAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [0])!;
            var dialog = owner.OwnedWindows.Single(w => w.Title == "Add partition and format");
            var destination = Path.Combine(_root, "advanced-copy.hdf");
            SettingsNamed<TextBox>(dialog, "New image file").Text = destination;
            var advanced = dialog.GetLogicalDescendants().OfType<Expander>().Single(e => e.Header is "Advanced geometry");
            advanced.IsExpanded = true;
            dialog.GetLogicalDescendants().OfType<CheckBox>().Single(c => c.Content is "Use explicit cylinder range").IsChecked = true;
            var first = SettingsNamed<TextBox>(dialog, "First cylinder");
            var last = SettingsNamed<TextBox>(dialog, "Last cylinder");
            Assert.True(first.IsEnabled); Assert.True(last.IsEnabled);
            Assert.False(SettingsNamed<TextBox>(dialog, "Partition size").IsEnabled);
            first.Text = "100"; last.Text = "200";
            var save = dialog.GetLogicalDescendants().OfType<Button>().Single(b => b.Content is "Save partitioned copy");
            Click(save);
            Assert.True(dialog.IsVisible); Assert.False(File.Exists(destination));
            Assert.Equal(before, File.ReadAllBytes(source));
            first.Text = "129"; last.Text = "256";
            SettingsNamed<ComboBox>(dialog, "Filesystem block size").SelectedItem = "2 KiB";
            Assert.Equal("4", SettingsNamed<TextBox>(dialog, "Partition size").Text);
            Capture(dialog, "partition-advanced-range");
            Click(save); await open;
            var added = CopperScreenHardDiskSetup.Inspect(destination).Partitions.Single(p => p.DeviceName == "DH1");
            Assert.Equal(129u, added.FirstCylinder); Assert.Equal(256u, added.LastCylinder); Assert.Equal(2048u, added.BlockSize);
            Assert.Equal(before, File.ReadAllBytes(source));
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task DiskSetupRequiresAStoppedMachine() => UiAsync(async () =>
    {
        using var session = new EjectSession("disk.adf", false, true);
        using var runtime = CopperScreenRuntime.CreateForTests(session);
        var window = new MainWindow([], new CopperScreenRecentSession(_root)); window.Show();
        try
        {
            SetPrivate(window, "_runtime", runtime);
            await (Task)typeof(MainWindow).GetMethod("OpenHardDiskSetupAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [null])!;
            Assert.DoesNotContain(window.OwnedWindows, w => w.Title == "Create disk image");
            Assert.Empty(MenuDraft(window).HardDrives);
        }
        finally { SetPrivate(window, "_runtime", null); window.Close(); }
    });
}
