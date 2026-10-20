using CopperScreen;

namespace CopperScreen.Tests;

public sealed class RuntimeAudioPacingTests
{
    private static int Reserve => (int)typeof(CopperScreenRuntime).GetField("AudioOutputBufferCount",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.GetRawConstantValue()!;
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RunningCommandPublicationCannotHoldPcmRefill(bool lockAfterCopy)
    {
        using var sink = new ControlledSink();
        using var copied = new ManualResetEventSlim();
        using var proceed = new ManualResetEventSlim();
        var session = new ControlledSession(50);
        using var runtime = CopperScreenRuntime.CreateForTests(session, sink);
        var gate = typeof(CopperScreenRuntime).GetField("_presentationSync",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(runtime)!;
        try
        {
            runtime.Start();
            Assert.True(sink.Full.Wait(TimeSpan.FromSeconds(5)));
            long seen = 0;
            while (runtime.TryAcquireNextPresentationFrame(ref seen) is { } lease) lease.Dispose();
            Task<CopperScreenCommandResult>? command = null;
            if (lockAfterCopy)
            {
                var captures = 0;
                session.BeforeCapture = () =>
                {
                    if (Interlocked.Increment(ref captures) != 2) return;
                    copied.Set(); Assert.True(proceed.Wait(TimeSpan.FromSeconds(5)));
                };
                command = runtime.PulsePrimaryFireAsync();
                Assert.True(copied.Wait(TimeSpan.FromSeconds(5)));
            }
            lock (gate)
            {
                command ??= runtime.PulsePrimaryFireAsync();
                Assert.True(SpinWait.SpinUntil(() => command.IsCompleted, TimeSpan.FromSeconds(5)));
                Assert.True(command.IsCompletedSuccessfully);
                sink.Full.Reset(); proceed.Set(); sink.ConsumeFourBuffers();
                Assert.True(sink.Full.Wait(TimeSpan.FromSeconds(3)), "Running command publication held PCM behind the UI lock.");
                Assert.Equal(Reserve + 4, session.Fields);
            }
            runtime.Stop();
            Assert.Equal(Enumerable.Range(5, 4).SelectMany(n => Enumerable.Repeat((float)n, 4)), sink.ConsumeFourBuffers());
        }
        finally { proceed.Set(); runtime.Stop(); }
    }
    [Fact]
    public void BlockedUiDispatcherCannotStrandPcmProducer()
    {
        var dispatcher = Avalonia.Threading.Dispatcher.CurrentDispatcher;
        var gate = typeof(Avalonia.Threading.Dispatcher).GetProperty("InstanceLock",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(dispatcher)!;
        Action post = () => dispatcher.Post(() => { }, Avalonia.Threading.DispatcherPriority.Background);
        var type = typeof(CopperScreenRuntime).Assembly.GetType("CopperScreen.CopperScreenFrameNotification");
        object? notifier = null;
        Action signal = post; // Retained pre-fix package posts directly on the PCM owner.
        if (type != null)
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
            notifier = type.GetConstructor(flags, null, [typeof(Action)], null)!.Invoke([post]);
            signal = type.GetMethod("Signal", flags)!.CreateDelegate<Action>(notifier);
        }
        using var sink = new ControlledSink();
        var session = new ControlledSession(50);
        using var runtime = CopperScreenRuntime.CreateForTests(session, sink);
        runtime.FramePublished += signal;
        try
        {
            lock (gate)
            {
                runtime.Start();
                Assert.True(sink.Full.Wait(TimeSpan.FromSeconds(3)), "UI dispatch held the PCM producer before its reserve could fill.");
                Assert.Equal(Reserve, session.Fields);
            }
            runtime.Stop();
            Assert.Equal(Enumerable.Range(1, 4).SelectMany(n => Enumerable.Repeat((float)n, 4)), sink.ConsumeFourBuffers());
        }
        finally { runtime.Stop(); (notifier as IDisposable)?.Dispose(); dispatcher.RunJobs(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UiPresentationLockCannotHoldAudioProducer(bool lockDuringPublication)
    {
        using var sink = new ControlledSink();
        using var rendering = new ManualResetEventSlim();
        using var proceed = new ManualResetEventSlim();
        var session = new ControlledSession(50);
        if (lockDuringPublication) session.BeforeRender = () => { rendering.Set(); Assert.True(proceed.Wait(TimeSpan.FromSeconds(5))); };
        using var runtime = CopperScreenRuntime.CreateForTests(session, sink);
        var gate = typeof(CopperScreenRuntime).GetField("_presentationSync",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(runtime)!;
        try
        {
            runtime.Start();
            if (lockDuringPublication) Assert.True(rendering.Wait(TimeSpan.FromSeconds(5)));
            else Assert.True(sink.Full.Wait(TimeSpan.FromSeconds(5)));
            lock (gate)
            {
                sink.Full.Reset();
                if (lockDuringPublication) proceed.Set();
                else sink.ConsumeFourBuffers();
                Assert.True(sink.Full.Wait(TimeSpan.FromSeconds(3)), "A UI-owned presentation lock stopped PCM refill.");
                Assert.Equal(lockDuringPublication ? Reserve : Reserve + 4, session.Fields);
                Assert.Equal(session.Fields, session.Framebuffer[0]); // Complete pixel work still runs.
            }
            runtime.Stop();
            var first = lockDuringPublication ? 1 : 5;
            Assert.Equal(Enumerable.Range(first, 4).SelectMany(n => Enumerable.Repeat((float)n, 4)), sink.ConsumeFourBuffers());
        }
        finally { proceed.Set(); runtime.Stop(); }
    }
#if COPPERSCREEN_INSTALLED_TESTS
    [Fact]
    public async Task TimelineExportLeavesOwnerAvailableForNormalShutdownSave()
    {
        using var sink = new ControlledSink();
        using var runtime = CopperScreenRuntime.CreateForTests(new ControlledSession(50), sink);
        var path = Path.Combine(Path.GetTempPath(), "copperscreen-timeline-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            runtime.Start();
            Assert.True(sink.Full.Wait(TimeSpan.FromSeconds(5)));
            Assert.True((await runtime.TogglePausedAsync().WaitAsync(TimeSpan.FromSeconds(5))).Success);
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var export = typeof(CopperScreenRuntime).GetMethod("DiagnosticWriteAudioTimelineAsync", flags);
            if (export != null) await ((Task)export.Invoke(runtime, [path])!).WaitAsync(TimeSpan.FromSeconds(5));
            else typeof(CopperScreenRuntime).GetMethod("DiagnosticWriteAudioTimeline", flags)!.Invoke(runtime, [path]);
            Assert.True(File.Exists(path));
            // The normal close path still requires commands on this owner after export.
            Assert.True((await runtime.TogglePausedAsync().WaitAsync(TimeSpan.FromSeconds(3))).Success);
            Assert.Null(runtime.CurrentState.FaultMessage);
        }
        finally { runtime.Stop(); File.Delete(path); }
    }
#endif
    [Fact]
    public void NewNativeSinkReceivesItsCompleteReserveBeforePlaybackStarts()
    {
        using var sink = new ControlledSink { ClockRunning = false, Prefilling = true };
        var session = new ControlledSession(50);
        using var runtime = CopperScreenRuntime.CreateForTests(session, sink);
        runtime.Start();
        Assert.True(sink.Primed.Wait(TimeSpan.FromSeconds(5)), "A ready native device could not prime before its first callback.");
        runtime.Stop();
        Assert.Equal(Enumerable.Range(1, Reserve).SelectMany(n => Enumerable.Repeat((float)n, 4)), sink.PrimedSamples);
        Assert.Equal(0, runtime.CurrentState.AudioSubmitFailures);
        Assert.Null(runtime.CurrentState.FaultMessage);
    }

    [Fact]
    public async Task StoppedSinkDemandIsServicedWithoutBlockingCommandsOrChangingGuestOrder()
    {
        using var sink = new ControlledSink();
        var session = new ControlledSession();
        using var runtime = CopperScreenRuntime.CreateForTests(session, sink);
        runtime.Start();
        Assert.True(sink.Full.Wait(TimeSpan.FromSeconds(5)));
        Assert.True((await runtime.TogglePausedAsync().WaitAsync(TimeSpan.FromSeconds(5))).Success);
        Assert.True((await runtime.TogglePausedAsync().WaitAsync(TimeSpan.FromSeconds(5))).Success);
        sink.RequestRecovery(); // No callback/event arrives to wake the full sink.
        Assert.True(sink.RecoveryHandled.Wait(TimeSpan.FromSeconds(5)), "A full stopped sink stranded the runtime without servicing recovery.");
        Assert.True(sink.Full.Wait(TimeSpan.FromSeconds(5)));
        runtime.Stop();
        Assert.NotEqual(Environment.CurrentManagedThreadId, sink.RecoveryThreadId);
        Assert.Equal(new float[16], sink.RecoveredSamples); // Pause retired all pre-pause PCM.
        Assert.Equal(2 * Reserve, session.Fields);
        Assert.Equal(Enumerable.Range(Reserve + 1, 4).SelectMany(n => Enumerable.Repeat((float)n, 4)), sink.ConsumeFourBuffers());
        Assert.Equal(0, runtime.CurrentState.AudioSubmitFailures);
        runtime.Stop();
        Assert.Null(runtime.CurrentState.FaultMessage);
    }

    [Fact]
    public async Task SinkConsumptionRefillsTheExistingReserveWithoutAVideoDeadline()
    {
        // Artificial slow video clock makes an independent video gate observable
        // without a tight wall-time performance assertion. The sink owns demand.
        using var sink = new ControlledSink();
        var session = new ControlledSession();
        using var runtime = CopperScreenRuntime.CreateForTests(session, sink);
        runtime.Start();
        Assert.True(sink.Full.Wait(TimeSpan.FromSeconds(5)), "Initial audio reserve never filled.");
        Assert.Equal(Reserve, sink.QueuedBufferCount);
        Assert.Equal(Reserve, session.Fields);

        var received = new List<float>();
        for (var i = 0; i < 4; i++)
        {
            sink.Full.Reset();
            received.AddRange(sink.ConsumeFourBuffers());
            Assert.True(sink.Full.Wait(TimeSpan.FromSeconds(5)), "Sink demand was gated by the video clock.");
            Assert.Equal(Reserve, sink.QueuedBufferCount);
        }
        // Complete generated PCM stays in sequence; no repeated/stretched samples.
        Assert.Equal(Enumerable.Range(1, 16).SelectMany(n => Enumerable.Repeat((float)n, 4)), received);
        Assert.Equal(Reserve + 16, session.Fields);
        Assert.Equal(0, runtime.CurrentState.AudioSubmitFailures);
        Assert.True((await runtime.TogglePausedAsync().WaitAsync(TimeSpan.FromSeconds(5))).Success);
        runtime.Stop(); // A full audio sink must not prevent command/stop wakeup.
        Assert.Null(runtime.CurrentState.FaultMessage);
    }

    [Fact]
    public async Task UnavailableAudioClockDoesNotFreezeGuestAndRecoveryDropsStaleAudio()
    {
        using var sink = new ControlledSink();
        var session = new ControlledSession(50);
        using var runtime = CopperScreenRuntime.CreateForTests(session, sink);
        runtime.Start();
        Assert.True(sink.Full.Wait(TimeSpan.FromSeconds(5)));
        sink.ClockRunning = false; // Full queue; device cannot deliver any callback.
        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref session.Fields) >= Reserve + 4, TimeSpan.FromSeconds(5)),
            "An absent audio endpoint froze the guest behind its full PCM queue.");
        Assert.True((await runtime.TogglePausedAsync().WaitAsync(TimeSpan.FromSeconds(5))).Success);
        var pausedAt = Volatile.Read(ref session.Fields);
        await Task.Delay(100);
        Assert.Equal(pausedAt, Volatile.Read(ref session.Fields));
        Assert.Equal(0, runtime.CurrentState.AudioSubmitFailures);
        sink.Full.Reset();
        // A returning consumer releases discarded pre-outage slots; no stale PCM plays.
        Assert.Equal(new float[16], sink.ConsumeFourBuffers());
        sink.ClockRunning = true;
        Assert.True((await runtime.TogglePausedAsync().WaitAsync(TimeSpan.FromSeconds(5))).Success);
        Assert.True(sink.Full.Wait(TimeSpan.FromSeconds(5)));
        Assert.All(sink.ConsumeFourBuffers(), sample => Assert.True(sample > pausedAt));
        runtime.Stop();
        Assert.Null(runtime.CurrentState.FaultMessage);
    }
    private sealed class ControlledSink : ICopperScreenAudioOutput
    {
        private readonly MiniaudioSampleQueue _queue = new(4, Reserve);
        private readonly AutoResetEvent _available = new(false);
        public readonly ManualResetEventSlim Full = new();
        public readonly ManualResetEventSlim RecoveryHandled = new();
        public readonly ManualResetEventSlim Primed = new();
        public bool Prefilling;
        public bool NeedsPrefill => Prefilling;
        public float[]? PrimedSamples;
        private int _recoveryRequested;
        public volatile bool ClockRunning = true;
        public bool IsClockRunning => ClockRunning;
        public void DiscardQueuedSamples() => _queue.DiscardQueuedSamples();
        public int RecoveryThreadId;
        public float[]? RecoveredSamples;
        public void RequestRecovery() { Full.Reset(); Volatile.Write(ref _recoveryRequested, 1); }
        public void RecoverIfStalled()
        {
            if (Prefilling && _queue.QueuedBufferCount == Reserve)
            {
                PrimedSamples = new float[Reserve * 4];
                _queue.Dequeue(PrimedSamples);
                Prefilling = false;
                ClockRunning = true;
                Primed.Set();
            }
            if (Interlocked.Exchange(ref _recoveryRequested, 0) == 0) return;
            RecoveryThreadId = Environment.CurrentManagedThreadId;
            RecoveredSamples = ConsumeFourBuffers();
            RecoveryHandled.Set();
        }
        public WaitHandle BufferAvailable => _available;
        public int QueuedBufferCount => _queue.QueuedBufferCount;
        public bool Submit(ReadOnlySpan<float> samples)
        {
            var accepted = _queue.TryEnqueue(samples);
            if (QueuedBufferCount == Reserve) Full.Set();
            return accepted;
        }
        public float[] ConsumeFourBuffers()
        {
            var samples = new float[16];
            _queue.Dequeue(samples);
            _available.Set();
            return samples;
        }
        public void Dispose() { _available.Dispose(); Full.Dispose(); RecoveryHandled.Dispose(); Primed.Dispose(); }
    }

    private sealed class ControlledSession(double videoHz = 1d / 60) : ICopperScreenSession
    {
        public int Fields;
        public Action? BeforeRender;
        public Action? BeforeCapture;
        public int Width => 1;
        public int Height => 1;
        public int[] Framebuffer { get; } = new int[1];
        public double VideoVBlankHz => videoHz;
        public CopperScreenPresentationGeometry PresentationGeometry => default;
        public CopperScreenEmulatorFrameTiming LastFrameTiming => default;
        public bool IsInterlaced => false;
        public int CompletedInterlaceField => 0;
        public string ProfileName => "Controlled audio pacing";
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
        public void RenderNextFrame(int[] destination) { BeforeRender?.Invoke(); Fields++; destination[0] = Fields; }
        public int RenderAudio(Span<float> destination, int sampleRate, int channels) { destination.Fill(Fields); return 2; }
        public void CaptureDriveStates(Span<CopperScreenDriveState> destination) { BeforeCapture?.Invoke(); destination.Clear(); }
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
        public void Dispose() { }
    }
}
