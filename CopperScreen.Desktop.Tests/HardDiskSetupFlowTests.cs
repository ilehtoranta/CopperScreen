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
            SettingsNamed<TextBox>(dialog, "Capacity (MiB)").Text = "16";
            SettingsNamed<TextBox>(dialog, "New last cylinder").Text = "255";
            if (cpx)
            {
                SettingsNamed<ComboBox>(dialog, "Filesystem").SelectedItem = "CPX (experimental)";
                Click(dialog.GetLogicalDescendants().OfType<Button>().Single(b => b.Content is "Create image"));
                Assert.False(File.Exists(source));
                dialog.GetLogicalDescendants().OfType<CheckBox>().Single(c => c.Content is "I want to create an experimental CPX volume").IsChecked = true;
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
            Assert.Equal("256", SettingsNamed<TextBox>(dialog, "New first cylinder").Text);
            Assert.Equal("DH1", SettingsNamed<TextBox>(dialog, "New partition device").Text);
            Capture(dialog, "harddisk-add-partition");
            Click(dialog.GetLogicalDescendants().OfType<Button>().Single(b => b.Content is "Save partitioned copy"));
            await Until(() => !dialog.IsVisible);
            Assert.Equal(copy, Assert.Single(MenuDraft(window).HardDrives).Path);
            Assert.Equal(2, CopperScreenHardDiskSetup.Inspect(copy).Partitions.Count);
            Assert.True(before.AsSpan().SequenceEqual(File.ReadAllBytes(source)));
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
