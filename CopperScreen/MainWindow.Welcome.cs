using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace CopperScreen;

internal sealed partial class MainWindow
{
    private readonly CopperScreenRecentSession _recentSession;
    private Button _welcomeStart = null!, _welcomeRomButton = null!, _welcomeDiskButton = null!;
    private TextBlock _welcomeRom = null!, _welcomeDisk = null!, _welcomeMessage = null!;
    private StackPanel _welcomeRecent = null!;
    private ComboBox _welcomeBootMethod = null!;
    private Control? _bootRomRow, _welcomeRomRow;
    private Grid _welcomeDiskRow = null!;
    private bool _updatingBootMethod;
    private bool _welcomeCopperOs;
    private Control _welcomeWorkspacePanel = null!, _welcomeHdfPanel = null!;
    private TextBlock _welcomeRequirements = null!, _welcomeHdf = null!;
    private Button _welcomeRemoveDisk = null!;
    private ComboBox _welcomeWorkspaces = null!, _welcomeProfiles = null!;
    private TextBox _welcomeWorkspaceName = null!;
    private Button _welcomeCreateWorkspace = null!, _welcomeResumeWorkspace = null!, _welcomeSaveProfile = null!;
    private Expander _welcomeNewWorkspace = null!;
    private Control _welcomeResumeControls = null!;

    private Border CreateIdlePanel()
    {
        _welcomeCopperOs = CopperScreenLaunchWorkflow.IsWorkspacePath(_settingsDraft.DriveDiskPaths[0], _recentSession.DataDirectory);
        var content = new StackPanel { Spacing = 16, MaxWidth = 570, Margin = new Thickness(32), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(new TextBlock { Text = "COPPERSCREEN", Foreground = Accent, FontSize = 12, FontWeight = FontWeight.SemiBold, LetterSpacing = 2 });
        content.Children.Add(new TextBlock { Text = "Start your Amiga", Foreground = CopperScreenAppearance.Text, FontSize = 32, FontWeight = FontWeight.SemiBold });
        content.Children.Add(SettingsNote(CopperScreenLaunchWorkflow.ContentGuidance));
        _welcomeBootMethod = new ComboBox { ItemsSource = CopperScreenLaunchWorkflow.StartChoices, PlaceholderText = "Saved custom configuration", HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetName(_welcomeBootMethod, "Start configuration");
        _welcomeBootMethod.SelectionChanged += (_, _) =>
        {
            if (_updatingBootMethod || _kickstartSourceBox == null || _settingsVisible || _applyingSettings || _welcomeBootMethod.SelectedIndex < 0) return;
            try
            {
                _recentSession.RememberKickstart(_settingsDraft, AppContext.BaseDirectory);
                _recentSession.RememberDisks(_settingsDraft, AppContext.BaseDirectory);
                _recentSession.RememberConfiguration(_settingsDraft, AppContext.BaseDirectory);
                var draft = CopperScreenLaunchWorkflow.SelectStartChoice(_settingsDraft, _welcomeBootMethod.SelectedIndex);
                draft = _recentSession.RestoreConfiguration(draft, AppContext.BaseDirectory);
                draft = _recentSession.RestoreKickstart(draft);
                draft = _recentSession.RestoreDisks(draft);
                draft = CopperScreenBundledCopperOs.PrepareForLaunch(draft, _recentSession.DataDirectory);
                SetWelcomeDraft(draft);
                RememberCurrentSession();
                if (_recentSession.LastError is { } error) SetSettingsError(error);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException or FormatException or OverflowException)
            { SetSettingsError(ex.Message); }
        };
        _welcomeRom = new TextBlock();
        _welcomeDisk = new TextBlock();
        AutomationProperties.SetName(_welcomeDisk, "Startup disk selection");
        _welcomeRomButton = CreatePanelButton("Choose ROM…", () => _ = ChooseWelcomeRomAsync());
        _welcomeDiskButton = CreatePanelButton("Choose disk…", () => _ = OpenDiskPickerAsync(0));
        var setup = new StackPanel { Spacing = 14 };
        setup.Children.Add(_welcomeBootMethod);
        _welcomeRequirements = new TextBlock { Foreground = MutedText, FontSize = 13, TextWrapping = TextWrapping.Wrap };
        AutomationProperties.SetName(_welcomeRequirements, "ROM requirements");
        setup.Children.Add(_welcomeRequirements);
        _welcomeRomRow = WelcomeRow("1", "Boot ROM", _welcomeRom, _welcomeRomButton);
        setup.Children.Add(_welcomeRomRow);
        setup.Children.Add(new Border { Height = 1, Background = QuietBorder });
        _welcomeDiskRow = WelcomeRow("2", "Startup disk", _welcomeDisk, _welcomeDiskButton);
        setup.Children.Add(_welcomeDiskRow);
        _welcomeRemoveDisk = CreatePanelButton("Remove floppy disk", () => _ = EjectDriveDiskAsync(0));
        setup.Children.Add(_welcomeRemoveDisk);
        _welcomeHdf = new TextBlock();
        AutomationProperties.SetName(_welcomeHdf, "CopperOS HDF selection");
        var hdfPanel = new StackPanel { Spacing = 8 };
        hdfPanel.Children.Add(new TextBlock { Text = "CopperOS hard disk", Foreground = CopperScreenAppearance.Text, FontWeight = FontWeight.SemiBold });
        _welcomeHdf.Foreground = MutedText;
        _welcomeHdf.TextTrimming = TextTrimming.CharacterEllipsis;
        hdfPanel.Children.Add(_welcomeHdf);
        hdfPanel.Children.Add(SettingsNote("CopperOS opens its desktop with Files, Editor and About. Manage or back up your hard disk in Configure Amiga."));
        _welcomeHdfPanel = hdfPanel;
        setup.Children.Add(hdfPanel);
        _welcomeWorkspacePanel = CreateWelcomeWorkspacePanel();
        setup.Children.Add(_welcomeWorkspacePanel);
        content.Children.Add(new Border { Background = Surface, CornerRadius = new CornerRadius(12), Padding = new Thickness(22), Child = setup });
        _welcomeMessage = new TextBlock { Foreground = MutedText, TextWrapping = TextWrapping.Wrap, FontSize = 13 };
        AutomationProperties.SetName(_welcomeMessage, "Startup guidance");
        content.Children.Add(_welcomeMessage);
        _welcomeStart = CreatePanelButton("Start Amiga", () => _ = StartRuntimeFromSettingsAsync());
        MakePrimary(_welcomeStart);
        _welcomeStart.Padding = new Thickness(24, 10);
        var customise = CreatePanelButton("Configure Amiga…", ShowSettingsWindow);
        customise.Background = Brushes.Transparent;
        customise.BorderBrush = QuietBorder;
        content.Children.Add(CommandGroup(_welcomeStart, customise));
        content.Children.Add(SettingsNote("Your last launch is remembered. F10 releases captured input."));
        _welcomeProfiles = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetName(_welcomeProfiles, "Saved launch profiles");
        var load = CreatePanelButton("Load profile", LoadWelcomeProfile);
        _welcomeSaveProfile = CreatePanelButton("Save launch profile", () =>
        {
            SaveCurrentProfile(); RefreshWelcomeProfiles();
            if (_settingsStartupError == null) _welcomeMessage.Text = "Launch profile saved. Select it under Saved launch profiles and choose Load profile to use it again.";
        });
        var profilesPanel = new StackPanel { Spacing = 8 };
        profilesPanel.Children.Add(SettingsNote("Save a profile to keep several setups. Loading a profile restores its ROM, disks and write settings."));
        profilesPanel.Children.Add(_welcomeProfiles);
        profilesPanel.Children.Add(CommandGroup(load, _welcomeSaveProfile));
        content.Children.Add(new Expander { Header = "Saved launch profiles", Content = profilesPanel, HorizontalAlignment = HorizontalAlignment.Stretch });
        RefreshWelcomeProfiles();
        _welcomeRecent = new StackPanel { Spacing = 6 };
        RefreshWelcomeRecentDisks();
        content.Children.Add(_welcomeRecent);
        return new Border { Background = CopperScreenAppearance.Background, Child = new ScrollViewer { Name = "LauncherScroll", Content = content, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled }, MaxHeight = 720, IsVisible = _runtime == null };
    }

    private void UpdateBootMethodControls()
    {
        if (_kickstartSourceBox == null) return;
        var source = ParseKickstartSourceSelection(_kickstartSourceBox.SelectedItem);
        var minimal = source == CopperScreenKickstartSource.MinimalDiskBoot;
        _welcomeCopperOs = source == CopperScreenKickstartSource.CopperStart && CopperOsWorkspace.IsWorkspace(_settingsDraft.DriveDiskPaths[0]);
        var hdf = source == CopperScreenKickstartSource.CopperStart && _settingsDraft.Chipset.DisplayChip == DisplayChipModel.AgaLisa;
        if (_welcomeWorkspacePanel != null) _welcomeWorkspacePanel.IsVisible = _welcomeCopperOs;
        if (_welcomeHdfPanel != null) _welcomeHdfPanel.IsVisible = hdf;
        if (_welcomeDiskRow != null) _welcomeDiskRow.IsVisible = !_welcomeCopperOs && !hdf;
        if (_welcomeRemoveDisk != null) _welcomeRemoveDisk.IsVisible = !_welcomeCopperOs && !hdf && !string.IsNullOrWhiteSpace(_settingsDraft.DriveDiskPaths[0]);
        if (_bootRomRow != null) _bootRomRow.IsVisible = !minimal;
        if (_welcomeRomRow != null) _welcomeRomRow.IsVisible = !minimal && !hdf;
        if (_setupDiskBox != null) _setupDiskBox.PlaceholderText = minimal ? "Required · standard ADF or ZIP containing an ADF" : "Optional · boot without a floppy disk";
        if (_welcomeRequirements != null) _welcomeRequirements.Text = CopperScreenLaunchWorkflow.RomGuidance(_settingsDraft);
        if (_welcomeBootMethod != null)
        {
            _updatingBootMethod = true;
            try
            {
                _welcomeBootMethod.SelectedIndex = CopperScreenLaunchWorkflow.StartChoice(_settingsDraft);
            }
            finally { _updatingBootMethod = false; }
        }
        if (_welcomeRecent != null) _welcomeRecent.IsVisible = _recentSession.RecentDisks.Count > 0 && !_welcomeCopperOs && !hdf;
    }

    private static Grid WelcomeRow(string number, string title, TextBlock value, Button action)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 14 };
        row.Children.Add(new Border { Width = 28, Height = 28, CornerRadius = new CornerRadius(14), Background = CopperScreenAppearance.ProtectedSurface, VerticalAlignment = VerticalAlignment.Center, Child = new TextBlock { Text = number, Foreground = CopperScreenAppearance.ProtectedText, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } });
        var text = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock { Text = title, Foreground = CopperScreenAppearance.Text, FontWeight = FontWeight.SemiBold });
        value.Foreground = MutedText;
        value.FontSize = 12;
        value.TextTrimming = TextTrimming.CharacterEllipsis;
        text.Children.Add(value);
        Grid.SetColumn(text, 1);
        row.Children.Add(text);
        Grid.SetColumn(action, 2);
        row.Children.Add(action);
        return row;
    }

    private void UpdateWelcomeStatus(bool valid, string error)
    {
        if (_welcomeStart == null || _runtime != null) return;
        UpdateBootMethodControls();
        var minimal = ParseKickstartSourceSelection(_kickstartSourceBox.SelectedItem) == CopperScreenKickstartSource.MinimalDiskBoot;
        var copperStart = ParseKickstartSourceSelection(_kickstartSourceBox.SelectedItem) == CopperScreenKickstartSource.CopperStart;
        var hdf = copperStart && _settingsDraft.Chipset.DisplayChip == DisplayChipModel.AgaLisa;
        _welcomeHdf.Text = _settingsDraft.HardDrives.FirstOrDefault() is { } hardfile
            ? Path.GetFileName(hardfile.Path) + (hardfile.ReadOnly ? " · read-only" : " · changes saved between sessions")
            : "No personal hard disk available";
        ToolTip.SetTip(_welcomeHdf, _settingsDraft.HardDrives.FirstOrDefault()?.Path);
        var rom = _kickstartRomBox.Text;
        _welcomeRom.Text = string.IsNullOrWhiteSpace(rom) ? "No ROM selected" : Path.GetFileName(rom);
        ToolTip.SetTip(_welcomeRom, rom);
        var disk = _setupDiskBox.Text;
        _welcomeDisk.Text = string.IsNullOrWhiteSpace(disk) ? minimal ? "Required · standard ADF or ZIP containing an ADF" : "Optional · no disk selected" : Path.GetFileName(disk);
        ToolTip.SetTip(_welcomeDisk, disk);
        var workspaceReady = !_welcomeCopperOs || CopperOsWorkspace.IsWorkspace(disk);
        _welcomeStart.IsEnabled = valid && workspaceReady && !_settingsVisible && !_applyingSettings;
        _welcomeStart.Content = _applyingSettings ? "Starting…" : (_welcomeCopperOs || hdf) ? "Start CopperOS" : copperStart ? "Start CopperStart" : "Start Amiga";
        _welcomeRomButton.IsEnabled = _welcomeDiskButton.IsEnabled = !_settingsVisible && !_applyingSettings;
        _welcomeBootMethod.IsEnabled = !_settingsVisible && !_applyingSettings;
        _welcomeHdfPanel.IsEnabled = _welcomeRemoveDisk.IsEnabled = !_settingsVisible && !_applyingSettings;
        if (_welcomeCreateWorkspace != null)
        {
            _welcomeCreateWorkspace.IsEnabled = _welcomeResumeWorkspace.IsEnabled = _welcomeWorkspaces.IsEnabled = _welcomeWorkspaceName.IsEnabled = !_settingsVisible && !_applyingSettings;
            _welcomeSaveProfile.IsEnabled = valid && workspaceReady && !_settingsVisible && !_applyingSettings;
        }
        _welcomeMessage.Text = _settingsStartupError ?? (_welcomeCopperOs && valid
            ? workspaceReady ? "Ready: " + Path.GetFileName(Path.GetDirectoryName(disk)) + ". At the Shell prompt, type Demo and press Enter. More has help and workspace export." : "Create a workspace or select one and choose Resume workspace."
            : hdf ? valid ? "Ready to boot the CopperOS desktop. Allow around 25 seconds for startup. No floppy disk is needed." : _settingsDraft.HardDrives.Count == 0 ? "Select Amiga 1200 with CopperOS HDF to prepare your personal hard disk." : error
            : copperStart ? valid ? CopperScreenCopperStartRom.Description : error
            : minimal ? valid ? "Experimental boot for demos and games that take over the machine. DOS and Workbench need a Kickstart ROM." : error : valid ? string.IsNullOrWhiteSpace(disk) ? "Ready to start without a floppy disk. Settings will be remembered." : "Ready to start. Settings will be remembered." : string.IsNullOrWhiteSpace(rom) ? "Select a Kickstart ROM to start." : error);
        _welcomeMessage.Foreground = _settingsStartupError != null || (!valid && (minimal ? !string.IsNullOrWhiteSpace(disk) : !string.IsNullOrWhiteSpace(rom))) ? new SolidColorBrush(Color.Parse("#FFBCAC")) : MutedText;
    }

    private Control CreateWelcomeWorkspacePanel()
    {
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(SettingsNote("Create a private workspace from the qualified system image, or resume your saved files and settings. The original image stays unchanged."));
        panel.Children.Add(new Expander { Header = "Preview requirements and keeping your files", HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = SettingsNote(CopperScreenLaunchWorkflow.CopperOsGuidance + "\nPair: " + CopperOsWorkspace.BundledSeed.PairId + "\nROM: " + CopperScreenCopperStartRom.Version) });
        _welcomeWorkspaceName = new TextBox { Text = "personal", PlaceholderText = "Workspace name" };
        AutomationProperties.SetName(_welcomeWorkspaceName, "New CopperOS workspace name");
        _welcomeCreateWorkspace = CreatePanelButton("Create workspace…", () => _ = CreateWelcomeWorkspaceAsync());
        var create = new StackPanel { Spacing = 8 };
        create.Children.Add(SettingsNote("New workspace name (letters, numbers, '-' or '_')"));
        create.Children.Add(_welcomeWorkspaceName);
        create.Children.Add(_welcomeCreateWorkspace);
        create.Children.Add(CreatePanelButton("Import workspace backup…", () => _ = ImportWorkspaceBackupAsync()));
        _welcomeNewWorkspace = new Expander { Header = "Create a new workspace", Content = create, HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(_welcomeNewWorkspace);
        _welcomeWorkspaces = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetName(_welcomeWorkspaces, "CopperOS workspaces");
        _welcomeResumeWorkspace = CreatePanelButton("Resume workspace", ResumeWelcomeWorkspace);
        var resume = new StackPanel { Spacing = 8 };
        resume.Children.Add(_welcomeWorkspaces);
        resume.Children.Add(_welcomeResumeWorkspace);
        var recovery = new StackPanel { Spacing = 8 };
        recovery.Children.Add(SettingsNote("Export a saved image or its previous checkpoint before repair. Import backups into a new workspace. Uninstalling removes app data."));
        recovery.Children.Add(CreatePanelButton("Export saved workspace…", () => _ = ExportWorkspaceBackupAsync(saved: true)));
        recovery.Children.Add(CreatePanelButton("Export previous checkpoint…", () => _ = ExportWorkspaceBackupAsync(saved: true, previous: true)));
        recovery.Children.Add(CreatePanelButton("Recover previous checkpoint…", () => _ = RecoverWorkspaceAsync()));
        resume.Children.Add(new Expander { Header = "Back up or recover a workspace", Content = recovery, HorizontalAlignment = HorizontalAlignment.Stretch });
        _welcomeResumeControls = resume;
        panel.Children.Add(resume);
        RefreshWelcomeWorkspaces();
        return panel;
    }

    private void RefreshWelcomeWorkspaces()
    {
        if (_welcomeWorkspaces == null) return;
        try
        {
            var selected = _welcomeWorkspaces.SelectedItem as string;
            var names = CopperScreenLaunchWorkflow.Workspaces(_recentSession.DataDirectory);
            _welcomeWorkspaces.ItemsSource = names;
            _welcomeWorkspaces.SelectedItem = names.FirstOrDefault(n => n == selected) ?? names.FirstOrDefault();
            _welcomeResumeControls.IsVisible = names.Length > 0;
            _welcomeNewWorkspace.IsExpanded = names.Length == 0;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        { if (_settingsStatus != null) SetSettingsError("Could not list workspaces: " + ex.Message + " Check your app-data permissions."); }
    }

    private async Task CreateWelcomeWorkspaceAsync()
    {
        try
        {
            var draft = ReadSettingsDraft();
            _ = CopperScreenCopperStartRom.Read(draft.KickstartRomPath);
            var id = _welcomeWorkspaceName.Text?.Trim() ?? "";
            CopperOsWorkspace.ValidateId(id);
            if (CopperScreenLaunchWorkflow.Workspaces(_recentSession.DataDirectory).Contains(id, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException("That workspace already exists. Choose Resume workspace, or use a new name. Your files have not been replaced.");
            var seed = CopperScreenLaunchWorkflow.DiagnosticSeed(AppContext.BaseDirectory);
            if (seed == null)
            {
                var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Choose qualified CopperOS system image (original is preserved)", AllowMultiple = false,
                    FileTypeFilter = [new FilePickerFileType("CopperOS system image") { Patterns = ["*.adf"] }]
                });
                seed = files.FirstOrDefault()?.TryGetLocalPath();
                if (seed == null) return;
            }
            SetWelcomeDraft(CopperScreenLaunchWorkflow.Workspace(draft, _recentSession.DataDirectory, id, seed));
            RememberCurrentSession(); RefreshWelcomeWorkspaces();
            _welcomeWorkspaces.SelectedItem = id;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException or System.Text.Json.JsonException)
        { SetSettingsError(ex.Message + " Choose the qualified pair, or resume another workspace. Existing work is preserved."); }
    }

    private void ResumeWelcomeWorkspace()
    {
        try
        {
            if (_welcomeWorkspaces.SelectedItem is not string id) throw new InvalidOperationException("Create a workspace first, or select an existing workspace.");
            SetWelcomeDraft(CopperScreenLaunchWorkflow.Workspace(ReadSettingsDraft(), _recentSession.DataDirectory, id, null));
            RememberCurrentSession();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException or System.Text.Json.JsonException)
        { SetSettingsError(ex.Message + " Keep the workspace files before repair; choose another workspace to continue."); }
    }

    private void SetWelcomeDraft(CopperScreenSettingsDraft draft)
    {
        ClearSettingsStartupError();
        _settingsDraft = draft;
        _committedSettings = draft.Clone();
        _latestState = CreateIdleState(draft);
        RefreshSettingsUi(); UpdateSettingsStatus(); UpdateToolbarStatus();
    }

    private void RefreshWelcomeProfiles()
    {
        var profiles = CopperScreenProfileStore.ListProfiles(AppContext.BaseDirectory, out var error, _recentSession.DataDirectory);
        _welcomeProfiles.ItemsSource = profiles.Where(p => p.IsAvailable).ToArray();
        _welcomeProfiles.SelectedIndex = profiles.Count > 0 ? 0 : -1;
        if (error != null && _settingsStatus != null) SetSettingsError(error);
    }

    private void LoadWelcomeProfile()
    {
        if (_welcomeProfiles.SelectedItem is not CopperScreenProfileSummary profile) return;
        if (!CopperScreenProfile.TryLoad(profile.Path, AppContext.BaseDirectory, out var loaded, out var error, _recentSession.DataDirectory))
        { SetSettingsError(error ?? "Could not load profile. Choose another profile."); return; }
        var draft = CopperScreenSettingsDraft.FromProfile(loaded);
        draft.Engine = CopperScreenEngine.Lightweight;
        _welcomeCopperOs = CopperScreenLaunchWorkflow.IsWorkspacePath(draft.DriveDiskPaths[0], _recentSession.DataDirectory);
        SetWelcomeDraft(draft);
    }

    private async Task ChooseWelcomeRomAsync()
    {
        await PickRomAsync();
        if (_settingsStartupError == null)
        {
            _settingsDraft = ReadSettingsDraft();
            _committedSettings = _settingsDraft.Clone();
            _latestState = CreateIdleState(_settingsDraft);
            RememberCurrentSession();
        }
        UpdateSettingsStatus();
        UpdateToolbarStatus();
    }

    private void RememberCurrentSession()
    {
        var machine = _committedSettings.Clone();
        var options = machine.ToStartupOptions(AppContext.BaseDirectory);
        if (machine.KickstartSource != CopperScreenKickstartSource.MinimalDiskBoot)
            machine.KickstartRomPath = options.KickstartRomPath;
        for (var i = machine.DriveDiskPaths.Length - 1; i >= 0; i--)
        {
            machine.DriveDiskPaths[i] = options.DriveDiskPaths[i];
            _recentSession.RememberDisk(machine.DriveDiskPaths[i]);
        }
        _recentSession.Save(machine, _outputVolume, _outputMuted, AppContext.BaseDirectory);
        if (_recentSession.LastError is { } error) SetSettingsError(error);
    }

    private void RefreshWelcomeRecentDisks()
    {
        _welcomeRecent.Children.Clear();
        _welcomeRecent.IsVisible = _recentSession.RecentDisks.Count > 0 && !_welcomeCopperOs && CopperScreenLaunchWorkflow.StartChoice(_settingsDraft) != 3;
        if (!_welcomeRecent.IsVisible) return;
        _welcomeRecent.Children.Add(SectionLabel("Recent disks"));
        AddRecentDiskButtons(_welcomeRecent, 3, () => { });
    }

    private void AddRecentDiskButtons(StackPanel panel, int maximum, Action beforeInsert)
    {
        var count = 0;
        foreach (var path in _recentSession.RecentDisks)
        {
            if (CopperScreenLaunchWorkflow.IsWorkspacePath(path, _recentSession.DataDirectory)) continue;
            if (!CopperScreenDiskImageArchive.DiskPathExists(path)) continue;
            var button = CreatePanelButton(Path.GetFileName(path), () => { beforeInsert(); _ = InsertDriveDiskAsync(0, path); });
            button.Content = new TextBlock { Text = Path.GetFileName(path), TextTrimming = TextTrimming.CharacterEllipsis };
            button.HorizontalAlignment = HorizontalAlignment.Stretch;
            button.HorizontalContentAlignment = HorizontalAlignment.Left;
            button.Background = Brushes.Transparent;
            button.BorderThickness = new Thickness(0);
            ToolTip.SetTip(button, path);
            panel.Children.Add(button);
            if (++count == maximum) break;
        }
        if (count == 0) panel.Children.Add(new TextBlock { Text = "No recent disks available. Use Browse to choose a disk.", Foreground = MutedText, TextWrapping = TextWrapping.Wrap });
    }

    internal static bool IsSupportedDiskDrop(string? path) => path != null && (CopperScreenAdfImage.IsSupported(path) || Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase));

    private void ConfigureDiskDrop(Control target, int index)
    {
        DragDrop.SetAllowDrop(target, true);
        target.AddHandler(DragDrop.DragOverEvent, (_, args) =>
        {
            var files = args.DataTransfer.TryGetFiles()?.ToArray();
            var path = files is { Length: 1 } ? files[0].TryGetLocalPath() : null;
            args.DragEffects = !_settingsVisible && !_applyingSettings && !_welcomeCopperOs && CopperScreenLaunchWorkflow.StartChoice(_settingsDraft) != 3 && IsSupportedDiskDrop(path) ? DragDropEffects.Copy : DragDropEffects.None;
            args.Handled = true;
        });
        target.AddHandler(DragDrop.DropEvent, async (_, args) =>
        {
            args.Handled = true;
            if (_settingsVisible || _applyingSettings || _welcomeCopperOs || CopperScreenLaunchWorkflow.StartChoice(_settingsDraft) == 3) return;
            var files = args.DataTransfer.TryGetFiles()?.ToArray();
            var path = files is { Length: 1 } ? files[0].TryGetLocalPath() : null;
            if (!IsSupportedDiskDrop(path)) return;
            try
            {
                ReleaseInteractiveInput();
                if (!await TryAssignArchiveDiskSetAsync(path!)) await InsertDriveDiskAsync(index, path!);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException)
            { ShowDiskFeedback(ex.Message, false); }
        });
    }
}
