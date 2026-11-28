using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using CopperMod.Amiga.Lightweight;

namespace CopperScreen;

internal sealed partial class MainWindow
{
    private static readonly FilePickerFileType Cd32DiscFiles = new("CD32 discs")
    {
        Patterns = ["*.zip", "*.cue", "*.iso", "*.bin", "*.img"]
    };
    private static readonly FilePickerFileType Cd32RomFiles = new("CD32 ROMs")
    {
        Patterns = ["*.rom", "*.bin", "*.kick", "*.zip"]
    };

    private CopperScreenCd32Launch? _lastCd32Launch;
    private Grid _cd32Media = null!;
    private Control _cd32MediaActions = null!;
    private TextBlock _cd32DiscName = null!;
    private Button _mountCd32Disc = null!, _ejectCd32Disc = null!;
    private bool _changingCd32Disc;

    private async Task OpenCd32LauncherAsync()
    {
        if (_runtime != null || _applyingSettings) return;
        var launch = await ShowCd32LaunchDialogAsync();
        if (launch is null || _runtime != null || _applyingSettings) return;
        _lastCd32Launch = launch;
        _applyingSettings = true;
        UpdateSettingsStatus();
        if (_welcomeMessage != null) _welcomeMessage.Text = "Starting CD32…";
        try
        {
            var input = _committedSettings.Input;
            var nvram = Path.Combine(_recentSession.DataDirectory, "cd32.nvr");
            var replacement = await Task.Run(() => CopperScreenRuntime.CreateCd32(launch, input, AppContext.BaseDirectory, nvram));
            _runtime = replacement;
            StartAttachedRuntime(replacement, wasPaused: false);
            ClearSettingsStartupError();
            PresentNextFrame(forceStatus: true);
        }
        catch (Exception ex) when (IsCd32MediaError(ex)) { SetSettingsError(ex.Message); }
        finally { _applyingSettings = false; UpdateSettingsStatus(); UpdateToolbarStatus(); }
    }

    private Task<CopperScreenCd32Launch?> ShowCd32LaunchDialogAsync()
    {
        var dialog = CreateCd32Dialog("Launch CD32 (Experimental)");
        var previous = _lastCd32Launch;
        var kickstart = new TextBox { Text = previous?.KickstartPath };
        var extension = new TextBox { Text = previous?.ExtendedRomPath };
        var disc = new TextBox { Text = previous?.DiscPath };
        var region = new ComboBox { ItemsSource = new[] { "PAL", "NTSC" }, SelectedIndex = previous?.Region == LightweightCd32Region.Ntsc ? 1 : 0 };
        int[] fastSizes = [0, 512, 1024, 2048, 4096, 8192];
        var fastRam = new ComboBox
        {
            ItemsSource = new[] { "None", "512 KiB", "1 MiB", "2 MiB", "4 MiB", "8 MiB" },
            SelectedIndex = Array.IndexOf(fastSizes, previous?.FastRamKiB ?? 0)
        };
        var error = new TextBlock { Foreground = CopperScreenAppearance.Text, TextWrapping = TextWrapping.Wrap };
        var start = CreatePanelButton("Start CD32", () =>
        {
            if (string.IsNullOrWhiteSpace(kickstart.Text)) { error.Text = "Choose a CD32 ROM."; return; }
            dialog.Close(new CopperScreenCd32Launch(kickstart.Text.Trim(), OptionalPath(extension.Text), OptionalPath(disc.Text),
                region.SelectedIndex == 1 ? LightweightCd32Region.Ntsc : LightweightCd32Region.Pal, fastSizes[fastRam.SelectedIndex]));
        });
        MakePrimary(start);
        var cancel = CreatePanelButton("Cancel", () => dialog.Close(null));
        LabelField(region, "Video standard");
        LabelField(fastRam, "Fast RAM");
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(22), Spacing = 10,
            Children =
            {
                SettingsNote("68EC020 · 2 MiB Chip RAM"),
                FileRow("Stock CD32 or CopperStart Full ROM", kickstart, () => PickCd32LocalFileAsync(dialog, "Choose CD32 ROM", Cd32RomFiles)),
                FileRow("CD32 extended ROM (for split ROMs)", extension, () => PickCd32LocalFileAsync(dialog, "Choose CD32 extended ROM", Cd32RomFiles)),
                FileRow("CD32 disc (optional)", disc, () => PickCd32DiscPathAsync(dialog)),
                CommandGroup(new StackPanel { Children = { SectionLabel("Video standard"), region } },
                    new StackPanel { Children = { SectionLabel("Fast RAM"), fastRam } }),
                error, CommandGroup(start, cancel)
            }
        };
        return dialog.ShowDialog<CopperScreenCd32Launch?>(this);

        Control FileRow(string label, TextBox field, Func<Task<string?>> pick)
        {
            LabelField(field, label);
            var browse = CreatePanelButton("Browse…", async () =>
            {
                try { if (await pick() is { } path) { field.Text = path; error.Text = null; } }
                catch (Exception ex) when (IsCd32MediaError(ex)) { error.Text = ex.Message; }
            });
            AutomationProperties.SetName(browse, "Browse for " + label);
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8, Children = { field, browse } };
            Grid.SetColumn(browse, 1);
            return new StackPanel { Spacing = 4, Children = { SectionLabel(label), row } };
        }

        static void LabelField(Control field, string label) => AutomationProperties.SetName(field, label);
        static string? OptionalPath(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static async Task<string?> PickCd32LocalFileAsync(Window owner, string title, FilePickerFileType filter)
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title, AllowMultiple = false, FileTypeFilter = [filter, FilePickerFileTypes.All]
        });
        if (files.Count == 0) return null;
        return files[0].TryGetLocalPath() ?? throw new IOException("Choose a file on a local or mounted drive.");
    }

    private async Task<string?> PickCd32DiscPathAsync(Window owner)
    {
        var path = await PickCd32LocalFileAsync(owner, "Choose CD32 disc or ZIP", Cd32DiscFiles);
        if (path == null || !Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase)) return path;
        var discs = await Task.Run(() => LightweightA500Machine.ListCd32ArchiveDiscs(path));
        if (discs.Count == 0) throw new InvalidDataException("The ZIP contains no supported CD32 disc image.");
        if (discs.Count == 1) return path;

        var dialog = CreateCd32Dialog("Choose a disc in " + Path.GetFileName(path));
        var choices = new ComboBox { ItemsSource = discs, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetName(choices, "CD32 disc in ZIP");
        var select = CreatePanelButton("Select disc", () => dialog.Close(choices.SelectedItem as string));
        MakePrimary(select);
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(22), Spacing = 12,
            Children = { choices, CommandGroup(select, CreatePanelButton("Cancel", () => dialog.Close(null))) }
        };
        var entry = await dialog.ShowDialog<string?>(owner);
        return entry is null ? null : path + "#/" + entry;
    }

    private Window CreateCd32Dialog(string title) => new()
    {
        Title = title, Width = 610, CanResize = false, SizeToContent = SizeToContent.Height,
        WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Surface
    };

    private Grid CreateCd32MediaControls()
    {
        _cd32DiscName = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        AutomationProperties.SetName(_cd32DiscName, "Current CD32 disc");
        _mountCd32Disc = CreateToolbarButton("Mount CD…", () => _ = ChangeCd32DiscAsync(eject: false), "Mount a CD32 disc image or ZIP");
        _ejectCd32Disc = CreateToolbarButton("Eject CD", () => _ = ChangeCd32DiscAsync(eject: true), "Eject the current CD32 disc");
        var media = new Grid { IsVisible = false, ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 12, RowSpacing = 8 };
        _cd32MediaActions = CommandGroup(_mountCd32Disc, _ejectCd32Disc);
        media.Children.Add(SectionLabel("CD32 disc"));
        media.Children.Add(_cd32DiscName);
        media.Children.Add(_cd32MediaActions);
        Grid.SetColumn(_cd32DiscName, 1);
        Grid.SetColumn(_cd32MediaActions, 2);
        return media;
    }

    private void LayoutCd32MediaControls(bool side)
    {
        _cd32Media.ColumnDefinitions = new ColumnDefinitions(side ? "*" : "Auto,*,Auto");
        _cd32Media.RowDefinitions = new RowDefinitions(side ? "Auto,Auto,Auto" : "Auto");
        Grid.SetColumn(_cd32DiscName, side ? 0 : 1);
        Grid.SetRow(_cd32DiscName, side ? 1 : 0);
        Grid.SetColumn(_cd32MediaActions, side ? 0 : 2);
        Grid.SetRow(_cd32MediaActions, side ? 2 : 0);
    }

    private void UpdateCd32MediaControls(bool cd32, string? path)
    {
        _cd32Media.IsVisible = cd32;
        if (!cd32) return;
        _cd32DiscName.Text = path == null ? "No disc" : CopperScreenDiskImageArchive.GetDisplayName(path);
        ToolTip.SetTip(_cd32DiscName, path);
        _mountCd32Disc.IsEnabled = !_changingCd32Disc && !_applyingSettings && !_settingsVisible && !_checkingDiskChanges;
        _ejectCd32Disc.IsEnabled = _mountCd32Disc.IsEnabled && path != null;
    }

    private async Task ChangeCd32DiscAsync(bool eject)
    {
        if (_runtime is not { IsCd32: true } runtime || _changingCd32Disc) return;
        _changingCd32Disc = true;
        UpdateToolbarStatus();
        try
        {
            ReleaseInteractiveInput();
            var path = eject ? null : await PickCd32DiscPathAsync(this);
            if ((!eject && path is null) || _runtime != runtime) return;
            var result = await runtime.ChangeCd32DiscAsync(path);
            if (_runtime == runtime)
            {
                if (result.Success && _lastCd32Launch != null) _lastCd32Launch = _lastCd32Launch with { DiscPath = path };
                ShowDiskFeedback(result.Message, result.Success);
            }
        }
        catch (Exception ex) when (IsCd32MediaError(ex)) { ShowDiskFeedback(ex.Message, false); }
        finally { _changingCd32Disc = false; UpdateToolbarStatus(); }
    }

    private static bool IsCd32MediaError(Exception ex) => ex is IOException or InvalidDataException or
        UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException or OverflowException;
}
