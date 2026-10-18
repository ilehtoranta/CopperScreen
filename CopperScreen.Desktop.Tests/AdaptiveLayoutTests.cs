using System.Buffers.Binary;
using System.Reflection;
using Avalonia.Interactivity;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using CopperScreen;

namespace CopperScreen.Tests;

[Collection("Desktop UI")]
public sealed class AdaptiveLayoutTests(LaunchUiFixture ui) : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "copperscreen-adaptive-" + Guid.NewGuid().ToString("N"));
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
    private Task Ui(Action action) => ui.Session.Dispatch(action, CancellationToken.None);
    private async Task UiAsync(Func<Task> action) => await ui.Session.Dispatch(async () => { await action(); return true; }, CancellationToken.None);
    private static T Named<T>(MainWindow window, string name) where T : Control => window.GetLogicalDescendants()
        .OfType<T>().Single(c => AutomationProperties.GetName(c) == name);
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Press(Window window, Key key, RawInputModifiers modifiers = RawInputModifiers.None, PhysicalKey physical = PhysicalKey.None)
        => window.KeyPress(key, modifiers, physical, null);
    private static void Release(Window window, Key key, RawInputModifiers modifiers = RawInputModifiers.None, PhysicalKey physical = PhysicalKey.None)
        => window.KeyRelease(key, modifiers, physical, null);
    private static async Task Until(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (!condition()) await Task.Delay(20, timeout.Token);
    }
    private MainWindow Running()
    {
        Directory.CreateDirectory(_root);
        var disk = new byte[901120]; "DOS\0"u8.CopyTo(disk);
        new CopperScreenBootAssembler(CopperScreenMinimalBoot.BootAddress + 12).Assemble("loop:\n bra loop").CopyTo(disk, 12);
        uint sum = 0;
        for (var i = 0; i < 1024; i += 4)
        {
            var next = unchecked(sum + BinaryPrimitives.ReadUInt32BigEndian(disk.AsSpan(i)));
            if (next < sum) next++;
            sum = next;
        }
        BinaryPrimitives.WriteUInt32BigEndian(disk.AsSpan(4), ~sum);
        var path = Path.Combine(_root, "original.adf"); File.WriteAllBytes(path, disk);
        var window = new MainWindow(["--minimal-disk-boot", path], new CopperScreenRecentSession(_root));
        window.Show();
        return window;
    }
    private static CopperScreenRuntime Runtime(MainWindow window) => (CopperScreenRuntime)typeof(MainWindow)
        .GetField("_runtime", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;

    [Theory]
    [InlineData(1280)]
    [InlineData(712)]
    public Task WindowedF11ReclaimsControlSpaceAndPreservesItsChoiceAcrossFullscreen(int width) => UiAsync(async () =>
    {
        var window = Running();
        try
        {
            await Resize(window, width, 720);
            var toolbar = Named<Border>(window, "Emulator controls");
            var presenter = window.GetLogicalDescendants().OfType<FramebufferPresenter>().Single();
            var visible = BoundsInWindow(presenter, window);
            Press(window, Key.F11); Release(window, Key.F11);
            window.UpdateLayout();
            Assert.False(toolbar.IsVisible);
            var hidden = BoundsInWindow(presenter, window);
            if (width == 1280) Assert.True(hidden.Width > visible.Width);
            else Assert.True(hidden.Height > visible.Height);
            Assert.Equal(window.ClientSize.Width, hidden.Width);
            Assert.Equal(new Size(width, 720), window.ClientSize);
            Press(window, Key.Enter, RawInputModifiers.Alt, PhysicalKey.Enter);
            Release(window, Key.Enter, RawInputModifiers.Alt, PhysicalKey.Enter);
            Press(window, Key.F11); Release(window, Key.F11);
            Assert.True(toolbar.IsVisible);
            Press(window, Key.Enter, RawInputModifiers.Alt, PhysicalKey.Enter);
            Release(window, Key.Enter, RawInputModifiers.Alt, PhysicalKey.Enter);
            window.UpdateLayout();
            Assert.False(toolbar.IsVisible);
            await Resize(window, width == 1280 ? 712 : 1280, 720);
            Assert.False(toolbar.IsVisible);
            Assert.Equal(window.ClientSize.Width, presenter.Bounds.Width);
            Press(window, Key.F11); Release(window, Key.F11);
            window.UpdateLayout();
            Assert.True(toolbar.IsVisible);
            Press(window, Key.F11); Release(window, Key.F11);
            Press(window, Key.F1); Release(window, Key.F1);
            window.UpdateLayout();
            Assert.True(toolbar.IsVisible);
            var more = Named<Button>(window, "More");
            Assert.True(more.Flyout!.IsOpen);
            more.Flyout.Hide();
        }
        finally { window.Close(); await Until(() => !window.IsVisible); }
    });

    [Fact]
    public Task LauncherDefaultsToSideControlsAndFallsBackOnlyForWidth() => UiAsync(async () =>
    {
        var window = new MainWindow([], new CopperScreenRecentSession(_root));
        window.Show();
        try
        {
            window.UpdateLayout();
            var toolbar = Named<Border>(window, "Emulator controls");
            Assert.True(window.ClientSize.Width >= 920);
            Assert.Equal(1, Grid.GetColumn(toolbar));
            await Resize(window, 919, 760);
            Assert.Equal(0, Grid.GetColumn(toolbar));
            await Resize(window, 920, 1000);
            Assert.Equal(1, Grid.GetColumn(toolbar));
            await Resize(window, 920, 400);
            Assert.Equal(1, Grid.GetColumn(toolbar));
        }
        finally { window.Close(); }
    });

    private static async Task Resize(MainWindow window, double width, double height)
    {
        Assert.Equal(SizeToContent.Manual, window.SizeToContent);
        window.Width = width;
        window.Height = height;
        try { await Until(() => { window.UpdateLayout(); return window.ClientSize == new Size(width, height); }); }
        catch (OperationCanceledException)
        {
            Assert.Fail($"Resize requested {width}x{height}, properties {window.Width}x{window.Height}, client {window.ClientSize}, sizing {window.SizeToContent}");
        }
        window.UpdateLayout();
        Assert.Equal(new Size(width, height), window.ClientSize);
    }

    private static Rect BoundsInWindow(Control control, MainWindow window)
        => new(control.TranslatePoint(default, window)!.Value, control.Bounds.Size);

    private static void CaptureLayout(MainWindow window, string name)
    {
        if (Environment.GetEnvironmentVariable("COPPERSCREEN_LAYOUT_CAPTURE") is not { } directory) return;
        Directory.CreateDirectory(directory);
        using var bitmap = window.CaptureRenderedFrame();
        bitmap?.Save(Path.Combine(directory, name + ".png"));
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(2.0)]
    public Task ResizingMovesTheSameControlsAndKeepsUtilitiesBelowDisks(double scale) => UiAsync(async () =>
    {
        var window = Running();
        try
        {
            window.SetRenderScaling(scale);
            var toolbar = Named<Border>(window, "Emulator controls");
            var presenter = window.GetLogicalDescendants().OfType<FramebufferPresenter>().Single();
            var disk = Named<ComboBox>(window, "DF0 disk · choose from recent disks");
            var sound = Named<Button>(window, "Sound");
            var settings = Named<Button>(window, "Settings");
            var more = Named<Button>(window, "More");
            var source = presenter.EffectiveSourceSize;
            foreach (var size in new[] { new Size(1280, 720), new Size(712, 720), new Size(1600, 400), new Size(960, 480), new Size(1920, 1080) })
            {
                await Resize(window, size.Width, size.Height);
                var side = size.Width >= 920;
                var controls = BoundsInWindow(toolbar, window);
                var display = BoundsInWindow(presenter, window);
                var media = BoundsInWindow(disk, window);
                var utilities = BoundsInWindow(sound, window);
                Assert.Equal(side ? 1 : 0, Grid.GetColumn(toolbar));
                if (side)
                {
                    Assert.True(controls.Left >= display.Right - 1);
                    Assert.True(utilities.Top > media.Bottom);
                    Assert.True(BoundsInWindow(more, window).Bottom > window.ClientSize.Height - 70);
                }
                else Assert.True(controls.Bottom <= display.Top + 1);
                Assert.Same(sound, Named<Button>(window, "Sound"));
                Assert.Same(settings, Named<Button>(window, "Settings"));
                Assert.Same(disk, Named<ComboBox>(window, "DF0 disk · choose from recent disks"));
                foreach (var button in toolbar.GetLogicalDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible))
                {
                    var bounds = BoundsInWindow(button, window);
                    Assert.True(bounds.Left >= -1 && bounds.Right <= window.ClientSize.Width + 1 &&
                        bounds.Top >= -1 && bounds.Bottom <= window.ClientSize.Height + 1,
                        $"{AutomationProperties.GetName(button)} outside {window.ClientSize}: {bounds}");
                }
                Assert.Equal(source, presenter.EffectiveSourceSize);
                Assert.True(FramebufferPresenter.TryCalculateUniformDestination(presenter.Bounds.Size, source, scale, out var image));
                Assert.Equal(source.Width / source.Height, image.Width / image.Height, 6);
                CaptureLayout(window, $"layout-{size.Width}-{size.Height}-{scale}");
            }
            // Status updates and pause must not revert to content sizing.
            await (Task)typeof(MainWindow).GetMethod("ToggleRuntimePauseAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null)!;
            await Until(() => Runtime(window).CurrentState.IsPaused);
            Assert.Equal(SizeToContent.Manual, window.SizeToContent);
            Assert.Equal(new Size(1920, 1080), window.ClientSize);
            sound.Flyout!.ShowAt(sound);
            Assert.True(sound.Flyout.IsOpen);
            sound.Flyout.Hide();
        }
        finally { window.Close(); await Until(() => !window.IsVisible); }
    });

    [Theory]
    [InlineData(1280, 720)]
    [InlineData(712, 720)]
    public Task FullscreenControlsOverlayWithoutMovingOrResizingTheImage(int width, int height) => UiAsync(async () =>
    {
        var window = Running();
        try
        {
            await Resize(window, width, height);
            var toolbar = Named<Border>(window, "Emulator controls");
            var presenter = window.GetLogicalDescendants().OfType<FramebufferPresenter>().Single();
            Click(Named<Button>(window, "Fullscreen"));
            window.UpdateLayout();
            Assert.Equal(WindowState.FullScreen, window.WindowState);
            Assert.False(toolbar.IsVisible);
            var before = BoundsInWindow(presenter, window);
            Assert.True(presenter.TryGetRenderedImageCenter(out var center));
            Press(window, Key.F11);
            Release(window, Key.F11);
            window.UpdateLayout();
            Assert.True(toolbar.IsVisible);
            Assert.Equal(before, BoundsInWindow(presenter, window));
            Assert.True(presenter.TryGetRenderedImageCenter(out var shownCenter));
            Assert.Equal(center, shownCenter);
            CaptureLayout(window, $"fullscreen-controls-{width}");
            Press(window, Key.F11);
            Release(window, Key.F11);
            window.UpdateLayout();
            Assert.False(toolbar.IsVisible);
            Assert.Equal(before, BoundsInWindow(presenter, window));
            Press(window, Key.Enter, RawInputModifiers.Alt, PhysicalKey.Enter);
            Release(window, Key.Enter, RawInputModifiers.Alt, PhysicalKey.Enter);
            window.UpdateLayout();
            Assert.Equal(WindowState.Normal, window.WindowState);
            Assert.True(toolbar.IsVisible);
            Assert.Equal(new Size(width, height), window.ClientSize);
        }
        finally { window.Close(); await Until(() => !window.IsVisible); }
    });
}
