using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using CopperDisk;
using CopperDisk.Tests;
using CopperScreen;

namespace CopperScreen.Tests;

[Collection("Desktop UI")]
public sealed class DiskImageManagerTests(LaunchUiFixture ui) : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "copperscreen-manager-" + Guid.NewGuid().ToString("N"));
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
    private string Save(AmigaFileSystemFixture fixture)
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "Fixture.adf"); File.WriteAllBytes(path, fixture.Bytes); return path;
    }
    private string Destination()
    {
        var path = Path.Combine(_root, "host"); Directory.CreateDirectory(path); return path;
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public void ExtractsWholeTreesEmptyFilesAndDatesWithoutChangingTheImage(int dos)
    {
        var fixture = new AmigaFileSystemFixture(dos); var path = Save(fixture); var host = Destination();
        using (var image = CopperScreenDiskImage.Open(path))
        {
            var v = image.Volumes[0]; var result = CopperScreenDiskExtraction.Extract(v, v.ReadDirectory(v.Root), host);
            Assert.Equal(3, result.Files); Assert.Equal(fixture.Content, File.ReadAllBytes(Path.Combine(host, "ReadMe")));
            Assert.Empty(File.ReadAllBytes(Path.Combine(host, "EmptyFile")));
            Assert.Equal(new byte[] { 1, 3, 5, 7 }, File.ReadAllBytes(Path.Combine(host, "Tools", "Nested.bin")));
            Assert.True(Directory.Exists(Path.Combine(host, "Tools", "Empty")));
            Assert.Equal(1978, File.GetLastWriteTime(Path.Combine(host, "ReadMe")).Year);
            Assert.DoesNotContain(Directory.GetFileSystemEntries(host), p => Path.GetFileName(p).StartsWith(".copperscreen"));
        }
        Assert.Equal(fixture.Bytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData("existing-file")] [InlineData("existing-directory")] [InlineData("bad-data")]
    [InlineData("cancelled")] [InlineData("link")] [InlineData("unsafe-name")]
    public void FailedExtractionPreservesExistingContentAndRemovesStaging(string failure)
    {
        var fixture = new AmigaFileSystemFixture(0);
        if (failure == "bad-data") fixture.Bytes[100 * 512 + 24] ^= 1;
        if (failure == "link") { fixture.Put(10, 508, unchecked((uint)-4)); fixture.Fix(10); }
        if (failure == "unsafe-name") { fixture.BString(10, 432, "../escape"); fixture.Fix(10); }
        var path = Save(fixture); var host = Destination();
        File.WriteAllText(Path.Combine(host, "Keep"), "preserved");
        if (failure == "existing-file") File.WriteAllText(Path.Combine(host, "ReadMe"), "original");
        if (failure == "existing-directory") Directory.CreateDirectory(Path.Combine(host, "Tools"));
        var before = Directory.GetFileSystemEntries(host).Order().ToArray();
        using var image = CopperScreenDiskImage.Open(path); var v = image.Volumes[0];
        Assert.ThrowsAny<Exception>(() => CopperScreenDiskExtraction.Extract(v, v.ReadDirectory(v.Root), host,
            cancellationToken: new CancellationToken(failure == "cancelled")));
        Assert.Equal(before, Directory.GetFileSystemEntries(host).Order().ToArray());
        Assert.Equal("preserved", File.ReadAllText(Path.Combine(host, "Keep")));
        if (failure == "existing-file") Assert.Equal("original", File.ReadAllText(Path.Combine(host, "ReadMe")));
    }

    [Theory]
    [InlineData("..")] [InlineData(".")] [InlineData("/escape")] [InlineData("a/b")]
    [InlineData("a\\b")] [InlineData("x:y")] [InlineData("NUL.txt")] [InlineData("con")] [InlineData("NUL .txt")]
    [InlineData("LPT1")] [InlineData("COM¹")] [InlineData("name.")] [InlineData("name ")]
    public void RejectsUnsafeOrUnrepresentableHostNames(string name) => Assert.Throws<IOException>(() => CopperScreenDiskExtraction.ValidateName(name));

    [Fact]
    public void RejectsCaseCollisionsInsideDirectoriesBeforePublishingAnyRoots()
    {
        var f = new AmigaFileSystemFixture(); f.BString(31, 432, "nested.BIN"); f.Fix(31);
        var path = Save(f); var host = Destination();
        using var image = CopperScreenDiskImage.Open(path); var v = image.Volumes[0];
        Assert.Throws<IOException>(() => CopperScreenDiskExtraction.Extract(v, v.ReadDirectory(v.Root), host));
        Assert.Empty(Directory.GetFileSystemEntries(host));
    }

    private sealed class CancelAfterFile(CancellationTokenSource source) : IProgress<DiskExtractionProgress>
    {
        public void Report(DiskExtractionProgress value) => source.Cancel();
    }

    [Fact]
    public void CancellationAfterStagingAFileRemovesTheUnpublishedTree()
    {
        var f = new AmigaFileSystemFixture(); var path = Save(f); var host = Destination();
        using var image = CopperScreenDiskImage.Open(path); var v = image.Volumes[0];
        using var cancel = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => CopperScreenDiskExtraction.Extract(v, v.ReadDirectory(v.Root),
            host, new CancelAfterFile(cancel), cancel.Token));
        Assert.Empty(Directory.GetFileSystemEntries(host));
    }

    [Fact]
    public void DestinationWithATrailingSeparatorIsAccepted()
    {
        var f = new AmigaFileSystemFixture(); var path = Save(f); var host = Destination();
        using var image = CopperScreenDiskImage.Open(path); var v = image.Volumes[0];
        CopperScreenDiskExtraction.Extract(v, [v.ReadDirectory(v.Root).Single(e => e.Name == "EmptyFile")], host + Path.DirectorySeparatorChar);
        Assert.True(File.Exists(Path.Combine(host, "EmptyFile")));
    }

    private static T Named<T>(Window window, string name) where T : Control => window.GetLogicalDescendants()
        .OfType<T>().Single(c => AutomationProperties.GetName(c) == name);
    private static Button Command(Window window, string label) => window.GetLogicalDescendants().OfType<Button>().Single(b => b.Content is string s && s == label);
    private static bool Busy(DiskImageManagerWindow window) => (bool)typeof(DiskImageManagerWindow).GetField("_busy", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
    private static async Task Idle(DiskImageManagerWindow window)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        while (Busy(window)) await Task.Delay(20, timeout.Token);
    }
    private async Task UiAsync(Func<Task> action) => await ui.Session.Dispatch(async () => { await action(); return true; }, CancellationToken.None);

    [Theory]
    [InlineData(1060, 720, 1.0)] [InlineData(760, 620, 1.0)] [InlineData(1060, 720, 2.0)]
    public Task TwoPaneWindowBrowsesNavigatesAndCopiesSelectedDirectory(int width, int height, double scale) => UiAsync(async () =>
    {
        var f = new AmigaFileSystemFixture(); var path = Save(f); var host = Destination();
        var window = new DiskImageManagerWindow { Width = width, Height = height }; window.Show(); window.SetRenderScaling(scale);
        try
        {
            await Idle(window); await window.NavigateHostAsync(host); await window.OpenImageAsync(path);
            var files = Named<ListBox>(window, "Image files");
            Assert.Equal(3, files.ItemCount);
            files.SelectedIndex = 0;
            Assert.True(Command(window, "← Copy selected to host").IsEnabled);
            files.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
            await Idle(window);
            Assert.Equal(2, files.ItemCount);
            Assert.False(Command(window, "← Copy selected to host").IsEnabled);
            Command(window, "Parent directory").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await Idle(window); Assert.Equal(3, files.ItemCount); files.SelectedIndex = 0;
            await window.CopySelectedAsync();
            Assert.True(File.Exists(Path.Combine(host, "Tools", "Nested.bin")));
            Assert.Equal(1, Named<ListBox>(window, "Host files").ItemCount);
            Assert.Contains("Copied 1 entries", Named<TextBlock>(window, "Disk manager status").Text);
            window.UpdateLayout(); await Task.Delay(60);
            foreach (var list in new[] { files, Named<ListBox>(window, "Host files") })
            {
                var origin = list.TranslatePoint(default, window)!.Value;
                Assert.True(list.Bounds.Width > 200 && list.Bounds.Height > 100, $"List bounds at {width}×{height}: {list.Bounds}");
                Assert.InRange(origin.X, 0, window.ClientSize.Width - list.Bounds.Width + 1);
                Assert.InRange(origin.Y, 0, window.ClientSize.Height - list.Bounds.Height + 1);
            }
            if (Environment.GetEnvironmentVariable("COPPERSCREEN_MANAGER_CAPTURE") is { } directory)
            {
                Directory.CreateDirectory(directory);
                using var bitmap = new RenderTargetBitmap(new PixelSize((int)(width * scale), (int)(height * scale)), new Vector(96 * scale, 96 * scale));
                bitmap.Render(window); bitmap.Save(Path.Combine(directory, $"manager-{width}-{height}-{scale}.png"));
            }
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task UnreadableImageExplainsFailureAndDisablesCopy() => UiAsync(async () =>
    {
        var f = new AmigaFileSystemFixture(); var path = Save(f); File.WriteAllBytes(path, new byte[901120]);
        var window = new DiskImageManagerWindow(); window.Show();
        try
        {
            await Idle(window); await window.OpenImageAsync(path);
            Assert.Contains("OFS/FFS", Named<TextBlock>(window, "Disk manager status").Text);
            Assert.False(Command(window, "← Copy selected to host").IsEnabled);
            Assert.Equal(0, Named<ListBox>(window, "Image files").ItemCount);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task LauncherOpensTheOwnedManagerAndReturnsAfterClosing() => UiAsync(async () =>
    {
        Directory.CreateDirectory(_root);
        var main = new MainWindow([], new CopperScreenRecentSession(_root)); main.Show();
        try
        {
            var command = main.GetLogicalDescendants().OfType<Button>().First(b => b.Content is "Disk Image Manager…");
            command.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            var manager = Assert.IsType<DiskImageManagerWindow>(Assert.Single(main.OwnedWindows));
            await Idle(manager);
            manager.Close();
            Assert.Empty(main.OwnedWindows);
        }
        finally { main.Close(); }
    });

    [Fact]
    public Task MultipleSelectedFilesCopyTogetherAndUnappliedHostPathsDisableCopy() => UiAsync(async () =>
    {
        var f = new AmigaFileSystemFixture(); var path = Save(f); var host = Destination();
        var window = new DiskImageManagerWindow(); window.Show();
        try
        {
            await Idle(window); await window.NavigateHostAsync(host); await window.OpenImageAsync(path);
            var files = Named<ListBox>(window, "Image files");
            files.SelectedItems!.Add(files.Items[1]); files.SelectedItems.Add(files.Items[2]);
            Assert.True(Command(window, "← Copy selected to host").IsEnabled);
            var hostPath = Named<TextBox>(window, "Host folder path"); hostPath.Text = Path.Combine(host, "not-applied");
            Assert.False(Command(window, "← Copy selected to host").IsEnabled);
            hostPath.Text = host;
            await window.CopySelectedAsync();
            Assert.Equal(f.Content, File.ReadAllBytes(Path.Combine(host, "ReadMe")));
            Assert.Empty(File.ReadAllBytes(Path.Combine(host, "EmptyFile")));
            Assert.Equal(2, Named<ListBox>(window, "Host files").ItemCount);
        }
        finally { window.Close(); }
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task SmartRdbVolumeCanBeSelectedBrowsedAndCopied(bool sfs2) => UiAsync(async () =>
    {
        Directory.CreateDirectory(_root);
        var fixture = ServiceImageFixture.Smart(sfs2);
        var bytes = DiskImageServiceTests.MixedRdb(sfs2, fixture);
        var path = Path.Combine(_root, "Mixed.hdf"); File.WriteAllBytes(path, bytes); var host = Destination();
        var window = new DiskImageManagerWindow(); window.Show();
        try
        {
            await Idle(window); await window.NavigateHostAsync(host); await window.OpenImageAsync(path);
            var volumes = Named<ComboBox>(window, "Image volumes"); var files = Named<ListBox>(window, "Image files");
            Assert.Equal(3, files.ItemCount);
            volumes.SelectedIndex = 1; await Idle(window);
            Assert.Equal(2, files.ItemCount);
            Assert.Contains(sfs2 ? "SFS2" : "SFS", volumes.SelectedItem!.ToString());
            files.SelectedIndex = 0; await window.CopySelectedAsync();
            Assert.Equal(fixture.Expected, File.ReadAllBytes(Path.Combine(host, "Docs", "Payload")));
            Assert.Empty(File.ReadAllBytes(Path.Combine(host, "Docs", "EmptyFile")));
            files.SelectedIndex = 0;
            files.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter }); await Idle(window);
            Assert.Equal(2, files.ItemCount);
            // Normal SFS files expose their comment and protection metadata.
            files.SelectedIndex = 1;
            Assert.Contains("Comment", window.GetLogicalDescendants().OfType<TextBlock>().Single(t => t.Text?.Contains("Protection:") == true).Text);
            if (Environment.GetEnvironmentVariable("COPPERSCREEN_MANAGER_CAPTURE") is { } directory)
            {
                Directory.CreateDirectory(directory); window.UpdateLayout(); await Task.Delay(60);
                using var bitmap = new RenderTargetBitmap(new PixelSize(1060, 720), new Vector(96, 96));
                bitmap.Render(window); bitmap.Save(Path.Combine(directory, sfs2 ? "manager-sfs2.png" : "manager-sfs.png"));
            }
            volumes.SelectedIndex = 0; await Idle(window); Assert.Equal(3, files.ItemCount);
            volumes.SelectedIndex = 1; await Idle(window); Assert.Equal(2, files.ItemCount);
        }
        finally { window.Close(); }
        Assert.Equal(bytes, File.ReadAllBytes(path));
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task DirtySmartVolumeExplainsRecoveryRequirementAndDisablesCopy(bool sfs2) => UiAsync(async () =>
    {
        Directory.CreateDirectory(_root);
        var fixture = ServiceImageFixture.Smart(sfs2); ServiceImageFixture.Put(fixture.Blocks[5], 12, 1);
        ServiceImageFixture.Seal(fixture.Blocks[5], 4, sfs2 ? 2u : 1u);
        var bytes = fixture.Bytes(); var path = Path.Combine(_root, "Dirty.hdf"); File.WriteAllBytes(path, bytes);
        var window = new DiskImageManagerWindow(); window.Show();
        try
        {
            await Idle(window); await window.OpenImageAsync(path);
            Assert.Contains("recovery", Named<TextBlock>(window, "Disk manager status").Text);
            Assert.False(Command(window, "← Copy selected to host").IsEnabled);
            Assert.Equal(0, Named<ListBox>(window, "Image files").ItemCount);
        }
        finally { window.Close(); }
        Assert.Equal(bytes, File.ReadAllBytes(path));
    });

    [Fact]
    public Task CancelledCopyClearsTheSessionAndReselectingMountsAfresh() => UiAsync(async () =>
    {
        Directory.CreateDirectory(_root);
        var fixture = new AmigaFileSystemFixture(blockSize: 1024, contentBlocks: 203);
        var bytes = fixture.Rdb(); var path = Path.Combine(_root, "Large.hdf"); File.WriteAllBytes(path, bytes); var host = Destination();
        var window = new DiskImageManagerWindow(); window.Show();
        try
        {
            await Idle(window); await window.NavigateHostAsync(host); await window.OpenImageAsync(path);
            var files = Named<ListBox>(window, "Image files"); files.SelectedIndex = 2;
            var copy = window.CopySelectedAsync();
            Command(window, "Cancel").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await copy;
            Assert.Contains("Reselect", Named<TextBlock>(window, "Disk manager status").Text);
            Assert.Empty(Directory.GetFileSystemEntries(host)); Assert.Equal(0, files.ItemCount);
            var volumes = Named<ComboBox>(window, "Image volumes"); Assert.Equal(-1, volumes.SelectedIndex);
            volumes.SelectedIndex = 0; await Idle(window); Assert.Equal(3, files.ItemCount);
            files.SelectedIndex = 2; await window.CopySelectedAsync();
            Assert.Equal(fixture.Content, File.ReadAllBytes(Path.Combine(host, "ReadMe")));
        }
        finally { window.Close(); }
        Assert.Equal(bytes, File.ReadAllBytes(path));
    });

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public Task HostSelectionImportsIntoTheCurrentImageDirectoryAndRefreshesTheListing(int format) => UiAsync(async () =>
    {
        Directory.CreateDirectory(_root); var path = Path.Combine(_root, "Writable.hdf");
        using (var disk = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
        {
            disk.SetLength(2 * 1024 * 1024);
            CopperScreenFileSystemFormatter.Format(disk, 0, disk.Length, (CopperScreenHardDiskSetup.FileSystem)format, "Writable", default);
        }
        var host = Destination(); Directory.CreateDirectory(Path.Combine(host, "Folder", "Empty"));
        var bytes = Enumerable.Range(0, 70001).Select(i => (byte)i).ToArray();
        File.WriteAllBytes(Path.Combine(host, "Folder", "Payload"), bytes);
        var window = new DiskImageManagerWindow { Width = 760, Height = 620 }; window.Show();
        try
        {
            await Idle(window); await window.NavigateHostAsync(host); await window.OpenImageAsync(path);
            var source = Named<ListBox>(window, "Host files"); source.SelectedIndex = 0;
            var import = Command(window, "Copy host selection to image →"); Assert.True(import.IsEnabled);
            await window.CopyHostSelectedAsync(); Assert.Contains("Image saved", Named<TextBlock>(window, "Disk manager status").Text);
            var files = Named<ListBox>(window, "Image files"); Assert.Equal(1, files.ItemCount);
            files.SelectedIndex = 0; files.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter }); await Idle(window);
            Assert.Equal(2, files.ItemCount);
            File.WriteAllBytes(Path.Combine(host, "Extra"), [8, 6, 4]); await window.NavigateHostAsync(host);
            source.SelectedIndex = 1; await window.CopyHostSelectedAsync(); Assert.Equal(3, files.ItemCount);
            Assert.Contains("Imported", Named<TextBlock>(window, "Disk manager status").Text);
            if (Environment.GetEnvironmentVariable("COPPERSCREEN_MANAGER_CAPTURE") is { } directory)
            {
                Directory.CreateDirectory(directory); window.UpdateLayout(); await Task.Delay(60);
                using var bitmap = new RenderTargetBitmap(new PixelSize(760, 620), new Vector(96, 96));
                bitmap.Render(window); bitmap.Save(Path.Combine(directory, $"manager-import-{format}.png"));
            }
        }
        finally { window.Close(); }
        using var reopened = CopperScreenDiskImage.Open(path); var volume = reopened.Volumes[0];
        var folder = volume.ReadDirectory(volume.Root).Single(e => e.Name == "Folder"); var entries = volume.ReadDirectory(folder);
        Assert.Equal(3, entries.Count); using var output = new MemoryStream(); volume.CopyFileTo(entries.Single(e => e.Name == "Payload"), output);
        Assert.Equal(bytes, output.ToArray()); Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(host, "Folder", "Payload")));
    });

    [Fact]
    public Task InternationalVariantsRemainBrowseOnlyAndCannotImport() => UiAsync(async () =>
    {
        var path = Save(new CopperDisk.Tests.AmigaFileSystemFixture(3)); var host = Destination();
        File.WriteAllBytes(Path.Combine(host, "Source"), [1]);
        var window = new DiskImageManagerWindow(); window.Show();
        try
        {
            await Idle(window); await window.NavigateHostAsync(host); await window.OpenImageAsync(path);
            Named<ListBox>(window, "Host files").SelectedIndex = 0;
            Assert.False(Command(window, "Copy host selection to image →").IsEnabled);
            Assert.Contains("browse-only", Named<TextBlock>(window, "Disk manager status").Text);
            var files = Named<ListBox>(window, "Image files"); files.SelectedIndex = 2;
            Assert.True(Command(window, "← Copy selected to host").IsEnabled);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task CancelledImportPreservesTheImageAndReselectionAllowsRetry() => UiAsync(async () =>
    {
        Directory.CreateDirectory(_root); var path = Path.Combine(_root, "Writable.hdf");
        using (var disk = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
        {
            disk.SetLength(2 * 1024 * 1024);
            CopperScreenFileSystemFormatter.Format(disk, 0, disk.Length, CopperScreenHardDiskSetup.FileSystem.Ffs, "Writable", default);
        }
        var before = File.ReadAllBytes(path); var host = Destination(); File.WriteAllBytes(Path.Combine(host, "Payload"), new byte[140013]);
        var window = new DiskImageManagerWindow(); window.Show();
        try
        {
            await Idle(window); await window.NavigateHostAsync(host); await window.OpenImageAsync(path);
            Named<ListBox>(window, "Host files").SelectedIndex = 0;
            var import = window.CopyHostSelectedAsync();
            Command(window, "Cancel").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); await import;
            Assert.Contains("Reselect", Named<TextBlock>(window, "Disk manager status").Text); Assert.Equal(before, File.ReadAllBytes(path));
            Assert.DoesNotContain(Directory.GetFiles(_root), p => Path.GetFileName(p).StartsWith(".copperscreen-import-"));
            Named<ComboBox>(window, "Image volumes").SelectedIndex = 0; await Idle(window);
            Assert.True(Command(window, "Copy host selection to image →").IsEnabled);
            await window.CopyHostSelectedAsync(); Assert.Contains("Image saved", Named<TextBlock>(window, "Disk manager status").Text);
        }
        finally { window.Close(); }
    });
}
