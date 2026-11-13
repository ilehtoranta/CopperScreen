using Avalonia;
using Avalonia.Controls;

namespace CopperScreen;

internal sealed partial class MainWindow
{
    private bool _ejectingDisk;
    private enum FloppyEjectChoice { Cancel, Save, Discard }

    private async Task EjectDriveDiskAsync(int index)
    {
        if (_ejectingDisk) return;
        var runtime = _runtime;
        if (runtime == null)
        {
            _settingsDraft.DriveDiskPaths[index] = null;
            _committedSettings.DriveDiskPaths[index] = null;
            SetDrivePathTextSilently(index, string.Empty);
            if (index == 0) _setupDiskBox.Text = string.Empty;
            _latestState = CreateIdleState(_settingsDraft);
            RememberCurrentSession();
            UpdateSettingsStatus(); UpdateToolbarStatus();
            return;
        }

        _ejectingDisk = true;
        var resume = false;
        try
        {
            // The execution owner checks dirty state, including writes newer than the UI snapshot.
            var result = await runtime.EjectDiskAsync(index);
            if (_runtime != runtime) return;
            var drive = result.State.Drives[index];
            if (result.Success || !drive.HasUnsavedChanges || CopperOsWorkspace.IsWorkspace(drive.DiskPath))
            {
                await CompleteDiskCommandAsync(Task.FromResult(result));
                return;
            }

            // Keep the image stable through the decision, file picker, save and eject.
            if (!result.State.IsPaused)
            {
                result = await runtime.TogglePausedAsync();
                if (_runtime != runtime || !result.Success || !result.State.IsPaused) return;
                resume = true;
            }
            _latestState = result.State;
            UpdateToolbarStatus();
            ReleaseInteractiveInput();
            var choice = await ChooseFloppyEjectAsync(index);
            if (_runtime != runtime || choice == FloppyEjectChoice.Cancel) return;
            if (runtime.CurrentState.Drives[index].DiskPath != drive.DiskPath)
            {
                ShowDiskFeedback("The disk changed. Choose Eject again.", false);
                return;
            }
            if (choice == FloppyEjectChoice.Save && !await TrySaveDiskAdfAsync(index)) return;
            if (_runtime != runtime) return;
            // Saving must pass the ordinary dirty check; only explicit discard bypasses it.
            await CompleteDiskCommandAsync(choice == FloppyEjectChoice.Discard
                ? runtime.DiscardAndEjectDiskAsync(index) : runtime.EjectDiskAsync(index));
        }
        catch (Exception ex) when (IsWorkspaceFileError(ex)) { SetSettingsError(ex.Message); ShowDiskFeedback(ex.Message, false); }
        finally
        {
            if (resume && _runtime == runtime && runtime.CurrentState.IsPaused)
            {
                var result = await runtime.TogglePausedAsync();
                if (_runtime == runtime) _latestState = result.State;
            }
            _ejectingDisk = false;
            UpdateSettingsStatus(); UpdateToolbarStatus();
        }
    }

    private Task<FloppyEjectChoice> ChooseFloppyEjectAsync(int index)
    {
        var dialog = new Window { Title = $"Unsaved changes in DF{index}", Width = 460,
            SizeToContent = SizeToContent.Height, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var actions = CreateSettingsGroupForm();
        actions.Margin = new Thickness(20);
        actions.Children.Add(SettingsNote($"DF{index} has unsaved floppy changes. Save them as an ADF before ejecting, or discard them. The original image file stays unchanged unless you choose to overwrite it when saving."));
        actions.Children.Add(CreatePanelButton("Save ADF and eject…", () => dialog.Close(FloppyEjectChoice.Save)));
        actions.Children.Add(CreatePanelButton("Discard changes and eject", () => dialog.Close(FloppyEjectChoice.Discard)));
        var cancel = CreatePanelButton("Cancel", () => dialog.Close(FloppyEjectChoice.Cancel));
        cancel.IsCancel = true;
        actions.Children.Add(cancel);
        dialog.Content = actions;
        return dialog.ShowDialog<FloppyEjectChoice>(_settingsVisible ? _settingsWindow : this);
    }
}
