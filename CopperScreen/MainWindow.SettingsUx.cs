using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace CopperScreen;

internal sealed partial class MainWindow
{
    private CopperScreenSettingsDraft _committedSettings = null!;
    private CopperScreenPresentationOptions _presentationOptions = CopperScreenPresentationOptions.Default;
    private TextBox _setupDiskBox = null!;
    private TextBlock _romValidation = null!;
    private Slider _masterVolumeSlider = null!;
    private CheckBox _masterMuteBox = null!;
    private Button _settingsApplyButton = null!;
    private readonly TextBlock[] _bindingLabels = new TextBlock[6];
    private readonly Button[] _bindingButtons = new Button[6];
    private bool _applyingSettings;
    private string? _validatedRomPath, _romError;
    private DateTime _validatedRomWriteTime;
    private long _validatedRomLength;

    private static TextBlock SettingsNote(string text) => new()
    {
        Text = text, Foreground = MutedText, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6)
    };

    private Control CreateSetupPage()
    {
        var layout = CreateSettingsPageLayout();
        layout.Spacing = 16;
        layout.Children.Add(new TextBlock { Text = "Start your Amiga", FontSize = 24, FontWeight = FontWeight.SemiBold, Foreground = Brushes.White });
        layout.Children.Add(SettingsNote("Amiga 500 · PAL · Motorola 68000\n512 KiB Chip RAM + 512 KiB slow RAM · up to four read-only floppy drives"));
        var form = CreateSettingsGroupForm();
        _kickstartRomBox = new TextBox { PlaceholderText = "Choose your Kickstart 1.3 ROM", MinWidth = 0 };
        _kickstartRomBox.TextChanged += (_, _) => MarkSettingsRestartRequired();
        var browse = CreatePanelButton("Browse…", () => _ = PickRomAsync());
        AutomationProperties.SetName(browse, "Browse for Kickstart ROM");
        form.Children.Add(CreateSettingsRow("Kickstart ROM", WithAction(_kickstartRomBox, browse)));
        AutomationProperties.SetName(_kickstartRomBox, "Kickstart 1.3 ROM path");
        _romValidation = SettingsNote("Choose a Kickstart 1.3 ROM to continue.");
        AutomationProperties.SetName(_romValidation, "ROM validation");
        form.Children.Add(_romValidation);
        _setupDiskBox = new TextBox { PlaceholderText = "Optional · ADF, IPF or ZIP", MinWidth = 0 };
        _setupDiskBox.TextChanged += (_, _) =>
        {
            if (_updatingSettingsUi || _drivePathBoxes[0] == null) return;
            _drivePathBoxes[0].Text = _setupDiskBox.Text;
        };
        var diskBrowse = CreatePanelButton("Browse…", () => _ = PickSettingsDiskAsync(0));
        AutomationProperties.SetName(diskBrowse, "Browse for startup disk");
        form.Children.Add(CreateSettingsRow("Startup disk", WithAction(_setupDiskBox, diskBrowse)));
        AutomationProperties.SetName(_setupDiskBox, "Optional startup disk path");
        layout.Children.Add(CreateSettingsGroup("ROM and disk", form));

        var options = CreateSettingsGroupForm();
        _kickstartSourceBox = AddComboSetting(options, "Kickstart", ["Kickstart13Rom", "KickstartRom", "CopperStart", "DiagRom"]);
        _engineBox = AddComboSetting(options, "Engine", ["Lightweight", "Legacy"]);
        _cpuBackendBox = AddComboSetting(options, "CPU backend", ["AccurateM68000", "AccurateM68EC020", "AccurateM68020", "AccurateM68030", "AccurateM68040", "JitM68040"]);
        options.Children.Add(SettingsNote("Greyed choices are planned and not yet available. They cannot be selected."));
        var reset = CreatePanelButton("Use supported Amiga 500 settings", UseSupportedMachine);
        options.Children.Add(reset);
        layout.Children.Add(new Expander { Header = "Machine options and availability", Content = options, HorizontalAlignment = HorizontalAlignment.Stretch });
        return CreateScrollableSettingsPage(layout);
    }

    private static Grid WithAction(Control field, Control action)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
        row.Children.Add(field);
        Grid.SetColumn(action, 1);
        row.Children.Add(action);
        return row;
    }

    private void UseSupportedMachine()
    {
        var rom = _kickstartRomBox.Text;
        var disk = _setupDiskBox.Text;
        _settingsDraft = new CopperScreenSettingsDraft
        {
            KickstartRomPath = rom,
            Input = _committedSettings.Input,
            PresentationOptions = _committedSettings.PresentationOptions
        };
        _settingsDraft.DriveDiskPaths[0] = string.IsNullOrWhiteSpace(disk) ? null : disk;
        _settingsDraft.DriveWriteProtected[0] = true;
        _settingsStartupError = null;
        RefreshSettingsUi();
    }

    private Control CreateProfilesPage()
    {
        var layout = CreateSettingsPageLayout();
        layout.Children.Add(SettingsNote("Choose a profile, then Load profile. Machines that are not yet available remain listed but cannot be selected."));
        _profileDirectoryText = SettingsNote(string.Empty);
        _profileList = new ListBox
        {
            MinHeight = 180, MaxHeight = 270, HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemTemplate = new FuncDataTemplate<CopperScreenProfileSummary>((profile, _) =>
            {
                if (profile == null) return new TextBlock();
                var item = new StackPanel { Spacing = 3, Margin = new Thickness(4) };
                item.Children.Add(new TextBlock { Text = profile.DisplayName, TextWrapping = TextWrapping.Wrap, FontWeight = FontWeight.SemiBold });
                item.Children.Add(new TextBlock { Text = profile.IsAvailable ? "Available" : "Not yet available", Foreground = MutedText, FontSize = 12 });
                ToolTip.SetTip(item, profile.UnavailableReason ?? profile.Path);
                return item;
            })
        };
        AutomationProperties.SetName(_profileList, "Machine profiles");
        _profileList.ContainerPrepared += (_, args) =>
        {
            if (_profileList.Items[args.Index] is CopperScreenProfileSummary profile)
            {
                args.Container.IsEnabled = profile.IsAvailable;
                AutomationProperties.SetName(args.Container, profile.ToString());
            }
        };
        layout.Children.Add(_profileList);
        var actions = new WrapPanel();
        var load = CreatePanelButton("Load profile", LoadSelectedProfile);
        _profileList.SelectionChanged += (_, _) => load.IsEnabled = _profileList.SelectedItem is CopperScreenProfileSummary { IsAvailable: true };
        foreach (var button in new[] { load, CreatePanelButton("Save profile", SaveCurrentProfile), CreatePanelButton("Save a copy", SaveCurrentProfileAs) })
        { button.Margin = new Thickness(0, 0, 8, 0); actions.Children.Add(button); }
        layout.Children.Add(actions);
        var metadata = CreateSettingsGroupForm();
        _profileNameBox = AddTextSetting(metadata, "Display name");
        _profileDescriptionBox = AddTextSetting(metadata, "Description");
        _profileDescriptionBox.TextWrapping = TextWrapping.Wrap;
        _profileDescriptionBox.AcceptsReturn = true;
        _profileDescriptionBox.MinHeight = 64;
        _profileIdBox = AddTextSetting(metadata, "File name");
        layout.Children.Add(CreateSettingsGroup("Profile details", metadata));
        layout.Children.Add(new Expander { Header = "Profile folder", Content = _profileDirectoryText });
        return CreateScrollableSettingsPage(layout);
    }

    private void ConfigureChoices(ComboBox combo, string label)
    {
        bool Available(string value) => CopperScreenAvailability.IsChoiceAvailable(label, value) &&
            (label != "Profile kind" || value != "Mouse" ||
             _controllerProfileChooser.SelectedItem is not CopperScreenControllerProfile profile ||
             !string.Equals(profile.Id, _settingsDraft.Input.Port2ProfileId, StringComparison.OrdinalIgnoreCase));
        // Keyboard selection must also respect availability before a popup has
        // realized its containers. Programmatic loading keeps unsupported values visible.
        object? previous = combo.Items.FirstOrDefault(item => Available(item?.ToString() ?? ""));
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedItem is not string selected) return;
            if (_updatingSettingsUi || Available(selected)) previous = selected;
            else combo.SelectedItem = previous;
        };
        combo.ItemTemplate = new FuncDataTemplate<string>((value, _) => new TextBlock
        {
            Text = value == null ? string.Empty : CopperScreenAvailability.ChoiceLabel(value) + (Available(value) ? "" : " — Not yet available"),
            TextWrapping = TextWrapping.Wrap
        });
        combo.ContainerPrepared += (_, args) =>
        {
            var value = combo.Items[args.Index]?.ToString() ?? "";
            args.Container.IsEnabled = Available(value);
            AutomationProperties.SetName(args.Container, CopperScreenAvailability.ChoiceLabel(value) + (args.Container.IsEnabled ? "" : ", not yet available"));
        };
        combo.DropDownOpened += (_, _) =>
        {
            for (var i = 0; i < combo.ItemCount; i++)
                if (combo.ContainerFromIndex(i) is Control container)
                    container.IsEnabled = Available(combo.Items[i]?.ToString() ?? "");
        };
    }

    private void ConfigureControllerChoices(ComboBox combo, int port)
    {
        string? previousId = null;
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedItem is not CopperScreenControllerProfile selected) return;
            if (_updatingSettingsUi || CopperScreenAvailability.IsControllerAvailable(port, selected.Kind)) previousId = selected.Id;
            else SelectControllerProfile(combo, previousId ?? "none");
        };
        combo.ItemTemplate = new FuncDataTemplate<CopperScreenControllerProfile>((profile, _) => new TextBlock
        {
            Text = profile == null ? string.Empty : profile.DisplayName + (CopperScreenAvailability.IsControllerAvailable(port, profile.Kind) ? "" : " — Not yet available"),
            TextWrapping = TextWrapping.Wrap
        });
        combo.ContainerPrepared += (_, args) =>
        {
            if (combo.Items[args.Index] is not CopperScreenControllerProfile profile) return;
            args.Container.IsEnabled = CopperScreenAvailability.IsControllerAvailable(port, profile.Kind);
            AutomationProperties.SetName(args.Container, profile.DisplayName + (args.Container.IsEnabled ? "" : ", not yet available"));
        };
    }

    private Control CreateAudioPage()
    {
        var layout = CreateSettingsPageLayout();
        var playback = CreateSettingsGroupForm();
        _masterVolumeSlider = new Slider { Minimum = 0, Maximum = 100, Value = _outputVolume, TickFrequency = 1, IsSnapToTickEnabled = true };
        var level = new TextBlock { Text = $"{_outputVolume:0}%", VerticalAlignment = VerticalAlignment.Center, MinWidth = 44 };
        _masterVolumeSlider.PropertyChanged += (_, args) =>
        {
            if (args.Property == Slider.ValueProperty) level.Text = $"{_masterVolumeSlider.Value:0}%";
        };
        AutomationProperties.SetName(_masterVolumeSlider, "Emulator playback volume");
        playback.Children.Add(CreateSettingsRow("Emulator volume", WithAction(_masterVolumeSlider, level)));
        _masterMuteBox = new CheckBox { Content = "Mute emulator audio", IsChecked = _outputMuted };
        playback.Children.Add(_masterMuteBox);
        playback.Children.Add(SettingsNote("Playback volume applies to this app session and does not change the Amiga's audio output."));
        layout.Children.Add(CreateSettingsGroup("Playback", playback));
        var floppy = CreateSettingsGroupForm();
        _floppySoundsEnabledBox = new CheckBox { Content = "Play floppy drive sounds" };
        _floppySoundsEnabledBox.IsCheckedChanged += (_, _) => ApplyFloppySoundsEnabledSetting();
        floppy.Children.Add(_floppySoundsEnabledBox);
        _floppySoundModeBox = AddComboSetting(floppy, "Sound mode", ["Synthetic", "Samples"]);
        _floppySoundPackBox = AddTextSetting(floppy, "Sound pack");
        _floppySoundVolumeBox = AddTextSetting(floppy, "Drive volume (0–1)");
        floppy.Children.Add(SettingsNote("Changes to floppy drive sounds take effect after restarting the Amiga."));
        layout.Children.Add(CreateSettingsGroup("Floppy drive effects", floppy));
        return CreateScrollableSettingsPage(layout);
    }

    private Control CreateKeyBindingRow(string action, int index)
    {
        _bindingLabels[index] = new TextBlock { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        _bindingButtons[index] = CreatePanelButton("Change…", () => _ = CaptureBindingAsync(index, action));
        AutomationProperties.SetName(_bindingButtons[index], "Change joystick " + action.ToLowerInvariant() + " key");
        return CreateSettingsRow(action, WithAction(_bindingLabels[index], _bindingButtons[index]));
    }

    private void RefreshKeyBindingLabels(bool enabled)
    {
        for (var i = 0; i < 6; i++)
        {
            _bindingLabels[i].Text = (_joystickKeyBoxes[i].Text ?? "").Replace("NumPad", "Numpad ").Replace("LeftCtrl", "Left Ctrl");
            _bindingButtons[i].IsEnabled = enabled;
        }
    }

    private async Task CaptureBindingAsync(int index, string action)
    {
        if (_controllerProfileChooser.SelectedItem is not CopperScreenControllerProfile selected) return;
        var prompt = SettingsNote($"Press the key for {action.ToLowerInvariant()}.\nThis replaces the existing keys for this action. Escape cancels.");
        var dialog = new Window { Title = "Set joystick key", Width = 430, Height = 190, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Surface };
        var panel = new StackPanel { Spacing = 16, Margin = new Thickness(20) };
        panel.Children.Add(prompt);
        panel.Children.Add(CreatePanelButton("Cancel", () => dialog.Close((string?)null)));
        dialog.Content = panel;
        dialog.AddHandler(KeyDownEvent, (_, args) =>
        {
            args.Handled = true;
            if (args.Key == Key.Escape) { dialog.Close((string?)null); return; }
            var key = args.PhysicalKey == PhysicalKey.None ? args.Key.ToString() : args.PhysicalKey.ToString();
            if (args.Key == Key.F1 || CopperScreenJoystickKeyMap.IsReservedHostKeyName(key))
            { prompt.Text = "That key is reserved for CopperScreen controls. Choose another key, or Escape to cancel."; return; }
            dialog.Close(key);
        }, RoutingStrategies.Tunnel);
        var result = await dialog.ShowDialog<string?>(_settingsWindow);
        if (result == null) return;
        try
        {
            var current = _settingsDraft.Input.ControllerProfiles.First(profile => profile.Id == selected.Id);
            var keys = current.JoystickKeys;
            keys = index switch
            {
                0 => keys with { Up = [result] }, 1 => keys with { Down = [result] },
                2 => keys with { Left = [result] }, 3 => keys with { Right = [result] },
                4 => keys with { Fire = [result] }, _ => keys with { SecondFire = [result] }
            };
            _settingsDraft.Input = _settingsDraft.Input.WithControllerProfiles(_settingsDraft.Input.ControllerProfiles
                .Select(profile => profile.Id == current.Id ? current with { JoystickKeys = keys } : profile));
            RefreshControllerSelectors();
            UpdateSettingsStatus();
        }
        catch (Exception ex)
        {
            SetSettingsError("Could not change the key: " + ex.Message);
        }
    }

    private async Task PickRomAsync()
    {
        var files = await _settingsWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose Kickstart 1.3 ROM", AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Kickstart ROM") { Patterns = ["*.rom", "*.bin", "*.kick", "*.zip"] }, FilePickerFileTypes.All]
        });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path != null) _kickstartRomBox.Text = path;
    }

    private string? ValidateSelectedRom(CopperScreenSettingsDraft draft)
    {
        var path = draft.ToStartupOptions(AppContext.BaseDirectory).KickstartRomPath;
        if (string.IsNullOrWhiteSpace(path)) return "Choose a Kickstart 1.3 ROM to continue.";
        if (!File.Exists(path)) return "ROM file not found. Choose an existing Kickstart 1.3 ROM.";
        var info = new FileInfo(path);
        if (_validatedRomPath == path && _validatedRomWriteTime == info.LastWriteTimeUtc && _validatedRomLength == info.Length) return _romError;
        _validatedRomPath = path;
        _validatedRomWriteTime = info.LastWriteTimeUtc;
        _validatedRomLength = info.Length;
        try
        {
            _ = CopperScreenKickstartRomArchive.ReadNative13Rom(path, draft.KickstartSource, draft.RomVersion);
            _romError = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException or ArgumentException)
        { _romError = ex.Message; }
        return _romError;
    }

    private async Task ApplySettingsAsync()
    {
        if (_runtime == null || _applyingSettings) return;
        if (!TryApplySettingsFromUi(out var error)) { SetSettingsError(error); return; }
        if (_settingsDraft.NeedsRestartComparedWith(_committedSettings))
        { SetSettingsError("Use Restart Amiga to apply machine changes."); return; }
        _applyingSettings = true;
        UpdateSettingsStatus();
        try
        {
            CopperScreenLightweightSession.Validate(_settingsDraft.ToStartupOptions(AppContext.BaseDirectory));
            for (var i = 0; i < _settingsDraft.FloppyDriveCount; i++)
            {
                var disk = _settingsDraft.DriveDiskPaths[i];
                if (string.Equals(disk, _latestState.Drives[i].DiskPath, StringComparison.Ordinal)) continue;
                var result = string.IsNullOrWhiteSpace(disk) ? await _runtime.EjectDiskAsync(i) : await _runtime.InsertDriveDiskAsync(i, disk);
                if (!result.Success) { SetSettingsError(result.Message); return; }
                _latestState = result.State;
                _settingsDraft.DriveDiskPaths[i] = result.State.Drives[i].DiskPath;
                ShowDiskFeedback(result.Message, true);
            }
            _committedSettings = _settingsDraft.Clone();
            _committedSettings.ClearRestartRequired();
            ApplyCommittedHostSettings();
            CloseSettingsAfterApply();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException)
        { SetSettingsError(ex.Message); }
        finally { _applyingSettings = false; UpdateSettingsStatus(); }
    }

    private void ApplyCommittedHostSettings()
    {
        _inputOptions = _committedSettings.Input;
        _presentationOptions = _committedSettings.PresentationOptions;
        _outputVolume = _masterVolumeSlider.Value;
        _outputMuted = _masterMuteBox.IsChecked == true;
        _runtime?.SetInputOptions(_inputOptions);
        _runtime?.SetPresentationOptions(_presentationOptions);
        _runtime?.SetOutputVolume(_outputMuted ? 0 : (float)(_outputVolume / 100));
        if (_presentationOptions.LacedMode != CopperScreenLacedPresentationMode.CrtPhosphor) DisableCrtPhosphorPresentation();
        ApplyPresenterGeometry();
        RebuildGamepadPortBindings();
        ApplyCurrentGamepadSnapshots();
    }

    private void CloseSettingsAfterApply()
    {
        _settingsVisible = false;
        _settingsWindow.Hide();
        _settingsStartupError = null;
        RefreshCopperBenchUi();
        _presenter.Focus();
    }
}
