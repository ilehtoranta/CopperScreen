using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using CopperMod.Amiga.Lightweight;

namespace CopperScreen;

internal sealed partial class MainWindow
{
    private readonly Geometry _lockedDriveIcon = StreamGeometry.Parse("M3,7 L3,4 A3.5,3.5 0 0 1 10,4 L10,7 M2,7 L11,7 L11,14 L2,14 Z M6.5,10 L6.5,12");
    private readonly Geometry _writableDriveIcon = StreamGeometry.Parse("M6,7 L6,4 A3.5,3.5 0 0 1 13,4 M2,7 L11,7 L11,14 L2,14 Z M6.5,10 L6.5,12");
    private static readonly IBrush ProtectedDriveBackground = CopperScreenAppearance.ProtectedSurface;
    private readonly Border[] _driveTiles = new Border[4];
    private readonly Button[] _mediaDriveButtons = new Button[4];
    private readonly Button[] _mediaEjectButtons = new Button[4];
    private readonly Button[] _mediaSaveButtons = new Button[4];
    private readonly Border[] _mediaDriveLights = new Border[4];
    private readonly TextBlock[] _driveTrackTexts = new TextBlock[4];
    private readonly ToggleButton[] _driveProtectToggles = new ToggleButton[4];
    private readonly Avalonia.Controls.Shapes.Path[] _driveProtectIcons = new Avalonia.Controls.Shapes.Path[4];
    private readonly bool[] _driveProtectionPending = new bool[4];
    private readonly int[] _driveTrackPositions = [-3, -3, -3, -3];
    private bool _updatingDriveStrip;

    private WrapPanel CreateDriveStrip()
    {
        var strip = new WrapPanel();
        AutomationProperties.SetName(strip, "Floppy drives · tracks and write protection");
        for (var i = 0; i < _driveTiles.Length; i++)
        {
            var index = i;
            _mediaDriveLights[i] = DriveLight();
            var label = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            label.Children.Add(_mediaDriveLights[i]);
            label.Children.Add(new TextBlock { Text = $"DF{i}", FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
            label.Children.Add(new Avalonia.Controls.Shapes.Path
            {
                Data = StreamGeometry.Parse("M0,0 L3,3 L6,0"), Stroke = MutedText, StrokeThickness = 1,
                Width = 6, Height = 3, VerticalAlignment = VerticalAlignment.Center
            });
            var button = CreateToolbarButton($"DF{i} disk options", () => ReleaseInteractiveInput(), $"Manage DF{i}");
            button.Content = label;
            button.MinHeight = 28;
            button.Padding = new Thickness(4, 5);
            button.Background = Brushes.Transparent;
            button.BorderThickness = new Thickness(0);
            button.Resources["ButtonBackgroundPointerOver"] = CommandHover;
            var actions = new StackPanel { Spacing = 8, Width = 250 };
            actions.Children.Add(SectionLabel($"Drive DF{i}"));
            actions.Children.Add(CreateToolbarButton("Change disk…", () => { button.Flyout?.Hide(); _ = OpenDiskPickerAsync(index); }, $"Insert a disk in DF{i}"));
            _mediaEjectButtons[i] = CreateToolbarButton("Eject", () => { button.Flyout?.Hide(); _ = EjectDriveDiskAsync(index); }, $"Eject DF{i}");
            actions.Children.Add(_mediaEjectButtons[i]);
            _mediaSaveButtons[i] = CreateToolbarButton("Save ADF…", () => { button.Flyout?.Hide(); _ = SaveDiskAdfAsync(index); }, "Save the mounted ADF, including in-memory changes");
            actions.Children.Add(_mediaSaveButtons[i]);
            actions.Children.Add(SettingsNote("ADF changes stay in memory until you save. IPF disks are read-only."));
            button.Flyout = new Flyout { Content = actions };
            _mediaDriveButtons[i] = button;

            var track = _driveTrackTexts[i] = new TextBlock { Text = "--.-", FontFamily = FontFamily.Parse("Consolas"), FontSize = 13, MinWidth = 29, Foreground = MutedText, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

            _driveProtectIcons[i] = new Avalonia.Controls.Shapes.Path
            {
                Data = _lockedDriveIcon, Stroke = MutedText, StrokeThickness = 1.4,
                Width = 13, Height = 14, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center
            };
            var protect = new ToggleButton
            {
                Content = _driveProtectIcons[i], Width = 28, MinHeight = 28, Padding = new Thickness(5),
                VerticalAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Center,
                Background = Brushes.Transparent, BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(5)
            };
            protect.Resources["ToggleButtonBackgroundPointerOver"] = CommandHover;
            CopperScreenAppearance.ConfigureKeyboardFocus(protect);
            foreach (var state in new[] { "", "PointerOver", "Pressed", "Disabled" })
            {
                protect.Resources["ToggleButtonBackgroundChecked" + state] = state == "PointerOver" ? CommandHover : ProtectedDriveBackground;
                protect.Resources["ToggleButtonBorderBrushChecked" + state] = Brushes.Transparent;
            }
            protect.Styles.Add(new Style(selector => selector.OfType<ToggleButton>().Class(":disabled")) { Setters = { new Setter(OpacityProperty, 0.45) } });
            protect.IsCheckedChanged += async (_, _) =>
            {
                if (!_updatingDriveStrip) await SetDriveWriteProtectionAsync(index, protect.IsChecked == true);
            };
            _driveProtectToggles[i] = protect;

            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 4 };
            row.Children.Add(button);
            Grid.SetColumn(track, 1);
            row.Children.Add(track);
            Grid.SetColumn(protect, 2);
            row.Children.Add(protect);
            var tile = new Border
            {
                Width = 136, MinHeight = 38, Padding = new Thickness(4), Margin = new Thickness(0, 0, 8, 0),
                Background = Surface, CornerRadius = new CornerRadius(8), Child = row
            };
            ConfigureDiskDrop(tile, i);
            _driveTiles[i] = tile;
            strip.Children.Add(tile);
        }
        return strip;
    }

    private void UpdateDriveActivity(CopperScreenState state)
    {
        for (var i = 0; i < _driveTiles.Length; i++)
        {
            var drive = i < state.Drives.Length ? state.Drives[i] : default;
            _driveTiles[i].IsVisible = drive.Connected;
            if (!drive.Connected) continue;
            _mediaDriveLights[i].Background = drive.IsSwapPending ? Accent : drive.ActiveDma || drive.MotorOn ? DriveActivity : drive.HasDisk ? MutedText : QuietBorder;
            var hasTrack = _runtime != null || drive.HasDisk;
            var position = drive.IsSwapPending ? -2 : !hasTrack ? -1 : drive.Cylinder * 2 + drive.Head;
            if (_driveTrackPositions[i] != position)
            {
                SetText(_driveTrackTexts[i], drive.IsSwapPending ? "…" : !hasTrack ? "--.-" : $"{drive.Cylinder:00}.{drive.Head}");
                _driveTrackPositions[i] = position;
            }
            _driveTrackTexts[i].Foreground = drive.ActiveDma || drive.MotorOn ? DriveActivity : drive.HasDisk ? CopperScreenAppearance.Text : MutedText;
        }
    }

    private void UpdateDriveStrip(CopperScreenState state)
    {
        UpdateDriveActivity(state);
        _updatingDriveStrip = true;
        try
        {
            for (var i = 0; i < _driveTiles.Length; i++)
            {
                var drive = i < state.Drives.Length ? state.Drives[i] : default;
                if (!drive.Connected) continue;
                var ipf = IsDrivePermanentlyReadOnly(drive);
                var canChange = !_settingsVisible && !_applyingSettings && !_selectingRecentDisk && !_driveProtectionPending[i];
                _mediaDriveButtons[i].IsEnabled = canChange;
                _mediaEjectButtons[i].IsEnabled = drive.HasDisk || drive.IsSwapPending;
                _mediaSaveButtons[i].IsEnabled = drive.CanExportAdf;
                var disk = drive.IsSwapPending ? "Changing disk" : drive.HasDisk ? drive.DiskName + (drive.HasUnsavedChanges ? " · Unsaved" : "") : "Empty";
                ToolTip.SetTip(_mediaDriveButtons[i], $"DF{i} · {disk}\n{drive.DiskPath}\nClick for disk options; you can also drop a disk here.");
                AutomationProperties.SetName(_mediaDriveButtons[i], $"DF{i} disk options · {disk}");
                ToolTip.SetTip(_mediaDriveLights[i], drive.IsSwapPending ? "Changing disk" : drive.ActiveDma ? "Reading or writing disk" : drive.MotorOn ? "Drive motor running" : "Drive idle");
                ToolTip.SetTip(_driveTrackTexts[i], $"DF{i} · Track (cylinder) {drive.Cylinder:00} · Side {drive.Head}");
                AutomationProperties.SetName(_driveTrackTexts[i], $"DF{i} · Track (cylinder) {drive.Cylinder:00} · Side {drive.Head}");
                var protect = _driveProtectToggles[i];
                if (!_driveProtectionPending[i]) protect.IsChecked = ipf || drive.WriteProtected;
                protect.IsEnabled = canChange && !drive.IsSwapPending && !ipf;
                var locked = protect.IsChecked == true;
                _driveProtectIcons[i].Data = locked ? _lockedDriveIcon : _writableDriveIcon;
                _driveProtectIcons[i].Stroke = locked ? CopperScreenAppearance.ProtectedText : MutedText;
                protect.Background = locked ? ProtectedDriveBackground : Brushes.Transparent;
                AutomationProperties.SetName(protect, $"DF{i} write protection");
                ToolTip.SetTip(protect, ipf ? $"DF{i} · IPF disks are always write protected" : locked ? $"DF{i} · Write protection on. Click to allow writing in memory." : $"DF{i} · Write protection off. Click to protect the disk. Use Save ADF to keep changes.");
            }
        }
        finally { _updatingDriveStrip = false; }
    }

    private bool IsDrivePermanentlyReadOnly(CopperScreenDriveState drive) =>
        drive.Format == LightweightFloppyFormat.Ipf || (_runtime == null && Path.GetExtension(CopperScreenDiskImageArchive.GetDisplayName(drive.DiskPath)).Equals(".ipf", StringComparison.OrdinalIgnoreCase));

    private async Task SetDriveWriteProtectionAsync(int index, bool writeProtected)
    {
        if (_driveProtectionPending[index] || _settingsVisible || _applyingSettings) return;
        var drive = _latestState.Drives.FirstOrDefault(d => d.Index == index);
        if (!drive.Connected || drive.IsSwapPending || IsDrivePermanentlyReadOnly(drive))
        {
            UpdateDriveStrip(_latestState);
            return;
        }
        _driveProtectionPending[index] = true;
        UpdateDriveStrip(_latestState);
        try
        {
            ReleaseInteractiveInput();
            if (_runtime != null)
            {
                var result = await _runtime.SetDriveWriteProtectedAsync(index, writeProtected);
                _latestState = result.State;
                if (!result.Success) { ShowDiskFeedback(result.Message, false); return; }
                writeProtected = result.State.Drives.First(d => d.Index == index).WriteProtected;
            }
            _settingsDraft.DriveWriteProtected[index] = _committedSettings.DriveWriteProtected[index] = writeProtected;
            var wasUpdating = _updatingSettingsUi;
            _updatingSettingsUi = true;
            try { _driveWriteProtectBoxes[index].IsChecked = writeProtected; }
            finally { _updatingSettingsUi = wasUpdating; }
            if (_runtime == null) _latestState = CreateIdleState(_settingsDraft);
            RememberCurrentSession();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException)
        { ShowDiskFeedback(ex.Message, false); }
        finally
        {
            _driveProtectionPending[index] = false;
            UpdateToolbarStatus();
        }
    }
}
