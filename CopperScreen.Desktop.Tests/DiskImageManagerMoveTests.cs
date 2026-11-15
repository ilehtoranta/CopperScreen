using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.VisualTree;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed partial class DiskImageManagerTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task MoveRequiresConfirmationAndRefreshesBothPanes(bool shared) => UiAsync(async () =>
    {
        var window = new DiskImageManagerWindow(); window.Show();
        try
        {
            var sourcePath = await SharedImageSetup(window);
            if (!shared) await window.OpenImageAsync(EditableImage("Other", CopperScreenHardDiskSetup.FileSystem.Sfs));
            var left = Named<ListBox>(window, "Left image files"); var right = Named<ListBox>(window, "Image files");
            if (shared) { right.Focus(); await window.CreateDirectoryAsync("Destination"); await EnterDirectory(window, right, "Destination"); }
            left.Focus(); left.SelectedIndex = ItemIndex(left, "▸ Tree");
            var move = Named<Button>(window, "Move active selection"); Assert.True(move.IsEnabled); move.Focus();
            var before = File.ReadAllBytes(sourcePath); Click(move);
            var confirmation = Assert.Single(window.OwnedWindows); Assert.Equal("Move entries", confirmation.Title);
            Assert.Equal(before, File.ReadAllBytes(sourcePath)); Click(Command(confirmation, "Cancel"));
            await Eventually(() => !window.OwnedWindows.Any()); Assert.Equal(before, File.ReadAllBytes(sourcePath));
            left.Focus(); Press(window, Key.F5, RawInputModifiers.Shift);
            confirmation = Assert.Single(window.OwnedWindows); Click(Command(confirmation, "Move"));
            await Eventually(() => !Busy(window) && ItemIndex(left, "▸ Tree") < 0 && ItemIndex(right, "▸ Tree") >= 0);
            Assert.Contains("Moved 1 entries", Named<TextBlock>(window, "Disk manager status").Text);
            await EnterDirectory(window, right, "Tree"); Assert.Equal(2, right.ItemCount);
            if (Environment.GetEnvironmentVariable("COPPERSCREEN_MANAGER_CAPTURE") != null)
            {
                Capture(window, "move", 1); Capture(window, "move", 2);
            }
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task ProtectDialogSupportsMixedFlagsCancelAndInvertedPermissions() => UiAsync(async () =>
    {
        var window = new DiskImageManagerWindow(); window.Show();
        try
        {
            var path = await SharedImageSetup(window); var left = Named<ListBox>(window, "Left image files"); var right = Named<ListBox>(window, "Image files");
            await EnterDirectory(window, right, "Tree"); right.Focus(); right.SelectedIndex = ItemIndex(right, "Data");
            Click(Command(window, "Protect")); var dialog = Assert.Single(window.OwnedWindows);
            Assert.Equal("Protect entries", dialog.Title); Assert.True(Named<CheckBox>(dialog, "Deletable").IsChecked);
            Named<CheckBox>(dialog, "Deletable").IsChecked = false; Named<CheckBox>(dialog, "Script").IsChecked = true;
            var before = File.ReadAllBytes(path); Click(Command(dialog, "Cancel")); await Eventually(() => !window.OwnedWindows.Any());
            Assert.Equal(before, File.ReadAllBytes(path));
            Click(Command(window, "Protect")); dialog = Assert.Single(window.OwnedWindows);
            Named<CheckBox>(dialog, "Deletable").IsChecked = false; Named<CheckBox>(dialog, "Script").IsChecked = true;
            if (Environment.GetEnvironmentVariable("COPPERSCREEN_MANAGER_CAPTURE") != null) Capture(dialog, "protect", 2);
            Click(Command(dialog, "Apply")); await Eventually(() => !Busy(window) && Named<TextBlock>(window, "Disk manager status").Text!.Contains("Protection updated"));
            right.Focus(); right.SelectAll(); Click(Command(window, "Protect")); dialog = Assert.Single(window.OwnedWindows);
            Assert.Null(Named<CheckBox>(dialog, "Deletable").IsChecked); Assert.Null(Named<CheckBox>(dialog, "Script").IsChecked);
            Named<CheckBox>(dialog, "Deletable").IsChecked = true; Click(Command(dialog, "Apply"));
            await Eventually(() => !Busy(window) && !window.OwnedWindows.Any());
            await EnterDirectory(window, left, "Tree"); left.SelectedIndex = ItemIndex(left, "Data"); Click(Command(window, "Protect")); dialog = Assert.Single(window.OwnedWindows);
            Assert.True(Named<CheckBox>(dialog, "Script").IsChecked); Assert.True(Named<CheckBox>(dialog, "Deletable").IsChecked);
            Click(Command(dialog, "Cancel"));
        }
        finally { foreach (var dialog in window.OwnedWindows.ToArray()) dialog.Close(); window.Close(); }
    });

    // Use the exact in-process payload produced by the pointer gesture, then feed
    // real headless platform drag events through hit testing and the routed handlers.
    private static DataTransfer PaneTransfer(DiskImageManagerWindow window, bool left)
    {
        var type = typeof(DiskImageManagerWindow); var pane = type.GetField(left ? "_left" : "_right", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
        var paneType = pane.GetType(); var list = (ListBox)paneType.GetField("List", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(pane)!;
        var payloadType = type.GetNestedType("PaneDrag", BindingFlags.NonPublic)!;
        var payload = Activator.CreateInstance(payloadType, window, pane, list.SelectedItems!.Cast<object>().ToArray(),
            paneType.GetProperty("DirectoryPath", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(pane),
            paneType.GetField("Volume", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(pane));
        var format = type.GetField("PaneDragFormat", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null);
        var item = new DataTransferItem();
        typeof(DataTransferItem).GetMethods().Single(m => m.Name == "Set" && m.IsGenericMethod && m.GetParameters()[1].ParameterType.IsGenericParameter)
            .MakeGenericMethod(payloadType).Invoke(item, [format, payload]);
        var data = new DataTransfer(); data.Add(item); return data;
    }

    private static void Drop(Window window, ListBox target, DataTransfer data, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.UpdateLayout(); var point = target.TranslatePoint(new Point(80, 60), window)!.Value;
        window.DragDrop(point, RawDragEventType.DragEnter, data, DragDropEffects.Copy | DragDropEffects.Move, modifiers);
        window.DragDrop(point, RawDragEventType.DragOver, data, DragDropEffects.Copy | DragDropEffects.Move, modifiers);
        window.DragDrop(point, RawDragEventType.Drop, data, DragDropEffects.Copy | DragDropEffects.Move, modifiers);
    }

    [Fact]
    public Task PaneDropsCopyByDefaultAndShiftMoveRequiresConfirmation() => UiAsync(async () =>
    {
        var window = new DiskImageManagerWindow(); window.Show();
        try
        {
            await SharedImageSetup(window); var left = Named<ListBox>(window, "Left image files"); var right = Named<ListBox>(window, "Image files");
            right.Focus(); await window.CreateDirectoryAsync("Destination"); await EnterDirectory(window, right, "Destination");
            left.Focus(); left.SelectedIndex = ItemIndex(left, "▸ Tree"); Assert.True(DragDrop.GetAllowDrop(left)); Assert.True(DragDrop.GetAllowDrop(right));
            using (var data = PaneTransfer(window, true)) Drop(window, right, data);
            await Eventually(() => !Busy(window) && right.ItemCount == 1); Assert.Equal(2, left.ItemCount);
            await EnterDirectory(window, left, "Tree"); left.SelectAll();
            using (var data = PaneTransfer(window, true)) Drop(window, right, data, RawInputModifiers.Shift);
            var dialog = Assert.Single(window.OwnedWindows); Assert.Equal("Move entries", dialog.Title); Click(Command(dialog, "Cancel"));
            await Eventually(() => !window.OwnedWindows.Any()); Assert.Equal(2, left.ItemCount);
            using (var data = PaneTransfer(window, true)) Drop(window, right, data, RawInputModifiers.Shift);
            dialog = Assert.Single(window.OwnedWindows); Click(Command(dialog, "Move"));
            await Eventually(() => !Busy(window) && left.ItemCount == 0 && right.ItemCount == 3);
        }
        finally { foreach (var dialog in window.OwnedWindows.ToArray()) dialog.Close(); window.Close(); }
    });

    [Fact]
    public Task HostImageMovesWorkInBothDirectionsAndReadonlySourcesDisableMove() => UiAsync(async () =>
    {
        var host = Destination(); Directory.CreateDirectory(Path.Combine(host, "Tree", "Empty")); File.WriteAllBytes(Path.Combine(host, "Tree", "Data"), [8, 9]);
        var path = EditableImage("HostMove"); var window = new DiskImageManagerWindow(); window.Show();
        try
        {
            await Idle(window); await window.NavigateHostAsync(host); await window.OpenImageAsync(path);
            var left = Named<ListBox>(window, "Host files"); var right = Named<ListBox>(window, "Image files");
            left.Focus(); left.SelectedIndex = 0; Click(Named<Button>(window, "Move active selection"));
            Click(Command(Assert.Single(window.OwnedWindows), "Move"));
            await Eventually(() => !Busy(window) && left.ItemCount == 0 && right.ItemCount == 1);
            Assert.False(Directory.Exists(Path.Combine(host, "Tree"))); right.Focus(); right.SelectedIndex = 0;
            File.SetAttributes(path, FileAttributes.ReadOnly);
            try
            {
                right.UnselectAll(); right.SelectedIndex = 0;
                Assert.False(Named<Button>(window, "Move active selection").IsEnabled);
                Assert.True(Named<Button>(window, "Copy active selection").IsEnabled); Assert.False(Command(window, "Protect").IsEnabled);
            }
            finally { File.SetAttributes(path, FileAttributes.Normal); }
            right.UnselectAll(); right.SelectedIndex = 0; Click(Named<Button>(window, "Move active selection"));
            Click(Command(Assert.Single(window.OwnedWindows), "Move"));
            await Eventually(() => !Busy(window) && left.ItemCount == 1 && right.ItemCount == 0);
            Assert.Equal(new byte[] { 8, 9 }, File.ReadAllBytes(Path.Combine(host, "Tree", "Data"))); Assert.True(Directory.Exists(Path.Combine(host, "Tree", "Empty")));
        }
        finally { foreach (var dialog in window.OwnedWindows.ToArray()) dialog.Close(); window.Close(); }
    });

    [Fact]
    public Task HostToHostMoveRemovesOnlyAfterCopyAndPreservesDestinationSelection() => UiAsync(async () =>
    {
        var source = Destination(); File.WriteAllBytes(Path.Combine(source, "Data"), [1, 2]);
        var target = Path.Combine(_root, "Target"); Directory.CreateDirectory(target); File.WriteAllBytes(Path.Combine(target, "Keep"), [3]);
        var window = new DiskImageManagerWindow(); window.Show();
        try
        {
            await Idle(window); await window.NavigateHostAsync(source); await window.NavigateHostAsync(1, target);
            var left = Named<ListBox>(window, "Host files"); var right = Named<ListBox>(window, "Right host files");
            right.SelectedIndex = 0; left.Focus(); left.SelectedIndex = 0; Click(Named<Button>(window, "Move active selection"));
            Click(Command(Assert.Single(window.OwnedWindows), "Move"));
            await Eventually(() => !Busy(window) && left.ItemCount == 0 && right.ItemCount == 2);
            Assert.Contains("Keep", Assert.Single(right.SelectedItems!.Cast<object>()).ToString());
            Assert.Equal(new byte[] { 1, 2 }, File.ReadAllBytes(Path.Combine(target, "Data")));
        }
        finally { foreach (var dialog in window.OwnedWindows.ToArray()) dialog.Close(); window.Close(); }
    });

    [Fact]
    public Task FailedImageSourceRemovalKeepsTheCopyAndItsErrorVisibleAfterRefresh() => UiAsync(async () =>
    {
        var window = new DiskImageManagerWindow(); window.Show();
        try
        {
            var source = await SharedImageSetup(window); await window.OpenImageAsync(EditableImage("Partial", CopperScreenHardDiskSetup.FileSystem.Sfs2));
            var left = Named<ListBox>(window, "Left image files"); var right = Named<ListBox>(window, "Image files");
            left.Focus(); left.SelectedIndex = 0; Click(Command(window, "Protect")); var dialog = Assert.Single(window.OwnedWindows);
            Named<CheckBox>(dialog, "Deletable").IsChecked = false; Click(Command(dialog, "Apply"));
            await Eventually(() => !Busy(window) && Named<TextBlock>(window, "Disk manager status").Text!.Contains("Protection updated"));
            var before = File.ReadAllBytes(source); left.Focus(); left.SelectedIndex = 0; Click(Named<Button>(window, "Move active selection"));
            Click(Command(Assert.Single(window.OwnedWindows), "Move"));
            await Eventually(() => !Busy(window) && right.ItemCount == 1);
            Assert.Single(left.Items); Assert.Equal(before, File.ReadAllBytes(source));
            Assert.Contains("Destination files are retained", Named<TextBlock>(window, "Disk manager status").Text);
            await EnterDirectory(window, right, "Tree"); Assert.Equal(2, right.ItemCount);
        }
        finally { foreach (var dialog in window.OwnedWindows.ToArray()) dialog.Close(); window.Close(); }
    });

    [Fact]
    public Task LockedHostSourceRetainsBothCopiesAndReportsIncompleteMove() => UiAsync(async () =>
    {
        var source = Destination(); var file = Path.Combine(source, "Locked"); File.WriteAllBytes(file, [1, 2]);
        var target = Path.Combine(_root, "Target"); Directory.CreateDirectory(target);
        var window = new DiskImageManagerWindow(); window.Show();
        try
        {
            await Idle(window); await window.NavigateHostAsync(source); await window.NavigateHostAsync(1, target);
            using var held = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            var left = Named<ListBox>(window, "Host files"); var right = Named<ListBox>(window, "Right host files");
            left.Focus(); left.SelectedIndex = 0; Click(Named<Button>(window, "Move active selection"));
            Click(Command(Assert.Single(window.OwnedWindows), "Move"));
            await Eventually(() => !Busy(window) && right.ItemCount == 1);
            Assert.Single(left.Items); Assert.True(File.Exists(file)); Assert.Equal(new byte[] { 1, 2 }, File.ReadAllBytes(Path.Combine(target, "Locked")));
            Assert.Contains("host source removal did not finish", Named<TextBlock>(window, "Disk manager status").Text);
        }
        finally { foreach (var dialog in window.OwnedWindows.ToArray()) dialog.Close(); window.Close(); }
    });

    [Fact]
    public Task PointerDragStartsFromAFileRowAndCopiesBetweenPanes() => UiAsync(async () =>
    {
        var source = Destination(); File.WriteAllBytes(Path.Combine(source, "Dragged"), [1, 7]);
        var target = Path.Combine(_root, "DragTarget"); Directory.CreateDirectory(target);
        var window = new DiskImageManagerWindow(); window.Show();
        try
        {
            await Idle(window); await window.NavigateHostAsync(source); await window.NavigateHostAsync(1, target);
            var left = Named<ListBox>(window, "Host files"); var right = Named<ListBox>(window, "Right host files"); window.UpdateLayout();
            var row = Assert.IsType<ListBoxItem>(left.ContainerFromIndex(0));
            var start = row.TranslatePoint(new Point(70, 11), window)!.Value; var end = right.TranslatePoint(new Point(80, 60), window)!.Value;
            window.MouseDown(start, MouseButton.Left); window.MouseMove(start + new Point(12, 0), RawInputModifiers.LeftMouseButton);
            window.MouseMove(end, RawInputModifiers.LeftMouseButton); window.MouseUp(end, MouseButton.Left);
            await Eventually(() => !Busy(window) && right.ItemCount == 1);
            Assert.True(File.Exists(Path.Combine(source, "Dragged"))); Assert.Equal(new byte[] { 1, 7 }, File.ReadAllBytes(Path.Combine(target, "Dragged")));
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task ExternalHostDropsImportAndReadonlyImagesRefuseDrops() => UiAsync(async () =>
    {
        var path = EditableImage("Drop"); var host = Destination(); var file = Path.Combine(host, "Dropped"); File.WriteAllBytes(file, [4, 5, 6]);
        var window = new DiskImageManagerWindow(); window.Show();
        try
        {
            await Idle(window); await window.OpenImageAsync(path); var right = Named<ListBox>(window, "Image files");
            using var data = new DataTransfer(); data.Add(DataTransferItem.CreateFile((await window.StorageProvider.TryGetFileFromPathAsync(new Uri(file)))!));
            File.SetAttributes(path, FileAttributes.ReadOnly);
            try { Drop(window, right, data); await Idle(window); Assert.Empty(right.Items); }
            finally { File.SetAttributes(path, FileAttributes.Normal); }
            Drop(window, right, data); await Eventually(() => !Busy(window) && right.ItemCount == 1);
            Assert.Contains("Dropped", right.Items[0]!.ToString()); Assert.True(File.Exists(file));
        }
        finally { window.Close(); }
    });
}
