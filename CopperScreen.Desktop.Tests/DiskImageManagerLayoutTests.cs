using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using CopperDisk.Tests;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed partial class DiskImageManagerTests
{
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Press(Window window, Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPress(key, modifiers, PhysicalKey.None, null);
        window.KeyRelease(key, modifiers, PhysicalKey.None, null);
    }

    private static void ClickRow(Window window, ListBox list, int index, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.UpdateLayout();
        var row = Assert.IsType<ListBoxItem>(list.ContainerFromIndex(index));
        var point = row.TranslatePoint(new Point(40, 11), window)!.Value;
        window.MouseDown(point, MouseButton.Left, modifiers);
        window.MouseUp(point, MouseButton.Left, modifiers);
    }

    private static void Capture(Window window, string state, double scale)
    {
        if (Environment.GetEnvironmentVariable("COPPERSCREEN_MANAGER_CAPTURE") is not { } directory) return;
        Directory.CreateDirectory(directory); window.UpdateLayout();
        var size = window.ClientSize;
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)(size.Width * scale), (int)(size.Height * scale)), new Vector(96 * scale, 96 * scale));
        bitmap.Render(window); bitmap.Save(Path.Combine(directory, $"manager-{state}-{size.Width}-{size.Height}-{scale}.png"));
    }

    [Fact]
    public Task ActivePaneSurvivesCommandFocusAndRoutesBothCopyDirections() => UiAsync(async () =>
    {
        var host = Destination(); var path = Path.Combine(_root, "Writable.hdf");
        using (var disk = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
        {
            disk.SetLength(2 * 1024 * 1024);
            CopperScreenFileSystemFormatter.Format(disk, 0, disk.Length, CopperScreenHardDiskSetup.FileSystem.Ffs, "Writable", default);
        }
        File.WriteAllBytes(Path.Combine(host, "Incoming"), [7, 8, 9]);
        var window = new DiskImageManagerWindow(); window.Show();
        try
        {
            await Idle(window); await window.NavigateHostAsync(host); await window.OpenImageAsync(path);
            var source = Named<ListBox>(window, "Host files");
            var image = Named<ListBox>(window, "Image files");
            source.Focus(); source.SelectedIndex = 0;
            var copy = Named<Button>(window, "Copy active selection");
            Assert.Equal("Copy →", copy.Content);
            copy.Focus(); Click(copy); await Idle(window);
            Assert.Contains("Image saved", Named<TextBlock>(window, "Disk manager status").Text);
            Assert.Single(image.Items);
            var extracted = Path.Combine(_root, "Extracted"); Directory.CreateDirectory(extracted);
            File.WriteAllText(Path.Combine(extracted, "Keep"), "Existing destination entry");
            await window.NavigateHostAsync(extracted);
            source.SelectedIndex = 0;
            image.Focus(); image.SelectedIndex = 0;
            Assert.Equal("← Copy", copy.Content);
            Assert.Single(source.SelectedItems!); Assert.Single(image.SelectedItems!);
            Assert.Equal("Source pane", AutomationProperties.GetHelpText(image));
            copy.Focus(); Assert.Equal("← Copy", copy.Content);
            Press(window, Key.F5); await Idle(window);
            Assert.Equal(new byte[] { 7, 8, 9 }, File.ReadAllBytes(Path.Combine(extracted, "Incoming")));
            source.Focus(); source.SelectedIndex = 1;
            Assert.Equal("Copy →", copy.Content);
            copy.Focus();
            Press(window, Key.F6);
            Assert.Equal("← Copy", copy.Content);
            Assert.True(image.IsKeyboardFocusWithin);
            Press(window, Key.F6);
            Assert.Equal("Copy →", copy.Content);
            Assert.True(source.IsKeyboardFocusWithin);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task DesktopSelectionSortingInfoAndAddressEditingUseTheActivePane() => UiAsync(async () =>
    {
        var path = Save(new AmigaFileSystemFixture()); var host = Destination();
        Directory.CreateDirectory(Path.Combine(host, "Folder"));
        File.WriteAllBytes(Path.Combine(host, "Alpha"), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(host, "Beta"), [1]);
        File.WriteAllBytes(Path.Combine(host, "Gamma"), [1, 2]);
        var window = new DiskImageManagerWindow(); window.Show();
        try
        {
            await Idle(window); await window.NavigateHostAsync(host); await window.OpenImageAsync(path);
            var list = Named<ListBox>(window, "Host files");
            ClickRow(window, list, 0); Assert.Single(list.SelectedItems!);
            ClickRow(window, list, 2, RawInputModifiers.Control); Assert.Equal(2, list.SelectedItems!.Count);
            ClickRow(window, list, 1); Assert.Single(list.SelectedItems!);
            ClickRow(window, list, 3, RawInputModifiers.Shift); Assert.Equal(3, list.SelectedItems!.Count);
            Click(Command(window, "Invert")); Assert.Single(list.SelectedItems!);
            Assert.StartsWith("▸ Folder", list.SelectedItem!.ToString());
            Click(Command(window, "All")); Assert.Equal(4, list.SelectedItems!.Count);
            Assert.False(Command(window, "Info").IsEnabled);
            Click(Command(window, "Clear")); Assert.Empty(list.SelectedItems!);
            ClickRow(window, list, 1);
            Click(Named<Button>(window, "Sort host by size"));
            Assert.Collection(list.Items,
                i => Assert.StartsWith("▸ Folder", i!.ToString()),
                i => Assert.StartsWith("Beta", i!.ToString()),
                i => Assert.StartsWith("Gamma", i!.ToString()),
                i => Assert.StartsWith("Alpha", i!.ToString()));
            Assert.Contains("Alpha", list.SelectedItem!.ToString()); // sorting preserves selection
            Click(Named<Button>(window, "Sort host by size"));
            Assert.Contains("Alpha", list.Items[1]!.ToString());
            Click(Named<Button>(window, "Sort host by name"));
            Assert.Contains("Alpha", list.Items[1]!.ToString());
            Click(Command(window, "Info"));
            var info = Assert.IsType<Flyout>(FlyoutBase.GetAttachedFlyout(Command(window, "Info")));
            Assert.True(info.IsOpen);
            Assert.Contains(Assert.IsType<ScrollViewer>(info.Content).GetLogicalDescendants().OfType<TextBlock>(), t => t.Text?.Contains("Modified:") == true);
            info.Hide();

            var address = Named<TextBox>(window, "Host folder path"); address.Focus();
            Press(window, Key.A, RawInputModifiers.Control);
            Assert.Single(list.SelectedItems!); Assert.Equal(address.Text!.Length, address.SelectionEnd - address.SelectionStart);
            Press(window, Key.R, RawInputModifiers.Control); Assert.Single(list.SelectedItems!);
            address.Text = Path.Combine(host, "Folder"); Press(window, Key.Enter); await Idle(window);
            Assert.Equal(0, list.ItemCount);
            list.Focus(); Press(window, Key.Up, RawInputModifiers.Alt); await Idle(window);
            Assert.Equal(4, list.ItemCount);
            Press(window, Key.A, RawInputModifiers.Control); Assert.Equal(4, list.SelectedItems!.Count);
            File.WriteAllBytes(Path.Combine(host, "Delta"), []);
            Press(window, Key.R, RawInputModifiers.Control); await Idle(window); Assert.Equal(5, list.ItemCount);
            window.UpdateLayout();
            Assert.All(list.GetVisualDescendants().OfType<ListBoxItem>(), item => Assert.Equal(22, item.Bounds.Height));
            var image = Named<ListBox>(window, "Image files"); image.Focus(); image.SelectedIndex = 2;
            Click(Named<Button>(window, "Sort image by name"));
            Assert.StartsWith("▸ Tools", image.Items[0]!.ToString());
            Assert.StartsWith("ReadMe", image.Items[1]!.ToString());
            Assert.Single(image.SelectedItems!);
            Click(Named<Button>(window, "Sort image by size"));
            Assert.StartsWith("▸ Tools", image.Items[0]!.ToString());
            Assert.StartsWith("EmptyFile", image.Items[1]!.ToString());
            Assert.StartsWith("ReadMe", image.SelectedItem!.ToString());
            Click(Command(window, "Help"));
            var help = Assert.IsType<Flyout>(FlyoutBase.GetAttachedFlyout(Command(window, "Help")));
            Assert.True(help.IsOpen); help.Hide();
        }
        finally { window.Close(); }
    });

    [Theory]
    [InlineData(1060, 720, 1.0)] [InlineData(760, 620, 1.0)]
    [InlineData(1060, 720, 2.0)] [InlineData(760, 620, 2.0)]
    public Task CompactStatesKeepFileViewsAlignedAndBounded(int width, int height, double scale) => UiAsync(async () =>
    {
        var path = Save(new AmigaFileSystemFixture()); var host = Destination();
        Directory.CreateDirectory(Path.Combine(host, "Documents"));
        Directory.CreateDirectory(Path.Combine(host, "Pictures"));
        File.WriteAllText(Path.Combine(host, "A long file name that stays inside the Name column " + new string('W', 120) + ".txt"), "Long names stay in the name column");
        for (var i = 1; i <= 35; i++) File.WriteAllBytes(Path.Combine(host, $"File {i:00}.bin"), new byte[i * 173]);
        var window = new DiskImageManagerWindow { Width = width, Height = height }; window.Show(); window.SetRenderScaling(scale);
        try
        {
            await Idle(window); await window.NavigateHostAsync(host); await window.OpenImageAsync(path);
            var files = Named<ListBox>(window, "Image files");
            var source = Named<ListBox>(window, "Host files");
            source.SelectedIndex = 0;
            window.UpdateLayout();
            Assert.Equal(files.Bounds.Height, source.Bounds.Height);
            Assert.True(files.Bounds.Height >= window.ClientSize.Height * .75);
            Assert.Equal(source.TranslatePoint(default, window)!.Value.Y, files.TranslatePoint(default, window)!.Value.Y);
            Assert.All(source.GetVisualDescendants().OfType<TextBlock>(), text => Assert.True(text.Bounds.Width <= source.Bounds.Width));
            foreach (var list in new[] { source, files })
            {
                var folders = list.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>()
                    .Where(icon => icon.Parent is Grid grid && grid.Children.Count == 3 && grid.ColumnDefinitions.Count == 3).ToArray();
                Assert.NotEmpty(folders);
                foreach (var icon in folders)
                {
                    var row = (Grid)icon.Parent!;
                    var name = row.Children.OfType<TextBlock>().Single(text => Grid.GetColumn(text) == 1);
                    var iconStart = icon.TranslatePoint(default, list)!.Value.X;
                    var nameStart = name.TranslatePoint(default, list)!.Value.X;
                    Assert.True(nameStart - iconStart - icon.Bounds.Width >= 8, $"Folder icon overlaps its name: {name.Text}");
                }
            }
            Capture(window, "populated", scale);
            files.Focus(); files.SelectedIndex = 0; Press(window, Key.Enter); await Idle(window);
            files.SelectedIndex = 0; Press(window, Key.Enter); await Idle(window);
            Assert.Equal(0, files.ItemCount); Capture(window, "empty", scale);

            var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var operation = (Task)typeof(DiskImageManagerWindow).GetMethod("RunAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(window, new object[] { (Func<CancellationToken, Task>)(ct => pending.Task.WaitAsync(ct)) })!;
            Assert.True(Busy(window));
            Assert.False(Named<Button>(window, "Copy active selection").IsVisible);
            Assert.True(Named<Button>(window, "Cancel operation").IsVisible);
            Assert.False(source.IsEffectivelyEnabled); Assert.False(Command(window, "Parent").IsEffectivelyEnabled);
            Named<TextBlock>(window, "Disk manager status").Text = "Copying: 42 files · 123,456 bytes · " + new string('W', 150);
            Capture(window, "busy", scale);
            Press(window, Key.Escape); await operation;
            Assert.False(Busy(window)); Assert.False(Named<Button>(window, "Cancel operation").IsVisible);

            File.WriteAllBytes(Path.Combine(_root, "Unavailable.adf"), new byte[901120]);
            await window.OpenImageAsync(Path.Combine(_root, "Unavailable.adf"));
            Assert.Equal(0, files.ItemCount); Assert.False(Named<Button>(window, "Copy active selection").IsEnabled);
            Assert.Contains("OFS/FFS", Named<TextBlock>(window, "Disk manager status").Text);
            Capture(window, "unavailable", scale);
        }
        finally { window.Close(); }
    });
}
