using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;

namespace CopperScreen;

internal sealed partial class MainWindow
{
    private void CreateWelcomeModelSelector()
    {
        _welcomeModel = new ComboBox { ItemsSource = new[] { "Amiga 500", "Amiga 500+", "Amiga 1200" }, HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetName(_welcomeModel, "Amiga model");
        _welcomeModel.SelectionChanged += (_, _) =>
        {
            if (_updatingWelcomeModel || _welcomeModel.SelectedIndex < 0 || _settingsVisible) return;
            var model = (CopperScreenMachineModel)_welcomeModel.SelectedIndex;
            ApplyRomSelection(draft => SelectModelSetup(draft, model));
        };
    }

    private CopperScreenSettingsDraft SelectModelSetup(CopperScreenSettingsDraft current, CopperScreenMachineModel model)
    {
        var method = CopperScreenBootMethods.Method(current);
        if (method != CopperScreenBootMethod.CopperStart)
        {
            var usesMini = _recentSession.ModelUsesMini(model) ??
                (method == CopperScreenBootMethod.MinimalDiskBoot || string.IsNullOrWhiteSpace(current.KickstartRomPath));
            method = usesMini && CopperScreenBundledMiniRom.Included ? CopperScreenBootMethod.MinimalDiskBoot : CopperScreenBootMethod.KickstartRom;
        }
        return SelectRememberedSetup(current, CopperScreenBootMethods.Choice(model, method));
    }

    private CopperScreenSettingsDraft SelectRememberedSetup(CopperScreenSettingsDraft current, int choice)
    {
        _recentSession.RememberKickstart(current, AppContext.BaseDirectory);
        _recentSession.RememberDisks(current, AppContext.BaseDirectory);
        _recentSession.RememberConfiguration(current, AppContext.BaseDirectory);
        var fallback = CopperScreenLaunchWorkflow.SelectStartChoice(current, choice);
        var draft = _recentSession.RestoreConfiguration(fallback, AppContext.BaseDirectory);
        draft = _recentSession.RestoreKickstart(draft);
        draft = _recentSession.RestoreDisks(draft);
        if (current.CpuBackend == M68kBackendKind.AccurateM68060 &&
            (!CopperScreenMachinePresets.CanUse68060(current.Chipset, current.KickstartSource, current.RomVersion) ||
             CopperScreenBootMethods.Method(current) == CopperScreenBootMethod.KickstartRom && string.IsNullOrWhiteSpace(current.KickstartRomPath)))
        {
            // Keep the pending CPU choice while the user fixes the model or boot ROM.
            draft.CpuBackend = current.CpuBackend;
        }
        if (ReferenceEquals(draft, fallback) && draft.DriveDiskPaths.All(string.IsNullOrWhiteSpace) &&
            CopperScreenMachinePresets.Identify(current) == CopperScreenMachinePresets.Identify(draft) &&
            CopperScreenBootMethods.Method(current) != CopperScreenBootMethods.Method(draft) &&
            current.KickstartSource != CopperScreenKickstartSource.CopperStart &&
            draft.KickstartSource != CopperScreenKickstartSource.CopperStart)
        {
            // The first switch between the bundled and original ROM starts
            // with the floppy already selected for this machine.
            Array.Copy(current.DriveDiskPaths, draft.DriveDiskPaths, draft.FloppyDriveCount);
            Array.Copy(current.DriveWriteProtected, draft.DriveWriteProtected, draft.FloppyDriveCount);
            Array.Copy(current.FloppyDriveTypes, draft.FloppyDriveTypes, draft.FloppyDriveCount);
        }
        return CopperScreenBundledCopperOs.PrepareForLaunch(draft, _recentSession.DataDirectory);
    }

    private void SelectRomSetup(int choice) => ApplyRomSelection(draft => SelectRememberedSetup(draft, choice));

    private void UseBundledRom()
    {
        if (!CopperScreenBundledMiniRom.Included) return;
        SelectRomSetup(CopperScreenBootMethods.Choice(CopperScreenMachinePresets.Identify(_settingsDraft), CopperScreenBootMethod.MinimalDiskBoot));
    }

    private void UseCopperOs()
    {
        if (!CopperScreenBootMethods.CopperStartEnabled) return;
        SelectRomSetup(CopperScreenBootMethods.Choice(CopperScreenMachinePresets.Identify(_settingsDraft), CopperScreenBootMethod.CopperStart));
    }

    private void ApplyRomSelection(Func<CopperScreenSettingsDraft, CopperScreenSettingsDraft> select)
    {
        if (_applyingSettings) return;
        if (_settingsVisible)
        {
            EditMachine(select);
            return;
        }

        try
        {
            SetWelcomeDraft(select(ReadSettingsDraft()));
            RememberCurrentSession();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException or FormatException or OverflowException)
        {
            SetSettingsError(ex.Message);
            RefreshWelcomeModel();
        }
    }

    private void RefreshWelcomeModel()
    {
        _updatingWelcomeModel = true;
        try { _welcomeModel.SelectedIndex = (int)CopperScreenMachinePresets.Identify(_settingsDraft); }
        finally { _updatingWelcomeModel = false; }
    }
}
