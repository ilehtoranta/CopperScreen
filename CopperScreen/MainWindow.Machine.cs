using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;

namespace CopperScreen;

internal sealed partial class MainWindow
{
    private sealed record MachineControls(ComboBox Model, ComboBox FastSize, CheckBox SlowRam, CheckBox FastRam, CheckBox Rtc, CheckBox DriveSounds, TextBlock Note, TextBlock RamNote);
    private readonly List<MachineControls> _machineControls = [];
    private TextBlock _cpuRestrictionNote = null!;
    private bool _updatingMachineControls;

    private Control CreateMachineControls()
    {
        var model = new ComboBox
        {
            ItemsSource = Enum.GetValues<CopperScreenMachineModel>(), HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemTemplate = new FuncDataTemplate<CopperScreenMachineModel>((value, _) => new TextBlock { Text = CopperScreenMachinePresets.Label(value) })
        };
        AutomationProperties.SetName(model, "Amiga model");
        var fastSize = new ComboBox { MinWidth = 105,
            ItemTemplate = new FuncDataTemplate<int>((value, _) => new TextBlock { Text = value == 0 ? "None" : value < 1024 ? $"{value} KiB" : $"{value / 1024} MiB" }) };
        AutomationProperties.SetName(fastSize, "Fast RAM size");
        var slow = new CheckBox { Content = "512 KiB slow RAM expansion" };
        var fast = new CheckBox { Content = "Fast RAM" };
        var rtc = new CheckBox { Content = "RTC clock" };
        var sounds = new CheckBox { Content = "Floppy drive sounds" };
        var note = SettingsNote("");
        var ramNote = SettingsNote("");
        AutomationProperties.SetName(ramNote, "RAM requirements");
        var controls = new MachineControls(model, fastSize, slow, fast, rtc, sounds, note, ramNote);
        _machineControls.Add(controls);
        model.SelectionChanged += (_, _) =>
        {
            if (_updatingMachineControls || _updatingSettingsUi || _applyingSettings || model.SelectedItem is not CopperScreenMachineModel selected) return;
            EditMachine(draft => SelectModelSetup(draft, selected));
        };
        fastSize.SelectionChanged += (_, _) =>
        {
            if (_updatingMachineControls || _updatingSettingsUi || _applyingSettings || fastSize.SelectedItem is not int kib) return;
            EditMachine(draft => { CopperScreenMachinePresets.SetFastRam(draft, kib); draft.MarkRequiresRestart(); return draft; });
        };
        void Toggle(CheckBox box, Action<CopperScreenSettingsDraft, bool> apply)
        {
            AutomationProperties.SetName(box, box.Content!.ToString());
            box.IsCheckedChanged += (_, _) =>
            {
                if (_updatingMachineControls || _updatingSettingsUi || _applyingSettings) return;
                EditMachine(draft => { apply(draft, box.IsChecked == true); draft.MarkRequiresRestart(); return draft; });
            };
        }
        Toggle(slow, (draft, enabled) => draft.PseudoFastRamKb = enabled ? 512 : 0);
        Toggle(fast, (draft, enabled) => CopperScreenMachinePresets.SetFastRam(draft, enabled ? draft.RealFastRamKb > 0 ? draft.RealFastRamKb : 2048 : 0));
        Toggle(rtc, (draft, enabled) => draft.RtcEnabled = enabled);
        Toggle(sounds, (draft, enabled) => draft.FloppyDriveAudio = draft.FloppyDriveAudio with { Enabled = enabled });
        var options = new StackPanel { Spacing = 4 };
        options.Children.Add(slow);
        options.Children.Add(WithAction(fast, fastSize));
        options.Children.Add(ramNote);
        options.Children.Add(rtc);
        options.Children.Add(sounds);
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(SectionLabel("Amiga model"));
        panel.Children.Add(model);
        panel.Children.Add(note);
        panel.Children.Add(SettingsNote("Switching models restores the settings last saved for that model."));
        var cpuForm = CreateSettingsGroupForm();
        _cpuBackendBox = AddComboSetting(cpuForm, "CPU backend",
            ["AccurateM68000", "AccurateM68010", "AccurateM68EC020", "AccurateM68020", "AccurateM68030", "AccurateM68040", "AccurateM68060", "JitM68040"],
            markRestart: false);
        AutomationProperties.SetName(_cpuBackendBox, "Amiga CPU");
        _cpuBackendBox.SelectionChanged += (_, _) =>
        {
            if (_updatingSettingsUi || _updatingMachineControls || _applyingSettings || _cpuBackendBox.SelectedItem is not string selected) return;
            EditMachine(draft => { CopperScreenMachinePresets.SetCpu(draft, Enum.Parse<M68kBackendKind>(selected)); draft.MarkRequiresRestart(); return draft; });
        };
        _cpuRestrictionNote = SettingsNote("");
        AutomationProperties.SetName(_cpuRestrictionNote, "CPU requirements");
        cpuForm.Children.Add(_cpuRestrictionNote);
        cpuForm.Children.Add(SettingsNote("Changing CPU can reduce available Fast RAM. CPU upgrades use approximate accelerator timing."));
        panel.Children.Add(cpuForm);
        panel.Children.Add(new Expander { Header = "Optional features", Content = options, HorizontalAlignment = HorizontalAlignment.Stretch });
        return panel;
    }

    private void EditMachine(Func<CopperScreenSettingsDraft, CopperScreenSettingsDraft> edit)
    {
        try
        {
            _settingsDraft = edit(ReadSettingsDraft());
            _settingsStartupError = null;
            if (_runtime == null && !_settingsVisible)
            {
                _committedSettings = _settingsDraft.Clone();
                _latestState = CreateIdleState(_settingsDraft);
            }
            RefreshSettingsUi();
            UpdateToolbarStatus();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or FormatException or OverflowException)
        { SetSettingsError(ex.Message); }
    }

    private void UpdateMachineControls()
    {
        var draft = _settingsDraft;
        try { draft = ReadSettingsDraft(); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException or OverflowException) { }
        _updatingMachineControls = true;
        try
        {
            var cpuChoices = CopperScreenMachinePresets.CpuChoices(draft);
            var fixedCpu = cpuChoices.Length == 1;
            _cpuBackendBox.IsEnabled = !_applyingSettings && (!fixedCpu || !cpuChoices.Contains(draft.CpuBackend.ToString()));
            _cpuRestrictionNote.IsVisible = fixedCpu || draft.CpuBackend == M68kBackendKind.AccurateM68060;
            if (fixedCpu)
            {
                var rom = draft.KickstartSource == CopperScreenKickstartSource.CopperStart ? "CopperStart" : "CopperStart Mini";
                _cpuRestrictionNote.Text = $"{rom} requires this model’s standard CPU. Choose an original Kickstart ROM for CPU upgrades.";
            }
            else if (draft.CpuBackend == M68kBackendKind.AccurateM68060)
                _cpuRestrictionNote.Text = "The 68060 starts with its FPU disabled. Guest 68060 software can enable it.";
            foreach (var controls in _machineControls)
            {
                var enabled = !_applyingSettings;
                controls.Model.SelectedItem = CopperScreenMachinePresets.Identify(draft);
                controls.Model.IsEnabled = enabled;
                var sizes = CopperScreenMachinePresets.FastRamChoices(draft);
                if (controls.FastSize.ItemsSource is not int[] currentSizes || !currentSizes.SequenceEqual(sizes)) controls.FastSize.ItemsSource = sizes;
                controls.FastSize.SelectedItem = draft.RealFastRamKb;
                var canUseFast = CopperScreenMachinePresets.CanUseFastRam(draft);
                var canUseSlow = CopperScreenMachinePresets.CanUseSlowRam(draft);
                controls.FastSize.IsEnabled = enabled && canUseFast;
                controls.SlowRam.IsChecked = draft.PseudoFastRamKb > 0;
                controls.SlowRam.IsEnabled = enabled && (canUseSlow || draft.PseudoFastRamKb > 0);
                controls.FastRam.IsChecked = draft.RealFastRamKb > 0;
                controls.FastRam.IsEnabled = enabled && (canUseFast || draft.RealFastRamKb > 0);
                var fastLimit = !canUseFast ? "Fast RAM requires an original Kickstart ROM."
                    : sizes[^1] <= 8192 ? "This CPU supports up to 8 MiB of Fast RAM."
                    : draft.RomVersion is KickstartVersion.Kickstart30 or KickstartVersion.Kickstart31
                        ? $"This configuration supports up to {sizes[^1] / 1024} MiB of Fast RAM."
                        : "Fast RAM above 8 MiB requires Kickstart 3.x.";
                controls.RamNote.Text = canUseSlow ? fastLimit
                    : "Slow RAM requires 512 KiB of Chip RAM with an 8371 or 8372A Agnus. " + fastLimit;
                controls.Rtc.IsChecked = draft.RtcEnabled;
                controls.Rtc.IsEnabled = controls.DriveSounds.IsEnabled = enabled;
                controls.DriveSounds.IsChecked = draft.FloppyDriveAudio.Enabled;
                controls.Note.Text = CopperScreenMachinePresets.Identify(draft) == CopperScreenMachineModel.Amiga1200
                    ? $"PAL · AGA · {CopperScreenAvailability.ChoiceLabel(draft.CpuBackend.ToString())} · 2 MiB Chip RAM · any supported native Kickstart version. Compatibility is experimental."
                    : $"PAL · {(draft.Chipset.DisplayChip == DisplayChipModel.EcsDenise ? "ECS" : "OCS")} · {CopperScreenAvailability.ChoiceLabel(draft.CpuBackend.ToString())} · {draft.ChipRamKb} KiB Chip RAM";
            }
        }
        finally { _updatingMachineControls = false; }
    }
}
