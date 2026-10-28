using System.Buffers.Binary;
using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.VisualTree;
using CopperScreen;

namespace CopperScreen.Tests;

// Headless desktop controls. These do not certify physical devices, monitors or accessibility tools.
[Collection("Desktop UI")]
public sealed partial class DesktopQualificationTests(LaunchUiFixture ui) : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "copperscreen-desktop-" + Guid.NewGuid().ToString("N"));
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
    [InlineData(false)]
    [InlineData(true)]
    public Task FocusLossAndReleaseCommandClearHeldJoystickWithoutAKeyUp(bool loseFocus) => UiAsync(async () =>
    {
        var window = Running();
        try
        {
            var presenter = window.GetLogicalDescendants().OfType<FramebufferPresenter>().Single();
            Assert.True(presenter.Focus());
            Assert.True(presenter.IsKeyboardFocusWithin);
            Press(window, Key.NumPad5, RawInputModifiers.None, PhysicalKey.NumPad5);
            await Until(() => Runtime(window).CurrentState.IsPrimaryFirePressed);
            if (loseFocus)
            {
                var deactivate = Assert.IsType<Action>(typeof(IWindowBaseImpl).GetProperty("Deactivated")!.GetValue(window.PlatformImpl));
                deactivate();
            }
            else
            {
                var more = Named<Button>(window, "More");
                var content = Assert.IsAssignableFrom<Control>(Assert.IsType<Flyout>(more.Flyout).Content);
                // Invoke the command while input is still held. Opening the flyout can
                // move focus off the display and release it before the command runs.
                Assert.True(presenter.IsKeyboardFocusWithin);
                Assert.True(Runtime(window).CurrentState.IsPrimaryFirePressed);
                Click(content.GetLogicalDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Release input"));
            }
            await Until(() => !Runtime(window).CurrentState.IsPrimaryFirePressed);
            Assert.Null(Runtime(window).CurrentState.FaultMessage);
            // A new key-down must work after release, even though the first key-up was lost.
            Assert.True(presenter.Focus());
            Assert.True(presenter.IsKeyboardFocusWithin);
            Press(window, Key.NumPad5, RawInputModifiers.None, PhysicalKey.NumPad5);
            await Until(() => Runtime(window).CurrentState.IsPrimaryFirePressed);
            Release(window, Key.NumPad5, RawInputModifiers.None, PhysicalKey.NumPad5);
            await Until(() => !Runtime(window).CurrentState.IsPrimaryFirePressed);
        }
        finally { window.Close(); await Until(() => !window.IsVisible); }
    });

    [Fact]
    public Task AltEnterRoundTripKeepsTheSessionAndKeyboardPauseReachable() => UiAsync(async () =>
    {
        var window = Running();
        try
        {
            var runtime = Runtime(window);
            window.Focus();
            Press(window, Key.Enter, RawInputModifiers.Alt, PhysicalKey.Enter);
            Release(window, Key.Enter, RawInputModifiers.Alt, PhysicalKey.Enter);
            Assert.Equal(WindowState.FullScreen, window.WindowState);
            Press(window, Key.Enter, RawInputModifiers.Alt, PhysicalKey.Enter);
            Release(window, Key.Enter, RawInputModifiers.Alt, PhysicalKey.Enter);
            Assert.Equal(WindowState.Normal, window.WindowState);
            Assert.Same(runtime, Runtime(window));
            var pause = Named<Button>(window, "Pause");
            pause.Focus();
            Press(window, Key.Space); Release(window, Key.Space);
            await Until(() => window.GetLogicalDescendants().OfType<Button>().Any(b => AutomationProperties.GetName(b) == "Resume"));
            Press(window, Key.Space); Release(window, Key.Space);
            await Until(() => window.GetLogicalDescendants().OfType<Button>().Any(b => AutomationProperties.GetName(b) == "Pause"));
        }
        finally { window.Close(); await Until(() => !window.IsVisible); }
    });

    [Fact]
    public Task LauncherCanReachBootChoiceAndSetupUsingOnlyKeys() => Ui(() =>
    {
        var window = new MainWindow([], new CopperScreenRecentSession(_root)); window.Show();
        try
        {
            var boot = Named<ComboBox>(window, "Start configuration");
            boot.Focus();
            Press(window, Key.Down); Release(window, Key.Down);
            Assert.Equal(1, boot.SelectedIndex);
            var reached = new HashSet<string>();
            for (var i = 0; i < 40; i++)
            {
                Press(window, Key.Tab); Release(window, Key.Tab);
                if (window.FocusManager?.GetFocusedElement() is Control focused)
                    reached.Add(ControlAutomationPeer.CreatePeerForElement(focused)?.GetName() ?? "");
            }
            Assert.Contains("Choose disk…", reached);
            Assert.Contains("Configure Amiga…", reached);
        }
        finally { window.Close(); }
    });

    [Theory]
    [InlineData(800, 600, 1.0)]
    [InlineData(800, 600, 1.25)]
    [InlineData(1280, 720, 1.5)]
    [InlineData(960, 640, 2.0)]
    public Task LauncherScrollsAtBoundedClientSizesAndScaling(int width, int height, double scale) => UiAsync(async () =>
    {
        var window = new MainWindow([], new CopperScreenRecentSession(_root)); window.Show();
        try
        {
            window.SetRenderScaling(scale);
            window.SizeToContent = SizeToContent.Manual; window.Width = width; window.Height = height;
            Named<ComboBox>(window, "Start configuration").SelectedIndex = 3;
            window.GetLogicalDescendants().OfType<Expander>().Single(e => e.Header is "Saved launch profiles").IsExpanded = true;
            await Task.Delay(50); // Platform resize/scaling notifications arrive after the property setters.
            window.UpdateLayout();
            var scroll = window.GetLogicalDescendants().OfType<ScrollViewer>().Single(s => s.Name == "LauncherScroll");
            Assert.True(scroll.Viewport.Width > 0 && scroll.Viewport.Height > 0);
            Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 1);
            var viewportPosition = scroll.TranslatePoint(default, window)!.Value;
            Assert.True(viewportPosition.Y >= 0 && viewportPosition.Y + scroll.Bounds.Height <= window.ClientSize.Height + 1,
                $"Launcher viewport {viewportPosition}/{scroll.Bounds.Size} extends outside client {window.ClientSize}.");
            var save = window.GetLogicalDescendants().OfType<Button>().Single(b => b.Content is "Save launch profile");
            Assert.True(save.IsEffectivelyVisible);
            Assert.True(save.Bounds.Width > 0 && save.Bounds.Height > 0, "Saved-profile action must have a rendered hit target.");
            save.BringIntoView();
            await Task.Delay(50); // Allow the routed scroll request and a render tick to settle.
            window.UpdateLayout();
            var position = save.TranslatePoint(default, scroll)!.Value;
            if (Environment.GetEnvironmentVariable("COPPERSCREEN_DESKTOP_CAPTURE") is { } debugDirectory)
            {
                Directory.CreateDirectory(debugDirectory);
                File.WriteAllText(Path.Combine(debugDirectory, $"layout-{width}-{height}-{scale}.json"), System.Text.Json.JsonSerializer.Serialize(new {
                    client = window.ClientSize.ToString(), viewport = scroll.Viewport.ToString(), extent = scroll.Extent.ToString(), offset = scroll.Offset.ToString(),
                    bounds = scroll.Bounds.ToString(), target = save.Bounds.ToString(), position = position.ToString(),
                    ancestors = save.GetVisualAncestors().Select(v => v.GetType().Name).ToArray()
                }));
            }
            Assert.InRange(position.Y, -1, scroll.Bounds.Height - save.Bounds.Height + 1);
            var clientPosition = save.TranslatePoint(default, window)!.Value;
            Assert.InRange(clientPosition.Y, 0, window.ClientSize.Height - save.Bounds.Height + 1);
            if (Environment.GetEnvironmentVariable("COPPERSCREEN_DESKTOP_CAPTURE") is { } directory)
            {
                Directory.CreateDirectory(directory);
                using var frame = window.CaptureRenderedFrame();
                frame?.Save(Path.Combine(directory, $"launcher-{width}x{height}-{scale:0.##}.png"));
            }
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task MainTextPaletteMeetsFourPointFiveContrastOnItsDeclaredSurfaces() => Ui(() =>
    {
        static double Luminance(IBrush brush)
        {
            var color = Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;
            static double Linear(byte value) { var v = value / 255.0; return v <= .04045 ? v / 12.92 : Math.Pow((v + .055) / 1.055, 2.4); }
            return .2126 * Linear(color.R) + .7152 * Linear(color.G) + .0722 * Linear(color.B);
        }
        foreach (var (text, surface) in new[] {
            (CopperScreenAppearance.Text, CopperScreenAppearance.Surface),
            (CopperScreenAppearance.MutedText, CopperScreenAppearance.Background),
            (CopperScreenAppearance.MutedText, CopperScreenAppearance.ControlSurface),
            (CopperScreenAppearance.OnAccent, CopperScreenAppearance.Accent),
            (CopperScreenAppearance.ProtectedText, CopperScreenAppearance.ProtectedSurface) })
        {
            var a = Luminance(text); var b = Luminance(surface);
            Assert.True((Math.Max(a, b) + .05) / (Math.Min(a, b) + .05) >= 4.5);
        }
    });
}
