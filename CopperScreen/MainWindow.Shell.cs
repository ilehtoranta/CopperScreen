using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace CopperScreen;

internal sealed partial class MainWindow
{
    private static readonly IBrush Surface = new SolidColorBrush(Color.Parse("#191E25"));
    private static readonly IBrush MutedText = new SolidColorBrush(Color.Parse("#AEB9C7"));
    private static readonly IBrush Accent = new SolidColorBrush(Color.Parse("#DCA578"));
    private Button _restartButton = null!, _insertDiskButton = null!, _ejectDiskButton = null!;
    private Button _previousDiskButton = null!, _nextDiskButton = null!, _muteButton = null!, _moreButton = null!;
    private TextBlock _sessionStatus = null!, _captureStatus = null!, _mediaFeedback = null!;
    private Border _statusBar = null!, _idlePanel = null!;
    private bool _outputMuted;
    private double _outputVolume = 100;
    private string? _navigationDiskPath;
    private bool _hasPreviousDisk, _hasNextDisk;

    private Border CreateMainToolbar()
    {
        _benchToggleButton.Content = "CopperBench — Not yet available";
        _benchToggleButton.IsEnabled = false;
        ToolTip.SetTip(_benchToggleButton, "CopperBench and CopperStart are not yet available in this build.");
        _writeProtectButton.Content = "Disk writing — Not yet available";
        _writeProtectButton.IsEnabled = false;
        ToolTip.SetTip(_writeProtectButton, "Disk writing is not yet available. Mounted disk images are read-only.");
        _fullscreenButton.Content = "Fullscreen";
        _restartButton = CreateToolbarButton("Restart", () => _ = ResetRuntimeAsync(), "Restart the current Amiga; its unsaved work will be lost");
        _muteButton = CreateToolbarButton("Mute", ToggleMute, "Mute emulator audio");
        _moreButton = CreateToolbarButton("More", () => { }, "Display, input, diagnostics and keyboard shortcuts");
        var commands = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var control in new[] { _pauseButton, _restartButton, _fullscreenButton, _muteButton, _settingsButton, _moreButton })
        {
            control.Margin = new Thickness(0, 0, 6, 0);
            commands.Children.Add(control);
        }

        var more = new StackPanel { Spacing = 12, Width = 380 };
        more.Children.Add(SectionLabel("Display and input"));
        var display = new WrapPanel();
        _overscanButton.Margin = new Thickness(0, 0, 6, 0);
        display.Children.Add(_overscanButton);
        display.Children.Add(_numpadModeButton);
        more.Children.Add(display);
        more.Children.Add(SectionLabel("In development"));
        more.Children.Add(_benchToggleButton);
        more.Children.Add(_writeProtectButton);

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
        more.Children.Add(new Expander { Header = "Diagnostics", Content = diagnostics, HorizontalAlignment = HorizontalAlignment.Stretch });
        more.Children.Add(SectionLabel("Keyboard shortcuts"));
        more.Children.Add(new TextBlock
        {
            Text = "Alt+Enter   Fullscreen / window\nF10   Release the mouse\nF11   Show / hide controls in fullscreen\nF12 / Shift+F12   Next / previous disk\nNumLock   Switch numpad mode\nF1   Show this help",
            Foreground = MutedText, TextWrapping = TextWrapping.Wrap, LineHeight = 23
        });
        _moreButton.Flyout = new Flyout { Content = more };

        _insertDiskButton = CreateToolbarButton("Insert disk…", () => _ = OpenDiskPickerAsync(0), "Choose an ADF disk image or ZIP archive for DF0");
        _ejectDiskButton = CreateToolbarButton("Eject", () => _ = EjectPrimaryDiskAsync(), "Eject the disk from DF0");
        _previousDiskButton = CreateToolbarButton("Previous", () => _ = InsertPreviousDiskAsync(), "Previous disk in this set (Shift+F12)");
        _nextDiskButton = CreateToolbarButton("Next", () => _ = InsertNextDiskAsync(), "Next disk in this set (F12)");
        var mediaActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        mediaActions.Children.Add(_insertDiskButton);
        mediaActions.Children.Add(_ejectDiskButton);
        mediaActions.Children.Add(_previousDiskButton);
        mediaActions.Children.Add(_nextDiskButton);
        var media = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        _diskStatus.FontFamily = FontFamily.Default;
        _diskStatus.FontSize = 14;
        media.Children.Add(_diskStatus);
        Grid.SetColumn(mediaActions, 1);
        media.Children.Add(mediaActions);
        _mediaFeedback = new TextBlock { Foreground = MutedText, TextWrapping = TextWrapping.Wrap, IsVisible = false };
        AutomationProperties.SetName(_mediaFeedback, "Disk operation result");

        var layout = new StackPanel { Spacing = 12 };
        layout.Children.Add(commands);
        layout.Children.Add(media);
        layout.Children.Add(_mediaFeedback);
        return new Border { Background = Surface, Padding = new Thickness(14, 12), Child = layout };
    }

    private static TextBlock SectionLabel(string text) => new()
    {
        Text = text, Foreground = MutedText, FontSize = 12, FontWeight = FontWeight.SemiBold
    };

    private Border CreateStatusBar()
    {
        _sessionStatus = new TextBlock { Foreground = MutedText, FontSize = 12 };
        _captureStatus = new TextBlock { Foreground = MutedText, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Right };
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 16 };
        row.Children.Add(_sessionStatus);
        Grid.SetColumn(_captureStatus, 1);
        row.Children.Add(_captureStatus);
        return new Border { Background = Surface, Padding = new Thickness(14, 8), Child = row };
    }

    private Border CreateIdlePanel()
    {
        var content = new StackPanel { Spacing = 16, MaxWidth = 380, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(new TextBlock { Text = "Your Amiga starts here", FontSize = 26, FontWeight = FontWeight.SemiBold, Foreground = Brushes.White });
        content.Children.Add(new TextBlock { Text = "Choose a Kickstart 1.3 ROM and an optional disk image to start your Amiga 500.", TextWrapping = TextWrapping.Wrap, Foreground = MutedText, FontSize = 15 });
        var setup = CreatePanelButton("Set up Amiga…", ShowSettingsWindow);
        MakePrimary(setup);
        setup.HorizontalAlignment = HorizontalAlignment.Left;
        content.Children.Add(setup);
        return new Border { Background = new SolidColorBrush(Color.Parse("#101318")), Child = content, IsVisible = _runtime == null };
    }

    private static void MakePrimary(Button button)
    {
        button.Background = Accent;
        button.Foreground = new SolidColorBrush(Color.Parse("#181512"));
        button.FontWeight = FontWeight.SemiBold;
        button.BorderThickness = new Thickness(0);
    }

    private void UpdateShellStatus(CopperScreenState state)
    {
        if (_sessionStatus == null) return;
        var running = _runtime != null;
        _pauseButton.IsEnabled = running && state.FaultMessage == null && !_settingsVisible;
        _restartButton.IsEnabled = running && !_settingsVisible;
        _pauseButton.Content = state.IsPaused ? "Resume" : "Pause";
        _fullscreenButton.Content = WindowState == WindowState.FullScreen ? "Windowed" : "Fullscreen";
        _overscanButton.Content = _showFullOverscan ? "Overscan: Full" : "Overscan: Cropped";
        _numpadModeButton.Content = _numpadMode == NumpadInputMode.Joystick ? "Numpad: Joystick" : "Numpad: Keyboard";
        _writeProtectButton.Content = "Disk writing — Not yet available";
        _writeProtectButton.IsEnabled = false;
        _benchToggleButton.Content = "CopperBench — Not yet available";
        _benchToggleButton.IsEnabled = false;
        var drive = state.Drives.FirstOrDefault();
        _diskStatus.Text = drive.HasDisk ? "DF0 · " + drive.DiskName : "DF0 · No disk inserted";
        ToolTip.SetTip(_diskStatus, drive.DiskPath ?? "Choose Insert disk to mount an ADF or ZIP file.");
        _insertDiskButton.Content = drive.HasDisk ? "Change…" : "Insert disk…";
        _insertDiskButton.IsEnabled = !_settingsVisible;
        _ejectDiskButton.IsEnabled = (drive.HasDisk || drive.IsSwapPending) && !_settingsVisible;
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
        _previousDiskButton.IsEnabled = running && _hasPreviousDisk && !_settingsVisible;
        _nextDiskButton.IsEnabled = running && _hasNextDisk && !_settingsVisible;
        _muteButton.Content = _outputMuted ? "Unmute" : "Mute";
		_muteButton.IsEnabled = !_settingsVisible;
		foreach (var button in new[] { _pauseButton, _fullscreenButton, _overscanButton, _numpadModeButton, _muteButton, _insertDiskButton, _benchToggleButton, _writeProtectButton })
			AutomationProperties.SetName(button, button.Content?.ToString() ?? string.Empty);
        _sessionStatus.Text = !running ? "Ready to start" : state.FaultMessage != null ? "Stopped" : state.IsPaused ? "Paused" : "Running · Amiga 500";
        _captureStatus.Text = _mouseGrabActive ? "Mouse captured · F10 to release" : WindowState == WindowState.FullScreen ? "F11 controls · Alt+Enter windowed" : "Click display to capture mouse · F1 help";
        _idlePanel.IsVisible = !running;
    }

    private void ToggleMute()
    {
        _outputMuted = !_outputMuted;
        _runtime?.SetOutputVolume(_outputMuted ? 0 : (float)(_outputVolume / 100));
        UpdateShellStatus(_latestState);
    }

    private void ShowDiskFeedback(string message, bool success)
    {
        _mediaFeedback.Text = message;
        _mediaFeedback.Foreground = success ? MutedText : new SolidColorBrush(Color.Parse("#FFBCAC"));
        _mediaFeedback.IsVisible = !string.IsNullOrWhiteSpace(message);
    }

    private async Task EjectPrimaryDiskAsync()
    {
        if (_runtime != null) await CompleteDiskCommandAsync(_runtime.EjectDiskAsync());
        else
        {
            _settingsDraft.DriveDiskPaths[0] = null;
            _committedSettings.DriveDiskPaths[0] = null;
            SetDrivePathTextSilently(0, string.Empty);
            _latestState = CreateIdleState(_settingsDraft);
            UpdateToolbarStatus();
        }
    }
}
