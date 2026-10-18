using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using CopperScreen;

namespace CopperScreen.Tests;

// Keep real-window dispatcher initialization in a separate process from host diagnostics.
public static class LaunchTestApp
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

public sealed class LaunchUiFixture : IDisposable
{
    public HeadlessUnitTestSession Session { get; } = HeadlessUnitTestSession.StartNew(typeof(LaunchTestApp), AvaloniaTestIsolationLevel.PerAssembly);
    public void Dispose() => Session.Dispose();
}

[CollectionDefinition("Desktop UI", DisableParallelization = true)]
public sealed class LaunchUiCollection : ICollectionFixture<LaunchUiFixture> { }

[Collection("Desktop UI")]
public sealed partial class LaunchWorkflowTests : IDisposable
{
    private readonly LaunchUiFixture _ui;
    private readonly string _root = Path.Combine(Path.GetTempPath(), "copperscreen-launch-" + Guid.NewGuid().ToString("N"));
    public LaunchWorkflowTests(LaunchUiFixture ui) { _ui = ui; Directory.CreateDirectory(_root); }
    public void Dispose() => Directory.Delete(_root, true);
    private async Task Ui(Action action) => await _ui.Session.Dispatch(action, CancellationToken.None);

    [Theory]
    [InlineData("Kickstart13Rom")]
    [InlineData("MinimalDiskBoot")]
    public Task LauncherFitsTheClientAreaOnItsFirstLayout(string source) => Ui(() =>
    {
        new CopperScreenRecentSession(_root).Save(new() { KickstartSource = Enum.Parse<CopperScreenKickstartSource>(source) }, 100, false);
        var window = new MainWindow([], new CopperScreenRecentSession(_root));
        Size? openedSize = null;
        Rect? openedBounds = null;
        window.Opened += (_, _) =>
        {
            openedSize = window.ClientSize;
            openedBounds = ((Grid)window.Content!).Bounds;
        };
        window.Show();
        try
        {
            var root = Assert.IsType<Grid>(window.Content);
            var firstSize = window.ClientSize;
            var firstBounds = root.Bounds;
            Assert.Equal(openedSize, openedBounds?.Size);
            window.UpdateLayout();
            Assert.True(firstBounds.Size == firstSize && firstSize == window.ClientSize,
                $"First client {firstSize}, content {firstBounds}; settled client {window.ClientSize}, content {root.Bounds}");
        }
        finally { window.Close(); }
    });

}
