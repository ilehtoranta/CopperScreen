using System.Reflection;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Platform.Storage;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed partial class LaunchWorkflowTests
{
    [Theory]
    [InlineData(false, false, "Cancel")]
    [InlineData(true, true, "Cancel")]
    [InlineData(false, true, "Discard changes and eject")]
    [InlineData(true, false, "Discard changes and eject")]
    [InlineData(false, true, "Save ADF and eject…")]
    [InlineData(true, false, "Save ADF and eject…")]
    [InlineData(false, false, "Cancel save")]
    [InlineData(true, true, "Fail save")]
    public Task UnsavedFloppyEjectHonorsTheDecisionAndPreservesPause(bool paused, bool settingsOpen, string choice) => UiAsync(async () =>
    {
        var original = Path.Combine(_root, "original.adf"); File.WriteAllText(original, "original");
        var saved = choice == "Fail save" ? Path.Combine(_root, "absent", "saved.adf") : Path.Combine(_root, "saved.adf");
        using var session = new EjectSession(original, dirty: true, paused);
        using var runtime = CopperScreenRuntime.CreateForTests(session);
        var window = new MainWindow([], new CopperScreenRecentSession(_root)); window.Show();
        try
        {
            var draft = MenuDraft(window); draft.DriveDiskPaths[0] = original; SetMenuDraft(window, draft);
            SetPrivate(window, "_runtime", runtime); SetPrivate(window, "_latestState", runtime.CurrentState);
            runtime.Start();
            if (settingsOpen) { Click(Button(window, "Configure Amiga…")); SettingsCall(window, "SelectSettingsPage", 3); }
            var owner = settingsOpen ? SettingsField<Window>(window, "_settingsWindow") : window;
            SetPicker(owner, choice == "Cancel save" ? null : saved);
            if (settingsOpen)
            {
                SettingsCall(window, "UpdateDriveSettingsEnabled");
                owner.UpdateLayout();
                Assert.True(SettingsNamed<TextBlock>(owner, "DF0 unsaved changes").IsEffectivelyVisible);
            }
            var eject = (Task)typeof(MainWindow).GetMethod("EjectDriveDiskAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [0])!;
            await Until(() => owner.OwnedWindows.Any(w => w.Title == "Unsaved changes in DF0"));
            var dialog = owner.OwnedWindows.Single(w => w.Title == "Unsaved changes in DF0");
            Assert.True(runtime.CurrentState.IsPaused);
            Assert.True(runtime.CurrentState.Drives[0].HasUnsavedChanges);
            // Repeated eject requests cannot open a second decision dialog.
            await (Task)typeof(MainWindow).GetMethod("EjectDriveDiskAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [0])!;
            Assert.Single(owner.OwnedWindows);
            var label = choice is "Cancel save" or "Fail save" ? "Save ADF and eject…" : choice;
            Click(dialog.GetLogicalDescendants().OfType<Button>().Single(b => b.Content as string == label));
            await eject;
            var ejected = choice is "Discard changes and eject" or "Save ADF and eject…";
            Assert.Equal(!ejected, runtime.CurrentState.Drives[0].HasDisk);
            Assert.Equal(!ejected, runtime.CurrentState.Drives[0].HasUnsavedChanges);
            Assert.Equal(paused, runtime.CurrentState.IsPaused);
            Assert.Equal("original", File.ReadAllText(original));
            if (choice == "Save ADF and eject…") Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(saved));
            else Assert.False(File.Exists(saved));
            Assert.Equal(ejected ? null : original, MenuDraft(window).DriveDiskPaths[0]);
            Assert.Equal(ejected ? "" : original, SettingsField<TextBox[]>(window, "_drivePathBoxes")[0].Text);
            Assert.Equal(ejected ? "" : original, SettingsField<TextBox>(window, "_setupDiskBox").Text);
        }
        finally
        {
            SetPrivate(window, "_runtime", null);
            window.Close();
        }
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task CleanFloppyEjectNeedsNoPromptAndClearsTheRememberedDisk(bool settingsOpen) => UiAsync(async () =>
    {
        var disk = Path.Combine(_root, "clean.adf");
        using var session = new EjectSession(disk, dirty: false, paused: false);
        using var runtime = CopperScreenRuntime.CreateForTests(session);
        var window = new MainWindow([], new CopperScreenRecentSession(_root)); window.Show();
        try
        {
            var draft = MenuDraft(window); draft.DriveDiskPaths[0] = disk; SetMenuDraft(window, draft);
            SetPrivate(window, "_runtime", runtime); runtime.Start();
            if (settingsOpen) Click(Button(window, "Configure Amiga…"));
            await (Task)typeof(MainWindow).GetMethod("EjectDriveDiskAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [0])!;
            Assert.False(runtime.CurrentState.Drives[0].HasDisk);
            Assert.False(runtime.CurrentState.IsPaused);
            Assert.DoesNotContain(window.OwnedWindows, w => w.Title == "Unsaved changes in DF0");
            Assert.Null(MenuDraft(window).DriveDiskPaths[0]);
            Assert.Null(new CopperScreenRecentSession(_root).LoadMachine(AppContext.BaseDirectory)!.DriveDiskPaths[0]);
        }
        finally { SetPrivate(window, "_runtime", null); window.Close(); }
    });

    [Fact]
    public Task BlankDiskCreationIsExplicitAndDoesNotTouchExistingFiles() => UiAsync(async () =>
    {
        var window = new MainWindow([], new CopperScreenRecentSession(_root)); window.Show();
        try
        {
            Click(Button(window, "Configure Amiga…")); SettingsCall(window, "SelectSettingsPage", 3);
            var settings = SettingsField<Window>(window, "_settingsWindow");
            var tabs = SettingsNamed<TabControl>(settings, "Storage tabs");
            Assert.Equal(new[] { "Floppy drives", "Hard disks & folders", "Backups" }, tabs.Items.OfType<TabItem>().Select(t => t.Header));
            tabs.SelectedIndex = 1;
            var create = (Task)typeof(MainWindow).GetMethod("CreateHardDiskImageAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null)!;
            var dialog = settings.OwnedWindows.Single(w => w.Title == "Create disk image");
            var path = SettingsNamed<TextBox>(dialog, "New image file");
            var size = SettingsNamed<TextBox>(dialog, "Capacity (MiB)");
            var filesystem = SettingsNamed<ComboBox>(dialog, "Filesystem");
            Assert.Equal("FFS", filesystem.SelectedItem);
            var firstCylinder = SettingsNamed<TextBox>(dialog, "New first cylinder");
            Assert.True(firstCylinder.GetLogicalAncestors().OfType<Control>().All(c => c.IsVisible));
            filesystem.SelectedItem = "CPX (experimental)";
            var experimental = dialog.GetLogicalDescendants().OfType<CheckBox>().Single(c => c.Content is "I want to create an experimental CPX volume");
            Assert.True(experimental.IsVisible);
            experimental.IsChecked = true;
            filesystem.SelectedItem = "Unformatted";
            Assert.False(experimental.IsVisible);
            Assert.False(experimental.IsChecked);
            Assert.Contains(firstCylinder.GetLogicalAncestors().OfType<Control>(), c => !c.IsVisible);
            // Hidden partition fields cannot prevent creating an unformatted image.
            firstCylinder.Text = "unused geometry";
            var add = dialog.GetLogicalDescendants().OfType<Button>().Single(b => b.Content is "Add blank image");
            var existing = Path.Combine(_root, "existing.hdf"); File.WriteAllText(existing, "keep");
            path.Text = existing; Click(add);
            Assert.True(dialog.IsVisible); Assert.Empty(MenuDraft(window).HardDrives);
            var destination = Path.Combine(_root, "blank.hdf"); path.Text = destination;
            foreach (var invalid in new[] { "0", "-1", "1.5", long.MaxValue.ToString() })
            {
                size.Text = invalid; Click(add);
                Assert.True(dialog.IsVisible); Assert.Empty(MenuDraft(window).HardDrives);
            }
            size.Text = "64"; Click(add); await create;
            var drive = Assert.Single(MenuDraft(window).HardDrives);
            Assert.Equal(64L * 1024 * 1024, drive.CreateSizeBytes);
            Assert.True(drive.ReadOnly); Assert.Equal(destination, drive.Path);
            Assert.False(File.Exists(destination)); Assert.Equal("keep", File.ReadAllText(existing));
            var controls = SettingsField<StackPanel>(window, "_hardfileRows").GetLogicalDescendants().ToArray();
            Assert.False(controls.OfType<Expander>().Single(e => e.Header is "Disk layout and partition settings").IsExpanded);
            Assert.False(controls.OfType<Expander>().Single(e => e.Header is "Host storage allocation").IsExpanded);
            tabs.SelectedIndex = 0; tabs.SelectedIndex = 1;
            Assert.Equal(drive, Assert.Single(MenuDraft(window).HardDrives));
        }
        finally { window.Close(); }
    });

    private static void SetPrivate(object target, string field, object? value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);

    // Use Avalonia's existing storage boundary, without adding picker hooks to the product.
    private static void SetPicker(TopLevel owner, string? path)
    {
        IStorageFile? file = null;
        if (path != null)
        {
            file = DispatchProxy.Create<IStorageFile, PickerProxy>();
            ((PickerProxy)file).Handle = method => method.Name == "get_Path" ? new Uri(path) : throw new NotSupportedException(method.Name);
        }
        var provider = DispatchProxy.Create<IStorageProvider, PickerProxy>();
        ((PickerProxy)provider).Handle = method => method.Name == "SaveFilePickerAsync" ? Task.FromResult(file) : throw new NotSupportedException(method.Name);
        typeof(TopLevel).GetField("_storageProvider", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(owner, provider);
    }

    public class PickerProxy : DispatchProxy
    {
        public Func<MethodInfo, object?> Handle = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Handle(method!);
    }

    private sealed class EjectSession(string path, bool dirty, bool paused) : ICopperScreenSession
    {
        private bool _hasDisk = true, _dirty = dirty;
        public int Width => 1;
        public int Height => 1;
        public int[] Framebuffer { get; } = new int[1];
        public double VideoVBlankHz => 50;
        public CopperScreenPresentationGeometry PresentationGeometry => default;
        public CopperScreenEmulatorFrameTiming LastFrameTiming => default;
        public bool IsInterlaced => false;
        public int CompletedInterlaceField => 0;
        public string ProfileName => "Eject test";
        public string DiskName => _hasDisk ? "disk.adf" : "None";
        public string? DiskPath => _hasDisk ? path : null;
        public string BaseDirectory => AppContext.BaseDirectory;
        public FloppyDriveAudioOptions FloppyDriveAudioOptions => default;
        public CopperScreenCpuState CpuState => default;
        public CopperScreenDebugSnapshot? DebugSnapshot => null;
        public string StatusText { get; private set; } = "";
        public bool IsPaused { get; private set; } = paused;
        public bool IsWorkbenchHandoffPending => false;
        public bool IsDiskSwapPending => false;
        public bool IsPrimaryFirePressed => false;
        public bool AudioFilterEnabled => false;
        public int AudioFramesPerAppFrame(int sampleRate) => 2;
        public void RenderNextFrame(int[] destination) { }
        public int RenderAudio(Span<float> destination, int sampleRate, int channels) => 0;
        public void CaptureDriveStates(Span<CopperScreenDriveState> destination)
        {
            destination.Clear();
            for (var i = 0; i < destination.Length; i++) destination[i] = new(i, i == 0, i == 0 && _hasDisk, DiskName, i == 0 ? DiskPath : null, 0, 0, false, false, false, false)
                { HasUnsavedChanges = i == 0 && _dirty, CanExportAdf = i == 0 && _hasDisk };
        }
        public byte[] ExportAdf(int drive) => [1, 2, 3];
        public void MarkAdfSaved(int drive) => _dirty = false;
        public bool EjectDisk(int drive) => !_dirty && DiscardAndEjectDisk(drive);
        public bool DiscardAndEjectDisk(int drive) { _hasDisk = _dirty = false; return true; }
        public void SetStatusText(string message) => StatusText = message;
        public void CaptureFatalException(Exception exception) => throw exception;
        public void Reset() { }
        public bool TogglePaused() => IsPaused = !IsPaused;
        public void PulsePrimaryFire(int frames) { }
        public void KeyDown(AmigaRawKey key) { }
        public void KeyUp(AmigaRawKey key) { }
        public void MoveMousePort(int x, int y) { }
        public void SetMousePortPosition(int x, int y) { }
        public void SetMousePresentationPosition(int x, int y) { }
        public void SetMouseButtons(bool primary, bool secondary) { }
        public void SetJoystickPort(bool up, bool down, bool left, bool right, bool primary, bool secondary) { }
        public void SetJoystickPort(int port, bool up, bool down, bool left, bool right, bool primary, bool secondary) { }
        public void SetInputOptions(CopperScreenInputOptions options) { }
        public void SetPresentationOptions(CopperScreenPresentationOptions options) { }
        public bool InsertLoadedDisk(string source, CopperScreenAdfImage disk, bool markChanged) => false;
        public bool InsertLoadedDisk(int drive, string source, CopperScreenAdfImage disk, bool markChanged) => false;
        public bool SetDriveWriteProtected(int drive, bool writeProtected) => false;
        public bool ConsumeCopperBenchRequest() => false;
        public bool LaunchCopperBenchPath(string source, out string message) { message = ""; return false; }
        public void QueueHostClipboardText(string text) { }
        public void QueueHostClipboardImage(ClipboardImage image) { }
        public bool TryTakeHostClipboardText(out string text) { text = ""; return false; }
        public bool TryTakeHostClipboardImage(out ClipboardImage? image) { image = null; return false; }
        public void Dispose() { }
    }
}
