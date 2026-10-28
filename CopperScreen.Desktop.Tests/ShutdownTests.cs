using System.Reflection;
using System.Runtime.ExceptionServices;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed partial class LaunchWorkflowTests
{
    [Fact]
    public Task SlowShutdownKeepsDispatcherAliveAndRejectsRepeatedClose() => UiAsync(async () =>
    {
        using var session = new ShutdownSession { BlockDispose = true };
        using var runtime = CopperScreenRuntime.CreateForTests(session);
        var window = new MainWindow([], new CopperScreenRecentSession(_root));
        window.Show();
        typeof(MainWindow).GetField("_runtime", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, runtime);
        runtime.Start();
        try
        {
            window.Close();
            await session.DisposeStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(window.IsVisible);
            Assert.Null(SettingsField<CopperScreenRuntime?>(window, "_runtime"));
            Assert.NotEqual(Environment.CurrentManagedThreadId, session.DisposeThread);
            var dispatched = false;
            Dispatcher.UIThread.Post(() => dispatched = true);
            await Until(() => dispatched);
            window.Close();
            Assert.True(window.IsVisible);
            Assert.Equal(1, session.Saves);
            runtime.ReleaseInteractiveInput([]); // A notification already in flight is harmless.
            Assert.False((await runtime.TogglePausedAsync()).Success);
        }
        finally
        {
            session.AllowDispose.Set();
            await Until(() => !window.IsVisible);
        }
        Assert.Equal(1, session.Disposals);
    });

    [Fact]
    public Task FailedCloseSaveCanKeepTheRunningSessionOpen() => UiAsync(async () =>
    {
        using var session = new ShutdownSession { FailSave = true };
        using var runtime = CopperScreenRuntime.CreateForTests(session);
        var window = new MainWindow([], new CopperScreenRecentSession(_root));
        window.Show();
        typeof(MainWindow).GetField("_runtime", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, runtime);
        runtime.Start();
        try
        {
            window.Close();
            await Until(() => window.OwnedWindows.Any(w => w.Title == "Storage could not be saved"));
            var dialog = window.OwnedWindows.Single(w => w.Title == "Storage could not be saved");
            Click(dialog.GetLogicalDescendants().OfType<Button>().Single(b => b.Content is "Keep open"));
            await Until(() => !SettingsField<bool>(window, "_checkingDiskChanges"));
            Assert.True(window.IsVisible);
            Assert.Same(runtime, SettingsField<CopperScreenRuntime>(window, "_runtime"));
            Assert.False(runtime.CurrentState.IsPaused);
            Assert.Equal(0, session.Disposals);
        }
        finally
        {
            session.FailSave = false;
            window.Close();
            await Until(() => !window.IsVisible);
        }
    });

    [Fact]
    public async Task RuntimeDisposalCompletesPendingCommandsAndAllowsLateFrameRelease()
    {
        using var session = new ShutdownSession();
        var runtime = CopperScreenRuntime.CreateForTests(session);
        long seen = -1;
        var lease = runtime.TryAcquireNextPresentationFrame(ref seen, force: true);
        Assert.NotNull(lease);
        var pending = runtime.TogglePausedAsync(); // Queued before the owner starts.
        runtime.Dispose();
        Assert.False((await pending.WaitAsync(TimeSpan.FromSeconds(2))).Success);
        runtime.KeyUp(AmigaRawKey.A);
        runtime.ReleaseInteractiveInput([]);
        Assert.False((await runtime.SavePersistentStorageAsync()).Success);
        lease.Dispose();
        runtime.Stop();
        runtime.Dispose();
        Assert.Equal(1, session.Disposals);
    }

    [Fact]
    public void ListingUnavailablePresetsDoesNotThrowExpectedExceptions()
    {
        var exceptions = new List<Exception>();
        var thread = Environment.CurrentManagedThreadId;
        void Capture(object? sender, FirstChanceExceptionEventArgs args)
        {
            if (Environment.CurrentManagedThreadId == thread && args.Exception is NotSupportedException or DirectoryNotFoundException)
                exceptions.Add(args.Exception);
        }
        IReadOnlyList<CopperScreenProfileSummary> profiles;
        AppDomain.CurrentDomain.FirstChanceException += Capture;
        try { profiles = CopperScreenProfileStore.ListProfiles(AppContext.BaseDirectory, _root); }
        finally { AppDomain.CurrentDomain.FirstChanceException -= Capture; }
        Assert.Contains(profiles, p => p.UnavailableReason != null);
        Assert.Empty(exceptions);
    }

    private sealed class ShutdownSession : ICopperScreenSession
    {
        public bool BlockDispose, FailSave;
        public int Saves, Disposals, DisposeThread;
        public TaskCompletionSource DisposeStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ManualResetEventSlim AllowDispose { get; } = new();
        public int Width => 1;
        public int Height => 1;
        public int[] Framebuffer { get; } = new int[1];
        public double VideoVBlankHz => 50;
        public CopperScreenPresentationGeometry PresentationGeometry => default;
        public CopperScreenEmulatorFrameTiming LastFrameTiming => default;
        public bool IsInterlaced => false;
        public int CompletedInterlaceField => 0;
        public string ProfileName => "Shutdown test";
        public string DiskName => "None";
        public string? DiskPath => null;
        public string BaseDirectory => AppContext.BaseDirectory;
        public FloppyDriveAudioOptions FloppyDriveAudioOptions => default;
        public CopperScreenCpuState CpuState => default;
        public CopperScreenDebugSnapshot? DebugSnapshot => null;
        public string StatusText => "";
        public bool IsPaused { get; private set; }
        public bool IsWorkbenchHandoffPending => false;
        public bool IsDiskSwapPending => false;
        public bool IsPrimaryFirePressed => false;
        public bool AudioFilterEnabled => false;
        public int AudioFramesPerAppFrame(int sampleRate) => 2;
        public void RenderNextFrame(int[] destination) { }
        public int RenderAudio(Span<float> destination, int sampleRate, int channels) => 0;
        public void CaptureDriveStates(Span<CopperScreenDriveState> destination) => destination.Clear();
        public void SavePersistentStorage() { Saves++; if (FailSave) throw new IOException("Test save failure"); }
        public void SetStatusText(string message) { }
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
        public bool InsertLoadedDisk(string path, CopperScreenAdfImage disk, bool markChanged) => false;
        public bool InsertLoadedDisk(int drive, string path, CopperScreenAdfImage disk, bool markChanged) => false;
        public bool SetDriveWriteProtected(int drive, bool writeProtected) => false;
        public bool ConsumeCopperBenchRequest() => false;
        public bool LaunchCopperBenchPath(string path, out string message) { message = ""; return false; }
        public void QueueHostClipboardText(string text) { }
        public void QueueHostClipboardImage(ClipboardImage image) { }
        public bool TryTakeHostClipboardText(out string text) { text = ""; return false; }
        public bool TryTakeHostClipboardImage(out ClipboardImage? image) { image = null; return false; }
        public void Dispose()
        {
            if (Disposals++ > 0) return;
            DisposeThread = Environment.CurrentManagedThreadId;
            DisposeStarted.TrySetResult();
            if (BlockDispose && !AllowDispose.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("UI blocked shutdown");
        }
    }
}
