using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed partial class LaunchWorkflowTests
{
    [Fact]
    public Task ImageLayoutHintUpdatesAndWrapsAtMinimumSettingsSize() => UiAsync(async () =>
    {
        var window = new MainWindow([], new CopperScreenRecentSession(_root));
        window.Show();
        try
        {
            Click(Button(window, "Configure Amiga…"));
            var settings = SettingsField<Window>(window, "_settingsWindow");
            settings.Width = 780;
            settings.Height = 620;
            var draft = MenuDraft(window);
            draft.HardDrives.Add(new(0, Path.Combine(_root, "Workbench.hdf"), true, 0));
            draft.KickstartSource = CopperScreenKickstartSource.KickstartRom;
            SetMenuDraft(window, draft);
            var rows = SettingsField<StackPanel>(window, "_hardfileRows");
            ComboBox LayoutChoice() => rows.GetLogicalDescendants().OfType<ComboBox>().Single();
            Assert.Contains("Otherwise it mounts", Avalonia.Automation.AutomationProperties.GetHelpText(LayoutChoice()));

            draft.KickstartSource = CopperScreenKickstartSource.CopperStart;
            SetMenuDraft(window, draft);
            SettingsCall(window, "SelectSettingsPage", 3);
            var tabs = Assert.IsType<TabControl>(SettingsField<Control[]>(window, "_settingsPages")[3]);
            tabs.SelectedIndex = 1;
            rows.GetLogicalDescendants().OfType<Expander>().Single(e => e.Header is "Disk layout and partition settings").IsExpanded = true;
            var mode = LayoutChoice();
            mode.SelectedItem = "RDB";
            Assert.Contains("boot priorities", Avalonia.Automation.AutomationProperties.GetHelpText(mode));
            mode.SelectedItem = "Partition";
            Assert.Contains("geometry and filesystem settings below", Avalonia.Automation.AutomationProperties.GetHelpText(mode));
            mode.SelectedItem = "Auto";
            var hint = rows.GetLogicalDescendants().OfType<TextBlock>().Single(t => t.Text == Avalonia.Automation.AutomationProperties.GetHelpText(mode));
            Assert.Contains("CopperStart requires an explicit layout", hint.Text);
            settings.UpdateLayout();
            await Task.Delay(100);
            settings.UpdateLayout();
            var scroll = Assert.IsType<ScrollViewer>(tabs.SelectedContent);
            var content = Assert.IsAssignableFrom<Control>(scroll.Content);
            scroll.Offset = new Vector(0, Math.Max(0, mode.TranslatePoint(default, content)!.Value.Y - 80));
            settings.UpdateLayout();
            var hintTop = hint.TranslatePoint(default, scroll)!.Value.Y;
            Assert.InRange(hintTop, 0, scroll.Viewport.Height - hint.Bounds.Height);
            Assert.True(hint.TextLayout.TextLines.Count > 1);
            Assert.True(hint.TextLayout.Height <= hint.Bounds.Height + 1);
            Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 1);
            var directory = Environment.GetEnvironmentVariable("COPPERSCREEN_SETTINGS_CAPTURE");
            if (directory != null)
            {
                Directory.CreateDirectory(directory);
                using var bitmap = new RenderTargetBitmap(new PixelSize(780, 620));
                bitmap.Render(settings);
                bitmap.Save(Path.Combine(directory, "minimum-image-layout.png"));
            }
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task MachineRamHintUpdatesAndWrapsAtMinimumSettingsSize() => UiAsync(async () =>
    {
        var window = new MainWindow([], new CopperScreenRecentSession(_root));
        window.Show();
        try
        {
            window.SelectOriginalRom(DefaultRomFixture(0, "machine-ram-hint"));
            Click(Button(window, "Configure Amiga…"));
            var settings = SettingsField<Window>(window, "_settingsWindow");
            settings.Width = settings.MinWidth;
            settings.Height = settings.MinHeight;
            SettingsCall(window, "SelectSettingsPage", 1);
            var page = SettingsField<Control[]>(window, "_settingsPages")[1];
            var options = page.GetLogicalDescendants().OfType<Expander>().Single(e => e.Header is "Optional features");
            options.IsExpanded = true;
            var hint = options.GetLogicalDescendants().OfType<TextBlock>().Single(t => Avalonia.Automation.AutomationProperties.GetName(t) == "RAM requirements");
            var draft = CopperScreenMachinePresets.Apply(MenuDraft(window), CopperScreenMachineModel.Amiga500Ecs);
            SetMenuDraft(window, draft);
            Assert.Contains("Slow RAM requires 512 KiB", hint.Text);
            Assert.Contains("CPU supports up to 8 MiB", hint.Text);
            SettingsField<ComboBox>(window, "_cpuBackendBox").SelectedItem = "AccurateM68020";
            Assert.Contains("above 8 MiB requires Kickstart 3.x", hint.Text);
            settings.UpdateLayout();
            hint.BringIntoView();
            await Task.Delay(100);
            settings.UpdateLayout();
            Assert.True(hint.TextLayout.TextLines.Count > 1);
            Assert.True(hint.TextLayout.Height <= hint.Bounds.Height + 1);
            var scroll = Assert.IsType<ScrollViewer>(page);
            Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 1);
            var directory = Environment.GetEnvironmentVariable("COPPERSCREEN_SETTINGS_CAPTURE");
            if (directory != null)
            {
                Directory.CreateDirectory(directory);
                using var bitmap = new RenderTargetBitmap(new PixelSize((int)settings.ClientSize.Width, (int)settings.ClientSize.Height));
                bitmap.Render(settings);
                bitmap.Save(Path.Combine(directory, "minimum-machine-ram.png"));
            }
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task UnavailableProfileReasonWrapsAtMinimumSettingsSize() => UiAsync(async () =>
    {
        var window = new MainWindow([], new CopperScreenRecentSession(_root));
        window.Show();
        try
        {
            Click(Button(window, "Configure Amiga…"));
            var settings = SettingsField<Window>(window, "_settingsWindow");
            settings.Width = settings.MinWidth;
            settings.Height = settings.MinHeight;
            SettingsCall(window, "SelectSettingsPage", 0);
            var list = SettingsField<ListBox>(window, "_profileList");
            const string reason = "This profile requires a Kickstart ROM that could not be found in the configured ROM directory. Choose a compatible ROM before loading this configuration.";
            list.ItemsSource = new[] { new CopperScreenProfileSummary("missing", "Missing ROM", "", reason) };
            settings.UpdateLayout();
            await Task.Delay(40);
            var row = Assert.IsType<ListBoxItem>(list.ContainerFromIndex(0));
            var hint = Assert.Single(row.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == reason);
            Assert.False(row.IsEnabled);
            Assert.True(hint.TextLayout.TextLines.Count > 1);
            Assert.True(hint.TextLayout.Height <= hint.Bounds.Height + 1);
            Assert.True(hint.TranslatePoint(default, list)!.Value.X + hint.Bounds.Width <= list.Bounds.Width);
            var directory = Environment.GetEnvironmentVariable("COPPERSCREEN_SETTINGS_CAPTURE");
            if (directory != null)
            {
                Directory.CreateDirectory(directory);
                using var bitmap = new RenderTargetBitmap(new PixelSize((int)settings.ClientSize.Width, (int)settings.ClientSize.Height));
                bitmap.Render(settings);
                bitmap.Save(Path.Combine(directory, "minimum-profile-reason.png"));
            }
        }
        finally { window.Close(); }
    });

    [Theory]
    [InlineData(980, 760, 1.0)]
    [InlineData(980, 760, 1.25)]
    [InlineData(980, 760, 2.0)]
    [InlineData(780, 620, 1.0)]
    [InlineData(780, 620, 1.25)]
    [InlineData(780, 620, 2.0)]
    public Task SettingsPagesFitAndCanBeCaptured(int width, int height, double scale) => UiAsync(async () =>
    {
        var window = new MainWindow([], new CopperScreenRecentSession(_root)); window.Show();
        try
        {
            window.SelectOriginalRom(DefaultRomFixture(0, "settings-layout"));
            var draft = MenuDraft(window);
            Click(Button(window, "Configure Amiga…"));
            var settings = SettingsField<Window>(window, "_settingsWindow");
            settings.Width = width; settings.Height = height; settings.SetRenderScaling(scale);
            var pages = SettingsField<Control[]>(window, "_settingsPages");
            var directory = Environment.GetEnvironmentVariable("COPPERSCREEN_SETTINGS_CAPTURE");
            if (directory != null) Directory.CreateDirectory(directory);
            var views = pages.SelectMany((control, index) => control is TabControl tabs
                ? Enumerable.Range(0, tabs.Items.Count).Select(tab => (Page: index, Tab: tab))
                : [(Page: index, Tab: -1)]);
            foreach (var (page, tab) in views)
            {
                typeof(MainWindow).GetMethod("SelectSettingsPage", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [page]);
                if (pages[page] is TabControl tabs) tabs.SelectedIndex = tab;
                settings.UpdateLayout();
                var scroll = Assert.IsType<ScrollViewer>(pages[page] is TabControl selected ? selected.SelectedContent : pages[page]);
                scroll.Offset = default;
                settings.UpdateLayout();
                await Task.Delay(40);
                Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 1, $"Page {page} overflows horizontally: {scroll.Extent} / {scroll.Viewport}");
                foreach (var field in pages[page].GetLogicalDescendants().OfType<Control>()
                    .Where(c => c.IsEffectivelyVisible && c is TextBox or ComboBox or Slider))
                {
                    var origin = field.TranslatePoint(default, settings);
                    if (origin is { } point)
                        Assert.True(point.X >= 0 && point.X + field.Bounds.Width <= settings.ClientSize.Width + 1,
                            $"Page {page}: {field.GetType().Name} outside settings width");
                }
                if (directory == null) continue;
                var prefix = $"{width}-{height}-{scale}-page-{page}" + (tab < 0 ? "" : $"-tab-{tab}");
                var visibleText = pages[page].GetVisualDescendants().OfType<TextBlock>()
                    .Where(t => t.IsEffectivelyVisible).Select(t => t.Text).ToArray();
                File.WriteAllText(Path.Combine(directory, prefix + ".json"), JsonSerializer.Serialize(visibleText));
                var step = 0;
                do
                {
                    settings.UpdateLayout();
                    await Task.Delay(40);
                    // Render the entire window; compositor damage frames can omit unchanged headers.
                    using var bitmap = new RenderTargetBitmap(
                        new PixelSize((int)Math.Ceiling(settings.ClientSize.Width * scale), (int)Math.Ceiling(settings.ClientSize.Height * scale)),
                        new Vector(96 * scale, 96 * scale));
                    bitmap.Render(settings);
                    bitmap.Save(Path.Combine(directory, prefix + $"-{step++}.png"));
                    if (scroll.Offset.Y >= scroll.Extent.Height - scroll.Viewport.Height - 1) break;
                    scroll.Offset = new Vector(0, Math.Min(scroll.Offset.Y + scroll.Viewport.Height * .8, scroll.Extent.Height - scroll.Viewport.Height));
                } while (step < 20);
            }
            // Expanded explanations and populated storage must also fit the narrow layout.
            draft = MenuDraft(window);
            draft.HardDrives.Add(new(0, Path.Combine(_root, "long-hard-disk-name-for-layout.hdf"), false, 0));
            draft.HostDirectories.Add(new("HOST0", "Shared", _root, false));
            SetMenuDraft(window, draft);
            foreach (var page in new[] { 1, 2, 3 })
            {
                SettingsCall(window, "SelectSettingsPage", page);
                if (pages[page] is TabControl tabs) tabs.SelectedIndex = 1;
                foreach (var expander in pages[page].GetLogicalDescendants().OfType<Expander>()) expander.IsExpanded = true;
                settings.UpdateLayout();
                await Task.Delay(40);
                var scroll = Assert.IsType<ScrollViewer>(pages[page] is TabControl selected ? selected.SelectedContent : pages[page]);
                Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 1, $"Expanded page {page} overflows horizontally");
                if (directory != null && page == 3)
                {
                    foreach (var expander in pages[page].GetLogicalDescendants().OfType<Expander>()) expander.IsExpanded = false;
                    foreach (var rows in new[] { "_hardfileRows", "_sharedFolderRows" })
                        foreach (var expander in SettingsField<StackPanel>(window, rows).Children.OfType<Expander>()) expander.IsExpanded = true;
                    settings.UpdateLayout();
                    using var bitmap = new RenderTargetBitmap(
                        new PixelSize((int)Math.Ceiling(settings.ClientSize.Width * scale), (int)Math.Ceiling(settings.ClientSize.Height * scale)),
                        new Vector(96 * scale, 96 * scale));
                    bitmap.Render(settings);
                    bitmap.Save(Path.Combine(directory, $"{width}-{height}-{scale}-storage-populated.png"));
                }
            }
        }
        finally { window.Close(); }
    });
}
