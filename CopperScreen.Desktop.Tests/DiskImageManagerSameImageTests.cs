using Avalonia.Controls;
using Avalonia.Input;
using CopperDisk.Tests;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed partial class DiskImageManagerTests
{
    private async Task<string> SharedImageSetup(DiskImageManagerWindow window)
    {
        var path = EditableImage("Shared"); var host = Destination();
        Directory.CreateDirectory(Path.Combine(host, "Tree", "Empty")); File.WriteAllBytes(Path.Combine(host, "Tree", "Data"), [1, 3, 5]);
        await Idle(window); await window.NavigateHostAsync(host); await window.OpenImageAsync(path);
        Named<ListBox>(window, "Host files").SelectedIndex = 0; await window.CopyHostSelectedAsync();
        await window.OpenImageAsync(0, path); return path;
    }
    private static async Task EnterDirectory(DiskImageManagerWindow window, ListBox list, string name)
    {
        list.Focus(); list.SelectedIndex = ItemIndex(list, "▸ " + name); Assert.True(list.SelectedIndex >= 0);
        Press(window, Key.Enter); await Idle(window);
    }

    [Fact]
    public Task SameImageCopiesKeepIndependentPathsSelectionsAndReaderOwnership() => UiAsync(async () =>
    {
        var window = new DiskImageManagerWindow(); window.Show();
        try
        {
            var path = await SharedImageSetup(window);
            var left = Named<ListBox>(window, "Left image files"); var right = Named<ListBox>(window, "Image files");
            right.Focus(); await window.CreateDirectoryAsync("Destination");
            Assert.Equal(2, left.ItemCount); Assert.Equal(2, right.ItemCount);
            await EnterDirectory(window, right, "Destination"); await EnterDirectory(window, left, "Tree"); left.SelectAll();
            Assert.Equal("/Tree", Named<TextBlock>(window, "Left image directory path").Text);
            Assert.Equal("/Destination", Named<TextBlock>(window, "Right image directory path").Text);
            var copy = Named<Button>(window, "Copy active selection"); Assert.True(copy.IsEnabled); Click(copy);
            await Eventually(() => !Busy(window) && right.ItemCount == 2);
            Assert.Equal(2, left.SelectedItems!.Count);
            Assert.Equal("/Tree", Named<TextBlock>(window, "Left image directory path").Text);
            Assert.Equal("/Destination", Named<TextBlock>(window, "Right image directory path").Text);
            Assert.Contains("Image saved", Named<TextBlock>(window, "Disk manager status").Text);
            if (Environment.GetEnvironmentVariable("COPPERSCREEN_MANAGER_CAPTURE") != null) Capture(window, "same-image", 1);

            Named<ComboBox>(window, "Left location type").SelectedIndex = 0; await Idle(window);
            var extracted = Path.Combine(_root, "Extracted"); Directory.CreateDirectory(extracted); await window.NavigateHostAsync(extracted);
            right.Focus(); right.SelectedIndex = ItemIndex(right, "Data"); await window.CopySelectedAsync();
            Assert.Equal(new byte[] { 1, 3, 5 }, File.ReadAllBytes(Path.Combine(extracted, "Data")));
            Assert.Throws<IOException>(() => new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None));
            Named<ComboBox>(window, "Right location type").SelectedIndex = 0; await Idle(window);
            using var writable = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task RenamingAndDeletingAnOpenDirectoryRebindsBothViews() => UiAsync(async () =>
    {
        var window = new DiskImageManagerWindow(); window.Show();
        try
        {
            await SharedImageSetup(window);
            var left = Named<ListBox>(window, "Left image files"); var right = Named<ListBox>(window, "Image files");
            await EnterDirectory(window, left, "Tree");
            right.Focus(); right.SelectedIndex = 0; await window.RenameSelectedAsync("Renamed");
            Assert.Equal("/Renamed", Named<TextBlock>(window, "Left image directory path").Text); Assert.Equal(2, left.ItemCount);
            Assert.StartsWith("▸ Renamed", Assert.Single(right.Items)!.ToString());
            right.Focus(); right.SelectedIndex = 0; Press(window, Key.Delete);
            var confirmation = Assert.Single(window.OwnedWindows); Click(Command(confirmation, "Delete"));
            await Eventually(() => !Busy(window) && left.ItemCount == 0 && right.ItemCount == 0);
            Assert.Equal("/", Named<TextBlock>(window, "Left image directory path").Text);
            Assert.Equal("/", Named<TextBlock>(window, "Right image directory path").Text);
            Assert.Contains("Deleted 1", Named<TextBlock>(window, "Disk manager status").Text);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task SameHdfPanesCanUseDifferentPartitionsAndCopyBetweenThem() => UiAsync(async () =>
    {
        Directory.CreateDirectory(_root); var path = Path.Combine(_root, "Partitions.hdf");
        File.WriteAllBytes(path, new AmigaFileSystemFixture().Rdb(true));
        using (var disk = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            CopperScreenFileSystemFormatter.Format(disk, 44 * 512, 1760 * 512, CopperScreenHardDiskSetup.FileSystem.Ffs, "First", default);
            CopperScreenFileSystemFormatter.Format(disk, 1804 * 512, 1760 * 512, CopperScreenHardDiskSetup.FileSystem.Sfs, "Second", default);
        }
        var host = Destination(); File.WriteAllBytes(Path.Combine(host, "Data"), [1, 3, 5]);
        var window = new DiskImageManagerWindow(); window.Show();
        try
        {
            await Idle(window); await window.NavigateHostAsync(host); await window.OpenImageAsync(path);
            Named<ListBox>(window, "Host files").SelectedIndex = 0; await window.CopyHostSelectedAsync();
            await window.OpenImageAsync(0, path);
            var leftVolumes = Named<ComboBox>(window, "Left image volumes"); var rightVolumes = Named<ComboBox>(window, "Image volumes");
            rightVolumes.SelectedIndex = 1; await Idle(window);
            var left = Named<ListBox>(window, "Left image files"); var right = Named<ListBox>(window, "Image files");
            left.Focus(); left.SelectedIndex = 0; var copy = Named<Button>(window, "Copy active selection"); Assert.True(copy.IsEnabled);
            Click(copy); await Eventually(() => !Busy(window) && right.ItemCount == 1);
            Assert.Equal(0, leftVolumes.SelectedIndex); Assert.Equal(1, rightVolumes.SelectedIndex); Assert.Single(left.SelectedItems!);
            rightVolumes.SelectedIndex = 0; await Idle(window);
            right.Focus(); right.SelectedIndex = 0; Assert.False(copy.IsEnabled); // same partition and directory
            leftVolumes.SelectedIndex = 1; await Idle(window); Assert.Single(left.Items); Assert.Single(right.Items);
            right.Focus(); await window.CreateDirectoryAsync("New");
            Assert.Equal(1, leftVolumes.SelectedIndex); Assert.Equal(0, rightVolumes.SelectedIndex);
            Assert.Single(left.Items); Assert.Equal(2, right.ItemCount);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task CancelledSameImageCopyLeavesBytesUnchangedAndBothPanesCanRemount() => UiAsync(async () =>
    {
        var window = new DiskImageManagerWindow(); window.Show();
        try
        {
            var path = await SharedImageSetup(window);
            var left = Named<ListBox>(window, "Left image files"); var right = Named<ListBox>(window, "Image files");
            right.Focus(); await window.CreateDirectoryAsync("Destination"); await EnterDirectory(window, right, "Destination");
            left.Focus(); left.SelectedIndex = ItemIndex(left, "▸ Tree");
            var before = File.ReadAllBytes(path); var copy = window.CopyHostSelectedAsync();
            Click(Named<Button>(window, "Cancel operation")); await copy;
            Assert.Equal(before, File.ReadAllBytes(path));
            Assert.Empty(left.Items); Assert.Empty(right.Items);
            var lv = Named<ComboBox>(window, "Left image volumes"); var rv = Named<ComboBox>(window, "Image volumes");
            Assert.Equal(-1, lv.SelectedIndex); Assert.Equal(-1, rv.SelectedIndex);
            lv.SelectedIndex = 0; await Idle(window); rv.SelectedIndex = 0; await Idle(window);
            Assert.Equal(2, left.ItemCount); Assert.Equal(2, right.ItemCount);
            await EnterDirectory(window, right, "Destination"); Assert.Empty(right.Items);
        }
        finally { window.Close(); }
    });
}
