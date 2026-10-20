using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed partial class LaunchWorkflowTests
{
    private async Task UiAsync(Func<Task> action)
    {
        await _ui.Session.Dispatch(async () => { await action(); return true; }, CancellationToken.None);
    }
    private static async Task Until(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        while (!condition()) await Task.Delay(25, timeout.Token);
    }
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
    private static void Capture(Window window, string name)
    {
        if (Environment.GetEnvironmentVariable("COPPERSCREEN_ONBOARDING_CAPTURE") is not { } directory) return;
        Directory.CreateDirectory(directory);
        window.UpdateLayout();
        using var bitmap = window.CaptureRenderedFrame();
        bitmap?.Save(Path.Combine(directory, name + ".png"));
    }

    private static T Named<T>(MainWindow window, string name) where T : Control => window.GetLogicalDescendants()
        .OfType<T>().Single(c => AutomationProperties.GetName(c) == name);
    private static Button Button(MainWindow window, string label) => window.GetLogicalDescendants()
        .OfType<Button>().Single(c => c.Content is string value && value == label);

    [Fact]
    public Task OfflineFirstRunExplainsContentAndCopperOsCannotStartWithoutAWorkspace() => Ui(() =>
    {
        var window = new MainWindow([], new CopperScreenRecentSession(_root));
        window.Show();
        try
        {
            Assert.Contains(window.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == CopperScreenLaunchWorkflow.ContentGuidance);
            Assert.False(Button(window, "Start Amiga").IsEnabled);
            Capture(window, "first-run");
            Named<ComboBox>(window, "Boot method").SelectedIndex = 3;
            Assert.True(Button(window, "Create workspace…").IsVisible);
            Assert.False(Button(window, "Start CopperOS").IsEnabled);
            Capture(window, "copperos-setup");
            Click(Button(window, "Create workspace…"));
            Assert.Contains("CopperStart ROM is missing", Named<TextBlock>(window, "Startup guidance").Text);
            Assert.False(Directory.Exists(Path.Combine(_root, "CopperOS")));
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task SelectingCopperStartResetsUnsupportedHardwareAndKeepsTheOriginalDraftIndependent() => Ui(() =>
    {
        var original = new CopperScreenSettingsDraft { ChipRamKb = 2048, RealFastRamKb = 8192, RtcEnabled = true, FloppyDriveCount = 4 };
        new CopperScreenRecentSession(_root).Save(original, 37, true, AppContext.BaseDirectory);
        var window = new MainWindow([], new CopperScreenRecentSession(_root));
        window.Show();
        try
        {
            Named<ComboBox>(window, "Boot method").SelectedIndex = 2;
            Assert.False(Button(window, "Start CopperStart").IsEnabled);
            Button(window, "Configure Amiga…").RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            // Configuration opens normally after selecting the bounded preview.
            Assert.Equal(2048, original.ChipRamKb);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void CopperStartLaunchUsesTheQualifiedMachineAndImportedMediaAreReadOnly()
    {
        var current = new CopperScreenSettingsDraft { Engine = CopperScreenEngine.Legacy, ChipRamKb = 2048, RealFastRamKb = 8192, RtcEnabled = true, FloppyDriveCount = 4, KickstartRomPath = "native.rom" };
        current.DriveDiskPaths[0] = "original.adf"; current.DriveWriteProtected[0] = false;
        var launch = CopperScreenLaunchWorkflow.CopperStart(current);
        Assert.Null(CopperScreenAvailability.GetUnavailableReason(launch, AppContext.BaseDirectory));
        Assert.Equal(CopperScreenEngine.Lightweight, launch.Engine);
        Assert.Equal(512, launch.ChipRamKb); Assert.Equal(512, launch.PseudoFastRamKb);
        Assert.Equal(0, launch.RealFastRamKb); Assert.False(launch.RtcEnabled);
        Assert.Equal(1, launch.FloppyDriveCount); Assert.Null(launch.KickstartRomPath);
        Assert.All(launch.DriveWriteProtected, value => Assert.True(value));
        Assert.All(launch.DriveDiskPaths, Assert.Null);
        Assert.Equal("original.adf", current.DriveDiskPaths[0]); Assert.Equal(4, current.FloppyDriveCount);
    }

    [Fact]
    public void MissingSavedMediaAreRetainedForRepairRatherThanSilentlyBootingAnotherEnvironment()
    {
        var draft = new CopperScreenSettingsDraft { KickstartRomPath = Path.Combine(_root, "missing.rom") };
        draft.DriveDiskPaths[0] = Path.Combine(_root, "missing.adf");
        new CopperScreenRecentSession(_root).Save(draft, 37, false, AppContext.BaseDirectory);
        var restored = new CopperScreenRecentSession(_root).LoadMachine(AppContext.BaseDirectory)!;
        Assert.Equal(draft.DriveDiskPaths[0], restored.DriveDiskPaths[0]);
        Assert.Equal(draft.KickstartRomPath, restored.KickstartRomPath);
    }

    [Fact]
    public void WrongRomCannotCreateWorkspaceOrAlterAnExistingWorkspace()
    {
        var path = Path.Combine(_root, "wrong.rom"); File.WriteAllBytes(path, new byte[524288]);
        var draft = new CopperScreenSettingsDraft { KickstartSource = CopperScreenKickstartSource.CopperStart, KickstartRomPath = path };
        Assert.Throws<InvalidOperationException>(() => CopperScreenLaunchWorkflow.Workspace(draft, _root, "personal", "missing.adf"));
        Assert.Empty(CopperScreenLaunchWorkflow.Workspaces(_root));
        Assert.False(Directory.Exists(Path.Combine(_root, "CopperOS")));
    }

    [Fact]
    public void ProductionLauncherDoesNotDiscoverDiagnosticProofMedia()
    {
        var proof = Path.Combine(_root, "Diagnostics", "GuestProof"); Directory.CreateDirectory(proof);
        File.WriteAllText(Path.Combine(proof, "CopperStart-DiskBoot.rom"), "unadmitted");
        File.WriteAllText(Path.Combine(proof, "System.adf"), "unadmitted");
        Assert.Null(CopperScreenLaunchWorkflow.DiagnosticRom(_root));
        Assert.Null(CopperScreenLaunchWorkflow.DiagnosticSeed(_root));
    }

    [Fact]
    public Task InvalidArchiveImportKeepsThePreviousDiskAndGivesRecoveryGuidance() => UiAsync(async () =>
    {
        var disk = Path.Combine(_root, "user-owned.adf"); File.WriteAllBytes(disk, new byte[901120]);
        var invalid = Path.Combine(_root, "damaged.zip"); File.WriteAllText(invalid, "broken archive");
        var window = new MainWindow([], new CopperScreenRecentSession(_root)); window.Show();
        try
        {
            await window.InsertDriveDiskAsync(0, disk);
            Assert.Equal("user-owned.adf", Named<TextBlock>(window, "Startup disk selection").Text);
            await window.InsertDriveDiskAsync(0, invalid);
            Assert.Equal("user-owned.adf", Named<TextBlock>(window, "Startup disk selection").Text);
            Assert.Contains("previous selection is unchanged", Named<TextBlock>(window, "Startup guidance").Text);
            Assert.Equal(new byte[901120], File.ReadAllBytes(disk));
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task CorruptRomArchiveInSavedProfileKeepsTheLauncherAvailableForRepair() => Ui(() =>
    {
        var zip = Path.Combine(_root, "damaged-rom.zip"); File.WriteAllText(zip, "broken archive");
        new CopperScreenRecentSession(_root).Save(new() { KickstartRomPath = zip }, 37, true, AppContext.BaseDirectory);
        var window = new MainWindow([], new CopperScreenRecentSession(_root)); window.Show();
        try
        {
            Assert.False(Button(window, "Start Amiga").IsEnabled);
            Assert.False(string.IsNullOrWhiteSpace(Named<TextBlock>(window, "Startup guidance").Text));
            Named<ComboBox>(window, "Boot method").SelectedIndex = 2;
            Assert.False(Button(window, "Start CopperStart").IsEnabled);
            Assert.Equal("broken archive", File.ReadAllText(zip));
        }
        finally { window.Close(); }
    });

    [LaunchAssetsFact]
    public Task DamagedWorkspaceResumeReportsRecoveryAndPreservesItsFiles() => Ui(() =>
    {
        var draft = CopperScreenLaunchWorkflow.CopperStart(new());
        draft.KickstartRomPath = Environment.GetEnvironmentVariable("COPPERSCREEN_LAUNCH_ROM");
        draft = CopperScreenLaunchWorkflow.Workspace(draft, _root, "damaged", Environment.GetEnvironmentVariable("COPPERSCREEN_LAUNCH_SEED"));
        var image = draft.DriveDiskPaths[0]!;
        var bytes = File.ReadAllBytes(image); bytes[880 * 512 + 20] ^= 1; File.WriteAllBytes(image, bytes);
        new CopperScreenRecentSession(_root).Save(draft, 37, true, AppContext.BaseDirectory);
        var window = new MainWindow([], new CopperScreenRecentSession(_root)); window.Show();
        try
        {
            Click(Button(window, "Resume workspace"));
            Assert.Contains("repair", Named<TextBlock>(window, "Startup guidance").Text);
            Assert.False(Button(window, "Start CopperOS").IsEnabled);
            Assert.Equal(bytes, File.ReadAllBytes(image));
        }
        finally { window.Close(); }
    });

    [LaunchAssetsFact]
    public Task QualifiedWorkspaceLaunchPauseRestartStopAndResumeUseTheStandardWindow() => UiAsync(async () =>
    {
        var rom = Environment.GetEnvironmentVariable("COPPERSCREEN_LAUNCH_ROM")!;
        var seed = Environment.GetEnvironmentVariable("COPPERSCREEN_LAUNCH_SEED")!;
        var original = File.ReadAllBytes(seed);
        var draft = CopperScreenLaunchWorkflow.CopperStart(new() { Input = CopperScreenInputOptions.Default.WithPortAssignment(2, "wasd-joystick") }); draft.KickstartRomPath = rom;
        draft = CopperScreenLaunchWorkflow.Workspace(draft, _root, "personal", seed);
        Assert.Equal("none", draft.Input.Port2ProfileId);
        var disk = draft.DriveDiskPaths[0]!;
        CopperScreenProfileStore.Save(draft, AppContext.BaseDirectory, _root);
        new CopperScreenRecentSession(_root).Save(draft, 37, true, AppContext.BaseDirectory);
        var window = new MainWindow([], new CopperScreenRecentSession(_root));
        window.Show();
        try
        {
            Assert.Equal(3, Named<ComboBox>(window, "Boot method").SelectedIndex);
            Assert.True(Button(window, "Start CopperOS").IsEnabled);
            Capture(window, "copperos-ready");
            Click(Button(window, "Start CopperOS"));
            await Until(() => Named<Button>(window, "Pause").IsEnabled);
            Click(Named<Button>(window, "Pause"));
            await Until(() => window.GetLogicalDescendants().OfType<Button>().Any(b => AutomationProperties.GetName(b) == "Resume"));
            Assert.False(Named<Button>(window, "Browse…").IsEnabled);
            Click(Named<Button>(window, "Restart"));
            await Until(() => Named<Button>(window, "Restart").IsEnabled);
            Assert.True(Named<Button>(window, "Resume").IsEnabled); // A paused session stays paused after restart.
            Click(Named<Button>(window, "Stop"));
            await Until(() => window.GetLogicalDescendants().OfType<Button>()
                .Any(b => b.Content is string label && label == "Start CopperOS" && b.IsEnabled && b.IsEffectivelyVisible));
            Click(Button(window, "Resume workspace"));
            Assert.True(Button(window, "Start CopperOS").IsEnabled);
            Assert.Equal(disk, new CopperScreenRecentSession(_root).LoadMachine(AppContext.BaseDirectory)!.DriveDiskPaths[0]);
            Assert.Equal(original, File.ReadAllBytes(seed));
            using var lease = CopperOsWorkspace.Open(disk); // Stop released the runtime's ownership.
        }
        finally
        {
            await Until(() => Named<Button>(window, "Stop").IsEnabled || !Named<Button>(window, "Stop").IsEffectivelyVisible);
            window.Close();
            await Until(() => !window.IsVisible);
        }
    });

    [LaunchAssetsFact]
    public Task PreviousCheckpointRecoveryRequiresConfirmationAndPreservesTheDamagedImage() => UiAsync(async () =>
    {
        var draft = CopperScreenLaunchWorkflow.CopperStart(new());
        draft.KickstartRomPath = Environment.GetEnvironmentVariable("COPPERSCREEN_LAUNCH_ROM");
        draft = CopperScreenLaunchWorkflow.Workspace(draft, _root, "recover", Environment.GetEnvironmentVariable("COPPERSCREEN_LAUNCH_SEED"));
        var image = draft.DriveDiskPaths[0]!; var original = File.ReadAllBytes(image);
        using (var owner = CopperOsWorkspace.Open(image)) owner.Save(original);
        var damaged = (byte[])original.Clone(); damaged[880 * 512 + 20] ^= 1; File.WriteAllBytes(image, damaged);
        new CopperScreenRecentSession(_root).Save(draft, 37, true, AppContext.BaseDirectory);
        var window = new MainWindow([], new CopperScreenRecentSession(_root)); window.Show();
        try
        {
            Assert.NotNull(Button(window, "Import workspace backup…"));
            Assert.NotNull(Button(window, "Export saved workspace…"));
            Assert.NotNull(Button(window, "Export previous checkpoint…"));
            window.GetLogicalDescendants().OfType<Expander>().Single(e => e.Header is "Back up or recover a workspace").IsExpanded = true;
            window.UpdateLayout(); Button(window, "Recover previous checkpoint…").BringIntoView();
            await Task.Delay(50);
            Assert.True(Button(window, "Recover previous checkpoint…").IsEffectivelyVisible);
            Capture(window, "workspace-recovery");
            Click(Button(window, "Recover previous checkpoint…"));
            await Until(() => window.OwnedWindows.Any(w => w.Title == "Recover CopperOS checkpoint"));
            var dialog = window.OwnedWindows.Single(w => w.Title == "Recover CopperOS checkpoint");
            Capture(dialog, "checkpoint-confirmation");
            Click(dialog.GetLogicalDescendants().OfType<Button>().Single(b => b.Content is "Cancel"));
            Assert.Equal(damaged, File.ReadAllBytes(image));
            Click(Button(window, "Recover previous checkpoint…"));
            await Until(() => window.OwnedWindows.Any(w => w.Title == "Recover CopperOS checkpoint"));
            dialog = window.OwnedWindows.Single(w => w.Title == "Recover CopperOS checkpoint");
            Click(dialog.GetLogicalDescendants().OfType<Button>().Single(b => b.Content is "Recover checkpoint"));
            await Until(() => Named<TextBlock>(window, "Startup guidance").Text?.StartsWith("Checkpoint recovered.") == true);
            Assert.Equal(original, File.ReadAllBytes(image));
            Assert.Equal(damaged, File.ReadAllBytes(Assert.Single(Directory.GetFiles(Path.GetDirectoryName(image)!, "Before-recovery-*.adf"))));
            Assert.Equal(original, File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(image)!, "Previous.adf")));
            Click(Button(window, "Resume workspace")); Assert.True(Button(window, "Start CopperOS").IsEnabled);
            Capture(window, "workspace-recovered");
        }
        finally { window.Close(); }
    });
}

public sealed class LaunchAssetsFactAttribute : FactAttribute
{
    public LaunchAssetsFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COPPERSCREEN_LAUNCH_ROM")) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COPPERSCREEN_LAUNCH_SEED")))
            Skip = "Qualified CopperStart/CopperOS pair unavailable; native desktop launch coverage unavailable.";
    }
}
