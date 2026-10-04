using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace CopperScreen;

internal sealed partial class MainWindow
{
    private static readonly IBrush Surface = CopperScreenAppearance.Surface;
    private static readonly IBrush MutedText = CopperScreenAppearance.MutedText;
    private static readonly IBrush Accent = CopperScreenAppearance.Accent;
    private static readonly IBrush QuietBorder = CopperScreenAppearance.QuietBorder;
    private static readonly IBrush DriveActivity = new SolidColorBrush(Color.Parse("#9EC99C"));
    private static readonly IBrush ToolbarInset = CopperScreenAppearance.Inset;
    private static readonly IBrush CommandHover = CopperScreenAppearance.Hover;
    private Grid _toolbarCommands = null!;
    private Control _sessionCommands = null!;
    private Button _restartButton = null!, _browseDiskButton = null!, _ejectDiskButton = null!;
    private Button _previousDiskButton = null!, _nextDiskButton = null!, _muteButton = null!, _moreButton = null!;
    private Button _inputHelpButton = null!, _volumeButton = null!;
    private ComboBox _primaryDiskCombo = null!;
    private bool _updatingDiskChoices, _selectingRecentDisk;
    private string? _diskChoicesCurrentPath, _diskChoicesCurrentName;
    private string[] _diskChoicesHistory = [];
    private sealed record RecentDiskChoice(string? Path, string Name)
    {
        public override string ToString() => Name;
    }
    private Slider _shellVolume = null!;
    private TextBlock _sessionStatus = null!, _captureStatus = null!, _mediaFeedback = null!, _inputHelpLabel = null!;
    private Border _statusBar = null!, _idlePanel = null!;
    private StackPanel _inputHelp = null!;
    private readonly Dictionary<Button, (TextBlock Label, Avalonia.Controls.Shapes.Path Icon)> _shellButtonLabels = [];
    private bool _outputMuted;
    private double _outputVolume = 100;
    private string? _navigationDiskPath;
    private bool _hasPreviousDisk, _hasNextDisk;

    private Border CreateMainToolbar()
    {
        _restartButton = CreateToolbarButton("Restart", () => _ = ResetRuntimeAsync(), "Restart the current Amiga; its unsaved work will be lost");
        _muteButton = CreateToolbarButton("Mute", ToggleMute, "Mute emulator audio");
        _moreButton = CreateToolbarButton("More", () => { }, "Display, diagnostics and keyboard shortcuts (F1)");
        _volumeButton = CreateToolbarButton("Sound", () => _shellVolume.Value = _outputVolume, "Adjust volume or mute emulator audio");
        _shellVolume = new Slider { Minimum = 0, Maximum = 100, Value = _outputVolume, MinWidth = 230 };
        AutomationProperties.SetName(_shellVolume, "Emulator volume");
        _shellVolume.ValueChanged += (_, _) =>
        {
            _outputVolume = _shellVolume.Value;
            if (_masterVolumeSlider != null && !_settingsVisible) _masterVolumeSlider.Value = _outputVolume;
            _runtime?.SetOutputVolume(_outputMuted ? 0 : (float)(_outputVolume / 100));
        };
        var audio = new StackPanel { Spacing = 12, Width = 250 };
        audio.Children.Add(SectionLabel("Emulator volume"));
        audio.Children.Add(_shellVolume);
        audio.Children.Add(_muteButton);
        _volumeButton.Flyout = new Flyout { Content = audio };
        _volumeButton.Flyout.Closed += (_, _) => _recentSession.Save(null, _outputVolume, _outputMuted);

        DecorateShellButton(_pauseButton, "Pause", "pause");
        DecorateShellButton(_restartButton, "Restart", "restart");
        DecorateShellButton(_fullscreenButton, "Fullscreen", "fullscreen");
        DecorateShellButton(_volumeButton, "Sound", "sound");
        DecorateShellButton(_settingsButton, "Settings", "settings");
        DecorateShellButton(_moreButton, "More", "more");
        StyleShellCommand(_pauseButton, primary: true);
        foreach (var button in new[] { _restartButton, _fullscreenButton, _volumeButton, _settingsButton, _moreButton })
            StyleShellCommand(button);
        _toolbarCommands = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 20 };
        _sessionCommands = LabelledCommandGroup("Amiga", CommandGroup(_pauseButton, _restartButton), Accent);
        _toolbarCommands.Children.Add(_sessionCommands);
        var playback = LabelledCommandGroup("Display & sound", new Border
        {
            Background = CopperScreenAppearance.ControlSurface,
            CornerRadius = new CornerRadius(8),
            Child = CommandGroup(_fullscreenButton, _volumeButton)
        });
        Grid.SetColumn(playback, 1);
        _toolbarCommands.Children.Add(playback);
        var utilities = LabelledCommandGroup("Tools", CommandGroup(_settingsButton, _moreButton));
        utilities.Margin = new Thickness(12, 0, 0, 0);
        Grid.SetColumn(utilities, 2);
        _toolbarCommands.Children.Add(utilities);

        var more = new StackPanel { Spacing = 14, Width = 370 };
        more.Children.Add(SectionLabel("Display"));
        more.Children.Add(_overscanButton);
        var diagnostics = new StackPanel { Spacing = 8 };
        var counters = new WrapPanel();
        foreach (var text in new[] { _cpuPcStatus, _lastPcStatus, _frameStatus, _perfStatus })
        {
            var box = CreateIndicatorBox(text, 172, "Emulator diagnostics");
            box.Margin = new Thickness(0, 0, 6, 6);
            counters.Children.Add(box);
            if (text == _perfStatus) _perfStatusBox = box;
        }
        diagnostics.Children.Add(counters);
        var drives = new WrapPanel();
        for (var i = 0; i < 4; i++)
        {
            _driveStatusTexts[i] = CreateToolbarTextBlock(12);
            _driveStatusBoxes[i] = CreateIndicatorBox(_driveStatusTexts[i], 172, $"DF{i} drive diagnostics");
            _driveStatusButtons[i] = CreateDriveStatusButton(_driveStatusBoxes[i], i);
            _driveStatusButtons[i].Margin = new Thickness(0, 0, 6, 6);
            drives.Children.Add(_driveStatusButtons[i]);
        }
        diagnostics.Children.Add(drives);
        _ledFilterBox = CreateIndicatorBox(_ledFilterStatus, 172, "Power LED and audio filter state");
        diagnostics.Children.Add(_ledFilterBox);
        more.Children.Add(new Expander { Header = "Diagnostics", Content = diagnostics, BorderBrush = QuietBorder, HorizontalAlignment = HorizontalAlignment.Stretch });
        more.Children.Add(SectionLabel("Keyboard shortcuts"));
        more.Children.Add(new TextBlock
        {
            Text = "Alt+Enter   Fullscreen / window\nF10   Release the mouse\nF11   Show / hide controls in fullscreen\nF12 / Shift+F12   Next / previous disk\nNumLock   Switch keyboard joystick mode\nF1   Show this help",
            Foreground = MutedText, TextWrapping = TextWrapping.Wrap, LineHeight = 24
        });
        _moreButton.Flyout = new Flyout { Content = more };

        _browseDiskButton = CreateToolbarButton("Browse…", () => _ = OpenDiskPickerAsync(0), "Choose an ADF, IPF or ZIP archive for DF0");
        _ejectDiskButton = CreateToolbarButton("Eject", () => _ = EjectDriveDiskAsync(0), "Eject the disk from DF0");
        _previousDiskButton = CreateToolbarIconButton("Previous disk", "previous", () => _ = InsertPreviousDiskAsync(), "Previous disk in this set (Shift+F12)");
        _nextDiskButton = CreateToolbarIconButton("Next disk", "next", () => _ = InsertNextDiskAsync(), "Next disk in this set (F12)");
        var mediaActions = CommandGroup(_browseDiskButton, _ejectDiskButton, _previousDiskButton, _nextDiskButton);
        var media = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 12 };
        media.Children.Add(new TextBlock { Text = "DF0", Foreground = MutedText, VerticalAlignment = VerticalAlignment.Center });
        _primaryDiskCombo = new ComboBox
        {
            MinHeight = 40, MinWidth = 0, MaxDropDownHeight = 300,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = CopperScreenAppearance.ControlSurface, BorderBrush = CopperScreenAppearance.FieldBorder,
            CornerRadius = new CornerRadius(8),
            SelectionBoxItemTemplate = new FuncDataTemplate<RecentDiskChoice>((choice, _) => new TextBlock { Text = choice?.Name, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center }),
            ItemTemplate = new FuncDataTemplate<RecentDiskChoice>((choice, _) =>
            {
                var item = new StackPanel { Spacing = 3 };
                item.Children.Add(new TextBlock { Text = choice?.Name, TextTrimming = TextTrimming.CharacterEllipsis });
                if (choice?.Path is { } path)
                    item.Children.Add(new TextBlock { Text = path, FontSize = 11, Foreground = MutedText, TextTrimming = TextTrimming.CharacterEllipsis });
                ToolTip.SetTip(item, choice?.Path);
                return item;
            })
        };
        AutomationProperties.SetName(_primaryDiskCombo, "DF0 disk · choose from recent disks");
        _primaryDiskCombo.SelectionChanged += async (_, _) => await SelectRecentDiskAsync();
        _primaryDiskCombo.DropDownOpened += (_, _) =>
        {
            ReleaseInteractiveInput();
            RefreshPrimaryDiskChoices(_latestState.Drives.FirstOrDefault(), force: true);
        };
        _primaryDiskCombo.ContainerPrepared += (_, args) =>
        {
            if (_primaryDiskCombo.Items[args.Index] is RecentDiskChoice choice)
            {
                args.Container.IsEnabled = choice.Path != null;
                AutomationProperties.SetName(args.Container, choice.Path ?? choice.Name);
            }
        };
        ConfigureDiskDrop(_primaryDiskCombo, 0);
        Grid.SetColumn(_primaryDiskCombo, 1);
        media.Children.Add(_primaryDiskCombo);
        Grid.SetColumn(mediaActions, 2);
        media.Children.Add(mediaActions);

        _mediaFeedback = new TextBlock { Foreground = MutedText, TextWrapping = TextWrapping.Wrap, IsVisible = false };
        AutomationProperties.SetName(_mediaFeedback, "Disk operation result");
        var disks = new StackPanel { Spacing = 8 };
        disks.Children.Add(media);
        disks.Children.Add(CreateDriveStrip());
        disks.Children.Add(_mediaFeedback);
        var layout = new StackPanel();
        layout.Children.Add(new Border { Padding = new Thickness(16, 12, 16, 14), Child = _toolbarCommands });
        layout.Children.Add(new Border { Background = ToolbarInset, Padding = new Thickness(16, 10), Child = disks });
        return new Border { Background = Surface, Child = layout };
    }

    private static StackPanel LabelledCommandGroup(string title, Control content, IBrush? titleBrush = null)
    {
        var group = new StackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Left };
        group.Children.Add(new TextBlock { Text = title, Foreground = titleBrush ?? MutedText, FontSize = 11, FontWeight = FontWeight.SemiBold, Margin = new Thickness(2, 0, 0, 0) });
        group.Children.Add(content);
        return group;
    }

    private void StyleShellCommand(Button button, bool primary = false)
    {
        button.Background = primary ? Accent : Brushes.Transparent;
        button.Foreground = primary ? CopperScreenAppearance.OnAccent : CopperScreenAppearance.Text;
        button.BorderThickness = new Thickness(0);
        button.FontWeight = primary ? FontWeight.SemiBold : FontWeight.Normal;
        button.Padding = new Thickness(12, 8);
        button.Resources["ButtonBackgroundPointerOver"] = primary ? CopperScreenAppearance.AccentHover : CommandHover;
        button.Resources["ButtonBackgroundPressed"] = primary ? CopperScreenAppearance.AccentPressed : QuietBorder;
        button.Resources["ButtonForegroundPointerOver"] = button.Foreground;
        button.Resources["ButtonForegroundPressed"] = button.Foreground;
        _shellButtonLabels[button].Icon.Stroke = button.Foreground;
    }

    private static StackPanel CommandGroup(params Control[] controls)
    {
        var group = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        foreach (var control in controls) group.Children.Add(control);
        return group;
    }

    private static Border DriveLight() => new() { Width = 7, Height = 7, CornerRadius = new CornerRadius(4), Background = MutedText, VerticalAlignment = VerticalAlignment.Center };
    private static TextBlock SectionLabel(string text) => new() { Text = text, Foreground = MutedText, FontSize = 12, FontWeight = FontWeight.SemiBold };

    private static Button CreateToolbarIconButton(string name, string icon, Action action, string tooltip)
    {
        var button = CreateToolbarButton(name, action, tooltip);
        button.Width = button.MinWidth = 40;
        button.HorizontalContentAlignment = HorizontalAlignment.Center;
        button.Styles.Add(new Style(selector => selector.OfType<Button>().Class(":disabled"))
        {
            Setters = { new Setter(OpacityProperty, 0.45) }
        });
        button.Content = new Avalonia.Controls.Shapes.Path
        {
            Data = StreamGeometry.Parse(IconPath(icon)), Stroke = button.Foreground, StrokeThickness = 1.5,
            Width = 16, Height = 16, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center
        };
        return button;
    }

    private void DecorateShellButton(Button button, string text, string icon)
    {
        var label = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
        var shape = new Avalonia.Controls.Shapes.Path { Data = StreamGeometry.Parse(IconPath(icon)), Stroke = Brushes.White, StrokeThickness = 1.5, Width = 14, Height = 14, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center };
        button.Content = CommandGroup(shape, label);
        _shellButtonLabels[button] = (label, shape);
        AutomationProperties.SetName(button, text);
    }

    private void SetShellButtonLabel(Button button, string text, string icon)
    {
        var content = _shellButtonLabels[button];
        if (content.Label.Text != text) { content.Label.Text = text; content.Icon.Data = StreamGeometry.Parse(IconPath(icon)); }
        AutomationProperties.SetName(button, text);
    }

    private static string IconPath(string icon) => icon switch
    {
        "pause" => "M4,2 L4,14 M11,2 L11,14",
        "play" => "M3,2 L13,8 L3,14 Z",
        "previous" => "M14,8 L2,8 M8,2 L2,8 L8,14",
        "next" => "M2,8 L14,8 M8,2 L14,8 L8,14",
        "restart" => "M3,5 A6,6 0 1 1 2,10 M3,1 L3,5 L7,5",
        "fullscreen" => "M6,2 L2,2 L2,6 M10,2 L14,2 L14,6 M14,10 L14,14 L10,14 M6,14 L2,14 L2,10",
        "sound" => "M2,6 L5,6 L9,2 L9,14 L5,10 L2,10 Z M12,5 Q16,8 12,11",
        "muted" => "M2,6 L5,6 L9,2 L9,14 L5,10 L2,10 Z M12,6 L16,10 M16,6 L12,10",
        "settings" => "M2,4 L14,4 M2,12 L14,12 M5,1 L5,7 M11,9 L11,15",
        _ => "M2,7 L4,7 L4,9 L2,9 Z M7,7 L9,7 L9,9 L7,9 Z M12,7 L14,7 L14,9 L12,9 Z"
    };

    private Border CreateStatusBar()
    {
        _sessionStatus = new TextBlock { Foreground = MutedText, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        _captureStatus = new TextBlock { Foreground = MutedText, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Right };
        _inputHelpButton = CreateToolbarButton("Inputs", RefreshInputHelp, "Choose the input for each Amiga port and view its controls");
        _inputHelpButton.FontSize = 12;
        _inputHelpButton.MinHeight = 30;
        _inputHelpButton.MaxWidth = 290;
        _inputHelpButton.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        _inputHelpLabel = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis };
        _inputHelpButton.Content = CommandGroup(_inputHelpLabel, new Avalonia.Controls.Shapes.Path
        {
            Data = StreamGeometry.Parse("M0,0 L4,4 L8,0"), Stroke = MutedText, StrokeThickness = 1,
            Width = 8, Height = 4, VerticalAlignment = VerticalAlignment.Center
        });
        _inputHelpButton.Padding = new Thickness(8, 4);
        _inputHelpButton.Background = Brushes.Transparent;
        _inputHelpButton.BorderThickness = new Thickness(0);
        _inputHelp = CreateInputHelp();
        _inputHelpButton.Flyout = new Flyout { Content = new ScrollViewer { Content = _inputHelp, MaxHeight = 600, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled } };
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*"), ColumnSpacing = 16 };
        row.Children.Add(_sessionStatus);
        Grid.SetColumn(_inputHelpButton, 1);
        row.Children.Add(_inputHelpButton);
        Grid.SetColumn(_captureStatus, 2);
        row.Children.Add(_captureStatus);
        return new Border { Background = Surface, Padding = new Thickness(16, 7), Child = row };
    }

    private static void MakePrimary(Button button)
    {
        button.Background = Accent;
        button.Foreground = CopperScreenAppearance.OnAccent;
        button.FontWeight = FontWeight.SemiBold;
        button.BorderThickness = new Thickness(0);
        button.Resources["ButtonBackgroundPointerOver"] = CopperScreenAppearance.AccentHover;
        button.Resources["ButtonBackgroundPressed"] = CopperScreenAppearance.AccentPressed;
        button.Resources["ButtonForegroundPointerOver"] = CopperScreenAppearance.OnAccent;
        button.Resources["ButtonForegroundPressed"] = CopperScreenAppearance.OnAccent;
    }

    private void UpdateShellStatus(CopperScreenState state)
    {
        if (_sessionStatus == null) return;
        var running = _runtime != null;
        _sessionCommands.IsVisible = running;
        _toolbarCommands.ColumnSpacing = running ? 20 : 0;
        _pauseButton.IsVisible = _restartButton.IsVisible = running;
        _pauseButton.IsEnabled = running && state.FaultMessage == null && !_settingsVisible;
        _restartButton.IsEnabled = running && !_settingsVisible;
        SetShellButtonLabel(_pauseButton, state.IsPaused ? "Resume" : "Pause", state.IsPaused ? "play" : "pause");
        SetShellButtonLabel(_fullscreenButton, WindowState == WindowState.FullScreen ? "Windowed" : "Fullscreen", "fullscreen");
        SetShellButtonLabel(_volumeButton, _outputMuted ? "Muted" : "Sound", _outputMuted ? "muted" : "sound");
        _overscanButton.Content = _showFullOverscan ? "Overscan: Full" : "Overscan: Cropped";
        var drive = state.Drives.FirstOrDefault();
        RefreshPrimaryDiskChoices(drive);
        var canChangeDisk = !_settingsVisible && !_applyingSettings && !_selectingRecentDisk;
        _browseDiskButton.IsEnabled = _primaryDiskCombo.IsEnabled = canChangeDisk;
        _ejectDiskButton.IsEnabled = (drive.HasDisk || drive.IsSwapPending) && canChangeDisk;
        ToolTip.SetTip(_primaryDiskCombo, drive.DiskPath ?? "Choose a recent disk, use Browse, or drop an ADF, IPF or ZIP here.");
        if (!string.Equals(_navigationDiskPath, state.DiskPath, StringComparison.Ordinal))
        {
            _navigationDiskPath = state.DiskPath;
            try
            {
                _hasPreviousDisk = CopperScreenDiskNavigation.ResolveAdjacentDiskPath(state.DiskPath, -1) != null;
                _hasNextDisk = CopperScreenDiskNavigation.ResolveAdjacentDiskPath(state.DiskPath, 1) != null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            { _hasPreviousDisk = _hasNextDisk = false; }
        }
        _previousDiskButton.IsVisible = _nextDiskButton.IsVisible = _hasPreviousDisk || _hasNextDisk;
        _previousDiskButton.IsEnabled = running && _hasPreviousDisk && canChangeDisk;
        _nextDiskButton.IsEnabled = running && _hasNextDisk && canChangeDisk;
        _muteButton.Content = _outputMuted ? "Unmute" : "Mute";
        _volumeButton.IsEnabled = _muteButton.IsEnabled = !_settingsVisible;
        AutomationProperties.SetName(_muteButton, _muteButton.Content.ToString()!);
        _sessionStatus.Text = !running ? "Ready when you are" : state.FaultMessage != null ? "Stopped" : state.IsPaused ? "Paused" : "Running · " + MachineName(_committedSettings);
        _sessionStatus.Foreground = state.IsPaused ? Accent : MutedText;
        _captureStatus.Text = !running ? "Drop an ADF, IPF or ZIP to choose a disk" : _mouseGrabActive ? "Mouse captured · F10 to release" : WindowState == WindowState.FullScreen ? "F11 controls · Alt+Enter windowed" : _inputOptions.IsMousePort(0) ? "Click display to capture mouse · F1 help" : "Inputs: switch devices and view controls · F1 help";
        _captureStatus.Foreground = _mouseGrabActive ? Accent : MutedText;
        UpdateInputSummary();
        _idlePanel.IsVisible = !running;
        UpdateDriveStrip(state);
    }

    private static string MachineName(CopperScreenSettingsDraft draft) => draft.Chipset.DisplayChip == DisplayChipModel.AgaLisa ? "Amiga 1200" : draft.Chipset.DisplayChip == DisplayChipModel.EcsDenise ? "Amiga 500 Plus" : "Amiga 500";

    private void ToggleMute()
    {
        _outputMuted = !_outputMuted;
        if (!_settingsVisible) _masterMuteBox.IsChecked = _outputMuted;
        _runtime?.SetOutputVolume(_outputMuted ? 0 : (float)(_outputVolume / 100));
        _recentSession.Save(null, _outputVolume, _outputMuted);
        UpdateShellStatus(_latestState);
    }

    private static string FormatControlKeys(string[] keys)
    {
        if (keys.Length == 0) return "Unassigned";
        var names = keys.Select(key => key switch { "NumPadClear" => "NumPad5", "NumPadDecimal" => "NumPad.", _ => key }).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (names.All(key => key.StartsWith("NumPad", StringComparison.Ordinal)))
            return "Numpad " + string.Join(" / ", names.Select(key => key[6..]));
        return string.Join(" / ", names.Select(key => key.Replace("NumPad", "Numpad ", StringComparison.Ordinal)));
    }

    private void RefreshPrimaryDiskChoices(CopperScreenDriveState drive, bool force = false)
    {
        if (_selectingRecentDisk) return;
        var path = drive.HasDisk || drive.IsSwapPending ? drive.DiskPath : null;
        var name = path != null ? drive.DiskName + (drive.IsSwapPending ? " · Changing…" : drive.HasUnsavedChanges ? " · Unsaved" : "") : "No disk inserted";
        if (!force && _diskChoicesCurrentPath == path && _diskChoicesCurrentName == name && _diskChoicesHistory.SequenceEqual(_recentSession.RecentDisks)) return;
        var current = new RecentDiskChoice(path, name);
        var choices = new List<RecentDiskChoice> { current };
        foreach (var recent in _recentSession.RecentDisks)
        {
            if (string.Equals(recent, path, StringComparison.OrdinalIgnoreCase) || !CopperScreenDiskImageArchive.DiskPathExists(recent)) continue;
            choices.Add(new RecentDiskChoice(recent, CopperScreenDiskImageArchive.GetDisplayName(recent)));
        }
        _updatingDiskChoices = true;
        try
        {
            _primaryDiskCombo.ItemsSource = choices;
            _primaryDiskCombo.SelectedItem = current;
            _diskChoicesCurrentPath = path;
            _diskChoicesCurrentName = name;
            _diskChoicesHistory = _recentSession.RecentDisks.ToArray();
        }
        finally { _updatingDiskChoices = false; }
    }

    private async Task SelectRecentDiskAsync()
    {
        if (_updatingDiskChoices || _selectingRecentDisk || _settingsVisible || _applyingSettings) return;
        if (_primaryDiskCombo.SelectedItem is not RecentDiskChoice { Path: { } path } ||
            string.Equals(path, _latestState.Drives.FirstOrDefault().DiskPath, StringComparison.OrdinalIgnoreCase)) return;
        _selectingRecentDisk = true;
        UpdateShellStatus(_latestState);
        try
        {
            ReleaseInteractiveInput();
            if (!CopperScreenDiskImageArchive.DiskPathExists(path))
                ShowDiskFeedback("This recent disk is no longer available. Use Browse to choose its new location.", false);
            else await InsertDriveDiskAsync(0, path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException)
        { ShowDiskFeedback(ex.Message, false); }
        finally
        {
            _selectingRecentDisk = false;
            RefreshPrimaryDiskChoices(_latestState.Drives.FirstOrDefault(), force: true);
            UpdateShellStatus(_latestState);
        }
    }

    private void ShowDiskFeedback(string message, bool success)
    {
        _mediaFeedback.Text = message;
        _mediaFeedback.Foreground = success ? MutedText : new SolidColorBrush(Color.Parse("#FFBCAC"));
        _mediaFeedback.IsVisible = !string.IsNullOrWhiteSpace(message);
    }

    private async Task EjectDriveDiskAsync(int index)
    {
        if (_runtime != null) await CompleteDiskCommandAsync(_runtime.EjectDiskAsync(index));
        else
        {
            _settingsDraft.DriveDiskPaths[index] = null;
            _committedSettings.DriveDiskPaths[index] = null;
            SetDrivePathTextSilently(index, string.Empty);
            if (index == 0) _setupDiskBox.Text = string.Empty;
            _latestState = CreateIdleState(_settingsDraft);
            UpdateSettingsStatus();
            UpdateToolbarStatus();
        }
    }
}
