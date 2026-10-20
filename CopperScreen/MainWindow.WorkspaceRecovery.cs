using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;

namespace CopperScreen;

internal sealed partial class MainWindow
{
    private string SelectedWorkspaceImage()
    {
        if (_runtime != null) throw new InvalidOperationException("Stop the session before recovering saved storage.");
        if (_welcomeWorkspaces.SelectedItem is not string id) throw new InvalidOperationException("Select a saved workspace first.");
        CopperOsWorkspace.ValidateId(id);
        return Path.Combine(_recentSession.DataDirectory, "CopperOS", "Workspaces", id, "System.adf");
    }

    private async Task ExportWorkspaceBackupAsync(bool saved = false, bool previous = false)
    {
        try
        {
            var image = saved ? SelectedWorkspaceImage() : null; var runtime = _runtime;
            if (!saved && runtime == null) throw new InvalidOperationException("Start a workspace or choose Export saved workspace.");
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = previous ? "Export previous CopperOS checkpoint" : "Export CopperOS backup",
                SuggestedFileName = "CopperOS-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".copperos",
                DefaultExtension = "copperos", ShowOverwritePrompt = false,
                FileTypeChoices = [new("CopperOS workspace backup") { Patterns = ["*.copperos"] }]
            });
            if (file?.TryGetLocalPath() is not { } path) return;
            if (saved) CopperOsWorkspace.ExportSaved(image!, path, previous);
            else
            {
                if (_runtime != runtime) throw new InvalidOperationException("The session changed. Choose Export again.");
                var result = await runtime!.ExportWorkspaceAsync(path); _latestState = result.State;
                if (!result.Success) throw new IOException(result.Message);
            }
            ShowDiskFeedback("Workspace backup exported. Keep it outside app data before uninstalling.", true);
        }
        catch (Exception ex) when (IsWorkspaceFileError(ex))
        { SetSettingsError(ex.Message + " Choose a new backup filename. Existing files are preserved."); ShowDiskFeedback(ex.Message, false); }
    }

    private async Task ImportWorkspaceBackupAsync()
    {
        try
        {
            if (_runtime != null) throw new InvalidOperationException("Stop the session before importing a workspace.");
            var draft = ReadSettingsDraft(); _ = CopperScreenCopperStartRom.Read(draft.KickstartRomPath);
            var id = _welcomeWorkspaceName.Text?.Trim() ?? ""; CopperOsWorkspace.ValidateId(id);
            if (CopperScreenLaunchWorkflow.Workspaces(_recentSession.DataDirectory).Contains(id, StringComparer.OrdinalIgnoreCase))
                throw new IOException("Choose a new workspace name. Import never replaces an existing workspace.");
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import CopperOS backup into a new workspace", AllowMultiple = false,
                FileTypeFilter = [new("CopperOS workspace backup") { Patterns = ["*.copperos"] }]
            });
            if (files.FirstOrDefault()?.TryGetLocalPath() is not { } path) return;
            CopperOsWorkspace.ImportBackup(_recentSession.DataDirectory, id, path);
            SetWelcomeDraft(CopperScreenLaunchWorkflow.Workspace(draft, _recentSession.DataDirectory, id, null));
            RememberCurrentSession(); RefreshWelcomeWorkspaces(); _welcomeWorkspaces.SelectedItem = id;
        }
        catch (Exception ex) when (IsWorkspaceFileError(ex))
        { SetSettingsError(ex.Message + " Keep the backup; choose a compatible pair or a new name."); }
    }

    private async Task RecoverWorkspaceAsync()
    {
        try
        {
            var image = SelectedWorkspaceImage();
            var dialog = new Window { Title = "Recover CopperOS checkpoint", Width = 480, SizeToContent = SizeToContent.Height,
                CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            dialog.Content = new StackPanel { Margin = new Avalonia.Thickness(24), Spacing = 16, Children =
            {
                SettingsNote("Recover the previous checkpoint? It may have older files and settings. The current image will be preserved separately. The previous checkpoint will remain available. Stop other sessions using this workspace first."),
                CommandGroup(CreatePanelButton("Cancel", () => dialog.Close(false)), CreatePanelButton("Recover checkpoint", () => dialog.Close(true)))
            } };
            if (!await dialog.ShowDialog<bool>(this)) return;
            var preserved = CopperOsWorkspace.RecoverPrevious(image);
            ClearSettingsStartupError(); UpdateSettingsStatus();
            _welcomeMessage.Text = "Checkpoint recovered. Current image preserved at " + preserved + ". Resume the workspace to continue.";
        }
        catch (Exception ex) when (IsWorkspaceFileError(ex))
        { SetSettingsError(ex.Message + " Recovery stopped. Existing files have not been reset; preserve them before further repair."); }
    }

    private static bool IsWorkspaceFileError(Exception ex) => ex is IOException or InvalidDataException or UnauthorizedAccessException
        or ArgumentException or InvalidOperationException or NotSupportedException or System.Text.Json.JsonException;
}
