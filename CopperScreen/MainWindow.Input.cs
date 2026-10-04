using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;

namespace CopperScreen;

internal sealed partial class MainWindow
{
    private readonly ComboBox[] _quickPortSelectors = new ComboBox[2];
    private readonly StackPanel[] _quickPortBindings = new StackPanel[2];
    private StackPanel _quickKeyboardMode = null!;
    private TextBlock _quickKeyboardModeNote = null!;
    private Button _swapInputPortsButton = null!;
    private bool _updatingQuickInputs;
    private CopperScreenInputOptions? _displayedQuickInputs;
    private NumpadInputMode? _displayedKeyboardMode;

    private StackPanel CreateInputHelp()
    {
        var panel = new StackPanel { Spacing = 14, Width = 370 };
        panel.Children.Add(new TextBlock { Text = "Input ports", FontSize = 20, FontWeight = FontWeight.SemiBold });
        panel.Children.Add(new TextBlock { Text = "Most games use a joystick in Port 2. Port 1 is usually the mouse.", Foreground = MutedText, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        for (var i = 0; i < 2; i++)
        {
            var port = i + 1;
            var group = new StackPanel { Spacing = 7 };
            group.Children.Add(new TextBlock { Text = port == 1 ? "Port 1 · Mouse or second joystick" : "Port 2 · Main joystick", FontWeight = FontWeight.SemiBold });
            var combo = new ComboBox
            {
                MinWidth = 0, MinHeight = 38, HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch, MaxDropDownHeight = 260,
                SelectionBoxItemTemplate = new FuncDataTemplate<CopperScreenControllerProfile>((profile, _) => new TextBlock { Text = profile == null ? "" : InputChoiceName(port, profile), TextTrimming = TextTrimming.CharacterEllipsis }),
                ItemTemplate = new FuncDataTemplate<CopperScreenControllerProfile>((profile, _) => new TextBlock { Text = profile == null ? "" : InputChoiceName(port, profile), TextWrapping = TextWrapping.Wrap })
            };
            combo.CornerRadius = new CornerRadius(8);
            AutomationProperties.SetName(combo, $"Port {port} input");
            combo.SelectionChanged += (_, _) =>
            {
                if (_updatingQuickInputs || combo.SelectedItem is not CopperScreenControllerProfile selected) return;
                if (IsQuickInputAvailable(port, selected))
                    ApplyQuickInputs(_inputOptions.WithPortAssignment(port, selected.Id), enableKeyboardJoystick: selected.Kind == CopperScreenControllerKind.KeyboardJoystick);
                else RefreshQuickInputControls(force: true);
            };
            combo.ContainerPrepared += (_, args) =>
            {
                if (combo.Items[args.Index] is CopperScreenControllerProfile profile)
                {
                    args.Container.IsEnabled = IsQuickInputAvailable(port, profile);
                    AutomationProperties.SetName(args.Container, InputChoiceName(port, profile));
                }
            };
            combo.DropDownOpened += (_, _) =>
            {
                ReleaseInteractiveInput();
                for (var item = 0; item < combo.ItemCount; item++)
                    if (combo.ContainerFromIndex(item) is Control container && combo.Items[item] is CopperScreenControllerProfile profile)
                        container.IsEnabled = IsQuickInputAvailable(port, profile);
            };
            _quickPortSelectors[i] = combo;
            group.Children.Add(combo);
            _quickPortBindings[i] = new StackPanel { Spacing = 4 };
            group.Children.Add(_quickPortBindings[i]);
            panel.Children.Add(group);
        }
        _swapInputPortsButton = CreatePanelButton("Swap joystick ports", () => ApplyQuickInputs(_inputOptions with { Port1ProfileId = _inputOptions.Port2ProfileId, Port2ProfileId = _inputOptions.Port1ProfileId }));
        ToolTip.SetTip(_swapInputPortsButton, "Exchange the inputs assigned to Port 1 and Port 2");
        panel.Children.Add(_swapInputPortsButton);
        _quickKeyboardMode = new StackPanel { Spacing = 6 };
        _quickKeyboardMode.Children.Add(_numpadModeButton);
        _quickKeyboardModeNote = new TextBlock { Foreground = MutedText, FontSize = 12, TextWrapping = TextWrapping.Wrap };
        _quickKeyboardMode.Children.Add(_quickKeyboardModeNote);
        panel.Children.Add(_quickKeyboardMode);
        panel.Children.Add(CreatePanelButton("Edit bindings…", () => { _inputHelpButton.Flyout?.Hide(); ShowSettingsWindow(); SelectSettingsPage(6); }));
        RefreshQuickInputControls(force: true);
        return panel;
    }

    private bool IsGamepadProfileConnected(CopperScreenControllerProfile profile) =>
        CopperScreenGamepadInput.TryFindControllerIdForProfile(profile.Id, _gamepadProfileIdsByControllerId, out var id) && _connectedGamepadControllerIds.Contains(id);

    private bool IsQuickInputAvailable(int port, CopperScreenControllerProfile profile) =>
        CopperScreenAvailability.IsControllerAvailable(port, profile.Kind) && (profile.Kind != CopperScreenControllerKind.Gamepad || IsGamepadProfileConnected(profile));

    private static string InputShortName(CopperScreenControllerProfile profile) => profile.Kind switch
    {
        CopperScreenControllerKind.None => "None",
        CopperScreenControllerKind.Mouse => "Mouse",
        _ when profile.Id == "numpad-joystick" && profile.DisplayName == CopperScreenControllerProfile.NumpadJoystick.DisplayName => "Numpad",
        _ when profile.Id == "wasd-joystick" && profile.DisplayName == CopperScreenControllerProfile.WasdJoystick.DisplayName => "WASD",
        _ => profile.DisplayName
    };

    private static string InputDisplayName(CopperScreenControllerProfile profile) => profile.Kind switch
    {
        CopperScreenControllerKind.None => "No input",
        CopperScreenControllerKind.KeyboardJoystick => "Keyboard · " + InputShortName(profile),
        CopperScreenControllerKind.Gamepad => "Gamepad · " + profile.DisplayName,
        _ => InputShortName(profile)
    };

    private string InputChoiceName(int port, CopperScreenControllerProfile profile) => InputDisplayName(profile) +
        (!CopperScreenAvailability.IsControllerAvailable(port, profile.Kind) ? " · Port 1 only" : profile.Kind == CopperScreenControllerKind.Gamepad && !IsGamepadProfileConnected(profile) ? " · Not connected" : "");

    private void RefreshInputHelp()
    {
        ReleaseInteractiveInput();
        RefreshQuickInputControls(force: true);
    }

    private void RefreshQuickInputControls(bool force = false)
    {
        if (_quickPortSelectors[1] == null) return;
        var changed = !ReferenceEquals(_displayedQuickInputs, _inputOptions) || _displayedKeyboardMode != _numpadMode;
        if (!changed && !force) return;
        _updatingQuickInputs = true;
        try
        {
            for (var i = 0; i < 2; i++)
            {
                var combo = _quickPortSelectors[i];
                if (!ReferenceEquals(combo.ItemsSource, _inputOptions.ControllerProfiles)) combo.ItemsSource = _inputOptions.ControllerProfiles;
                var profile = _inputOptions.GetProfileForPort(i + 1);
                combo.SelectedItem = profile;
                combo.IsEnabled = !_settingsVisible && !_applyingSettings;
                RefreshPortBindings(_quickPortBindings[i], profile);
            }
            _swapInputPortsButton.IsVisible = _inputOptions.GetProfileForPort(1).Kind != CopperScreenControllerKind.Mouse;
            _swapInputPortsButton.IsEnabled = !_settingsVisible && !_applyingSettings && _inputOptions.Port1ProfileId != _inputOptions.Port2ProfileId;
            _quickKeyboardMode.IsVisible = _inputOptions.ControllerProfiles.Any(profile => profile.Kind == CopperScreenControllerKind.KeyboardJoystick && (profile.Id == _inputOptions.Port1ProfileId || profile.Id == _inputOptions.Port2ProfileId));
            _quickKeyboardModeNote.Text = _numpadMode == NumpadInputMode.Joystick ? "Mapped keys control the joystick. NumLock switches them back to Amiga keys." : "Mapped keys currently type Amiga keys. Click above or press NumLock to restore joystick controls.";
            _quickKeyboardModeNote.Foreground = _numpadMode == NumpadInputMode.Joystick ? MutedText : Accent;
            _displayedQuickInputs = _inputOptions;
            _displayedKeyboardMode = _numpadMode;
        }
        finally { _updatingQuickInputs = false; }
        UpdateInputSummary();
    }

    private static void RefreshPortBindings(StackPanel panel, CopperScreenControllerProfile profile)
    {
        panel.Children.Clear();
        if (profile.Kind == CopperScreenControllerKind.KeyboardJoystick)
        {
            var keys = profile.JoystickKeys;
            foreach (var binding in new[] { ("Up", keys.Up), ("Down", keys.Down), ("Left", keys.Left), ("Right", keys.Right), ("Fire", keys.Fire), ("Second fire", keys.SecondFire) })
            {
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("82,*"), ColumnSpacing = 10 };
                row.Children.Add(new TextBlock { Text = binding.Item1, Foreground = MutedText, FontSize = 12 });
                var value = new TextBlock { Text = FormatControlKeys(binding.Item2), TextWrapping = TextWrapping.Wrap, FontSize = 12 };
                Grid.SetColumn(value, 1);
                row.Children.Add(value);
                panel.Children.Add(row);
            }
            return;
        }
        panel.Children.Add(new TextBlock
        {
            Text = profile.Kind switch
            {
                CopperScreenControllerKind.Mouse => "Click the Amiga display to capture the mouse. F10 releases it.",
                CopperScreenControllerKind.Gamepad => "D-pad / left stick: move · A / × or right trigger: fire\nB / ○ or left trigger: second fire",
                _ => "No device assigned to this port."
            },
            FontSize = 12, Foreground = MutedText, TextWrapping = TextWrapping.Wrap
        });
    }

    private void ApplyQuickInputs(CopperScreenInputOptions input, bool enableKeyboardJoystick = false)
    {
        if (_settingsVisible || _applyingSettings) return;
        if (!CopperScreenAvailability.IsControllerAvailable(2, input.GetProfileForPort(2).Kind)) { RefreshQuickInputControls(force: true); return; }
        ReleaseInteractiveInput();
        _inputOptions = _settingsDraft.Input = _committedSettings.Input = input;
        if (enableKeyboardJoystick) _numpadMode = NumpadInputMode.Joystick;
        _runtime?.SetInputOptions(input);
        RebuildGamepadPortBindings();
        ApplyCurrentGamepadSnapshots();
        RefreshControllerSelectors();
        if (_runtime == null) _latestState = CreateIdleState(_settingsDraft);
        RememberCurrentSession();
        UpdateSettingsStatus();
        RefreshQuickInputControls(force: true);
        UpdateToolbarStatus();
    }

    private void UpdateInputSummary()
    {
        if (_inputHelpLabel == null) return;
        string Summary(int port)
        {
            var profile = _inputOptions.GetProfileForPort(port);
            return InputShortName(profile) + (profile.Kind == CopperScreenControllerKind.KeyboardJoystick && _numpadMode != NumpadInputMode.Joystick ? " (keys)" : profile.Kind == CopperScreenControllerKind.Gamepad && !IsGamepadProfileConnected(profile) ? " (disconnected)" : "");
        }
        _inputHelpLabel.Text = $"Inputs · 1: {Summary(1)} · 2: {Summary(2)}";
        var description = $"Port 1: {InputChoiceName(1, _inputOptions.GetProfileForPort(1))}\nPort 2: {InputChoiceName(2, _inputOptions.GetProfileForPort(2))}\nClick to switch inputs or view controls.";
        ToolTip.SetTip(_inputHelpButton, description);
        AutomationProperties.SetName(_inputHelpButton, "Input ports · " + description);
        _inputHelpButton.IsEnabled = !_settingsVisible && !_applyingSettings;
        _numpadModeButton.Content = _numpadMode == NumpadInputMode.Joystick ? "Keyboard: joystick controls" : "Keyboard: Amiga keys";
        AutomationProperties.SetName(_numpadModeButton, _numpadModeButton.Content.ToString());
        ToolTip.SetTip(_numpadModeButton, "Switch mapped keyboard keys between joystick controls and Amiga keys (NumLock)");
        RefreshQuickInputControls();
    }
}
