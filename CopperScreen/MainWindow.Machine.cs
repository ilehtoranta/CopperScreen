using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;

namespace CopperScreen;

internal sealed partial class MainWindow
{
    private sealed record MachineControls(ComboBox Model, ComboBox Cpu, ComboBox FastSize, CheckBox SlowRam, CheckBox FastRam, CheckBox Rtc, CheckBox DriveSounds, TextBlock Note, bool Welcome);
    private readonly List<MachineControls> _machineControls = [];
    private bool _updatingMachineControls;

    private Control CreateMachineControls(bool welcome)
    {
        var model = new ComboBox
        {
            ItemsSource = Enum.GetValues<CopperScreenMachineModel>(), HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemTemplate = new FuncDataTemplate<CopperScreenMachineModel>((value, _) => new TextBlock { Text = CopperScreenMachinePresets.Label(value) })
        };
        AutomationProperties.SetName(model, "Amiga model");
        var cpu = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemTemplate = new FuncDataTemplate<string>((value, _) => new TextBlock { Text = CopperScreenAvailability.ChoiceLabel(value ?? "") }) };
        AutomationProperties.SetName(cpu, "Amiga CPU");
        var fastSize = new ComboBox { MinWidth = 105,
            ItemTemplate = new FuncDataTemplate<int>((value, _) => new TextBlock { Text = value == 0 ? "None" : value < 1024 ? $"{value} KiB" : $"{value / 1024} MiB" }) };
        AutomationProperties.SetName(fastSize, "Fast RAM size");
        var slow = new CheckBox { Content = "512 KiB slow RAM expansion" };
        var fast = new CheckBox { Content = "Fast RAM" };
        var rtc = new CheckBox { Content = "RTC clock" };
        var sounds = new CheckBox { Content = "Floppy drive sounds" };
        var note = SettingsNote("");
        var controls = new MachineControls(model, cpu, fastSize, slow, fast, rtc, sounds, note, welcome);
        _machineControls.Add(controls);
        model.SelectionChanged += (_, _) =>
        {
            if (_updatingMachineControls || _updatingSettingsUi || _applyingSettings || model.SelectedItem is not CopperScreenMachineModel selected) return;
            EditMachine(draft =>
            {
                _recentSession.RememberKickstart(draft, AppContext.BaseDirectory);
                _recentSession.RememberDisks(draft, AppContext.BaseDirectory);
                _recentSession.RememberConfiguration(draft, AppContext.BaseDirectory);
                return _recentSession.RestoreDisks(_recentSession.RestoreKickstart(
                    _recentSession.RestoreConfiguration(CopperScreenMachinePresets.Apply(draft, selected), AppContext.BaseDirectory)));
            });
        };
        cpu.SelectionChanged += (_, _) =>
        {
            if (_updatingMachineControls || _updatingSettingsUi || _applyingSettings || cpu.SelectedItem is not string value) return;
            EditMachine(draft => { CopperScreenMachinePresets.SetCpu(draft, Enum.Parse<M68kBackendKind>(value)); draft.MarkRequiresRestart(); return draft; });
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
        options.Children.Add(SectionLabel("CPU"));
        options.Children.Add(cpu);
        options.Children.Add(SettingsNote("CPU changes may reduce Fast RAM to its address limit. Upgrades use experimental timing."));
        options.Children.Add(slow);
        options.Children.Add(WithAction(fast, fastSize));
        options.Children.Add(rtc);
        options.Children.Add(sounds);
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(SectionLabel("Amiga model"));
        panel.Children.Add(model);
        panel.Children.Add(note);
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
            foreach (var controls in _machineControls)
            {
                var enabled = !_applyingSettings && (!controls.Welcome || !_settingsVisible);
                controls.Model.SelectedItem = CopperScreenMachinePresets.Identify(draft);
                controls.Model.IsEnabled = enabled;
                var cpuChoices = CopperScreenMachinePresets.CpuChoices(draft);
                if (controls.Cpu.ItemsSource is not string[] currentCpus || !currentCpus.SequenceEqual(cpuChoices)) controls.Cpu.ItemsSource = cpuChoices;
                controls.Cpu.SelectedItem = draft.CpuBackend.ToString();
                controls.Cpu.IsEnabled = enabled && draft.KickstartSource != CopperScreenKickstartSource.MinimalDiskBoot;
                var sizes = CopperScreenMachinePresets.FastRamChoices(draft);
                if (controls.FastSize.ItemsSource is not int[] currentSizes || !currentSizes.SequenceEqual(sizes)) controls.FastSize.ItemsSource = sizes;
                controls.FastSize.SelectedItem = draft.RealFastRamKb;
                controls.FastSize.IsEnabled = enabled && CopperScreenMachinePresets.CanUseFastRam(draft);
                controls.SlowRam.IsChecked = draft.PseudoFastRamKb > 0;
                controls.SlowRam.IsEnabled = enabled && (CopperScreenMachinePresets.CanUseSlowRam(draft) || draft.PseudoFastRamKb > 0);
                ToolTip.SetTip(controls.SlowRam, "Available with a 512 KiB Chip RAM layout.");
                controls.FastRam.IsChecked = draft.RealFastRamKb > 0;
                controls.FastRam.IsEnabled = enabled && (CopperScreenMachinePresets.CanUseFastRam(draft) || draft.RealFastRamKb > 0);
                ToolTip.SetTip(controls.FastRam, "24-bit CPUs: up to 8 MiB. Full 32-bit CPUs: up to 1 GiB; more than 8 MiB requires Kickstart 3.x. Minimal disk boot has no Fast RAM.");
                controls.Rtc.IsChecked = draft.RtcEnabled;
                controls.Rtc.IsEnabled = controls.DriveSounds.IsEnabled = enabled;
                controls.DriveSounds.IsChecked = draft.FloppyDriveAudio.Enabled;
                controls.Note.Text = CopperScreenMachinePresets.Identify(draft) == CopperScreenMachineModel.Amiga1200
                    ? $"PAL · AGA · {CopperScreenAvailability.ChoiceLabel(draft.CpuBackend.ToString())} · 2 MiB Chip RAM · requires A1200 Kickstart 3.0. Compatibility is experimental."
                    : $"PAL · {(draft.Chipset.DisplayChip == DisplayChipModel.EcsDenise ? "ECS" : "OCS")} · {CopperScreenAvailability.ChoiceLabel(draft.CpuBackend.ToString())} · {draft.ChipRamKb} KiB Chip RAM";
            }
        }
        finally { _updatingMachineControls = false; }
    }
}
