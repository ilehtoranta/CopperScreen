using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed partial class DiskImageManagerTests
{
    private string EditableImage(string name, CopperScreenHardDiskSetup.FileSystem format = CopperScreenHardDiskSetup.FileSystem.Ffs)
    {
        Directory.CreateDirectory(_root); var path = Path.Combine(_root, name + ".hdf");
        using var disk = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        disk.SetLength(2 * 1024 * 1024); CopperScreenFileSystemFormatter.Format(disk, 0, disk.Length, format, name, default); return path;
    }
    private static int ItemIndex(ListBox list, string name) => list.Items.Cast<object>().ToList().FindIndex(i => i.ToString()!.StartsWith(name));
    private static async Task Eventually(Func<bool> ready)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (!ready()) await Task.Delay(20, timeout.Token);
    }

    [Fact]
    public Task EitherPaneCanOpenAnImageAndCopyTreesDirectlyAcrossFilesystems() => UiAsync(async () =>
    {
        var a = EditableImage("Source"); var b = EditableImage("Target", CopperScreenHardDiskSetup.FileSystem.Sfs2);
        var host = Destination(); Directory.CreateDirectory(Path.Combine(host, "Tree", "Empty")); File.WriteAllBytes(Path.Combine(host, "Tree", "Data"), [1, 3, 5]);
        var window = new DiskImageManagerWindow(); window.Show();
        try
        {
            await Idle(window); await window.NavigateHostAsync(host); await window.OpenImageAsync(a);
            var left = Named<ListBox>(window, "Host files"); left.SelectedIndex = 0; await window.CopyHostSelectedAsync();
            await window.OpenImageAsync(b); await window.OpenImageAsync(0, a);
            left = Named<ListBox>(window, "Left image files"); var right = Named<ListBox>(window, "Image files");
            Assert.Single(left.Items); Assert.Empty(right.Items);
            Assert.Contains("Source.hdf", Named<ComboBox>(window, "Left image volumes").SelectedItem!.ToString());
            Assert.Contains("Target.hdf", Named<ComboBox>(window, "Image volumes").SelectedItem!.ToString());
            var before = File.ReadAllBytes(a);
            left.Focus(); left.SelectedIndex = 0; var copy = Named<Button>(window, "Copy active selection");
            Assert.Equal("Copy →", copy.Content); Assert.True(copy.IsEnabled); copy.Focus(); Click(copy);
            await Eventually(() => !Busy(window) && right.ItemCount == 1);
            Assert.Contains("Image saved", Named<TextBlock>(window, "Disk manager status").Text); Assert.Equal(before, File.ReadAllBytes(a));
            if (Environment.GetEnvironmentVariable("COPPERSCREEN_MANAGER_CAPTURE") != null) Capture(window, "two-images", 1);
            right.Focus(); right.SelectedIndex = 0; Press(window, Key.Enter); await Idle(window); Assert.Equal(2, right.ItemCount);
            Assert.Single(left.Items); // independent directory and selection state
            await window.OpenImageAsync(a);
            Assert.Single(right.Items); // opening the same image gives this pane its own root view
            Assert.Single(left.Items);
        }
        finally { window.Close(); }
        using var image = CopperScreenDiskImage.Open(b); var volume = image.Volumes[0];
        var tree = Assert.Single(volume.ReadDirectory(volume.Root));
        var data = volume.ReadDirectory(tree).Single(e => e.Name == "Data"); using var output = new MemoryStream(); volume.CopyFileTo(data, output);
        Assert.Equal(new byte[] { 1, 3, 5 }, output.ToArray());
    });

    [Fact]
    public Task ImageCommandsUseNameDialogsAndRequireDeleteConfirmation() => UiAsync(async () =>
    {
        var path = EditableImage("Commands"); var window = new DiskImageManagerWindow(); window.Show();
        try
        {
            await Idle(window); await window.OpenImageAsync(path); var files = Named<ListBox>(window, "Image files"); files.Focus();
            Assert.True(Command(window, "New dir").IsEnabled); Press(window, Key.F7);
            var create = Assert.Single(window.OwnedWindows); Assert.Equal("New directory", create.Title);
            var name = create.GetLogicalDescendants().OfType<TextBox>().Single(t => AutomationProperties.GetName(t) == "Entry name");
            name.Text = "Created"; Click(Command(create, "Create"));
            await Eventually(() => !Busy(window) && files.ItemCount == 1);
            files.Focus(); files.SelectedIndex = 0; Press(window, Key.F2);
            var rename = Assert.Single(window.OwnedWindows); Assert.Equal("Rename", rename.Title);
            rename.GetLogicalDescendants().OfType<TextBox>().Single().Text = "Renamed"; Click(Command(rename, "Rename"));
            await Eventually(() => !Busy(window) && ItemIndex(files, "▸ Renamed") == 0);
            var before = File.ReadAllBytes(path);
            files.Focus(); files.SelectedIndex = 0; Press(window, Key.Delete);
            var confirmation = Assert.Single(window.OwnedWindows); Assert.Equal("Delete entries", confirmation.Title);
            Assert.Equal(before, File.ReadAllBytes(path)); Click(Command(confirmation, "Cancel"));
            await Eventually(() => !window.OwnedWindows.Any()); Assert.Equal(before, File.ReadAllBytes(path)); Assert.Single(files.Items);
            files.Focus(); Press(window, Key.Delete); confirmation = Assert.Single(window.OwnedWindows); Click(Command(confirmation, "Delete"));
            await Eventually(() => !Busy(window) && files.ItemCount == 0);
            Assert.Contains("Deleted 1", Named<TextBlock>(window, "Disk manager status").Text);
            Named<ListBox>(window, "Host files").Focus(); Assert.False(Command(window, "Delete").IsEnabled);
        }
        finally { window.Close(); }
        using var image = CopperScreenDiskImage.Open(path); Assert.Empty(image.Volumes[0].ReadDirectory(image.Volumes[0].Root));
    });

    [Fact]
    public Task BothPanesCanBrowseHostFoldersAndCopyWithoutMerging() => UiAsync(async () =>
    {
        var host = Destination(); var destination = Path.Combine(_root, "OtherHost"); Directory.CreateDirectory(destination);
        Directory.CreateDirectory(Path.Combine(host, "Tree", "Empty")); File.WriteAllBytes(Path.Combine(host, "Tree", "Data"), [7, 8, 9]);
        var window = new DiskImageManagerWindow(); window.Show();
        try
        {
            await Idle(window); await window.NavigateHostAsync(host);
            Named<ComboBox>(window, "Right location type").SelectedIndex = 0; await Idle(window); await window.NavigateHostAsync(1, destination);
            var source = Named<ListBox>(window, "Host files"); var target = Named<ListBox>(window, "Right host files");
            source.Focus(); source.SelectedIndex = 0; Click(Named<Button>(window, "Copy active selection"));
            await Eventually(() => !Busy(window) && target.ItemCount == 1);
            Assert.Equal(new byte[] { 7, 8, 9 }, File.ReadAllBytes(Path.Combine(destination, "Tree", "Data")));
            Assert.True(Directory.Exists(Path.Combine(destination, "Tree", "Empty")));
            source.Focus(); source.SelectedIndex = 0; Click(Named<Button>(window, "Copy active selection")); await Idle(window);
            Assert.Contains("already exists", Named<TextBlock>(window, "Disk manager status").Text);
            Assert.DoesNotContain(Directory.GetFileSystemEntries(destination), p => Path.GetFileName(p).StartsWith(".copperscreen"));
        }
        finally { window.Close(); }
    });
}
