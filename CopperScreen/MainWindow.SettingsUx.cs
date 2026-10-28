using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using System.Text.Json;

namespace CopperScreen;

internal sealed partial class MainWindow
{
    private CopperScreenSettingsDraft _committedSettings = null!;
    private CopperScreenPresentationOptions _presentationOptions = CopperScreenPresentationOptions.Default;
    private TextBox _setupDiskBox = null!;
    private TextBlock _romValidation = null!;
    private Slider _masterVolumeSlider = null!;
    private CheckBox _masterMuteBox = null!;
    private Button _settingsRevertButton = null!;
    private readonly TextBlock[] _bindingLabels = new TextBlock[6];
    private readonly Button[] _bindingButtons = new Button[6];
    private bool _applyingSettings;
    private string? _validatedRomPath, _romError;
    private DateTime _validatedRomWriteTime;
    private long _validatedRomLength;
    private CopperScreenKickstartSource _validatedRomSource;
    private KickstartVersion _validatedRomVersion;
    private bool _validatedRomA1200;
    private string? _lastAutoSavedSettings;
    private CopperScreenSettingsDraft? _lastAutoSavedDraft;
    private bool _applyingLiveMedia;
    private string? _lastLiveMediaRequest;

    private void ApplyLiveSettings()
    {
        if (!_settingsVisible || _updatingSettingsUi || _applyingSettings) return;
        try
        {
            var draft = ReadSettingsDraft();
            _outputVolume = _masterVolumeSlider.Value;
            _outputMuted = _masterMuteBox.IsChecked == true;
            _runtime?.SetOutputVolume(_outputMuted ? 0 : (float)(_outputVolume / 100));
            if (JsonSerializer.Serialize(draft.Input) != JsonSerializer.Serialize(_committedSettings.Input))
            {
                _inputOptions = draft.Input;
                _committedSettings.Input = draft.Input;
                _runtime?.SetInputOptions(draft.Input);
                RebuildGamepadPortBindings();
                ApplyCurrentGamepadSnapshots();
            }
            if (draft.PresentationOptions != _committedSettings.PresentationOptions)
            {
                _presentationOptions = draft.PresentationOptions;
                _committedSettings.PresentationOptions = draft.PresentationOptions;
                _runtime?.SetPresentationOptions(draft.PresentationOptions);
                if (_presentationOptions.LacedMode != CopperScreenLacedPresentationMode.CrtPhosphor) DisableCrtPhosphorPresentation();
                ApplyPresenterGeometry();
            }
            if (_runtime != null && !_applyingLiveMedia && CopperScreenLaunchWorkflow.StartChoice(draft) == CopperScreenLaunchWorkflow.StartChoice(_committedSettings))
            {
                var request = JsonSerializer.Serialize(new { draft.DriveDiskPaths, draft.DriveWriteProtected, draft.FloppyDriveCount });
                if (request != _lastLiveMediaRequest)
                {
                    _lastLiveMediaRequest = request;
                    _ = ApplyLiveMediaAsync(draft);
                }
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or FormatException or OverflowException) { }
    }

    private async Task ApplyLiveMediaAsync(CopperScreenSettingsDraft draft)
    {
        var runtime = _runtime;
        if (runtime == null) return;
        _applyingLiveMedia = true;
        try
        {
            for (var i = 0; i < Math.Min(draft.FloppyDriveCount, _committedSettings.FloppyDriveCount); i++)
            {
                var state = runtime.CurrentState.Drives[i];
                if (state.DiskPath != draft.DriveDiskPaths[i])
                {
                    var result = string.IsNullOrWhiteSpace(draft.DriveDiskPaths[i])
                        ? await runtime.EjectDiskAsync(i) : await runtime.InsertDriveDiskAsync(i, draft.DriveDiskPaths[i]!);
                    if (!result.Success) { SetSettingsError(result.Message); return; }
                    state = result.State.Drives[i];
                }
                if (draft.DriveWriteProtected[i] is { } writeProtected && state.WriteProtected != writeProtected)
                {
                    var result = await runtime.SetDriveWriteProtectedAsync(i, writeProtected);
                    if (!result.Success) { SetSettingsError(result.Message); return; }
                }
                if (_runtime != runtime) return;
                _committedSettings.DriveDiskPaths[i] = draft.DriveDiskPaths[i];
                _committedSettings.DriveWriteProtected[i] = draft.DriveWriteProtected[i];
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        { SetSettingsError(ex.Message); }
        catch (OperationCanceledException) when (_isClosed || _runtime != runtime) { }
        finally
        {
            _applyingLiveMedia = false;
            if (!_isClosed && _runtime == runtime) UpdateSettingsStatus();
        }
    }

    private void RememberSettingsEdits()
    {
        if (!_settingsVisible || _updatingSettingsUi || _applyingSettings) return;
        try
        {
            var draft = ReadSettingsDraft();
            var serialized = CopperScreenProfileStore.Serialize(draft, AppContext.BaseDirectory);
            var volume = _masterVolumeSlider.Value;
            var muted = _masterMuteBox.IsChecked == true;
            if (serialized == _lastAutoSavedSettings && volume == _recentSession.Volume && muted == _recentSession.Muted) return;
            _recentSession.Save(draft, volume, muted, AppContext.BaseDirectory);
            if (_recentSession.LastError is { } error) _settingsStartupError = error;
            else
            {
                _lastAutoSavedSettings = serialized;
                _lastAutoSavedDraft = draft.Clone();
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException or FormatException or OverflowException)
        {
            // A partially typed number/address is not a complete configuration.
            // Keep the most recent complete edit until the field can be read.
        }
    }

    private static TextBlock SettingsNote(string text) => new()
    {
        Text = text, Foreground = MutedText, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6)
    };

    private Control CreateSetupPage()
    {
        var layout = CreateSettingsPageLayout();
        layout.Spacing = 16;
        layout.Children.Add(new TextBlock { Text = "Start your Amiga", FontSize = 24, FontWeight = FontWeight.SemiBold, Foreground = Brushes.White });
        layout.Children.Add(CreateMachineControls(welcome: false));
        layout.Children.Add(SettingsNote("Selecting a model restores its saved configuration. Changes are saved automatically. Change memory sizes and connected drives in their settings pages."));
        var form = CreateSettingsGroupForm();
        _kickstartSourceBox = AddComboSetting(form, "Boot method", ["Kickstart13Rom", "Kickstart31Rom", "KickstartRom", "MinimalDiskBoot", "CopperStart", "DiagRom"]);
        _kickstartSourceBox.SelectionChanged += (_, _) => UpdateBootMethodControls();
        _kickstartRomBox = new TextBox { PlaceholderText = "Choose Kickstart 1.3, A500+ 2.04, A500 3.1 or A1200 3.0 ROM", MinWidth = 0 };
        _kickstartRomBox.TextChanged += (_, _) => MarkSettingsRestartRequired();
        var browse = CreatePanelButton("Browse…", () => _ = PickRomAsync());
        AutomationProperties.SetName(browse, "Browse for boot ROM");
        _bootRomRow = CreateSettingsRow("Boot ROM", WithAction(_kickstartRomBox, browse));
        form.Children.Add(_bootRomRow);
        AutomationProperties.SetName(_kickstartRomBox, "Boot ROM path");
        _romValidation = SettingsNote("Choose a Kickstart 1.3, A500+ 2.04, A500 3.1 or A1200 3.0 ROM to continue.");
        AutomationProperties.SetName(_romValidation, "ROM validation");
        form.Children.Add(_romValidation);
        form.Children.Add(SettingsNote("CopperStart requires the separately qualified ROM for the selected Amiga model. A1200 uses a CopperOS FFS HDF with empty floppy drives; no Fast RAM, RTC or shared folders."));
        _setupDiskBox = new TextBox { PlaceholderText = "Optional · ADF, ADZ, DMS, IPF, SCP or ZIP", MinWidth = 0 };
        _setupDiskBox.TextChanged += (_, _) =>
        {
            if (_updatingSettingsUi || _drivePathBoxes[0] == null) return;
            _drivePathBoxes[0].Text = _setupDiskBox.Text;
        };
        var diskBrowse = CreatePanelButton("Browse…", () => _ = PickSettingsDiskAsync(0));
        AutomationProperties.SetName(diskBrowse, "Browse for startup disk");
        form.Children.Add(CreateSettingsRow("Startup disk", WithAction(_setupDiskBox, diskBrowse)));
        AutomationProperties.SetName(_setupDiskBox, "Optional startup disk path");
        layout.Children.Add(CreateSettingsGroup("Boot and disk", form));

        var options = CreateSettingsGroupForm();
        _engineBox = AddComboSetting(options, "Engine", ["Lightweight", "Legacy"]);
        _cpuBackendBox = AddComboSetting(options, "CPU backend", ["AccurateM68000", "AccurateM68010", "AccurateM68EC020", "AccurateM68020", "AccurateM68030", "AccurateM68040", "AccurateM68060", "JitM68040"], markRestart: false);
        _cpuBackendBox.SelectionChanged += (_, _) =>
        {
            if (_updatingSettingsUi || _updatingMachineControls || _cpuBackendBox.SelectedItem is not string selected) return;
            EditMachine(draft => { CopperScreenMachinePresets.SetCpu(draft, Enum.Parse<M68kBackendKind>(selected)); draft.MarkRequiresRestart(); return draft; });
        };
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

    private void SetChipRamChoices(DmaChipModel model)
        => _chipRamBox.ItemsSource = CopperScreenAvailability.ChipRamChoices(model);

    private void UpdateChipRamChoices()
    {
        if (_updatingSettingsUi || _agnusModelBox.SelectedItem is not string selected) return;
        var model = Enum.Parse<DmaChipModel>(selected);
        var previous = _chipRamBox.SelectedItem as string;
        _updatingSettingsUi = true;
        try
        {
            var choices = CopperScreenAvailability.ChipRamChoices(model);
            _chipRamBox.ItemsSource = choices;
            _chipRamBox.SelectedItem = choices.Contains(previous) ? previous : choices[0];
            if (_chipRamBox.SelectedItem is not "512") _pseudoFastRamBox.Text = "0";
        }
        finally { _updatingSettingsUi = false; }
        MarkSettingsRestartRequired();
    }

    private Control CreateProfilesPage()
    {
        var layout = CreateSettingsPageLayout();
        layout.Children.Add(SettingsNote("Choose a profile, then Load profile. Saving a preset creates a personal profile and keeps the bundled original. Unavailable machines cannot be selected."));
        _profileDirectoryText = SettingsNote(string.Empty);
        _profileList = new ListBox
        {
            MinHeight = 180, MaxHeight = 270, HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemTemplate = new FuncDataTemplate<CopperScreenProfileSummary>((profile, _) =>
            {
                if (profile == null) return new TextBlock();
                var item = new StackPanel { Spacing = 3, Margin = new Thickness(4) };
                item.Children.Add(new TextBlock { Text = profile.DisplayName, TextWrapping = TextWrapping.Wrap, FontWeight = FontWeight.SemiBold });
                item.Children.Add(new TextBlock { Text = profile.IsAvailable ? (profile.IsBundled ? "Bundled preset" : "Personal profile") : "Not yet available", Foreground = MutedText, FontSize = 12 });
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
        layout.Children.Add(new Expander { Header = "Personal profile folder", Content = _profileDirectoryText });
        return CreateScrollableSettingsPage(layout);
    }

    private void ConfigureChoices(ComboBox combo, string label)
    {
        bool Available(string value) => CopperScreenAvailability.IsChoiceAvailable(label, value) &&
            (label != "CPU backend" || CopperScreenMachinePresets.CpuChoices(_settingsDraft).Contains(value)) &&
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
            Text = profile == null ? string.Empty : InputChoiceName(port, profile),
            TextWrapping = TextWrapping.Wrap
        });
        combo.ContainerPrepared += (_, args) =>
        {
            if (combo.Items[args.Index] is not CopperScreenControllerProfile profile) return;
            args.Container.IsEnabled = CopperScreenAvailability.IsControllerAvailable(port, profile.Kind);
            AutomationProperties.SetName(args.Container, InputChoiceName(port, profile));
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
            if (args.Property == Slider.ValueProperty) { level.Text = $"{_masterVolumeSlider.Value:0}%"; UpdateSettingsStatus(); }
        };
        AutomationProperties.SetName(_masterVolumeSlider, "Emulator playback volume");
        playback.Children.Add(CreateSettingsRow("Emulator volume", WithAction(_masterVolumeSlider, level)));
        _masterMuteBox = new CheckBox { Content = "Mute emulator audio", IsChecked = _outputMuted };
        _masterMuteBox.IsCheckedChanged += (_, _) => UpdateSettingsStatus();
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
        var copperStart = ParseKickstartSourceSelection(_kickstartSourceBox.SelectedItem) == CopperScreenKickstartSource.CopperStart;
        var owner = _settingsVisible ? (TopLevel)_settingsWindow : this;
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = copperStart ? _settingsDraft.Chipset.DisplayChip == DisplayChipModel.AgaLisa ? "Choose CopperStart A1200 ROM (1 MiB)" : "Choose qualified CopperStart A500 ROM" : "Choose Kickstart 1.3, A500+ 2.04, A500 3.1 or A1200 3.0 ROM", AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Boot ROM") { Patterns = ["*.rom", "*.bin", "*.kick", "*.zip"] }, FilePickerFileTypes.All]
        });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path == null) return;
        _kickstartRomBox.Text = path;
        try
        {
            if (copperStart)
            {
                _ = CopperScreenCopperStartRom.Read(path, _settingsDraft.Chipset.DisplayChip == DisplayChipModel.AgaLisa);
                ClearSettingsStartupError();
                UpdateSettingsStatus();
                return;
            }
            if (!_settingsVisible)
            {
                // The start selector owns the model/version. A picker must not
                // silently turn an A500+ selection into Kickstart 1.3.
                _ = CopperScreenKickstartRomArchive.ReadNativeRom(path, _settingsDraft.KickstartSource,
                    _settingsDraft.RomVersion, _settingsDraft.Chipset.DisplayChip == DisplayChipModel.AgaLisa);
                ClearSettingsStartupError();
                UpdateSettingsStatus();
                return;
            }
            var selectedVersion = _settingsDraft.Chipset.DisplayChip == DisplayChipModel.AgaLisa ? KickstartVersion.Kickstart30 : ParseKickstartSourceSelection(_kickstartSourceBox.SelectedItem) switch
            {
                CopperScreenKickstartSource.Kickstart13Rom => KickstartVersion.Kickstart13,
                CopperScreenKickstartSource.Kickstart31Rom => KickstartVersion.Kickstart31,
                _ => _settingsDraft.RomVersion
            };
            var rom = CopperScreenKickstartRomArchive.ReadRomImage(path, CopperScreenKickstartSource.KickstartRom, selectedVersion);
            if (_settingsDraft.Chipset.DisplayChip == DisplayChipModel.AgaLisa)
            {
                _ = CopperScreenKickstartRomArchive.ReadNativeRom(path, CopperScreenKickstartSource.KickstartRom, KickstartVersion.Kickstart30, a1200: true);
                _settingsDraft.RomVersion = KickstartVersion.Kickstart30;
                _kickstartSourceBox.SelectedItem = "KickstartRom";
            }
            else
            {
                _settingsDraft.RomVersion = CopperScreenKickstartRomArchive.IdentifyNativeVersion(rom);
                _kickstartSourceBox.SelectedItem = _settingsDraft.RomVersion switch
                {
                    KickstartVersion.Kickstart20 => "KickstartRom",
                    KickstartVersion.Kickstart31 => "Kickstart31Rom",
                    _ => "Kickstart13Rom"
                };
            }
            UpdateSettingsStatus();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException or ArgumentException)
        { SetSettingsError(ex.Message); }
    }

    private string? ValidateSelectedRom(CopperScreenSettingsDraft draft)
    {
        if (draft.KickstartSource == CopperScreenKickstartSource.MinimalDiskBoot)
        {
            var disk = draft.ToStartupOptions(AppContext.BaseDirectory).DriveDiskPaths[0];
            if (string.IsNullOrWhiteSpace(disk)) return "Choose an ADF startup disk in DF0. No Kickstart ROM is needed.";
            if (!CopperScreenDiskImageArchive.DiskPathExists(disk)) return "Startup disk not found. Choose an existing ADF disk.";
            return ValidateMinimalBootDisk(disk);
        }
        var path = draft.ToStartupOptions(AppContext.BaseDirectory).KickstartRomPath;
        if (string.IsNullOrWhiteSpace(path)) return draft.KickstartSource == CopperScreenKickstartSource.CopperStart ? CopperScreenCopperStartRom.RecoveryFor(draft.Chipset.DisplayChip == DisplayChipModel.AgaLisa) : "Choose a Kickstart 1.3, A500+ 2.04, A500 3.1 or A1200 3.0 ROM to continue.";
        if (!File.Exists(path)) return draft.KickstartSource == CopperScreenKickstartSource.CopperStart ? "ROM file not found. " + CopperScreenCopperStartRom.RecoveryFor(draft.Chipset.DisplayChip == DisplayChipModel.AgaLisa) : "ROM file not found. Choose an existing Kickstart 1.3, A500+ 2.04, A500 3.1 or A1200 3.0 ROM.";
        var info = new FileInfo(path);
        if (_validatedRomPath == path && _validatedRomWriteTime == info.LastWriteTimeUtc && _validatedRomLength == info.Length &&
            _validatedRomSource == draft.KickstartSource && _validatedRomVersion == draft.RomVersion && _validatedRomA1200 == (draft.Chipset.DisplayChip == DisplayChipModel.AgaLisa)) return _romError;
        _validatedRomPath = path;
        _validatedRomWriteTime = info.LastWriteTimeUtc;
        _validatedRomLength = info.Length;
        _validatedRomSource = draft.KickstartSource;
        _validatedRomVersion = draft.RomVersion;
        _validatedRomA1200 = draft.Chipset.DisplayChip == DisplayChipModel.AgaLisa;
        try
        {
            _ = draft.KickstartSource == CopperScreenKickstartSource.CopperStart ? CopperScreenCopperStartRom.Read(path, draft.Chipset.DisplayChip == DisplayChipModel.AgaLisa)
                : CopperScreenKickstartRomArchive.ReadNativeRom(path, draft.KickstartSource, draft.RomVersion, draft.Chipset.DisplayChip == DisplayChipModel.AgaLisa);
            _romError = null;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException or ArgumentException)
        { _romError = ex.Message; }
        return _romError;
    }

    private string? _validatedBootDisk, _bootDiskError;
    private DateTime _validatedBootDiskWriteTime;
    private long _validatedBootDiskLength;
    private string? ValidateMinimalBootDisk(string path)
    {
        var backing = CopperScreenDiskImageArchive.TrySplitEntryPath(path, out var archive, out _) ? archive : path;
        var info = new FileInfo(backing);
        if (_validatedBootDisk == path && _validatedBootDiskWriteTime == info.LastWriteTimeUtc && _validatedBootDiskLength == info.Length)
            return _bootDiskError;
        _validatedBootDisk = path;
        _validatedBootDiskWriteTime = info.LastWriteTimeUtc;
        _validatedBootDiskLength = info.Length;
        try { CopperScreenMinimalBoot.ValidateDisk(CopperScreenDiskImageArchive.LoadDiskImage(path)); _bootDiskError = null; }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException or ArgumentException)
        { _bootDiskError = ex.Message; }
        return _bootDiskError;
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
        UpdateSettingsStatus();
        if (_runtime != null) _presenter.Focus();
        else _welcomeStart.Focus();
    }
}
