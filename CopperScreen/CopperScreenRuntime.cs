using System.Collections.Concurrent;
using System.Diagnostics;

namespace CopperScreen;

internal interface ICopperScreenAudioOutput : IDisposable
{
	int QueuedBufferCount { get; }
	// Signaled by the consumer after freeing a slot. Commands use a separate wake.
	WaitHandle? BufferAvailable => null;
	// An unavailable output must not become the guest's stopped host clock.
	bool IsClockRunning => true;
	bool NeedsPrefill => false;
	// Called on the runtime owner when audio demand stops arriving.
	void RecoverIfStalled() { }
	void SetPaused(bool paused) { if (paused) DiscardQueuedSamples(); }

	bool Submit(ReadOnlySpan<float> samples);

	void DiscardQueuedSamples() { }
	void SetVolume(float volume) { }
}

internal readonly record struct CopperScreenCommandResult(bool Success, string Message, CopperScreenState State);

internal readonly record struct CopperScreenCpuState(
	uint ProgramCounter,
	uint LastInstructionProgramCounter,
	ushort StatusRegister);

internal readonly record struct CopperScreenDriveState(
	int Index,
	bool Connected,
	bool HasDisk,
	string DiskName,
	string? DiskPath,
	int Cylinder,
	int Head,
	bool MotorOn,
	bool Selected,
	bool WriteProtected,
	bool ActiveDma)
{
	public bool IsSwapPending { get; init; }
	public bool HasUnsavedChanges { get; init; }
    public CopperMod.Amiga.Lightweight.LightweightFloppyFormat Format { get; init; }
    public bool CanExportAdf { get; init; }
}

internal readonly record struct CopperScreenState(
	string ProfileName,
	string DiskName,
	string? DiskPath,
	CopperScreenCpuState Cpu,
	CopperScreenDriveState[] Drives,
	CopperScreenDebugSnapshot? DebugSnapshot,
	string StatusText,
	bool IsPaused,
	bool IsWorkbenchHandoffPending,
	bool IsDiskSwapPending,
	bool IsPrimaryFirePressed,
	bool AudioFilterEnabled,
	bool CopperBenchRequestPending,
	int FramesRendered,
	int QueuedAudioBuffers,
	long FrameNumber,
	long DroppedFrames,
	long PresentationSkippedFrames,
	long PresentationBufferDroppedFrames,
	long AudioSubmitFailures,
	int PresentationQueueDepth,
	int PresentationQueueCapacity,
	long PresentationQueueFullThrottleCount,
	double LastPublishFrameMilliseconds,
	double LastPublishCopyMilliseconds,
	double LastPresentationBufferReserveMilliseconds,
	double LastEmulationFrameMilliseconds,
	double LastCpuFrameMilliseconds,
	double LastHardwareFrameMilliseconds,
	double LastDisplayFrameMilliseconds,
	double LastAudioFrameMilliseconds,
	bool IsInterlaced,
	int InterlaceField)
{
	public string? FaultMessage { get; init; }
	public int FramebufferWidth { get; init; }
	public int FramebufferHeight { get; init; }
	public CopperScreenPresentationGeometry PresentationGeometry { get; init; }
}

internal sealed class CopperScreenFrameLease : IDisposable
{
	private CopperScreenRuntime? _owner;

	internal CopperScreenFrameLease(CopperScreenRuntime owner, int bufferIndex, int[] framebuffer, CopperScreenState state)
	{
		_owner = owner;
		BufferIndex = bufferIndex;
		Framebuffer = framebuffer;
		State = state;
	}

	public int[] Framebuffer { get; }

	public CopperScreenState State { get; }

	private int BufferIndex { get; }

	public void Dispose()
	{
		var owner = Interlocked.Exchange(ref _owner, null);
		owner?.ReleaseFrameLease(BufferIndex);
	}
}

internal sealed class CopperScreenRuntime : IDisposable
{
	private readonly int _audioSampleRate;
	private const int AudioChannels = 2;
	// A retained WASAPI catch-up burst exhausted eight blocks by 2,926 stereo
	// values while guest work stayed short. Ten blocks cover that measured demand
	// without dropping PCM; this adds about 40 ms at the qualified PAL field rate.
	private const int AudioOutputBufferCount = 10;
	private const int TargetQueuedAudioBuffers = AudioOutputBufferCount;
	private const int CriticalQueuedAudioBuffers = 2;
	private const int MaxFramesPerTick = 5;
	private const int TargetQueuedPresentationFrames = 1;
	private const int MaxQueuedPresentationFrames = 2;
	private double DisplayFrameMilliseconds => 1000.0 / _emulator.VideoVBlankHz;
	private readonly WaitHandle[]? _audioWaitHandles;
	private readonly ICopperScreenSession _emulator;
	private readonly ICopperScreenAudioOutput? _audio;
	private readonly bool _disposeAudio;
	private readonly FloppyDriveAudio? _floppyDriveAudio;
	private readonly float[] _audioBuffer;
	private readonly CopperScreenDriveState[] _driveStates = new CopperScreenDriveState[4];
	private readonly CopperScreenDriveState[] _floppyDriveAudioStates = new CopperScreenDriveState[4];
	private readonly ConcurrentQueue<CopperScreenCommand> _commands = new ConcurrentQueue<CopperScreenCommand>();
	private readonly AutoResetEvent _wake = new AutoResetEvent(false);
	private readonly object _presentationSync = new object();
	private readonly int[][] _frameBuffers;
	private readonly CopperScreenDriveState[][] _frameDriveStates;
	private readonly int[] _frameBufferLeaseCounts;
	private readonly PresentationFrameEntry[] _presentationQueue = new PresentationFrameEntry[MaxQueuedPresentationFrames];
	private Thread? _thread;
	private volatile bool _running;
	private int _writeBufferIndex;
	private int _latestBufferIndex;
	private int _presentationQueueHead;
	private int _presentationQueueCount;
	private long _latestFrameNumber;
	private long _droppedFrames;
	private long _presentationSkippedFrames;
	private long _presentationBufferDroppedFrames;
	private long _presentationQueueFullThrottleCount;
	private long _audioSubmitFailures;
	private CopperScreenState _latestState;
	private bool _pendingCopperBenchRequest;
	private bool _pausedStatePublished;
	private double _lastPublishFrameMilliseconds;
	private double _lastPublishCopyMilliseconds;
	private double _lastPresentationBufferReserveMilliseconds;
	private double _lastEmulationFrameMilliseconds;
	private double _lastAudioFrameMilliseconds;
	private bool _disposed;

	private CopperScreenRuntime(ICopperScreenSession emulator, ICopperScreenAudioOutput? audio, bool disposeAudio)
	{
		_emulator = emulator ?? throw new ArgumentNullException(nameof(emulator));
		_audioSampleRate = emulator.AudioSampleRate;
		_audio = audio;
#if COPPERSCREEN_INSTALLED_TESTS
        if (audio is MiniaudioAudioOutput native) native.DiagnosticTimeline = _diagnosticTimeline;
#endif
		_audioWaitHandles = audio?.BufferAvailable is { } available ? [_wake, available] : null;
		_disposeAudio = disposeAudio;
		_audioBuffer = new float[_emulator.AudioFramesPerAppFrame(_audioSampleRate) * AudioChannels];
		_floppyDriveAudio = FloppyDriveAudio.TryCreate(
			_emulator.FloppyDriveAudioOptions,
			_emulator.BaseDirectory,
			_audioSampleRate,
			out var floppyDriveAudioStatus);
		if (floppyDriveAudioStatus != null)
		{
			_emulator.SetStatusText(floppyDriveAudioStatus);
		}

		_frameBuffers =
		[
			new int[_emulator.Framebuffer.Length],
			new int[_emulator.Framebuffer.Length],
			new int[_emulator.Framebuffer.Length]
		];
		_frameDriveStates =
		[
			new CopperScreenDriveState[4],
			new CopperScreenDriveState[4],
			new CopperScreenDriveState[4]
		];
		_frameBufferLeaseCounts = new int[_frameBuffers.Length];
		_latestBufferIndex = 0;
		PublishCurrentFrame(framesRendered: 0, queuedAudioBuffers: _audio?.QueuedBufferCount ?? 0);
	}

	public int Width => _emulator.Width;

	public int Height => _emulator.Height;

	public CopperScreenPresentationGeometry PresentationGeometry => _emulator.PresentationGeometry;

	public event Action? FramePublished;
	public event Action<string>? HostClipboardTextChanged;
	public event Action<ClipboardImage>? HostClipboardImageChanged;

	public CopperScreenState CurrentState
	{
		get
		{
			lock (_presentationSync)
			{
				return WithCurrentPresentationDiagnostics(_latestState);
			}
		}
	}

	public bool HasPendingPresentationFrames
	{
		get
		{
			lock (_presentationSync)
			{
				return _presentationQueueCount > 0;
			}
		}
	}

	public static CopperScreenRuntime Create(string[] args, string baseDirectory)
		=> Create(CopperScreenStartupOptions.Parse(args, baseDirectory));

    public static CopperScreenRuntime Create(CopperScreenStartupOptions startupOptions, CopperScreenRuntime? previous = null)
	{
        var emulator = CopperScreenSession.Create(startupOptions, previous?._emulator);
		try
		{
			var audio = MiniaudioAudioOutput.TryCreate(
				emulator.AudioSampleRate, AudioChannels,
				emulator.AudioFramesPerAppFrame(emulator.AudioSampleRate), AudioOutputBufferCount);
			try { return new CopperScreenRuntime(emulator, audio, disposeAudio: true); }
			catch { audio?.Dispose(); throw; }
		}
		catch { emulator.Dispose(); throw; }
	}

	internal static CopperScreenRuntime CreateForTests(ICopperScreenSession emulator, ICopperScreenAudioOutput? audio = null)
		=> new CopperScreenRuntime(emulator, audio, disposeAudio: false);
#if COPPERSCREEN_INSTALLED_TESTS
    private readonly CopperScreenAudioTimeline _diagnosticTimeline = new();
    private long DiagnosticMissing => (_audio as MiniaudioAudioOutput)?.DiagnosticMissingSamples ?? 0;
    internal async Task DiagnosticWriteAudioTimelineAsync(string path)
    {
        var nativeStopped = false;
        var frozen = await EnqueueAsync(_ =>
        {
            _diagnosticTimeline.Freeze();
            nativeStopped = (_audio as MiniaudioAudioOutput)?.DiagnosticStop() ?? true;
            return new CopperScreenCommandResult(true, "Audio trace writers frozen", CaptureState(0, _audio?.QueuedBufferCount ?? 0));
        });
        if (!frozen.Success) throw new InvalidOperationException(frozen.Message);
        // Keep the owner alive for the window's ordinary final save/close commands.
        CopperScreenInstalledSoak.Write(path, new { nativeStopped, timeline = _diagnosticTimeline.Export() });
    }
    private void DiagnosticOwnerEvent(int kind, long start, long reserved, long guestDone, long submitted,
        long cpuStart, int before, long missingBefore, int detail, double publicationCore = -1) => _diagnosticTimeline.RecordOwner(new(
            kind, start, reserved, guestDone, submitted, Stopwatch.GetTimestamp(), cpuStart,
            cpuStart < 0 ? -1 : CopperScreenAudioTimeline.CpuTime(), _diagnosticGuestFields,
            before, _audio?.QueuedBufferCount ?? 0, missingBefore, DiagnosticMissing, detail,
            GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2), publicationCore));
    internal Task<CopperScreenCommandResult> DiagnosticSetAudioSchedulingAsync(bool enabled) => EnqueueAsync(_ =>
    {
        var changed = _audioScheduling?.DiagnosticSetEnabled(enabled) == true;
        return new CopperScreenCommandResult(changed, "Diagnostic multimedia scheduling control", CaptureState(0, _audio?.QueuedBufferCount ?? 0));
    });
    internal Task<CopperScreenCommandResult> DiagnosticStopAudioAsync() => EnqueueAsync(_ =>
    {
        var stopped = (_audio as MiniaudioAudioOutput)?.DiagnosticStop() == true;
        return new CopperScreenCommandResult(stopped, stopped ? "Native audio sink stopped for recovery control" : "Native audio stop failed", CaptureState(0, _audio?.QueuedBufferCount ?? 0));
    });
    internal Task<CopperScreenCommandResult> DiagnosticCloseAudioAsync() => EnqueueAsync(_ =>
    {
        var closed = (_audio as MiniaudioAudioOutput)?.DiagnosticClose() == true;
        return new CopperScreenCommandResult(closed, closed ? "Native device closed for reopen control" : "Native audio close failed", CaptureState(0, _audio?.QueuedBufferCount ?? 0));
    });
    private long _diagnosticGuestSampleValues, _diagnosticGuestFields, _diagnosticGuestAudibleFields;
    private long _diagnosticSuppressedSampleValues;
    private CopperScreenAudioScheduling? _audioScheduling;
    private long _diagnosticPreviousFrameStart;
    private static readonly double[] DiagnosticGapUpperBounds = [1, 5, 10, 15, 20, 25, 30, 40, 80, 160];
    private readonly long[] _diagnosticFrameGaps = new long[11];
    internal object DiagnosticAudio => new { output = (_audio as MiniaudioAudioOutput)?.DiagnosticSnapshot,
        guestSampleValues = Interlocked.Read(ref _diagnosticGuestSampleValues), guestFields = Interlocked.Read(ref _diagnosticGuestFields),
        guestAudibleFields = Interlocked.Read(ref _diagnosticGuestAudibleFields),
        suppressedSampleValues = Interlocked.Read(ref _diagnosticSuppressedSampleValues),
        multimediaScheduling = _audioScheduling?.IsRegistered == true,
        pacing = new { protocol = "host-frame-start-gaps-v1", upperBoundsMilliseconds = DiagnosticGapUpperBounds,
            buckets = Enumerable.Range(0, _diagnosticFrameGaps.Length).Select(i => Interlocked.Read(ref _diagnosticFrameGaps[i])).ToArray() } };
    internal Task<CopperScreenCommandResult> DiagnosticObserveAsync(Action<ICopperScreenSession> observer) => EnqueueAsync(emulator =>
    {
        observer(emulator);
        return new CopperScreenCommandResult(true, "Diagnostic observation", CaptureState(0, _audio?.QueuedBufferCount ?? 0));
    });
#endif

	// Construct and validate a stopped replacement first. Only the old machine can
	// execute until it is disposed; the caller starts the new worker afterward.
	internal static CopperScreenRuntime CreateReplacement(CopperScreenRuntime? current, Func<CopperScreenRuntime> create)
	{
        if (current?.CurrentState.Drives.Any(d => d.HasUnsavedChanges) == true)
            throw new InvalidOperationException("Save ADF or discard and eject changed disks before replacing the Amiga.");
		var replacement = create();
		try { current?.Dispose(); }
		catch { replacement.Dispose(); throw; }
		return replacement;
	}

	public void SetOutputVolume(float volume)
	{
		var gain = CopperScreenAudioGain.Validate(volume);
		_audio?.SetVolume(gain);
	}

	public static int CalculateFramesToRender(int? queuedAudioBuffers, bool catchUpAudio)
	{
		if (!queuedAudioBuffers.HasValue)
		{
			return 1;
		}

		var queued = queuedAudioBuffers.Value;
		if (queued >= AudioOutputBufferCount)
		{
			return 0;
		}

		if (!catchUpAudio)
		{
			return 1;
		}

		if (queued >= TargetQueuedAudioBuffers)
		{
			return 0;
		}

		var availableBuffers = AudioOutputBufferCount - queued;
		return Math.Clamp(TargetQueuedAudioBuffers - queued, 1, Math.Min(MaxFramesPerTick, availableBuffers));
	}

	internal static int CalculatePresentationQueueCapacity(int? queuedAudioBuffers)
		=> queuedAudioBuffers.HasValue && queuedAudioBuffers.Value < CriticalQueuedAudioBuffers
			? MaxQueuedPresentationFrames
			: TargetQueuedPresentationFrames;

	public void Start()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		if (_running)
		{
			return;
		}

		_running = true;
		_thread = new Thread(Run)
		{
			IsBackground = true,
			Name = "CopperScreen runtime"
		};
		_thread.Start();
	}

	public void Stop()
	{
		_running = false;
		_wake.Set();
		var thread = _thread;
		if (thread != null && thread.IsAlive && thread != Thread.CurrentThread)
		{
            // A bounded native checkpoint can outlive two host seconds. Never
            // dispose its machine while the execution owner is still using it.
			thread.Join();
		}
	}

	public bool TryCopyNextPresentationFrame(Span<int> destination, ref long lastSeenFrameNumber, out CopperScreenState state, bool force = false)
	{
		lock (_presentationSync)
		{
			if (destination.Length < _emulator.Framebuffer.Length)
			{
				throw new ArgumentException("Destination framebuffer is too small.", nameof(destination));
			}

			if (_presentationQueueCount > 0)
			{
				var entry = DequeuePresentationFrameNoLock();
				RecordSkippedPresentationFrames(lastSeenFrameNumber, entry.FrameNumber);

				_frameBuffers[entry.BufferIndex].AsSpan().CopyTo(destination);
				state = WithCurrentPresentationDiagnostics(entry.State);
				lastSeenFrameNumber = entry.FrameNumber;
				_wake.Set();
				return true;
			}

			if (!force)
			{
				state = WithCurrentPresentationDiagnostics(_latestState);
				return false;
			}

			if (_latestFrameNumber != lastSeenFrameNumber)
			{
				RecordSkippedPresentationFrames(lastSeenFrameNumber, _latestFrameNumber);
			}

			_frameBuffers[_latestBufferIndex].AsSpan().CopyTo(destination);
			state = WithCurrentPresentationDiagnostics(_latestState);
			lastSeenFrameNumber = _latestFrameNumber;
			return true;
		}
	}

	public CopperScreenFrameLease? TryAcquireNextPresentationFrame(ref long lastSeenFrameNumber, bool force = false)
	{
		lock (_presentationSync)
		{
			if (_presentationQueueCount > 0)
			{
				var entry = DequeuePresentationFrameNoLock();
				RecordSkippedPresentationFrames(lastSeenFrameNumber, entry.FrameNumber);

				_frameBufferLeaseCounts[entry.BufferIndex]++;
				lastSeenFrameNumber = entry.FrameNumber;
				_wake.Set();
				return new CopperScreenFrameLease(
					this,
					entry.BufferIndex,
					_frameBuffers[entry.BufferIndex],
					WithCurrentPresentationDiagnostics(entry.State));
			}

			if (!force)
			{
				return null;
			}

			if (_latestFrameNumber != lastSeenFrameNumber)
			{
				RecordSkippedPresentationFrames(lastSeenFrameNumber, _latestFrameNumber);
			}

			var bufferIndex = _latestBufferIndex;
			_frameBufferLeaseCounts[bufferIndex]++;
			lastSeenFrameNumber = _latestFrameNumber;
			return new CopperScreenFrameLease(
				this,
				bufferIndex,
				_frameBuffers[bufferIndex],
				WithCurrentPresentationDiagnostics(_latestState));
		}
	}

	public void KeyDown(AmigaRawKey key)
		=> Post(emulator => emulator.KeyDown(key));

	public void KeyUp(AmigaRawKey key)
		=> Post(emulator => emulator.KeyUp(key));

    public void ReleaseInteractiveInput(AmigaRawKey[] keys)
        => Post(session => ReleaseInteractiveInput(session, keys));

    internal static void ReleaseInteractiveInput(ICopperScreenSession session, ReadOnlySpan<AmigaRawKey> keys)
    {
        foreach (var key in keys) session.KeyUp(key);
        session.SetMouseButtons(false, false);
        for (var port = 0; port < 2; port++) session.SetJoystickPort(port, false, false, false, false, false, false);
    }

	public void SetHostClipboardText(string text)
		=> Post(emulator => emulator.QueueHostClipboardText(text));

	public void SetHostClipboardImage(ClipboardImage image)
		=> Post(emulator => emulator.QueueHostClipboardImage(image));

	public void MoveMousePort(int deltaX, int deltaY)
		=> Post(emulator => emulator.MoveMousePort(deltaX, deltaY));

	public void SetMousePortPosition(int x, int y)
		=> Post(emulator => emulator.SetMousePortPosition(x, y));

	public void SetMousePresentationPosition(int x, int y)
		=> Post(emulator => emulator.SetMousePresentationPosition(x, y));

	public void SetMouseButtons(bool primaryFirePressed, bool secondFirePressed)
		=> Post(emulator => emulator.SetMouseButtons(primaryFirePressed, secondFirePressed));

	public void SetJoystickPort(bool up, bool down, bool left, bool right, bool primaryFirePressed, bool secondFirePressed)
		=> Post(emulator => emulator.SetJoystickPort(up, down, left, right, primaryFirePressed, secondFirePressed));

	public void SetJoystickPort(int portIndex, bool up, bool down, bool left, bool right, bool primaryFirePressed, bool secondFirePressed)
		=> Post(emulator => emulator.SetJoystickPort(portIndex, up, down, left, right, primaryFirePressed, secondFirePressed));

	public void SetInputOptions(CopperScreenInputOptions inputOptions)
		=> Post(emulator => emulator.SetInputOptions(inputOptions));

	public void SetPresentationOptions(CopperScreenPresentationOptions options)
		=> Post(emulator => emulator.SetPresentationOptions(options), publishAfterExecute: true);

	public Task<CopperScreenCommandResult> TogglePausedAsync()
		=> EnqueueAsync(emulator =>
		{
			var paused = emulator.TogglePaused();
			_audio?.SetPaused(paused);
			return new CopperScreenCommandResult(true, paused ? "Paused" : "Running", CaptureState(framesRendered: 0, queuedAudioBuffers: _audio?.QueuedBufferCount ?? 0));
		});

	public Task<CopperScreenCommandResult> ResetAsync()
		=> EnqueueAsync(emulator =>
		{
			emulator.Reset();
			if (emulator is CopperScreenLightweightSession) _audio?.DiscardQueuedSamples();
			return new CopperScreenCommandResult(true, "Reset", CaptureState(framesRendered: 0, queuedAudioBuffers: _audio?.QueuedBufferCount ?? 0));
		});

	public Task<CopperScreenCommandResult> PulsePrimaryFireAsync(int frames = 30)
		=> EnqueueAsync(emulator =>
		{
			emulator.PulsePrimaryFire(frames);
			return new CopperScreenCommandResult(true, "Fire", CaptureState(framesRendered: 0, queuedAudioBuffers: _audio?.QueuedBufferCount ?? 0));
		});

	public Task<CopperScreenCommandResult> InsertNextDiskAsync()
	{
		var nextDiskPath = CopperScreenDiskNavigation.ResolveAdjacentDiskPath(CurrentState.DiskPath, 1);
		return nextDiskPath == null
			? SetStatusAsync("no next disk image")
			: InsertDiskAsync(nextDiskPath, markChanged: true);
	}

	public Task<CopperScreenCommandResult> InsertPreviousDiskAsync()
	{
		var previousDiskPath = CopperScreenDiskNavigation.ResolveAdjacentDiskPath(CurrentState.DiskPath, -1);
		return previousDiskPath == null
			? SetStatusAsync("no previous disk image")
			: InsertDiskAsync(previousDiskPath, markChanged: true);
	}

	public async Task<CopperScreenCommandResult> InsertDiskAsync(string diskPath, bool markChanged = true)
	{
		if (!CopperScreenDiskImageArchive.DiskPathExists(diskPath))
		{
			return await SetStatusAsync("disk image not found").ConfigureAwait(false);
		}

		var fullPath = CopperScreenDiskImageArchive.NormalizeDiskPath(diskPath);
		CopperScreenAdfImage disk;
		try
		{
			disk = await Task.Run(() => CopperScreenDiskImageArchive.LoadDiskImage(fullPath)).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or InvalidDataException)
		{
			return await SetStatusAsync(ex.Message).ConfigureAwait(false);
		}

		return await EnqueueAsync(emulator =>
		{
			var inserted = emulator.InsertLoadedDisk(fullPath, disk, markChanged);
			var message = inserted ? "Inserted " + emulator.DiskName : emulator.StatusText;
			return new CopperScreenCommandResult(inserted, message, CaptureState(framesRendered: 0, queuedAudioBuffers: _audio?.QueuedBufferCount ?? 0));
		}).ConfigureAwait(false);
	}

	public async Task<CopperScreenCommandResult> InsertDriveDiskAsync(int driveIndex, string diskPath, bool markChanged = true)
	{
		if (driveIndex == 0)
		{
			return await InsertDiskAsync(diskPath, markChanged).ConfigureAwait(false);
		}

		if (!CopperScreenDiskImageArchive.DiskPathExists(diskPath))
		{
			return await SetStatusAsync("disk image not found").ConfigureAwait(false);
		}

		var fullPath = CopperScreenDiskImageArchive.NormalizeDiskPath(diskPath);
		CopperScreenAdfImage disk;
		try
		{
			disk = await Task.Run(() => CopperScreenDiskImageArchive.LoadDiskImage(fullPath)).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or InvalidDataException)
		{
			return await SetStatusAsync(ex.Message).ConfigureAwait(false);
		}

		return await EnqueueAsync(emulator =>
		{
			var inserted = emulator.InsertLoadedDisk(driveIndex, fullPath, disk, markChanged);
			var message = inserted ? $"Inserted DF{driveIndex}: {CopperScreenDiskImageArchive.GetDisplayName(fullPath)}" : emulator.StatusText;
			return new CopperScreenCommandResult(inserted, message, CaptureState(framesRendered: 0, queuedAudioBuffers: _audio?.QueuedBufferCount ?? 0));
		}).ConfigureAwait(false);
	}

	public Task<CopperScreenCommandResult> SetDriveWriteProtectedAsync(int driveIndex, bool writeProtected)
		=> EnqueueAsync(emulator =>
		{
			var updated = emulator.SetDriveWriteProtected(driveIndex, writeProtected);
			return new CopperScreenCommandResult(updated, emulator.StatusText, CaptureState(framesRendered: 0, queuedAudioBuffers: _audio?.QueuedBufferCount ?? 0));
		});

	public Task<CopperScreenCommandResult> EjectDiskAsync(int driveIndex = 0)
		=> EnqueueAsync(emulator =>
		{
			var ejected = emulator.EjectDisk(driveIndex);
			return new CopperScreenCommandResult(ejected, emulator.StatusText, CaptureState(0, _audio?.QueuedBufferCount ?? 0));
		});

    public Task<CopperScreenCommandResult> DiscardPersistentStorageAsync() => EnqueueAsync(emulator =>
    {
        emulator.DiscardPersistentStorage();
        return new CopperScreenCommandResult(true, "Last complete CopperOS checkpoint retained", CaptureState(0, _audio?.QueuedBufferCount ?? 0));
    });

    public Task<CopperScreenCommandResult> SavePersistentStorageAsync() => EnqueueAsync(emulator =>
    {
        try { emulator.SavePersistentStorage(); return new CopperScreenCommandResult(true, emulator.StatusText, CaptureState(0, _audio?.QueuedBufferCount ?? 0)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { emulator.SetStatusText(ex.Message); return new CopperScreenCommandResult(false, ex.Message, CaptureState(0, _audio?.QueuedBufferCount ?? 0)); }
    });

    public Task<CopperScreenCommandResult> ExportWorkspaceAsync(string destination) => EnqueueAsync(emulator =>
    {
        try
        {
            if (emulator is not CopperScreenLightweightSession session) throw new NotSupportedException("Workspace export requires Lightweight.");
            session.ExportWorkspaceBackup(destination);
            emulator.SetStatusText("Workspace backup exported. Primary save status is unchanged.");
            return new CopperScreenCommandResult(true, emulator.StatusText, CaptureState(0, _audio?.QueuedBufferCount ?? 0));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException)
        { return new CopperScreenCommandResult(false, ex.Message, CaptureState(0, _audio?.QueuedBufferCount ?? 0)); }
    });

	public Task<CopperScreenCommandResult> SaveAdfAsync(int driveIndex, string path, bool overwriteExisting = false)
		=> EnqueueAsync(emulator =>
		{
			try
			{
				var destination = Path.GetFullPath(path);
                if (CopperOsWorkspace.IsWorkspace(destination))
                {
                    if (driveIndex != 0 || emulator is not CopperScreenLightweightSession owner || !owner.OwnsWorkspacePath(destination))
                        throw new IOException("Export to a new ADF file rather than overwriting another CopperOS workspace.");
                    emulator.SavePersistentStorage();
                    return new CopperScreenCommandResult(true, "Saved CopperOS workspace", CaptureState(0, _audio?.QueuedBufferCount ?? 0));
                }
				if (!Path.GetExtension(destination).Equals(".adf", StringComparison.OrdinalIgnoreCase))
					throw new ArgumentException("Choose an .adf file for the saved disk.");
                if (CopperOsWorkspace.IsManagedDestination(destination)) throw new IOException("Export outside managed workspace folders to preserve their checkpoints and backups.");
                var drives = CaptureState(0, _audio?.QueuedBufferCount ?? 0).Drives;
                foreach (var drive in drives)
                    if (drive.Index != driveIndex && drive.DiskPath is { } source &&
                        Path.GetFullPath(source).Equals(destination, StringComparison.OrdinalIgnoreCase))
                        throw new IOException("Choose a new file rather than overwriting another mounted disk.");
                var workspaceExport = emulator is CopperScreenLightweightSession session && session.WorkspaceSeedPairId != null;
                if (workspaceExport && File.Exists(destination)) throw new IOException("Export the workspace to a new file. Existing disks, seeds and backups are preserved.");
                if (workspaceExport && emulator is CopperScreenLightweightSession ownerSession && ownerSession.IsInsideWorkspace(destination))
                    throw new IOException("Export outside the managed workspace folder so checkpoints and backups remain protected.");
				var bytes = emulator.ExportAdf(driveIndex);
                CopperScreenAtomicFile.Write(destination, bytes, overwriteExisting);
				emulator.MarkAdfSaved(driveIndex);
				emulator.SetStatusText($"Saved DF{driveIndex}: {destination}");
				return new CopperScreenCommandResult(true, emulator.StatusText, CaptureState(0, _audio?.QueuedBufferCount ?? 0));
			}
			catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException)
			{
				emulator.SetStatusText(ex.Message);
				return new CopperScreenCommandResult(false, ex.Message, CaptureState(0, _audio?.QueuedBufferCount ?? 0));
			}
		});

	public Task<CopperScreenCommandResult> DiscardAndEjectDiskAsync(int driveIndex)
		=> EnqueueAsync(emulator =>
		{
			var ejected = emulator.DiscardAndEjectDisk(driveIndex);
			return new CopperScreenCommandResult(ejected, emulator.StatusText, CaptureState(0, _audio?.QueuedBufferCount ?? 0));
		});

	public Task<CopperScreenCommandResult> LaunchCopperBenchPathAsync(string amigaPath)
		=> EnqueueAsync(emulator =>
		{
			var launched = emulator.LaunchCopperBenchPath(amigaPath, out var message);
			return new CopperScreenCommandResult(launched, message, CaptureState(framesRendered: 0, queuedAudioBuffers: _audio?.QueuedBufferCount ?? 0));
		});

	public void ConsumeCopperBenchRequest()
	{
		Post(_ =>
		{
			_pendingCopperBenchRequest = false;
		}, publishAfterExecute: true);
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		if (_thread == Thread.CurrentThread) throw new InvalidOperationException("Dispose the runtime after leaving its execution thread.");
		_disposed = true;
		Stop();
		try { _emulator.Dispose(); }
		finally
		{
			_wake.Dispose();
			try { if (_disposeAudio) _audio?.Dispose(); }
			finally { _floppyDriveAudio?.Dispose(); }
		}
	}

	private Task<CopperScreenCommandResult> SetStatusAsync(string message)
		=> EnqueueAsync(emulator =>
		{
			emulator.SetStatusText(message);
			return new CopperScreenCommandResult(false, message, CaptureState(framesRendered: 0, queuedAudioBuffers: _audio?.QueuedBufferCount ?? 0));
		});

	private void Post(Action<ICopperScreenSession> action, bool publishAfterExecute = false)
	{
		_commands.Enqueue(new CopperScreenCommand(action, null, publishAfterExecute));
		_wake.Set();
	}

	private Task<CopperScreenCommandResult> EnqueueAsync(Func<ICopperScreenSession, CopperScreenCommandResult> action)
	{
		var completion = new TaskCompletionSource<CopperScreenCommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
		_commands.Enqueue(new CopperScreenCommand(
			emulator =>
			{
				var result = action(emulator);
				completion.TrySetResult(result);
			},
			completion,
			publishAfterExecute: true));
		_wake.Set();
		return completion.Task;
	}

	private void Run()
	{
		using var scheduling = _audio == null ? null : CopperScreenAudioScheduling.TryEnter();
#if COPPERSCREEN_INSTALLED_TESTS
        _audioScheduling = scheduling;
#endif
		double nextSilentFrameTime = Environment.TickCount64;
		var audioUnavailable = false;
		while (_running)
		{
			ProcessCommands();
#if COPPERSCREEN_INSTALLED_TESTS
            var recoveryStart = Stopwatch.GetTimestamp();
#endif
			_audio?.RecoverIfStalled();
#if COPPERSCREEN_INSTALLED_TESTS
            if (Stopwatch.GetElapsedTime(recoveryStart).TotalMilliseconds >= 1)
                DiagnosticOwnerEvent(4, recoveryStart, 0, 0, 0, -1, 0, DiagnosticMissing, 0);
#endif

			if (_emulator.IsPaused)
			{
				if (!_pausedStatePublished)
				{
					PublishCurrentFrame(framesRendered: 0, queuedAudioBuffers: _audio?.QueuedBufferCount ?? 0);
					_pausedStatePublished = true;
				}

				_wake.WaitOne(10);
				nextSilentFrameTime = Environment.TickCount64;
				continue;
			}

			_pausedStatePublished = false;
			var unavailable = _audio != null && !_audio.IsClockRunning && !_audio.NeedsPrefill;
			if (unavailable != audioUnavailable)
			{
				// The callback owns read indexes. Epoch discard never races its reads.
				_audio?.DiscardQueuedSamples();
				nextSilentFrameTime = Environment.TickCount64;
				audioUnavailable = unavailable;
			}
			if (_audio == null || unavailable)
			{
				if (_audio == null && WaitForPresentationQueueCapacity(null))
				{
					continue;
				}

				var now = Environment.TickCount64;
				if (now < nextSilentFrameTime)
				{
					_wake.WaitOne((int)Math.Min(5, nextSilentFrameTime - now));
					continue;
				}

				RenderFrames(1, queuedAudioBuffers: 0, submitAudio: !unavailable);
				nextSilentFrameTime += DisplayFrameMilliseconds;
				if (nextSilentFrameTime < now - DisplayFrameMilliseconds)
				{
					nextSilentFrameTime = now + DisplayFrameMilliseconds;
				}

				continue;
			}

			var queued = _audio.QueuedBufferCount;
			var framesToRender = CalculateFramesToRender(queued, catchUpAudio: true);
			if (framesToRender <= 0)
			{
				if (_audioWaitHandles != null)
				{
#if COPPERSCREEN_INSTALLED_TESTS
                    var waitStart = Stopwatch.GetTimestamp();
                    var missing = DiagnosticMissing;
#endif
                    var waitResult = WaitHandle.WaitAny(_audioWaitHandles, 250);
#if COPPERSCREEN_INSTALLED_TESTS
                    DiagnosticOwnerEvent(3, waitStart, 0, 0, 0, -1, queued, missing, waitResult);
#endif
					if (waitResult == WaitHandle.WaitTimeout) _audio.RecoverIfStalled();
				}
				else _wake.WaitOne(1);
				continue;
			}

			// The sink is the host clock: restore its bounded reserve immediately.
			// A second video deadline loses refill time after scheduler delays.
			RenderFrames(framesToRender, queued);
		}
	}

	private void ProcessCommands()
	{
		while (_commands.TryDequeue(out var command))
		{
#if COPPERSCREEN_INSTALLED_TESTS
            var commandStart = Stopwatch.GetTimestamp();
            var commandCpu = CopperScreenAudioTimeline.CpuTime();
            var commandQueue = _audio?.QueuedBufferCount ?? 0;
            var commandMissing = DiagnosticMissing;
#endif
			try
			{
				command.Execute(_emulator);
				if (command.PublishAfterExecute)
				{
					PublishCurrentFrame(framesRendered: 0, queuedAudioBuffers: _audio?.QueuedBufferCount ?? 0);
				}
			}
			catch (Exception ex)
			{
				var result = SetEmulatorError(ex.Message);
				command.Completion?.TrySetResult(result);
			}
#if COPPERSCREEN_INSTALLED_TESTS
            finally { DiagnosticOwnerEvent(2, commandStart, 0, 0, 0, commandCpu, commandQueue, commandMissing, 1); }
#endif
		}
	}

	private CopperScreenCommandResult SetEmulatorError(string message)
	{
		_emulator.SetStatusText(message);
		PublishCurrentFrame(framesRendered: 0, queuedAudioBuffers: _audio?.QueuedBufferCount ?? 0);
		return new CopperScreenCommandResult(false, message, CurrentState);
	}

	private void RenderFrames(int framesToRender, int queuedAudioBuffers, bool submitAudio = true)
	{
		for (var frame = 0; frame < framesToRender && _running; frame++)
		{
#if COPPERSCREEN_INSTALLED_TESTS
            var fieldStart = Stopwatch.GetTimestamp();
            var fieldCpu = CopperScreenAudioTimeline.CpuTime();
            var fieldQueue = _audio?.QueuedBufferCount ?? 0;
            var fieldMissing = DiagnosticMissing;
            long guestDone = 0;
#endif
			ProcessCommands();
			if (_emulator.IsPaused)
			{
				PublishCurrentFrame(framesRendered: 0, queuedAudioBuffers);
				_pausedStatePublished = true;
				return;
			}

			if (_audio == null && WaitForPresentationQueueCapacity(null))
			{
				return;
			}

			var presentationQueuedAudioBuffers = _audio?.QueuedBufferCount ?? queuedAudioBuffers;
			var reservedBufferIndex = TryReservePresentationFrameBuffer(
				presentationQueuedAudioBuffers,
				out var reservedFrameNumber);
			if (reservedBufferIndex >= 0 && _frameBuffers[reservedBufferIndex].Length != _emulator.Framebuffer.Length)
				_frameBuffers[reservedBufferIndex] = new int[_emulator.Framebuffer.Length];
			var presentationFramebuffer = reservedBufferIndex >= 0
				? _frameBuffers[reservedBufferIndex]
				: _emulator.Framebuffer;
			var startTimestamp = Stopwatch.GetTimestamp();
#if COPPERSCREEN_INSTALLED_TESTS
            if (_diagnosticPreviousFrameStart != 0)
            {
                var gap = Stopwatch.GetElapsedTime(_diagnosticPreviousFrameStart, startTimestamp).TotalMilliseconds;
                var bucket = 0;
                while (bucket < DiagnosticGapUpperBounds.Length && gap > DiagnosticGapUpperBounds[bucket]) bucket++;
                Interlocked.Increment(ref _diagnosticFrameGaps[bucket]);
            }
            _diagnosticPreviousFrameStart = startTimestamp;
#endif
			int audioFrames;
			try
			{
				_emulator.RenderNextFrame(presentationFramebuffer);
#if COPPERSCREEN_INSTALLED_TESTS
                guestDone = Stopwatch.GetTimestamp();
#endif
				if (reservedBufferIndex >= 0 && presentationFramebuffer.Length != _emulator.Framebuffer.Length)
				{
					// Only this reserved, unleased slot is replaced. Existing leases retain their pixels and geometry.
					_frameBuffers[reservedBufferIndex] = _emulator.Framebuffer.ToArray();
				}
				while (_emulator.TryTakeHostClipboardText(out var clipboardText))
					HostClipboardTextChanged?.Invoke(clipboardText);
				while (_emulator.TryTakeHostClipboardImage(out var clipboardImage) && clipboardImage is not null)
					HostClipboardImageChanged?.Invoke(clipboardImage);
				var audioStartTimestamp = Stopwatch.GetTimestamp();
				audioFrames = _emulator.RenderAudio(_audioBuffer, _audioSampleRate, AudioChannels);
#if COPPERSCREEN_INSTALLED_TESTS
                Interlocked.Add(ref _diagnosticGuestSampleValues, audioFrames * AudioChannels);
                Interlocked.Increment(ref _diagnosticGuestFields);
                if (_audioBuffer.AsSpan(0, audioFrames * AudioChannels).ContainsAnyExcept(0f)) Interlocked.Increment(ref _diagnosticGuestAudibleFields);
#endif
				if (_audio != null && _floppyDriveAudio != null && audioFrames > 0)
				{
					_emulator.CaptureDriveStates(_floppyDriveAudioStates);
					_floppyDriveAudio.Mix(_audioBuffer.AsSpan(0, audioFrames * AudioChannels), audioFrames, AudioChannels, _floppyDriveAudioStates);
				}

				_lastAudioFrameMilliseconds = Stopwatch.GetElapsedTime(audioStartTimestamp).TotalMilliseconds;
			}
			catch (Exception ex)
			{
				CopperScreenCrashLog.WriteException("CopperScreenRuntime.RenderFrames", ex, ex);
				_emulator.CaptureFatalException(ex);
				if (_emulator is CopperScreenLightweightSession) _audio?.DiscardQueuedSamples();
				_audioBuffer.AsSpan().Clear();
				_lastAudioFrameMilliseconds = 0;
				_lastEmulationFrameMilliseconds = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
				PublishCurrentFrame(framesRendered: 0, queuedAudioBuffers: _audio?.QueuedBufferCount ?? queuedAudioBuffers);
				return;
			}

			_lastEmulationFrameMilliseconds = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
			if (_audio != null && submitAudio && !_audio.Submit(_audioBuffer.AsSpan(0, audioFrames * AudioChannels)))
			{
				_audioSubmitFailures++;
			}
#if COPPERSCREEN_INSTALLED_TESTS
            if (!submitAudio) Interlocked.Add(ref _diagnosticSuppressedSampleValues, audioFrames * AudioChannels);
            var submitted = Stopwatch.GetTimestamp();
#endif

			if (reservedBufferIndex >= 0)
			{
				PublishRenderedFrame(
					reservedBufferIndex,
					reservedFrameNumber,
					framesRendered: 1,
					queuedAudioBuffers: _audio?.QueuedBufferCount ?? queuedAudioBuffers);
			}
#if COPPERSCREEN_INSTALLED_TESTS
            DiagnosticOwnerEvent(1, fieldStart, startTimestamp, guestDone, submitted,
                fieldCpu, fieldQueue, fieldMissing, submitAudio ? audioFrames * AudioChannels : -audioFrames * AudioChannels,
                reservedBufferIndex >= 0 ? _lastPublishFrameMilliseconds : -1);
#endif
		}
	}

	private int TryReservePresentationFrameBuffer(int queuedAudioBuffers, out long frameNumber)
	{
		var reserveStartTimestamp = Stopwatch.GetTimestamp();
        var entered = false;
        if (_audio != null && !Monitor.TryEnter(_presentationSync))
        {
            _droppedFrames++; _presentationBufferDroppedFrames++;
            frameNumber = 0;
            _lastPresentationBufferReserveMilliseconds = Stopwatch.GetElapsedTime(reserveStartTimestamp).TotalMilliseconds;
            return -1; // Still render the complete field and PCM into the guest framebuffer.
        }
        if (_audio != null) entered = true;
        else Monitor.Enter(_presentationSync, ref entered);
        try
		{
			if (ShouldReplaceQueuedPresentationFrames(queuedAudioBuffers))
			{
				var queueCapacity = GetPublishPresentationQueueCapacity(queuedAudioBuffers);
				while (_presentationQueueCount >= queueCapacity && TryDropOldestPresentationFrameNoLock())
				{
					_presentationQueueFullThrottleCount++;
				}
			}

			if (_presentationQueueCount >= MaxQueuedPresentationFrames)
			{
				_presentationQueueFullThrottleCount++;
				frameNumber = 0;
				_lastPresentationBufferReserveMilliseconds = Stopwatch.GetElapsedTime(reserveStartTimestamp).TotalMilliseconds;
				return -1;
			}

			var bufferIndex = SelectWritableFrameBufferNoLock();
			if (bufferIndex < 0)
			{
				_droppedFrames++;
				_presentationBufferDroppedFrames++;
				frameNumber = 0;
				_lastPresentationBufferReserveMilliseconds = Stopwatch.GetElapsedTime(reserveStartTimestamp).TotalMilliseconds;
				return -1;
			}

			frameNumber = _latestFrameNumber + 1;
			_lastPresentationBufferReserveMilliseconds = Stopwatch.GetElapsedTime(reserveStartTimestamp).TotalMilliseconds;
			return bufferIndex;
		}
        finally { if (entered) Monitor.Exit(_presentationSync); }
	}

	private void PublishRenderedFrame(int bufferIndex, long frameNumber, int framesRendered, int queuedAudioBuffers)
	{
		var publishStartTimestamp = Stopwatch.GetTimestamp();
		_pendingCopperBenchRequest |= _emulator.ConsumeCopperBenchRequest();
		_lastPublishCopyMilliseconds = 0;
		var state = CaptureState(framesRendered, queuedAudioBuffers, _frameDriveStates[bufferIndex]);
        var entered = false;
        if (_audio != null && !Monitor.TryEnter(_presentationSync))
        {
            _droppedFrames++; _presentationBufferDroppedFrames++;
            _lastPublishFrameMilliseconds = Stopwatch.GetElapsedTime(publishStartTimestamp).TotalMilliseconds;
            return; // UI contention may drop presentation, never guest work or PCM.
        }
        if (_audio != null) entered = true;
        else Monitor.Enter(_presentationSync, ref entered);
        try
		{
			_writeBufferIndex = bufferIndex;
			_latestBufferIndex = bufferIndex;
			_latestFrameNumber = frameNumber;
			_latestState = WithCurrentPresentationDiagnostics(state with { FrameNumber = frameNumber });
			EnqueuePresentationFrameNoLock(new PresentationFrameEntry(bufferIndex, frameNumber, _latestState));
		}
        finally { if (entered) Monitor.Exit(_presentationSync); }

		_lastPublishFrameMilliseconds = Stopwatch.GetElapsedTime(publishStartTimestamp).TotalMilliseconds;
		FramePublished?.Invoke();
	}

	private void PublishCurrentFrame(int framesRendered, int queuedAudioBuffers)
	{
		var publishStartTimestamp = Stopwatch.GetTimestamp();
		_pendingCopperBenchRequest |= _emulator.ConsumeCopperBenchRequest();
		// Paused/silent publication can wait. Running PCM production must not wait
		// for UI state publication, including either side of this frame copy.
		var canWait = _audio == null || _emulator.IsPaused;
		_lastPublishCopyMilliseconds = 0;
		int nextBuffer;
		long frameNumber;
		var entered = false;
		if (!canWait && !Monitor.TryEnter(_presentationSync))
		{
			_droppedFrames++; _presentationBufferDroppedFrames++;
			_lastPublishFrameMilliseconds = Stopwatch.GetElapsedTime(publishStartTimestamp).TotalMilliseconds;
			return;
		}
		if (!canWait) entered = true;
		else Monitor.Enter(_presentationSync, ref entered);
		try
		{
			if (ShouldReplaceQueuedPresentationFrames(queuedAudioBuffers))
			{
				var queueCapacity = GetPublishPresentationQueueCapacity(queuedAudioBuffers);
				while (_presentationQueueCount >= queueCapacity && TryDropOldestPresentationFrameNoLock())
				{
					_presentationQueueFullThrottleCount++;
				}
			}

			if (_presentationQueueCount >= MaxQueuedPresentationFrames)
			{
				_presentationQueueFullThrottleCount++;
				_lastPublishCopyMilliseconds = 0;
				_lastPublishFrameMilliseconds = Stopwatch.GetElapsedTime(publishStartTimestamp).TotalMilliseconds;
				return;
			}

			nextBuffer = SelectWritableFrameBufferNoLock();
			if (nextBuffer < 0)
			{
				_droppedFrames++;
				_presentationBufferDroppedFrames++;
				_lastPublishCopyMilliseconds = 0;
				_lastPublishFrameMilliseconds = Stopwatch.GetElapsedTime(publishStartTimestamp).TotalMilliseconds;
				return;
			}

			frameNumber = _latestFrameNumber + 1;
		}
		finally { if (entered) Monitor.Exit(_presentationSync); }

		var copyStartTimestamp = Stopwatch.GetTimestamp();
		if (_frameBuffers[nextBuffer].Length != _emulator.Framebuffer.Length)
			_frameBuffers[nextBuffer] = new int[_emulator.Framebuffer.Length];
		_emulator.Framebuffer.AsSpan().CopyTo(_frameBuffers[nextBuffer]);
		_lastPublishCopyMilliseconds = Stopwatch.GetElapsedTime(copyStartTimestamp).TotalMilliseconds;
		_lastPublishFrameMilliseconds = Stopwatch.GetElapsedTime(publishStartTimestamp).TotalMilliseconds;
		var state = CaptureState(framesRendered, queuedAudioBuffers, _frameDriveStates[nextBuffer]);
		entered = false;
		if (!canWait && !Monitor.TryEnter(_presentationSync))
		{
			_droppedFrames++; _presentationBufferDroppedFrames++;
			_lastPublishFrameMilliseconds = Stopwatch.GetElapsedTime(publishStartTimestamp).TotalMilliseconds;
			return;
		}
		if (!canWait) entered = true;
		else Monitor.Enter(_presentationSync, ref entered);
		try
		{
			_writeBufferIndex = nextBuffer;
			_latestBufferIndex = nextBuffer;
			_latestFrameNumber = frameNumber;
			_latestState = WithCurrentPresentationDiagnostics(state with { FrameNumber = frameNumber });
			EnqueuePresentationFrameNoLock(new PresentationFrameEntry(nextBuffer, frameNumber, _latestState));
		}
		finally { if (entered) Monitor.Exit(_presentationSync); }

		_lastPublishFrameMilliseconds = Stopwatch.GetElapsedTime(publishStartTimestamp).TotalMilliseconds;
		FramePublished?.Invoke();
	}

	private bool WaitForPresentationQueueCapacity(int? queuedAudioBuffers)
	{
		lock (_presentationSync)
		{
			var capacity = CalculatePresentationQueueCapacity(queuedAudioBuffers);
			if (_presentationQueueCount < capacity)
			{
				return false;
			}

			_presentationQueueFullThrottleCount++;
		}

		_wake.WaitOne(1);
		return true;
	}

	private void RecordSkippedPresentationFrames(long lastSeenFrameNumber, long frameNumber)
	{
		if (lastSeenFrameNumber == 0 || frameNumber <= lastSeenFrameNumber + 1)
		{
			return;
		}

		var skipped = frameNumber - lastSeenFrameNumber - 1;
		_droppedFrames += skipped;
		_presentationSkippedFrames += skipped;
	}

	private CopperScreenState WithCurrentPresentationDiagnostics(CopperScreenState state)
		=> state with
		{
			DroppedFrames = _droppedFrames,
			PresentationSkippedFrames = _presentationSkippedFrames,
			PresentationBufferDroppedFrames = _presentationBufferDroppedFrames,
			PresentationQueueDepth = _presentationQueueCount,
			PresentationQueueCapacity = GetPublishPresentationQueueCapacity(state.QueuedAudioBuffers),
			PresentationQueueFullThrottleCount = _presentationQueueFullThrottleCount,
			LastPublishFrameMilliseconds = _lastPublishFrameMilliseconds,
			LastPublishCopyMilliseconds = _lastPublishCopyMilliseconds,
			LastPresentationBufferReserveMilliseconds = _lastPresentationBufferReserveMilliseconds
		};

	private int GetPublishPresentationQueueCapacity(int queuedAudioBuffers)
		=> _audio == null ? MaxQueuedPresentationFrames : CalculatePresentationQueueCapacity(queuedAudioBuffers);

	private bool ShouldReplaceQueuedPresentationFrames(int queuedAudioBuffers)
		=> _audio == null || queuedAudioBuffers < CriticalQueuedAudioBuffers;

	private int SelectWritableFrameBufferNoLock()
	{
		for (var offset = 1; offset <= _frameBuffers.Length; offset++)
		{
			var candidate = (_writeBufferIndex + offset) % _frameBuffers.Length;
			if (candidate != _latestBufferIndex &&
				_frameBufferLeaseCounts[candidate] == 0 &&
				!IsPresentationBufferQueuedNoLock(candidate))
			{
				return candidate;
			}
		}

		return -1;
	}

	internal void ReleaseFrameLease(int bufferIndex)
	{
		lock (_presentationSync)
		{
			if ((uint)bufferIndex >= (uint)_frameBufferLeaseCounts.Length || _frameBufferLeaseCounts[bufferIndex] <= 0)
			{
				return;
			}

			_frameBufferLeaseCounts[bufferIndex]--;
		}

		_wake.Set();
	}

	private CopperScreenState CaptureState(int framesRendered, int queuedAudioBuffers)
		=> CaptureState(framesRendered, queuedAudioBuffers, _driveStates);

	private CopperScreenState CaptureState(int framesRendered, int queuedAudioBuffers, CopperScreenDriveState[] driveStates)
	{
		return new CopperScreenState(
			_emulator.ProfileName,
			_emulator.DiskName,
			_emulator.DiskPath,
			_emulator.CpuState,
			CaptureDriveStates(driveStates),
			_emulator.DebugSnapshot,
			_emulator.StatusText,
			_emulator.IsPaused,
			_emulator.IsWorkbenchHandoffPending,
			_emulator.IsDiskSwapPending,
			_emulator.IsPrimaryFirePressed,
			_emulator.AudioFilterEnabled,
			_pendingCopperBenchRequest,
			framesRendered,
			queuedAudioBuffers,
			_latestFrameNumber,
			_droppedFrames,
			_presentationSkippedFrames,
			_presentationBufferDroppedFrames,
			_audioSubmitFailures,
			_presentationQueueCount,
			MaxQueuedPresentationFrames,
			_presentationQueueFullThrottleCount,
			_lastPublishFrameMilliseconds,
			_lastPublishCopyMilliseconds,
			_lastPresentationBufferReserveMilliseconds,
			_lastEmulationFrameMilliseconds,
			_emulator.LastFrameTiming.CpuMilliseconds,
			_emulator.LastFrameTiming.HardwareMilliseconds,
			_emulator.LastFrameTiming.DisplayMilliseconds,
			_lastAudioFrameMilliseconds,
			_emulator.IsInterlaced,
			_emulator.CompletedInterlaceField)
		{
			FaultMessage = _emulator.FaultMessage,
			FramebufferWidth = _emulator.Width, FramebufferHeight = _emulator.Height,
			PresentationGeometry = _emulator.PresentationGeometry
		};
	}

	private CopperScreenDriveState[] CaptureDriveStates()
		=> CaptureDriveStates(_driveStates);

	private CopperScreenDriveState[] CaptureDriveStates(CopperScreenDriveState[] destination)
	{
		_emulator.CaptureDriveStates(destination);
		return destination;
	}

	private void EnqueuePresentationFrameNoLock(PresentationFrameEntry entry)
	{
		var tail = (_presentationQueueHead + _presentationQueueCount) % _presentationQueue.Length;
		_presentationQueue[tail] = entry;
		_presentationQueueCount++;
	}

	private PresentationFrameEntry DequeuePresentationFrameNoLock()
	{
		var entry = _presentationQueue[_presentationQueueHead];
		_presentationQueue[_presentationQueueHead] = default;
		_presentationQueueHead = (_presentationQueueHead + 1) % _presentationQueue.Length;
		_presentationQueueCount--;
		return entry;
	}

	private bool TryDropOldestPresentationFrameNoLock()
	{
		if (_presentationQueueCount <= 0)
		{
			return false;
		}

		_presentationQueue[_presentationQueueHead] = default;
		_presentationQueueHead = (_presentationQueueHead + 1) % _presentationQueue.Length;
		_presentationQueueCount--;
		return true;
	}

	private bool IsPresentationBufferQueuedNoLock(int bufferIndex)
	{
		for (var offset = 0; offset < _presentationQueueCount; offset++)
		{
			var queueIndex = (_presentationQueueHead + offset) % _presentationQueue.Length;
			if (_presentationQueue[queueIndex].BufferIndex == bufferIndex)
			{
				return true;
			}
		}

		return false;
	}

	private sealed class CopperScreenCommand
	{
		public CopperScreenCommand(
			Action<ICopperScreenSession> execute,
			TaskCompletionSource<CopperScreenCommandResult>? completion,
			bool publishAfterExecute)
		{
			Execute = execute;
			Completion = completion;
			PublishAfterExecute = publishAfterExecute;
		}

		public Action<ICopperScreenSession> Execute { get; }

		public TaskCompletionSource<CopperScreenCommandResult>? Completion { get; }

		public bool PublishAfterExecute { get; }
	}

	private readonly record struct PresentationFrameEntry(int BufferIndex, long FrameNumber, CopperScreenState State);
}
