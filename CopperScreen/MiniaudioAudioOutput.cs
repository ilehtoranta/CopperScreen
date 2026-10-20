using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Miniaudio;
using static Miniaudio.ma_device_type;
using static Miniaudio.ma_format;
using static Miniaudio.ma_result;

namespace CopperScreen;

internal sealed unsafe class MiniaudioAudioOutput : ICopperScreenAudioOutput
{
	private readonly MiniaudioSampleQueue _queue;
	private readonly AutoResetEvent _bufferAvailable = new(false);
	private readonly object _deviceGate = new();
	private readonly int _sampleRate;
	private readonly int _framesPerBuffer;
	private readonly int _prefillBufferCount;
	private ma_backend? _backend;
	private ma_device* _device;
	private GCHandle _selfHandle;
	private bool _deviceInitialized;
	private bool _needsPrefill;
	private int _disposed;
	private float _volume = 1;
	private long _lastCallbackTick = Environment.TickCount64;
	private long _lastRecoveryAttemptTick;
	private bool _paused;

	private MiniaudioAudioOutput(int sampleRate, int channels, int framesPerBuffer, int bufferCount)
	{
		_sampleRate = sampleRate;
		_framesPerBuffer = framesPerBuffer;
		_prefillBufferCount = bufferCount;
		Channels = channels;
		_queue = new MiniaudioSampleQueue(checked(framesPerBuffer * channels), bufferCount);
		if (OperatingSystem.IsWindows()) _backend = ma_backend.ma_backend_wasapi;
	}

	private int Channels { get; }

	public int QueuedBufferCount => _queue.QueuedBufferCount;
	public WaitHandle BufferAvailable => _bufferAvailable;
	public bool IsClockRunning => _deviceInitialized && !_needsPrefill && Environment.TickCount64 - Volatile.Read(ref _lastCallbackTick) < 1000;
	public bool NeedsPrefill => _deviceInitialized && _needsPrefill;
	public void SetPaused(bool paused)
	{
		Volatile.Write(ref _paused, paused);
		if (paused) _queue.DiscardQueuedSamples();
	}
#if COPPERSCREEN_INSTALLED_TESTS
    internal CopperScreenAudioTimeline? DiagnosticTimeline { get; set; }
    internal long DiagnosticMissingSamples => _queue.DiagnosticMissingSamples;
    internal object DiagnosticSnapshot { get { lock (_deviceGate) return new { backend = !_deviceInitialized ? "unavailable" : _device->pContext->backend.ToString(),
        initialized = _deviceInitialized, clockRunning = IsClockRunning, needsPrefill = NeedsPrefill, queue = _queue.DiagnosticSnapshot, callbackErrors = Interlocked.Read(ref _callbackErrors),
        deviceState = !_deviceInitialized ? "unavailable" : ma.device_get_state(_device).ToString(),
        callbackAgeMilliseconds = Environment.TickCount64 - Volatile.Read(ref _lastCallbackTick),
        recoveryAttempts = Interlocked.Read(ref _recoveryAttempts), deviceReinitializations = Interlocked.Read(ref _deviceReinitializations),
        lastRecoveryResult = (ma_result)Volatile.Read(ref _lastRecoveryResult),
        maxCallbackGapMilliseconds = Interlocked.Read(ref _maxCallbackGapMilliseconds), nativeMessages = _nativeMessages.ToArray() }; } }
    private long _callbackErrors;
    private long _recoveryAttempts;
    private long _deviceReinitializations;
    private int _lastRecoveryResult;
    private long _previousCallbackTick, _maxCallbackGapMilliseconds;
    private ma_log* _nativeLog;
    private bool _nativeLogInitialized;
    private readonly System.Collections.Concurrent.ConcurrentQueue<string> _nativeMessages = new();

    // Injection stops the native sink only; production recovery must restart it.
    internal bool DiagnosticStop() { lock (_deviceGate) return _deviceInitialized && _device != null && ma.device_stop(_device) == MA_SUCCESS; }
    internal bool DiagnosticClose()
    {
        lock (_deviceGate)
        {
            if (_device == null || Volatile.Read(ref _disposed) != 0) return false;
            CloseDeviceForReopen();
            return true;
        }
    }
#endif

	public static MiniaudioAudioOutput? TryCreate(
		int sampleRate,
		int channels,
		int framesPerBuffer,
		int bufferCount)
	{
		if (sampleRate <= 0 || channels <= 0 || framesPerBuffer <= 0 || bufferCount < 2)
		{
			return null;
		}

		MiniaudioAudioOutput? output = null;
		try
		{
			output = new MiniaudioAudioOutput(sampleRate, channels, framesPerBuffer, bufferCount);
			output._selfHandle = GCHandle.Alloc(output);
			output._device = (ma_device*)NativeMemory.AllocZeroed((nuint)sizeof(ma_device));
#if COPPERSCREEN_INSTALLED_TESTS
            output._nativeLog = (ma_log*)NativeMemory.AllocZeroed((nuint)sizeof(ma_log));
            if (ma.log_init(null, output._nativeLog) != MA_SUCCESS) { output.Dispose(); return null; }
            output._nativeLogInitialized = true;
            ma.log_register_callback(output._nativeLog, ma.log_callback_init(&NativeLogCallback, (void*)GCHandle.ToIntPtr(output._selfHandle)));
#endif

			if (output.InitializeDevice() != MA_SUCCESS && !OperatingSystem.IsWindows())
			{
				output.Dispose();
				return null;
			}

			return output;
		}
		catch (Exception ex) when (
			ex is DllNotFoundException or
			EntryPointNotFoundException or
			BadImageFormatException or
			TypeInitializationException)
		{
			output?.Dispose();
			return null;
		}
	}

	public bool Submit(ReadOnlySpan<float> samples)
		=> Volatile.Read(ref _disposed) == 0 && _queue.TryEnqueue(samples);

	public void DiscardQueuedSamples() => _queue.DiscardQueuedSamples();
	public void SetVolume(float volume) => Volatile.Write(ref _volume, CopperScreenAudioGain.Validate(volume));

	public void RecoverIfStalled()
	{
		// Only the runtime owner calls this, outside every native callback. A device
		// that stops delivering callbacks cannot signal the normal demand wakeup.
		if (Volatile.Read(ref _disposed) != 0 || _device == null) return;
		var now = Environment.TickCount64;
		if (NeedsPrefill)
		{
			// The existing bounded reserve is generated before the first native
			// callback. Starting an empty replacement creates an avoidable gap.
			if (_queue.QueuedBufferCount < _prefillBufferCount) return;
			lock (_deviceGate)
			{
				var started = ma.device_start(_device);
				_needsPrefill = false;
				if (started == MA_SUCCESS) Volatile.Write(ref _lastCallbackTick, now);
				if (started != MA_SUCCESS) { CloseDeviceForReopen(); _lastRecoveryAttemptTick = now; }
#if COPPERSCREEN_INSTALLED_TESTS
                Volatile.Write(ref _lastRecoveryResult, (int)started);
#endif
			}
			return;
		}
		if ((_deviceInitialized && now - Volatile.Read(ref _lastCallbackTick) < 1000) || now - _lastRecoveryAttemptTick < 1000) return;
		_lastRecoveryAttemptTick = now;
#if COPPERSCREEN_INSTALLED_TESTS
        Interlocked.Increment(ref _recoveryAttempts);
#endif
		lock (_deviceGate)
		{
			if (Volatile.Read(ref _disposed) != 0) return;
			var result = _deviceInitialized ? MA_SUCCESS : MA_UNAVAILABLE;
			if (_deviceInitialized)
			{
				if (ma.device_is_started(_device) != 0) result = ma.device_stop(_device);
				if (result == MA_SUCCESS) result = ma.device_start(_device);
			}
			if (result != MA_SUCCESS)
			{
				// A removed or invalidated WASAPI client cannot be restarted. Join its
				// callbacks before clearing stale PCM and reopening the default output.
				CloseDeviceForReopen();
#if COPPERSCREEN_INSTALLED_TESTS
                Interlocked.Increment(ref _deviceReinitializations);
#endif
				result = InitializeDevice();
			}
#if COPPERSCREEN_INSTALLED_TESTS
        Volatile.Write(ref _lastRecoveryResult, (int)result);
#endif
		}
	}

	private ma_result InitializeDevice()
	{
		var config = ma.device_config_init(ma_device_type_playback);
		config.sampleRate = checked((uint)_sampleRate);
		config.periodSizeInFrames = checked((uint)_framesPerBuffer);
		config.playback.format = ma_format_f32;
		config.playback.channels = checked((uint)Channels);
		config.dataCallback = &DataCallback;
		config.pUserData = (void*)GCHandle.ToIntPtr(_selfHandle);
		// Reopen the original backend. Searching all backends while the endpoint
		// is absent can succeed on a silent null sink which would never recover.
#if COPPERSCREEN_INSTALLED_TESTS
        var context = ma.context_config_init(); context.pLog = _nativeLog;
        var result = _backend is { } backend
            ? ma.device_init_ex(&backend, 1, &context, &config, _device)
            : ma.device_init_ex(null, 0, &context, &config, _device);
#else
		var result = _backend is { } backend
			? ma.device_init_ex(&backend, 1, null, &config, _device)
			: ma.device_init(null, &config, _device);
#endif
		if (result != MA_SUCCESS) return result;
		_deviceInitialized = true;
		_needsPrefill = true;
		_backend ??= _device->pContext->backend;
		return MA_SUCCESS;
	}

	private void CloseDeviceForReopen()
	{
		if (_deviceInitialized) ma.device_uninit(_device);
		_deviceInitialized = false;
		_needsPrefill = false;
		new Span<byte>(_device, sizeof(ma_device)).Clear();
		// device_uninit joined every callback; exclusive access permits resetting
		// consumer indexes here. Unavailable-time PCM must not play on return.
		_queue.ResetAfterConsumerStopped();
	}

	public void Dispose()
	{
		if (Interlocked.Exchange(ref _disposed, 1) != 0)
		{
			return;
		}

		lock (_deviceGate)
		{
			if (_device != null)
			{
				if (_deviceInitialized)
				{
					ma.device_uninit(_device);
					_deviceInitialized = false;
				}
				NativeMemory.Free(_device);
				_device = null;
			}
		}

#if COPPERSCREEN_INSTALLED_TESTS
        if (_nativeLog != null) { if (_nativeLogInitialized) ma.log_uninit(_nativeLog); NativeMemory.Free(_nativeLog); _nativeLog = null; }
#endif
		if (_selfHandle.IsAllocated)
		{
			_selfHandle.Free();
		}
		_bufferAvailable.Dispose(); // device_uninit has joined the last callback.
	}

#if COPPERSCREEN_INSTALLED_TESTS
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void NativeLogCallback(void* user, uint level, sbyte* message)
    {
        if (level > 2 || user == null || message == null) return;
        try
        {
            if (GCHandle.FromIntPtr((nint)user).Target is MiniaudioAudioOutput output &&
                Volatile.Read(ref output._disposed) == 0 && output._nativeMessages.Count < 64)
                output._nativeMessages.Enqueue(level + ":" + Marshal.PtrToStringAnsi((nint)message));
        }
        catch { } // Diagnostic logging must never escape through a native callback.
    }
#endif
	[UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
	private static void DataCallback(ma_device* device, void* outputFrames, void* inputFrames, uint frameCount)
	{
		_ = inputFrames;
		if (device == null || outputFrames == null)
		{
			return;
		}
#if COPPERSCREEN_INSTALLED_TESTS
        MiniaudioAudioOutput? diagnosticOwner = null;
#endif

		try
		{
			var handle = GCHandle.FromIntPtr((nint)device->pUserData);
			if (handle.Target is not MiniaudioAudioOutput output || Volatile.Read(ref output._disposed) != 0)
			{
				return;
			}
#if COPPERSCREEN_INSTALLED_TESTS
            diagnosticOwner = output;
            var callbackTick = Environment.TickCount64;
            var previousTick = Interlocked.Exchange(ref output._previousCallbackTick, callbackTick);
            if (previousTick != 0)
            {
                var gap = callbackTick - previousTick;
                var maximum = Interlocked.Read(ref output._maxCallbackGapMilliseconds);
                while (gap > maximum)
                {
                    var observed = Interlocked.CompareExchange(ref output._maxCallbackGapMilliseconds, gap, maximum);
                    if (observed == maximum) break;
                    maximum = observed;
                }
            }
#endif
			Volatile.Write(ref output._lastCallbackTick, Environment.TickCount64);

			var sampleCount = checked((int)frameCount * output.Channels);
			var samples = new Span<float>(outputFrames, sampleCount);
#if COPPERSCREEN_INSTALLED_TESTS
            var callbackStart = System.Diagnostics.Stopwatch.GetTimestamp();
            var queueBefore = output._queue.QueuedBufferCount;
            var missingBefore = output.DiagnosticMissingSamples;
#endif
            var expectAudio = !Volatile.Read(ref output._paused);
			if (output._queue.Dequeue(samples, expectAudio: expectAudio)) output._bufferAvailable.Set();
			CopperScreenAudioGain.Apply(samples, Volatile.Read(ref output._volume));
#if COPPERSCREEN_INSTALLED_TESTS
            output.DiagnosticTimeline?.RecordCallback(new(callbackStart, System.Diagnostics.Stopwatch.GetTimestamp(),
                queueBefore, output._queue.QueuedBufferCount, sampleCount,
                output.DiagnosticMissingSamples - missingBefore, output.DiagnosticMissingSamples, expectAudio));
#endif
		}
		catch
		{
#if COPPERSCREEN_INSTALLED_TESTS
            if (diagnosticOwner != null) Interlocked.Increment(ref diagnosticOwner._callbackErrors);
#endif
			// Exceptions must never escape an unmanaged audio callback.
		}
	}
}

internal sealed class MiniaudioSampleQueue
{
#if COPPERSCREEN_INSTALLED_TESTS
    internal long DiagnosticMissingSamples => Interlocked.Read(ref _missingSamples);
    private long _callbacks, _requestedSamples, _consumedSamples, _starvedCallbacks, _missingSamples, _nonzeroCallbacks, _pausedSilenceSamples, _discardedSampleValues;
    internal object DiagnosticSnapshot => new { callbacks = Interlocked.Read(ref _callbacks), requestedSamples = Interlocked.Read(ref _requestedSamples),
        consumedSamples = Interlocked.Read(ref _consumedSamples), starvedCallbacks = Interlocked.Read(ref _starvedCallbacks),
        missingSamples = Interlocked.Read(ref _missingSamples), nonzeroCallbacks = Interlocked.Read(ref _nonzeroCallbacks),
        pausedSilenceSamples = Interlocked.Read(ref _pausedSilenceSamples), discardedSampleValues = Interlocked.Read(ref _discardedSampleValues) };
#endif
	private readonly float[][] _buffers;
	private readonly int[] _sampleCounts;
	private readonly int[] _generations;
	private int _generation;
	private int _readBufferIndex;
	private int _readSampleOffset;
	private int _writeBufferIndex;
	private int _queuedBufferCount;

	public MiniaudioSampleQueue(int samplesPerBuffer, int bufferCount)
	{
		if (samplesPerBuffer <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(samplesPerBuffer));
		}

		if (bufferCount < 2)
		{
			throw new ArgumentOutOfRangeException(nameof(bufferCount));
		}

		_buffers = new float[bufferCount][];
		_sampleCounts = new int[bufferCount];
		_generations = new int[bufferCount];
		for (var i = 0; i < _buffers.Length; i++)
		{
			_buffers[i] = new float[samplesPerBuffer];
		}
	}

	public int QueuedBufferCount => Volatile.Read(ref _queuedBufferCount);

	// Caller must have joined the native consumer. Ordinary pause/reset uses
	// epoch discard instead, leaving read indexes exclusively to the callback.
	internal void ResetAfterConsumerStopped()
	{
#if COPPERSCREEN_INSTALLED_TESTS
        var remaining = 0;
        for (var i = 0; i < _queuedBufferCount; i++)
            remaining += _sampleCounts[(_readBufferIndex + i) % _buffers.Length] - (i == 0 ? _readSampleOffset : 0);
        Interlocked.Add(ref _discardedSampleValues, remaining);
#endif
		Array.Clear(_sampleCounts);
		_readBufferIndex = _writeBufferIndex = _readSampleOffset = 0;
		Volatile.Write(ref _queuedBufferCount, 0);
	}

	// Producer requests a new epoch; only the audio consumer moves read indexes.
	// Already submitted device audio cannot be recalled, but the next callback
	// discards old queued/partially consumed buffers without racing ring indexes.
	public void DiscardQueuedSamples() => Interlocked.Increment(ref _generation);

	public bool TryEnqueue(ReadOnlySpan<float> samples)
	{
		if (samples.IsEmpty || samples.Length > _buffers[_writeBufferIndex].Length)
		{
			return false;
		}

		if (Volatile.Read(ref _queuedBufferCount) >= _buffers.Length)
		{
			return false;
		}

		var writeIndex = _writeBufferIndex;
		samples.CopyTo(_buffers[writeIndex]);
		_sampleCounts[writeIndex] = samples.Length;
		_generations[writeIndex] = Volatile.Read(ref _generation);
		_writeBufferIndex = (writeIndex + 1) % _buffers.Length;
		Interlocked.Increment(ref _queuedBufferCount);
		return true;
	}

	// True when a complete slot was released, including discarded old epochs.
	public bool Dequeue(Span<float> destination, bool expectAudio = true)
	{
		var released = false;
		destination.Clear();
		var destinationOffset = 0;
		while (destinationOffset < destination.Length && Volatile.Read(ref _queuedBufferCount) > 0)
		{
			var readIndex = _readBufferIndex;
			if (_generations[readIndex] != Volatile.Read(ref _generation))
			{
				FinishReadBuffer();
				released = true;
				continue;
			}
			var available = _sampleCounts[readIndex] - _readSampleOffset;
			if (available <= 0)
			{
				FinishReadBuffer();
				released = true;
				continue;
			}

			var copyCount = Math.Min(available, destination.Length - destinationOffset);
			_buffers[readIndex].AsSpan(_readSampleOffset, copyCount)
				.CopyTo(destination.Slice(destinationOffset, copyCount));
			_readSampleOffset += copyCount;
			destinationOffset += copyCount;
			if (_readSampleOffset >= _sampleCounts[readIndex])
			{
				FinishReadBuffer();
				released = true;
			}
		}
#if COPPERSCREEN_INSTALLED_TESTS
        Interlocked.Increment(ref _callbacks);
        Interlocked.Add(ref _requestedSamples, destination.Length);
        Interlocked.Add(ref _consumedSamples, destinationOffset);
        if (destinationOffset < destination.Length)
        {
            if (expectAudio) { Interlocked.Increment(ref _starvedCallbacks); Interlocked.Add(ref _missingSamples, destination.Length - destinationOffset); }
            else Interlocked.Add(ref _pausedSilenceSamples, destination.Length - destinationOffset);
        }
        if (destination.ContainsAnyExcept(0f)) Interlocked.Increment(ref _nonzeroCallbacks);
#endif
		return released;
	}

	private void FinishReadBuffer()
	{
		_sampleCounts[_readBufferIndex] = 0;
		_readSampleOffset = 0;
		_readBufferIndex = (_readBufferIndex + 1) % _buffers.Length;
		Interlocked.Decrement(ref _queuedBufferCount);
	}
}
