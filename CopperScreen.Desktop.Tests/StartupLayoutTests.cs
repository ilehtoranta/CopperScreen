using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
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
    [Theory]
    [InlineData(1040, 1.0)]
    [InlineData(1040, 1.25)]
    [InlineData(1040, 2.0)]
    [InlineData(712, 1.0)]
    [InlineData(712, 1.25)]
    [InlineData(712, 2.0)]
    public Task ModelSelectionKeepsTheLaunchCardAndSelectorWidth(int width, double scale) => CheckModelSelectionLayout(width, scale, includeCopperOs: false);

    [CopperStartTheory]
    [InlineData(1040, 1.0)]
    [InlineData(1040, 1.25)]
    [InlineData(1040, 2.0)]
    [InlineData(712, 1.0)]
    [InlineData(712, 1.25)]
    [InlineData(712, 2.0)]
    public Task CopperStartModelSelectionKeepsTheLaunchCardAndSelectorWidth(int width, double scale) => CheckModelSelectionLayout(width, scale, includeCopperOs: true);

    private Task CheckModelSelectionLayout(int width, double scale, bool includeCopperOs) => UiAsync(async () =>
    {
        var window = new MainWindow([], new CopperScreenRecentSession(_root)); window.Show();
        try
        {
            window.Width = width; window.Height = 760; window.SetRenderScaling(scale);
            var selector = Named<ComboBox>(window, "Amiga model");
            window.UpdateLayout();
            await Task.Delay(40);
            var card = selector.GetLogicalAncestors().OfType<Border>().First();
            var selectorWidth = selector.Bounds.Width;
            var cardWidth = card.Bounds.Width;
            var cardHeight = card.Bounds.Height;
            var position = selector.TranslatePoint(default, window)!.Value.X;
            Assert.True(selectorWidth > 0);
            foreach (var model in includeCopperOs ? new[] { 1, 2, 3, 4, 5, 0 } : new[] { 1, 2, 0 })
            {
                LaunchSelection.Select(window, model);
                window.UpdateLayout();
                await Task.Delay(40);
                Assert.Equal(selectorWidth, selector.Bounds.Width, 2);
                Assert.Equal(cardWidth, card.Bounds.Width, 2);
                Assert.Equal(cardHeight, card.Bounds.Height, 2);
                Assert.Equal(position, selector.TranslatePoint(default, window)!.Value.X, 2);
                Assert.True(position >= 0 && position + selector.Bounds.Width <= window.ClientSize.Width);
            }
        }
        finally { window.Close(); }
    });

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
