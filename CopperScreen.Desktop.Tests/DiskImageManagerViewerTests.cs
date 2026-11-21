using System.Text;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using CopperDisk.Tests;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed partial class DiskImageManagerTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public Task TextAndHexViewersReadFilesFromEitherPaneAndLocation(int location) => UiAsync(async () =>
    {
        var host = Destination(); var bytes = Encoding.UTF8.GetBytes("Hello åä😀\r\n" + new string('x', 9000) + "END");
        File.WriteAllBytes(Path.Combine(host, "Text.txt"), bytes); var imagePath = EditableImage("Viewer");
        var window = new DiskImageManagerWindow(); window.Show();
        try
        {
            await Idle(window); await window.NavigateHostAsync(host); await window.OpenImageAsync(imagePath);
            Named<ListBox>(window, "Host files").SelectAll(); await window.CopyHostSelectedAsync();
            if (location == 1) await window.NavigateHostAsync(1, host);
            if (location == 2) await window.OpenImageAsync(0, imagePath);
            var list = Named<ListBox>(window, location switch { 0 => "Host files", 1 => "Right host files", 2 => "Left image files", _ => "Image files" });
            list.Focus(); list.SelectedIndex = 0; var before = File.ReadAllBytes(imagePath);
            Assert.True(Command(window, "Text").IsEnabled); Assert.True(Command(window, "Hex").IsEnabled);
            var button = Command(window, "Text"); button.Focus(); Click(button);
            await Eventually(() => window.OwnedWindows.OfType<DiskImageFileViewerWindow>().Any());
            var viewer = window.OwnedWindows.OfType<DiskImageFileViewerWindow>().Single();
            var contents = Named<TextBox>(viewer, "File contents"); Assert.True(contents.IsReadOnly); Assert.StartsWith("Hello åä😀\n", contents.Text);
            Assert.Equal(0, Named<ComboBox>(viewer, "Text encoding").SelectedIndex);
            Named<ComboBox>(viewer, "View mode").SelectedIndex = 1;
            Assert.StartsWith("00000000  48 65 6C 6C 6F", contents.Text);
            Click(Named<Button>(viewer, "Next page"));
            await Eventually(() => !Busy(window) && Named<TextBox>(viewer, "Hex byte offset").Text == "00001000");
            Assert.StartsWith("00001000", contents.Text);
            var offset = Named<TextBox>(viewer, "Hex byte offset"); offset.Text = "2000"; offset.Focus(); Press(viewer, Key.Enter);
            await Eventually(() => !Busy(window) && contents.Text!.StartsWith("00002000"));
            Click(Named<Button>(viewer, "First page")); await Eventually(() => !Busy(window) && contents.Text!.StartsWith("00000000"));
            Press(viewer, Key.F3); Assert.StartsWith("Hello åä😀\n", contents.Text);
            if (location == 3 && Environment.GetEnvironmentVariable("COPPERSCREEN_MANAGER_CAPTURE") != null)
            {
                Capture(viewer, "text-viewer", 1); Press(viewer, Key.F3, RawInputModifiers.Shift); Capture(viewer, "hex-viewer", 2);
            }
            viewer.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.None, null); await Eventually(() => !window.OwnedWindows.Any());
            Assert.Single(list.SelectedItems!.Cast<object>()); Assert.Equal(before, File.ReadAllBytes(imagePath)); Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(host, "Text.txt")));
            list.Focus(); Press(window, Key.F3, RawInputModifiers.Shift);
            await Eventually(() => window.OwnedWindows.OfType<DiskImageFileViewerWindow>().Any());
            viewer = window.OwnedWindows.OfType<DiskImageFileViewerWindow>().Single(); Assert.Equal(1, Named<ComboBox>(viewer, "View mode").SelectedIndex); viewer.Close();
        }
        finally { foreach (var child in window.OwnedWindows.ToArray()) child.Close(); window.Close(); }
    });

    [Fact]
    public Task ViewerEligibilityEmptyFilesLatin1AndTextEditingShortcuts() => UiAsync(async () =>
    {
        var path = Save(new AmigaFileSystemFixture()); var host = Destination();
        Directory.CreateDirectory(Path.Combine(host, "Folder")); File.WriteAllBytes(Path.Combine(host, "Latin"), [0xE4, 0xF6]);
        var window = new DiskImageManagerWindow(); window.Show();
        try
        {
            await Idle(window); await window.NavigateHostAsync(host); await window.OpenImageAsync(path);
            var left = Named<ListBox>(window, "Host files"); left.Focus(); Assert.False(Command(window, "Text").IsEnabled);
            left.SelectedIndex = ItemIndex(left, "▸ Folder"); Assert.False(Command(window, "Hex").IsEnabled);
            left.SelectAll(); Assert.False(Command(window, "Text").IsEnabled);
            left.UnselectAll(); left.SelectedIndex = ItemIndex(left, "Latin"); Named<TextBox>(window, "Host folder path").Focus();
            Press(window, Key.F3); Assert.Empty(window.OwnedWindows);
            left.Focus(); Press(window, Key.Enter); await Eventually(() => window.OwnedWindows.Any());
            var viewer = Assert.IsType<DiskImageFileViewerWindow>(Assert.Single(window.OwnedWindows));
            Assert.Equal("äö", Named<TextBox>(viewer, "File contents").Text); Assert.Equal(1, Named<ComboBox>(viewer, "Text encoding").SelectedIndex);
            var offset = Named<TextBox>(viewer, "Hex byte offset"); offset.Text = "FFFFFFFFFFFFFFFF"; offset.Focus(); Press(viewer, Key.Enter);
            Assert.Contains("within the file", Named<TextBlock>(viewer, "File viewer status").Text); viewer.Close(); await Eventually(() => !window.OwnedWindows.Any());
            var right = Named<ListBox>(window, "Image files"); right.Focus(); right.SelectedIndex = ItemIndex(right, "EmptyFile");
            File.SetAttributes(path, FileAttributes.ReadOnly);
            try
            {
                right.UnselectAll(); right.SelectedIndex = ItemIndex(right, "EmptyFile"); Assert.True(Command(window, "Hex").IsEnabled);
                Press(window, Key.F3); await Eventually(() => window.OwnedWindows.Any()); viewer = Assert.IsType<DiskImageFileViewerWindow>(Assert.Single(window.OwnedWindows));
                Assert.Equal("", Named<TextBox>(viewer, "File contents").Text); Assert.Contains("Empty file", Named<TextBlock>(viewer, "File viewer status").Text);
                Assert.False(Named<Button>(viewer, "Next page").IsEffectivelyEnabled); viewer.Close();
            }
            finally { File.SetAttributes(path, FileAttributes.Normal); }
        }
        finally { foreach (var child in window.OwnedWindows.ToArray()) child.Close(); window.Close(); }
    });

    [Fact]
    public Task CorruptLaterPagesKeepErrorsVisibleAndPreserveTheSource() => UiAsync(async () =>
    {
        var fixture = new AmigaFileSystemFixture(0); fixture.Bytes[112 * 512 + 30] ^= 1; // beyond the first viewer page
        var path = Save(fixture); var before = File.ReadAllBytes(path); var window = new DiskImageManagerWindow(); window.Show();
        try
        {
            await Idle(window); await window.OpenImageAsync(path); var list = Named<ListBox>(window, "Image files"); list.Focus(); list.SelectedIndex = ItemIndex(list, "ReadMe");
            Press(window, Key.F3); await Eventually(() => window.OwnedWindows.Any()); var viewer = Assert.IsType<DiskImageFileViewerWindow>(Assert.Single(window.OwnedWindows));
            Click(Named<Button>(viewer, "Next page"));
            await Eventually(() => !Busy(window) && Named<TextBox>(viewer, "File contents").Text == "");
            Assert.False(Named<Button>(viewer, "Next page").IsEffectivelyEnabled);
            Assert.Contains("failed", Named<TextBlock>(viewer, "File viewer status").Text, StringComparison.OrdinalIgnoreCase);
            viewer.Close(); Assert.Contains("failed", Named<TextBlock>(window, "Disk manager status").Text, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(before, File.ReadAllBytes(path));
        }
        finally { foreach (var child in window.OwnedWindows.ToArray()) child.Close(); window.Close(); }
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task ClosingDuringAPageReadCancelsAndDisposesSafely(bool closeManager) => UiAsync(async () =>
    {
        var path = Save(new AmigaFileSystemFixture(0)); var window = new DiskImageManagerWindow(); window.Show();
        try
        {
            await Idle(window); await window.OpenImageAsync(path); var list = Named<ListBox>(window, "Image files"); list.Focus(); list.SelectedIndex = ItemIndex(list, "ReadMe");
            Press(window, Key.F3); await Eventually(() => window.OwnedWindows.Any()); var viewer = Assert.IsType<DiskImageFileViewerWindow>(Assert.Single(window.OwnedWindows));
            Click(Named<Button>(viewer, "Next page")); if (closeManager) window.Close(); else viewer.Close();
            await Eventually(() => !Busy(window) && !viewer.IsVisible); if (closeManager) Assert.False(window.IsVisible);
        }
        finally { foreach (var child in window.OwnedWindows.ToArray()) child.Close(); window.Close(); }
    });

    [Fact]
    public Task ViewerSupports64BitOffsetsAndCompactScaling() => UiAsync(async () =>
    {
        long lastRead = -1;
        var source = new CopperScreenFileViewSource("Large.bin", "Sparse file", 0x1_0000_0005,
            (offset, _) => new MemoryStream(Enumerable.Repeat((byte)65, (int)Math.Min(4101, 0x1_0000_0005 - offset)).ToArray()));
        var viewer = new DiskImageFileViewerWindow(source, source.ReadPage(0, default), true, (offset, ct) =>
        { lastRead = offset; return Task.FromResult(new FileViewReadResult(source.ReadPage(offset, ct))); }) { Width = 760, Height = 620 };
        viewer.Show(); viewer.SetRenderScaling(2);
        try
        {
            var offset = Named<TextBox>(viewer, "Hex byte offset"); offset.Text = "100000000"; offset.Focus(); Press(viewer, Key.Enter);
            await Eventually(() => lastRead == 0x1_0000_0000);
            Assert.StartsWith("0000000100000000  41 41 41 41 41", Named<TextBox>(viewer, "File contents").Text);
            Assert.False(Named<Button>(viewer, "Next page").IsEffectivelyEnabled);
            if (Environment.GetEnvironmentVariable("COPPERSCREEN_MANAGER_CAPTURE") != null) Capture(viewer, "hex-large", 2);
        }
        finally { viewer.Close(); }
    });
}
