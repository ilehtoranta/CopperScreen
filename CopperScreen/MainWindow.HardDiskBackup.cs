using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace CopperScreen;

internal sealed partial class MainWindow
{
    internal CopperScreenHardDiskBackupCleanup.Result StartupHardDiskBackupCleanup { get; }
    private async Task ExportHardDiskBackupAsync()
    {
        try
        {
            RequireStoppedForHardDiskBackup();
            var draft = ReadSettingsDraft();
            var owner = _settingsVisible ? (TopLevel)_settingsWindow : this;
            var file = await owner.StorageProvider.SaveFilePickerAsync(new()
            {
                Title = "Export hard-disk backup", SuggestedFileName = "Amiga-disks-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".copperhdf",
                DefaultExtension = "copperhdf", ShowOverwritePrompt = false,
                FileTypeChoices = [new("CopperScreen hard-disk backup") { Patterns = ["*.copperhdf"] }]
            });
            if (file?.TryGetLocalPath() is not { } path) return;
            await ExportHardDiskBackupToPathAsync(draft, path);
        }
        catch (Exception ex) when (IsWorkspaceFileError(ex) || ex is FormatException or OverflowException) { ObserveInstalledBackupError(ex); SetSettingsError(ex.Message); ShowDiskFeedback(ex.Message, false); }
    }

    private async Task ExportHardDiskBackupToPathAsync(CopperScreenSettingsDraft draft, string path)
    {
        RequireStoppedForHardDiskBackup();
        _applyingSettings = true; UpdateSettingsStatus(); UpdateToolbarStatus();
        try
        {
            await Task.Run(() => CopperScreenHardDiskBackup.Export(draft, path, AppContext.BaseDirectory, _recentSession.DataDirectory));
            ClearSettingsStartupError();
            _hardDiskBackupStatus.Text = "Backup exported to " + path;
            ShowDiskFeedback("Hard disks and machine settings backed up. Keep this file outside app data before uninstalling.", true);
        }
        finally { _applyingSettings = false; UpdateSettingsStatus(); UpdateToolbarStatus(); }
    }

    private async Task RestoreHardDiskBackupAsync()
    {
        try
        {
            RequireStoppedForHardDiskBackup();
            var owner = _settingsVisible ? (TopLevel)_settingsWindow : this;
            var files = await owner.StorageProvider.OpenFilePickerAsync(new()
            {
                Title = "Restore hard-disk backup", AllowMultiple = false,
                FileTypeFilter = [new("CopperScreen hard-disk backup") { Patterns = ["*.copperhdf"] }]
            });
            if (files.FirstOrDefault()?.TryGetLocalPath() is not { } path) return;
            await RestoreHardDiskBackupFromPathAsync(path);
        }
        catch (Exception ex) when (IsWorkspaceFileError(ex) || ex is FormatException or OverflowException) { ObserveInstalledBackupError(ex); SetSettingsError(ex.Message); ShowDiskFeedback(ex.Message, false); }
    }

    private async Task RestoreHardDiskBackupFromPathAsync(string path)
    {
        RequireStoppedForHardDiskBackup();
        _applyingSettings = true; UpdateSettingsStatus(); UpdateToolbarStatus();
        try
        {
            var draft = await Task.Run(() => CopperScreenHardDiskBackup.Restore(path, _recentSession.DataDirectory, AppContext.BaseDirectory,
                restored => _recentSession.RememberRestoredMachine(restored, AppContext.BaseDirectory)));
            SetWelcomeDraft(draft);
            _hardDiskBackupStatus.Text = "Restored into new disk files. The backup is unchanged. Check the firmware path, then start the Amiga.";
            ShowDiskFeedback("Backup restored into new disk files. Machine settings and disk write protection are preserved. Check the firmware path, then start the Amiga.", true);
        }
        finally { _applyingSettings = false; UpdateSettingsStatus(); UpdateToolbarStatus(); }
    }

    private void RequireStoppedForHardDiskBackup()
    {
        if (_runtime != null) throw new InvalidOperationException("Stop the Amiga before backing up or restoring hard disks. Stop saves storage and returns to the launcher.");
        if (_applyingSettings) throw new InvalidOperationException("Wait for the current operation to finish.");
    }

    [System.Diagnostics.Conditional("COPPERSCREEN_INSTALLED_TESTS")]
    private void ObserveInstalledBackupError(Exception error)
    {
#if COPPERSCREEN_INSTALLED_TESTS
        _installedBackupError = error;
#endif
    }
}
