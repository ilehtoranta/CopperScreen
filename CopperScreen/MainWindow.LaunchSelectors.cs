using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;

namespace CopperScreen;

internal sealed partial class MainWindow
{
    private void ConfigureBootMethodSelector(ComboBox selector)
    {
        AutomationProperties.SetName(selector, "Boot method");
        selector.ItemsSource = CopperScreenBootMethods.Choices;
        selector.HorizontalAlignment = HorizontalAlignment.Stretch;
        selector.PlaceholderText = "Choose an available boot method";
        selector.ContainerPrepared += (_, args) =>
        {
            var method = selector.Items[args.Index]?.ToString() ?? "";
            args.Container.IsEnabled = CopperScreenBootMethods.Unavailable(method, CopperScreenMachinePresets.Identify(_settingsDraft)) == null;
        };
        selector.DropDownOpened += (_, _) =>
        {
            for (var i = 0; i < selector.ItemCount; i++)
                if (selector.ContainerFromIndex(i) is Control container)
                    container.IsEnabled = CopperScreenBootMethods.Unavailable(selector.Items[i]?.ToString() ?? "", CopperScreenMachinePresets.Identify(_settingsDraft)) == null;
        };
    }

    private void RefreshBootMethodSelector(ComboBox selector)
    {
        // Recreate the labels when the model changes, including the closed selection.
        var model = CopperScreenMachinePresets.Identify(_settingsDraft);
        selector.ItemTemplate = new FuncDataTemplate<string>((value, _) => new TextBlock
        {
            Text = CopperScreenBootMethods.Label(value ?? "") +
                (CopperScreenBootMethods.Unavailable(value ?? "", model) is { } reason ? " — " + reason : ""),
            TextWrapping = TextWrapping.Wrap
        });
        selector.SelectedItem = _settingsDraft.KickstartSource == CopperScreenKickstartSource.DiagRom
            ? null : CopperScreenBootMethods.Method(_settingsDraft).ToString();
    }

    private void CreateWelcomeLaunchSelectors()
    {
        _welcomeModel = new ComboBox { ItemsSource = new[] { "Amiga 500", "Amiga 500+", "Amiga 1200" }, HorizontalAlignment = HorizontalAlignment.Stretch };
        _welcomeBootMethod = new ComboBox();
        AutomationProperties.SetName(_welcomeModel, "Amiga model");
        ConfigureBootMethodSelector(_welcomeBootMethod);
        _welcomeModel.SelectionChanged += (_, _) =>
        {
            if (_updatingBootMethod || _welcomeModel.SelectedIndex < 0) return;
            SelectWelcomeLaunch(CopperScreenBootMethods.Choice((CopperScreenMachineModel)_welcomeModel.SelectedIndex, CopperScreenBootMethods.Method(_settingsDraft)));
        };
        _welcomeBootMethod.SelectionChanged += (_, _) =>
        {
            if (_updatingBootMethod || _welcomeModel.SelectedIndex < 0 || _welcomeBootMethod.SelectedItem is not string selected) return;
            var model = (CopperScreenMachineModel)_welcomeModel.SelectedIndex;
            if (CopperScreenBootMethods.Unavailable(selected, model) != null)
            {
                _updatingBootMethod = true;
                try { RefreshWelcomeLaunchSelectors(); }
                finally { _updatingBootMethod = false; }
                return;
            }
            SelectWelcomeLaunch(CopperScreenBootMethods.Choice(model, Enum.Parse<CopperScreenBootMethod>(selected)));
        };
    }

    private void RefreshWelcomeLaunchSelectors()
    {
        _welcomeModel.SelectedIndex = (int)CopperScreenMachinePresets.Identify(_settingsDraft);
        RefreshBootMethodSelector(_welcomeBootMethod);
    }

    private CopperScreenSettingsDraft SelectRememberedSetup(CopperScreenSettingsDraft current, int choice)
    {
        _recentSession.RememberKickstart(current, AppContext.BaseDirectory);
        _recentSession.RememberDisks(current, AppContext.BaseDirectory);
        _recentSession.RememberConfiguration(current, AppContext.BaseDirectory);
        var draft = CopperScreenLaunchWorkflow.SelectStartChoice(current, choice);
        draft = _recentSession.RestoreConfiguration(draft, AppContext.BaseDirectory);
        draft = _recentSession.RestoreKickstart(draft);
        draft = _recentSession.RestoreDisks(draft);
        return CopperScreenBundledCopperOs.PrepareForLaunch(draft, _recentSession.DataDirectory);
    }

    private void SelectSettingsBootMethod()
    {
        if (_updatingSettingsUi || _applyingSettings || _kickstartSourceBox.SelectedItem is not string selected) return;
        var model = CopperScreenMachinePresets.Identify(_settingsDraft);
        if (CopperScreenBootMethods.Unavailable(selected, model) != null)
        {
            RefreshSettingsUi();
            return;
        }
        EditMachine(draft => SelectRememberedSetup(draft,
            CopperScreenBootMethods.Choice(model, Enum.Parse<CopperScreenBootMethod>(selected))));
        // A partial settings edit or an IO error may have prevented the switch.
        // Keep the selector consistent with the last complete draft.
        if (CopperScreenBootMethods.Method(_settingsDraft).ToString() != selected)
        {
            _updatingSettingsUi = true;
            try { RefreshBootMethodSelector(_kickstartSourceBox); }
            finally { _updatingSettingsUi = false; }
        }
    }

    private void SelectWelcomeLaunch(int choice)
    {
        if (_kickstartSourceBox == null || _settingsVisible || _applyingSettings) return;
        try
        {
            SetWelcomeDraft(SelectRememberedSetup(_settingsDraft, choice));
            RememberCurrentSession();
            if (_recentSession.LastError is { } error) SetSettingsError(error);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException or FormatException or OverflowException)
        {
            SetSettingsError(ex.Message);
            _updatingBootMethod = true;
            try { RefreshWelcomeLaunchSelectors(); }
            finally { _updatingBootMethod = false; }
        }
    }
}
