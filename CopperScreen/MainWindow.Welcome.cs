using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace CopperScreen;

internal sealed partial class MainWindow
{
    private readonly CopperScreenRecentSession _recentSession;
    private Button _welcomeStart = null!, _welcomeRomButton = null!, _welcomeDiskButton = null!;
    private TextBlock _welcomeRom = null!, _welcomeDisk = null!, _welcomeMessage = null!, _welcomeMachine = null!;
    private StackPanel _welcomeRecent = null!;
    private ComboBox _welcomeBootMethod = null!;
    private Control? _bootRomRow, _welcomeRomRow;
    private Grid _welcomeDiskRow = null!;
    private bool _updatingBootMethod;
    private CopperScreenKickstartSource _welcomeNativeBootSource = CopperScreenKickstartSource.Kickstart13Rom;

    private Border CreateIdlePanel()
    {
        if (_settingsDraft.KickstartSource == CopperScreenKickstartSource.MinimalDiskBoot)
            _welcomeNativeBootSource = _settingsDraft.RomVersion switch
            {
                KickstartVersion.Kickstart31 => CopperScreenKickstartSource.Kickstart31Rom,
                KickstartVersion.Kickstart30 => CopperScreenKickstartSource.KickstartRom,
                _ => CopperScreenKickstartSource.Kickstart13Rom
            };
        var content = new StackPanel { Spacing = 16, MaxWidth = 570, Margin = new Thickness(32), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(new TextBlock { Text = "COPPERSCREEN", Foreground = Accent, FontSize = 12, FontWeight = FontWeight.SemiBold, LetterSpacing = 2 });
        content.Children.Add(new TextBlock { Text = "Start your Amiga", Foreground = CopperScreenAppearance.Text, FontSize = 32, FontWeight = FontWeight.SemiBold });
        content.Children.Add(new TextBlock { Text = "Choose how to boot, then select a disk image.", Foreground = MutedText, FontSize = 15, TextWrapping = TextWrapping.Wrap, LineHeight = 23 });
        _welcomeBootMethod = new ComboBox { ItemsSource = new[] { "Kickstart ROM", "Minimal disk boot (experimental)" }, HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetName(_welcomeBootMethod, "Boot method");
        _welcomeBootMethod.SelectionChanged += (_, _) =>
        {
            if (_updatingBootMethod || _kickstartSourceBox == null || _settingsVisible || _applyingSettings) return;
            _kickstartSourceBox.SelectedItem = _welcomeBootMethod.SelectedIndex == 1 ? "MinimalDiskBoot" : _welcomeNativeBootSource.ToString();
            _settingsDraft = ReadSettingsDraft();
            _committedSettings = _settingsDraft.Clone();
            _latestState = CreateIdleState(_settingsDraft);
            UpdateSettingsStatus();
            UpdateToolbarStatus();
        };
        _welcomeMachine = new TextBlock { Foreground = MutedText, FontSize = 12, TextWrapping = TextWrapping.Wrap };
        _welcomeRom = new TextBlock();
        _welcomeDisk = new TextBlock();
        _welcomeRomButton = CreatePanelButton("Choose ROM…", () => _ = ChooseWelcomeRomAsync());
        _welcomeDiskButton = CreatePanelButton("Choose disk…", () => _ = OpenDiskPickerAsync(0));
        var setup = new StackPanel { Spacing = 14 };
        setup.Children.Add(_welcomeMachine);
        setup.Children.Add(_welcomeBootMethod);
        _welcomeRomRow = WelcomeRow("1", "Kickstart ROM", _welcomeRom, _welcomeRomButton);
        setup.Children.Add(_welcomeRomRow);
        setup.Children.Add(new Border { Height = 1, Background = QuietBorder });
        _welcomeDiskRow = WelcomeRow("2", "Startup disk", _welcomeDisk, _welcomeDiskButton);
        setup.Children.Add(_welcomeDiskRow);
        content.Children.Add(new Border { Background = Surface, CornerRadius = new CornerRadius(12), Padding = new Thickness(22), Child = setup });
        _welcomeMessage = new TextBlock { Foreground = MutedText, TextWrapping = TextWrapping.Wrap, FontSize = 13 };
        AutomationProperties.SetName(_welcomeMessage, "Startup guidance");
        content.Children.Add(_welcomeMessage);
        _welcomeStart = CreatePanelButton("Start Amiga", () => _ = StartRuntimeFromSettingsAsync());
        MakePrimary(_welcomeStart);
        _welcomeStart.Padding = new Thickness(24, 10);
        var customise = CreatePanelButton("Configure Amiga…", ShowSettingsWindow);
        customise.Background = Brushes.Transparent;
        customise.BorderBrush = QuietBorder;
        content.Children.Add(CommandGroup(_welcomeStart, customise));
        _welcomeRecent = new StackPanel { Spacing = 6 };
        RefreshWelcomeRecentDisks();
        content.Children.Add(_welcomeRecent);
        return new Border { Background = CopperScreenAppearance.Background, Child = new ScrollViewer { Content = content, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled }, MinHeight = 480, MaxHeight = 580, IsVisible = _runtime == null };
    }

    private void UpdateBootMethodControls()
    {
        if (_kickstartSourceBox == null) return;
        var source = ParseKickstartSourceSelection(_kickstartSourceBox.SelectedItem);
        var minimal = source == CopperScreenKickstartSource.MinimalDiskBoot;
        if (source is CopperScreenKickstartSource.Kickstart13Rom or CopperScreenKickstartSource.Kickstart31Rom or CopperScreenKickstartSource.KickstartRom)
            _welcomeNativeBootSource = source;
        if (_bootRomRow != null) _bootRomRow.IsVisible = !minimal;
        if (_welcomeRomRow != null) _welcomeRomRow.IsVisible = !minimal;
        if (_welcomeDiskRow != null) ((TextBlock)((Border)_welcomeDiskRow.Children[0]).Child!).Text = minimal ? "1" : "2";
        if (_setupDiskBox != null) _setupDiskBox.PlaceholderText = minimal ? "Required · standard ADF or ZIP containing an ADF" : "Optional · ADF, IPF or ZIP";
        if (_welcomeBootMethod != null)
        {
            _updatingBootMethod = true;
            try { _welcomeBootMethod.SelectedIndex = minimal ? 1 : 0; }
            finally { _updatingBootMethod = false; }
        }
    }

    private static Grid WelcomeRow(string number, string title, TextBlock value, Button action)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 14 };
        row.Children.Add(new Border { Width = 28, Height = 28, CornerRadius = new CornerRadius(14), Background = CopperScreenAppearance.ProtectedSurface, VerticalAlignment = VerticalAlignment.Center, Child = new TextBlock { Text = number, Foreground = CopperScreenAppearance.ProtectedText, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } });
        var text = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock { Text = title, Foreground = CopperScreenAppearance.Text, FontWeight = FontWeight.SemiBold });
        value.Foreground = MutedText;
        value.FontSize = 12;
        value.TextTrimming = TextTrimming.CharacterEllipsis;
        text.Children.Add(value);
        Grid.SetColumn(text, 1);
        row.Children.Add(text);
        Grid.SetColumn(action, 2);
        row.Children.Add(action);
        return row;
    }

    private void UpdateWelcomeStatus(bool valid, string error)
    {
        if (_welcomeStart == null || _runtime != null) return;
        UpdateBootMethodControls();
        var minimal = ParseKickstartSourceSelection(_kickstartSourceBox.SelectedItem) == CopperScreenKickstartSource.MinimalDiskBoot;
        var rom = _kickstartRomBox.Text;
        _welcomeRom.Text = string.IsNullOrWhiteSpace(rom) ? "No ROM selected" : Path.GetFileName(rom);
        ToolTip.SetTip(_welcomeRom, rom);
        var disk = _setupDiskBox.Text;
        _welcomeDisk.Text = string.IsNullOrWhiteSpace(disk) ? minimal ? "Required · standard ADF or ZIP containing an ADF" : "Optional · ADF, IPF or ZIP · drop a disk here" : Path.GetFileName(disk);
        ToolTip.SetTip(_welcomeDisk, disk);
        _welcomeMachine.Text = MachineName(_settingsDraft) + " · " + _settingsDraft.Chipset.VideoStandard.ToString().ToUpperInvariant() + " · " + _settingsDraft.ChipRamKb + " KB Chip RAM";
        _welcomeStart.IsEnabled = valid && !_settingsVisible && !_applyingSettings;
        _welcomeStart.Content = _applyingSettings ? "Starting…" : "Start Amiga";
        _welcomeRomButton.IsEnabled = _welcomeDiskButton.IsEnabled = !_settingsVisible && !_applyingSettings;
        _welcomeBootMethod.IsEnabled = !_settingsVisible && !_applyingSettings;
        _welcomeMessage.Text = _settingsStartupError ?? (minimal ? valid ? "Experimental boot for demos and games that take over the machine. DOS and Workbench need a Kickstart ROM." : error : valid ? "Ready to start. Settings will be remembered." : string.IsNullOrWhiteSpace(rom) ? "Select a Kickstart ROM to start." : error);
        _welcomeMessage.Foreground = _settingsStartupError != null || (!valid && (minimal ? !string.IsNullOrWhiteSpace(disk) : !string.IsNullOrWhiteSpace(rom))) ? new SolidColorBrush(Color.Parse("#FFBCAC")) : MutedText;
    }

    private async Task ChooseWelcomeRomAsync()
    {
        await PickRomAsync();
        if (_settingsStartupError == null)
        {
            _settingsDraft = ReadSettingsDraft();
            _committedSettings = _settingsDraft.Clone();
            _latestState = CreateIdleState(_settingsDraft);
        }
        UpdateSettingsStatus();
        UpdateToolbarStatus();
    }

    private void RememberCurrentSession()
    {
        var machine = _committedSettings.Clone();
        var options = machine.ToStartupOptions(AppContext.BaseDirectory);
        if (machine.KickstartSource != CopperScreenKickstartSource.MinimalDiskBoot)
            machine.KickstartRomPath = options.KickstartRomPath;
        for (var i = machine.DriveDiskPaths.Length - 1; i >= 0; i--)
        {
            machine.DriveDiskPaths[i] = options.DriveDiskPaths[i];
            _recentSession.RememberDisk(machine.DriveDiskPaths[i]);
        }
        _recentSession.Save(machine, _outputVolume, _outputMuted);
    }

    private void RefreshWelcomeRecentDisks()
    {
        _welcomeRecent.Children.Clear();
        _welcomeRecent.IsVisible = _recentSession.RecentDisks.Count > 0;
        if (!_welcomeRecent.IsVisible) return;
        _welcomeRecent.Children.Add(SectionLabel("Recent disks"));
        AddRecentDiskButtons(_welcomeRecent, 3, () => { });
    }

    private void AddRecentDiskButtons(StackPanel panel, int maximum, Action beforeInsert)
    {
        var count = 0;
        foreach (var path in _recentSession.RecentDisks)
        {
            if (!CopperScreenDiskImageArchive.DiskPathExists(path)) continue;
            var button = CreatePanelButton(Path.GetFileName(path), () => { beforeInsert(); _ = InsertDriveDiskAsync(0, path); });
            button.Content = new TextBlock { Text = Path.GetFileName(path), TextTrimming = TextTrimming.CharacterEllipsis };
            button.HorizontalAlignment = HorizontalAlignment.Stretch;
            button.HorizontalContentAlignment = HorizontalAlignment.Left;
            button.Background = Brushes.Transparent;
            button.BorderThickness = new Thickness(0);
            ToolTip.SetTip(button, path);
            panel.Children.Add(button);
            if (++count == maximum) break;
        }
        if (count == 0) panel.Children.Add(new TextBlock { Text = "No recent disks available. Use Browse to choose a disk.", Foreground = MutedText, TextWrapping = TextWrapping.Wrap });
    }

    internal static bool IsSupportedDiskDrop(string? path) => path != null && Path.GetExtension(path).ToLowerInvariant() is ".adf" or ".ipf" or ".zip";

    private void ConfigureDiskDrop(Control target, int index)
    {
        DragDrop.SetAllowDrop(target, true);
        target.AddHandler(DragDrop.DragOverEvent, (_, args) =>
        {
            var files = args.DataTransfer.TryGetFiles()?.ToArray();
            var path = files is { Length: 1 } ? files[0].TryGetLocalPath() : null;
            args.DragEffects = !_settingsVisible && !_applyingSettings && IsSupportedDiskDrop(path) ? DragDropEffects.Copy : DragDropEffects.None;
            args.Handled = true;
        });
        target.AddHandler(DragDrop.DropEvent, async (_, args) =>
        {
            args.Handled = true;
            if (_settingsVisible || _applyingSettings) return;
            var files = args.DataTransfer.TryGetFiles()?.ToArray();
            var path = files is { Length: 1 } ? files[0].TryGetLocalPath() : null;
            if (!IsSupportedDiskDrop(path)) return;
            try
            {
                ReleaseInteractiveInput();
                if (!await TryAssignArchiveDiskSetAsync(path!)) await InsertDriveDiskAsync(index, path!);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException)
            { ShowDiskFeedback(ex.Message, false); }
        });
    }
}
