using Avalonia.Controls;

namespace CopperScreen;

internal sealed partial class MainWindow
{
    // Pause and flush through the existing runtime owner before any host transition.
    // A failed save leaves the session and its storage lease available for export/retry.
    private async Task<bool> PrepareLaunchTransitionAsync(CopperScreenRuntime runtime)
    {
        ReleaseInteractiveInput();
        var wasPaused = runtime.CurrentState.IsPaused;
        if (!wasPaused) await runtime.TogglePausedAsync();
        var saved = await runtime.SavePersistentStorageAsync();
        _latestState = saved.State;
        if (!saved.Success || saved.State.Drives.Any(d => d.HasUnsavedChanges))
        {
            var message = saved.Success ? "Save ADF in Floppy settings, or discard and eject changed disks before stopping or restarting."
                : saved.Message + (CopperOsWorkspace.IsWorkspace(_committedSettings.DriveDiskPaths[0])
                    ? " Use Export workspace to save elsewhere, then retry."
                    : " Keep the session open, restore storage access and retry. Direct hard-disk and shared-folder writes already made cannot be undone.");
            if (!wasPaused) await runtime.TogglePausedAsync();
            SetSettingsError(message); ShowDiskFeedback(message, false);
            UpdateToolbarStatus();
            return false;
        }
        return true;
    }

    private async Task StopToLauncherAsync()
    {
        if (_runtime == null || _applyingSettings || _settingsVisible || _checkingDiskChanges) return;
        _applyingSettings = true;
        UpdateToolbarStatus();
        try
        {
            var runtime = _runtime;
            if (!await PrepareLaunchTransitionAsync(runtime)) return;
            RememberCurrentSession();
            runtime.FramePublished -= QueueFramePresentation;
            runtime.HostClipboardTextChanged -= PublishGuestClipboardText;
            runtime.HostClipboardImageChanged -= PublishGuestClipboardImage;
            runtime.Dispose();
            _runtime = null;
            DisableCrtPhosphorPresentation();
            if (WindowState == WindowState.FullScreen) WindowState = WindowState.Normal;
            _settingsDraft = _committedSettings.Clone();
            _latestState = CreateIdleState(_settingsDraft);
            _welcomeCopperOs = CopperOsWorkspace.IsWorkspace(_settingsDraft.DriveDiskPaths[0]);
            ClearSettingsStartupError();
            RefreshSettingsUi(); RefreshWelcomeWorkspaces(); RefreshWelcomeProfiles();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        { SetSettingsError(ex.Message + " Keep this session open and export your disk before retrying."); }
        finally { _applyingSettings = false; UpdateSettingsStatus(); UpdateToolbarStatus(); }
    }
}
