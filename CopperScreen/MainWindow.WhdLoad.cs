using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace CopperScreen;

internal sealed partial class MainWindow
{
    private sealed record WhdLoadRequest(string Game, string Data, CopperScreenWhdLoadSetup Setup);
    private CopperScreenWhdLoadLaunch? _whdLoadLaunch;
    private CopperScreenSettingsDraft? _whdLoadReturnDraft;

    private async Task OpenWhdLoadAsync(string? gamePath = null)
    {
        if (_runtime != null || _settingsVisible || _applyingSettings) return;
        CopperScreenWhdLoadLaunch? prepared = null;
        try
        {
            var previous = ReadSettingsDraft();
            var request = await PromptWhdLoadAsync(gamePath, previous);
            if (request == null || _runtime != null || _applyingSettings) return;
            _applyingSettings = true;
            UpdateSettingsStatus(); UpdateToolbarStatus();
            _welcomeMessage.Text = "Preparing the WHDLoad session…";
            CopperScreenSettingsDraft draft;
            try
            {
                prepared = await Task.Run(() => CopperScreenWhdLoadLaunch.OpenAsync(request.Game));
                var slave = prepared.Slaves.Count == 1 ? prepared.Slaves[0] : await ChooseWhdLoadSlaveAsync(prepared.Slaves);
                if (slave == null) return;
                var session = prepared;
                draft = await Task.Run(() => session.PrepareAsync(previous, request.Setup, slave, request.Data, _recentSession.DataDirectory));
                request.Setup.Save(_recentSession.DataDirectory);
            }
            finally { _applyingSettings = false; UpdateSettingsStatus(); UpdateToolbarStatus(); }

            _whdLoadReturnDraft = previous;
            _whdLoadLaunch = prepared;
            prepared = null;
            try
            {
                SetWelcomeDraft(draft);
                await StartRuntimeFromSettingsAsync();
            }
            finally
            {
                if (_runtime == null)
                {
                    var error = _settingsStartupError;
                    SetWelcomeDraft(EndWhdLoadLaunch() ?? previous);
                    if (error != null) SetSettingsError(error);
                }
            }
        }
        catch (Exception ex) when (IsWorkspaceFileError(ex) || ex is FormatException or OverflowException)
        { SetSettingsError(ex.Message); }
        finally { prepared?.Dispose(); }
    }

    private CopperScreenSettingsDraft? EndWhdLoadLaunch()
    {
        _whdLoadLaunch?.Dispose();
        _whdLoadLaunch = null;
        var previous = _whdLoadReturnDraft;
        _whdLoadReturnDraft = null;
        return previous;
    }

    private async Task<WhdLoadRequest?> PromptWhdLoadAsync(string? gamePath, CopperScreenSettingsDraft current)
    {
        var saved = CopperScreenWhdLoadSetup.Load(_recentSession.DataDirectory);
        var dialog = new Window
        {
            Title = "Open WHDLoad game", Width = 620, MaxHeight = 720, SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false
        };
        var form = CreateSettingsGroupForm();
        form.Margin = new Thickness(20);
        var game = AddFile(form, "Game file", gamePath, new("WHDLoad games") { Patterns = ["*.slave", "*.lha"] });
        var data = AddTextSetting(form, "Data drawer (optional)", markRestart: false);
        form.Children.Add(SettingsNote("Leave the data drawer blank to use the slave’s default. A bare slave needs its game files beside it."));
        var system = CreateSettingsGroupForm();
        system.Children.Add(SettingsNote("Choose your own system files once. CopperScreen copies them into a temporary Amiga and launches the game automatically."));
        var machine = AddComboSetting(system, "Amiga model", ["Amiga 500 (68010)", "Amiga 1200 (AGA)"], markRestart: false);
        machine.SelectedIndex = (saved?.Aga ?? (CopperScreenMachinePresets.Identify(current) == CopperScreenMachineModel.Amiga1200)) ? 1 : 0;
        var nativeRom = current.KickstartSource is CopperScreenKickstartSource.KickstartRom or CopperScreenKickstartSource.Kickstart31Rom
            ? current.KickstartRomPath : null;
        var rom = AddFile(system, "Native Kickstart ROM", saved?.Rom ?? nativeRom,
            new("Kickstart ROM") { Patterns = ["*.rom", "*.bin", "*.kick", "*.zip"] });
        var workbench = AddFile(system, "Workbench system disk", saved?.Workbench, new("Workbench ADF") { Patterns = ["*.adf"] });
        var loader = AddFile(system, "WHDLoad executable or user package", saved?.Loader,
            new("WHDLoad executable or LhA") { Patterns = ["WHDLoad", "*.lha"] });
        var kickstarts = AddTextSetting(system, "Kickstarts drawer (optional)", markRestart: false);
        kickstarts.Text = saved?.Kickstarts ?? "";
        system.Children.Add(CreatePanelButton("Browse Kickstarts drawer…", async () =>
        {
            var folders = await dialog.StorageProvider.OpenFolderPickerAsync(new() { Title = "Choose Kickstarts drawer" });
            if (folders.FirstOrDefault()?.TryGetLocalPath() is { } path) kickstarts.Text = path;
        }));
        form.Children.Add(new Expander { Header = "System files and Amiga model", Content = system, IsExpanded = saved == null });
        form.Children.Add(SettingsNote("Press F9 to quit WHDLoad inside the Amiga. CopperScreen keeps game saves between launches."));
        var status = SettingsNote(string.Empty);
        form.Children.Add(status);
        form.Children.Add(CommandGroup(CreatePanelButton("Cancel", () => dialog.Close()), CreatePanelButton("Launch game", () =>
        {
            try
            {
                var source = RequiredPath(game.Text, "Choose an installed .slave or a .lha archive.");
                if (!CopperScreenWhdLoadLaunch.IsGame(source)) throw new ArgumentException("Choose an installed .slave or a .lha archive.");
                var setup = new CopperScreenWhdLoadSetup(RequiredPath(rom.Text, "Choose a Kickstart ROM matching the Amiga model."),
                    RequiredPath(workbench.Text, "Choose the Workbench system ADF."),
                    RequiredPath(loader.Text, "Choose WHDLoad or its user package."),
                    OptionalPath(kickstarts.Text) ?? "", machine.SelectedIndex == 1);
                dialog.Close(new WhdLoadRequest(source, data.Text?.Trim() ?? "", setup));
            }
            catch (ArgumentException ex) { status.Text = ex.Message; }
        })));
        dialog.Content = new ScrollViewer { Content = form };
        return await dialog.ShowDialog<WhdLoadRequest?>(this);

        TextBox AddFile(StackPanel group, string label, string? initial, FilePickerFileType filter)
        {
            var field = AddTextSetting(group, label, markRestart: false);
            field.Text = initial ?? "";
            group.Children.Add(CreatePanelButton($"Browse {label.ToLowerInvariant()}…", async () =>
            {
                var files = await dialog.StorageProvider.OpenFilePickerAsync(new()
                {
                    Title = label, FileTypeFilter = [filter, FilePickerFileTypes.All]
                });
                if (files.FirstOrDefault()?.TryGetLocalPath() is { } path) field.Text = path;
            }));
            return field;
        }

        static string? OptionalPath(string? value) => string.IsNullOrWhiteSpace(value) ? null : Path.GetFullPath(value.Trim());
        static string RequiredPath(string? value, string error) => OptionalPath(value) ?? throw new ArgumentException(error);
    }

    private Task<string?> ChooseWhdLoadSlaveAsync(IReadOnlyList<string> slaves)
    {
        var dialog = new Window
        {
            Title = "Choose WHDLoad slave", Width = 540, SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false
        };
        var form = CreateSettingsGroupForm();
        form.Margin = new Thickness(20);
        var choice = AddComboSetting(form, "Slave", slaves.ToArray(), markRestart: false);
        choice.SelectedIndex = 0;
        form.Children.Add(CommandGroup(CreatePanelButton("Cancel", () => dialog.Close()),
            CreatePanelButton("Continue", () => dialog.Close(choice.SelectedItem as string))));
        dialog.Content = form;
        return dialog.ShowDialog<string?>(this);
    }
}
