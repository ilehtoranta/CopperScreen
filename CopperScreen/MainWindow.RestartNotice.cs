using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace CopperScreen;

internal sealed partial class MainWindow
{
    private Border _restartNotice = null!;
    private TextBlock _restartNoticeText = null!;
    private Button _restartNoticeButton = null!;
    private Button _revertPendingButton = null!;

    private Control CreateRestartNotice()
    {
        _restartNoticeText = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.Parse("#FFE0A6")),
            FontSize = 13, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center
        };
        AutomationProperties.SetName(_restartNoticeText, "Pending restart changes");
        _restartNoticeButton = CreatePanelButton("Restart Amiga", () => _ = RestartAmigaAsync());
        AutomationProperties.SetName(_restartNoticeButton, "Restart Amiga to apply changes");
        ToolTip.SetTip(_restartNoticeButton, "Restart the Amiga using the pending configuration. Running Amiga programs restart.");
        _revertPendingButton = CreatePanelButton("Revert pending changes", RevertPendingChanges);
        AutomationProperties.SetName(_revertPendingButton, "Revert pending changes");
        ToolTip.SetTip(_revertPendingButton, "Return to the running Amiga's configuration without restarting. Changes already applied live are kept.");
        var content = new StackPanel { Spacing = 10 };
        content.Children.Add(_restartNoticeText);
        content.Children.Add(CommandGroup(_restartNoticeButton, _revertPendingButton));
        _restartNotice = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#513D1D")),
            BorderBrush = new SolidColorBrush(Color.Parse("#C68F40")), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8), Padding = new Thickness(12), Margin = new Thickness(12),
            VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = content, IsVisible = false
        };
        AutomationProperties.SetName(_restartNotice, "Pending restart notice");
        Grid.SetRow(_restartNotice, 1);
        return _restartNotice;
    }

    private void UpdateRestartNotice(bool valid)
    {
        if (_restartNotice == null) return;
        var pending = _runtime != null && _settingsDraft.NeedsRestartComparedWith(_committedSettings);
        _restartNotice.IsVisible = pending && !_settingsVisible;
        _restartNoticeText.Text = pending
            ? "Restart required: " + _settingsDraft.RestartChangeSummary(_committedSettings) + ". Changes are saved and awaiting restart."
            : string.Empty;
        if (pending && !valid) _restartNoticeText.Text += " Review Settings before restarting.";
        _restartNoticeButton.IsEnabled = pending && valid && !_applyingSettings && !_checkingDiskChanges;
        _revertPendingButton.IsEnabled = pending && !_applyingSettings && !_checkingDiskChanges && !_applyingLiveMedia;
    }

    private void RevertPendingChanges()
    {
        if (_runtime == null || _applyingSettings || _checkingDiskChanges || _applyingLiveMedia) return;
        var restored = _committedSettings.Clone();
        restored.ClearRestartRequired();
        _recentSession.Save(restored, _outputVolume, _outputMuted, AppContext.BaseDirectory);
        if (_recentSession.LastError is { } error) { SetSettingsError(error); return; }
        _settingsDraft = restored;
        _lastAutoSavedDraft = restored.Clone();
        _lastAutoSavedSettings = CopperScreenProfileStore.Serialize(restored, AppContext.BaseDirectory);
        ClearSettingsStartupError();
        RefreshSettingsUi();
        UpdateToolbarStatus();
        if (_settingsVisible) _settingsCloseButton.Focus();
        else _presenter.Focus();
    }

    private Task RestartAmigaAsync()
    {
        if (_runtime == null || _applyingSettings || _checkingDiskChanges) return Task.CompletedTask;
        return _settingsDraft.NeedsRestartComparedWith(_committedSettings)
            ? StartRuntimeFromSettingsAsync()
            : ResetRuntimeAsync();
    }
}
