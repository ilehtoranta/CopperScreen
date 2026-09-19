
using CopperMod.Amiga.Lightweight;

namespace CopperScreen;

// One owner, one machine. Host commands run on CopperScreenRuntime's thread.
internal sealed class CopperScreenLightweightSession : ICopperScreenSession
{
    private readonly LightweightA500Machine _machine;
    private CopperScreenInputOptions _inputOptions;
    private byte _mouseButtons, _joystick0, _joystick1;
    private int _fireFrames;
    private bool _haveFrame, _audioAvailable, _faulted;
    private readonly CopperScreenAdfImage?[] _pendingAdf = new CopperScreenAdfImage?[4];
    private readonly int[] _insertDelay = new int[4];
    private readonly string?[] _diskPaths = new string?[4];

    public CopperScreenLightweightSession(CopperScreenStartupOptions options, CopperScreenLightweightSession? previous = null)
    {
        Validate(options);
        // Media decoding is mount-time only; none of the old execution machinery is created.
        var rom = CopperScreenKickstartRomArchive.ReadNative13Rom(options.KickstartRomPath!,
            options.Profile.KickstartSource, options.Profile.KickstartVersion);
        BaseDirectory = options.BaseDirectory;
        ProfileName = options.Profile.DisplayName + " [Lightweight]";
        FloppyDriveAudioOptions = options.FloppyDriveAudio;
        _inputOptions = options.Input;
        var configuration = new LightweightA500Configuration
            { FramebufferWidth = 908, FloppyDriveCount = options.Profile.FloppyDriveCount,
                Hardfiles = options.HardDrives.Select(h => new CopperDisk.AmigaHardfileConfiguration(h.Unit, h.Path, h.ReadOnly, h.CreateSizeBytes, (CopperDisk.AmigaHardfileMountMode)h.Mode, ConvertPartition(h.Partition))).ToArray() };
        if (previous is not null && !previous.IsPaused) throw new InvalidOperationException("Pause before preparing a replacement machine.");
        _machine = previous?._machine.CreateRestartCandidate(configuration) ?? new(configuration);
        try
        {
            _machine.LoadKickstart(rom);
            for (var i = 0; i < _machine.FloppyDriveCount; i++)
            {
                var path = options.DriveDiskPaths[i];
                if (path != null) CopperScreenDiskImageArchive.LoadDiskImage(path).Mount(_machine, i);
                _machine.SetDriveWriteProtected(i, options.DriveWriteProtected[i] ?? true);
                _diskPaths[i] = path;
            }
            ResetHostState();
        }
        catch { _machine.Dispose(); throw; }
    }

    private static CopperDisk.AmigaHardfilePartitionMetadata? ConvertPartition(AmigaHardfilePartitionMetadata? p)
        => p is null ? null : new()
        {
            DeviceName = p.DeviceName,
            TableSize = p.TableSize,
            SizeBlockLongs = p.SizeBlockLongs,
            SectorOrigin = p.SectorOrigin,
            Surfaces = p.Surfaces,
            SectorsPerBlock = p.SectorsPerBlock,
            BlocksPerTrack = p.BlocksPerTrack,
            ReservedBlocks = p.ReservedBlocks,
            PreAllocBlocks = p.PreAllocBlocks,
            Interleave = p.Interleave,
            LowCylinder = p.LowCylinder,
            HighCylinder = p.HighCylinder,
            NumBuffers = p.NumBuffers,
            BufferMemoryType = p.BufferMemoryType,
            MaxTransfer = p.MaxTransfer,
            Mask = p.Mask,
            BootPriority = p.BootPriority,
            DosType = p.DosType,
        };

    internal static void Validate(CopperScreenStartupOptions options, bool requireRomPath = true)
    {
        if (options.Error != null) throw new ArgumentException(options.Error);
        var p = options.Profile;
        if (p.Chipset != AmigaChipset.OcsPal || p.ChipRamSize != 512 * 1024 ||
            p.ExpansionRamSize != 512 * 1024 || p.ExpansionRamBase != 0xC00000 ||
            p.RealFastRamSize != 0 || p.RtgVramSize != 0 || p.RtcEnabled ||
            p.FloppyDriveCount is < 1 or > 4 ||
            (options.CpuBackendOverride ?? p.CpuBackend) != M68kBackendKind.AccurateM68000 ||
            p.KickstartSource is not (CopperScreenKickstartSource.Kickstart13Rom or CopperScreenKickstartSource.KickstartRom) ||
            p.KickstartVersion != KickstartVersion.Kickstart13)
            throw new NotSupportedException("Lightweight supports PAL OCS / 68000 / 512 KiB Chip + 512 KiB slow / native Kickstart 1.3 / one to four floppy drives; no RTC or RTG.");
        if (requireRomPath && string.IsNullOrWhiteSpace(options.KickstartRomPath))
            throw new NotSupportedException("Choose your Kickstart 1.3 ROM in Settings > Setup, or supply --kickstart <path>.");
        if (options.DriveDiskPaths.Skip(p.FloppyDriveCount).Any(path => path != null))
            throw new NotSupportedException("Lightweight accepts media in connected floppy drives only.");
        if (options.AgnusBusArbitration != AgnusBusArbitrationMode.Legacy ||
            options.CopperQuiescentFastPath || options.CopperQuiescentFastPathVerify || options.CopperQuiescentDiagnostics ||
            options.DeferredCpuBusBatchConfigured || options.DeferredCpuChipWriteJournalConfigured ||
            options.DeferredCpuChipReadSegmentsConfigured || options.DeferredCpuChipInstructionFetchBatchConfigured ||
            options.DeferredCpuChipInstructionFetchShadowConfigured || options.DeferredCpuCustomPointerWritesConfigured ||
            options.DeferredCpuCustomCompositionWritesConfigured || options.CpuWaitSlotReference || !options.HardwareSpecialization)
            throw new NotSupportedException("Legacy execution overrides do not apply to Lightweight.");
        ValidateInput(options.Input);
    }

    private static void ValidateInput(CopperScreenInputOptions input)
    {
        if (input.IsMousePort(1))
            throw new NotSupportedException("Lightweight currently supports a mouse on port 1 only.");
    }

    public int Width => 908;
    public int Height => 626;
    public int[] Framebuffer { get; } = new int[908 * 626];
    public int AudioSampleRate => 48_000;
    public double VideoVBlankHz => 7_093_790.0 / (454 * 313);
    internal static CopperScreenPresentationGeometry NativePresentationGeometry =>
        new(356, 285, 320, 256, 2, 2, true, false)
        {
            // The engine stores the complete beam: visible samples start at
            // lowres x=98, line=26. Standard PAL DIW is ($81,$2C), 320x256.
            FullViewport = new(196, 52, 712, 570),
            StandardViewport = new(258, 88, 640, 512)
        };
    public CopperScreenPresentationGeometry PresentationGeometry => NativePresentationGeometry;
    public CopperScreenEmulatorFrameTiming LastFrameTiming => default; // No invented CPU/device timing split.
    public bool IsInterlaced { get; private set; }
    public int CompletedInterlaceField { get; private set; }
    public string ProfileName { get; }
    public string DiskName => CopperScreenDiskImageArchive.GetDisplayName(DiskPath);
    public string? DiskPath => _diskPaths[0];
    public string BaseDirectory { get; }
    public FloppyDriveAudioOptions FloppyDriveAudioOptions { get; }
    public CopperScreenCpuState CpuState => new(_machine.Cpu.ProgramCounter,
        _machine.Cpu.LastInstructionProgramCounter, _machine.Cpu.StatusRegister);
    public CopperScreenDebugSnapshot? DebugSnapshot => null;
    public string StatusText { get; private set; } = "Lightweight experimental A500";
    public string? FaultMessage => _faulted ? StatusText : null;
    public bool IsPaused { get; private set; }
    public bool IsWorkbenchHandoffPending => false;
    public bool IsDiskSwapPending => Array.Exists(_pendingAdf, adf => adf != null);
    public bool IsPrimaryFirePressed => (_mouseButtons & 1) != 0 || ((_joystick0 | _joystick1) & 16) != 0 || _fireFrames > 0;
    public bool AudioFilterEnabled => _machine.AudioFilterControlEnabled;
    internal LightweightA500Machine Machine => _machine;

    public int AudioFramesPerAppFrame(int sampleRate)
        // Capacity, not a fixed sample count: a sync hold can extend a field.
        => sampleRate == AudioSampleRate ? 1024 : throw new NotSupportedException("Lightweight delivers 48 kHz stereo without host resampling.");

    public void RenderNextFrame(int[] destination)
    {
        if (destination.Length != Framebuffer.Length) throw new ArgumentException("Wrong framebuffer size.");
        if (IsPaused) { Framebuffer.CopyTo(destination, 0); return; }
        for (var i = 0; i < _machine.FloppyDriveCount; i++)
        {
            if (_pendingAdf[i] is not { } adf || _insertDelay[i]-- > 0) continue;
            adf.Mount(_machine, i);
            _pendingAdf[i] = null;
            StatusText = $"Lightweight: DF{i} inserted " + CopperScreenDiskImageArchive.GetDisplayName(_diskPaths[i]);
        }
        var field = _machine.IsLongField ? 0 : 1;
        _machine.ExecuteFrame();
        if (_machine.UnsupportedActiveFeature is { } feature)
        {
            CaptureFatalException(new NotSupportedException("Lightweight unsupported: " + feature));
            throw new NotSupportedException(StatusText);
        }
        // Preserve all native pixels. Vertical duplication/weaving is host presentation only.
        var interlaced = _machine.InterlaceEnabled;
        var pixels = _machine.Framebuffer.Span;
        for (var y = 0; y < 313; y++)
        {
            var row = pixels.Slice(y * Width, Width);
            row.CopyTo(Framebuffer.AsSpan((y * 2 + (interlaced ? field : 0)) * Width, Width));
            if (!interlaced || !_haveFrame || !IsInterlaced)
                row.CopyTo(Framebuffer.AsSpan((y * 2 + (interlaced ? 1 - field : 1)) * Width, Width));
        }
        IsInterlaced = interlaced;
        CompletedInterlaceField = field;
        _haveFrame = _audioAvailable = true;
        if (!ReferenceEquals(destination, Framebuffer)) Framebuffer.CopyTo(destination, 0);
        if (_fireFrames > 0 && --_fireFrames == 0) ApplyInput();
    }

    public int RenderAudio(Span<float> destination, int sampleRate, int channels)
    {
        if (sampleRate != AudioSampleRate || channels != 2) throw new NotSupportedException("48 kHz stereo required.");
        if (IsPaused || !_audioAvailable) { destination.Clear(); return 0; }
        var pcm = _machine.AudioSamples.Span;
        if (destination.Length < pcm.Length) throw new ArgumentException("Audio buffer too small.");
        for (var i = 0; i < pcm.Length; i++) destination[i] = pcm[i] * (1.0f / 32768);
        _audioAvailable = false;
        return pcm.Length / 2;
    }

    public void CaptureDriveStates(Span<CopperScreenDriveState> destination)
    {
        for (var i = 0; i < destination.Length; i++)
        {
            if (i >= _machine.FloppyDriveCount)
            {
                destination[i] = new(i, false, false, "Not connected", null, 0, 0, false, false, true, false);
                continue;
            }
            var drive = _machine.GetDriveState(i);
            destination[i] = new(i, true, _machine.IsDriveMounted(i),
                CopperScreenDiskImageArchive.GetDisplayName(_diskPaths[i]), _diskPaths[i], drive.Cylinder, drive.Head,
                drive.MotorOn, drive.Selected, _machine.IsDriveWriteProtected(i), drive.ActiveDma)
                { IsSwapPending = _pendingAdf[i] != null, HasUnsavedChanges = _machine.IsDriveDirty(i),
                    Format = _pendingAdf[i]?.Format ?? _machine.GetDriveFormat(i),
                    CanExportAdf = _pendingAdf[i] is null && _machine.CanWriteDrive(i) };
        }
    }

    public bool InsertLoadedDisk(string path, CopperScreenAdfImage disk, bool markChanged)
        => InsertLoadedDisk(0, path, disk, markChanged);

    public bool InsertLoadedDisk(int drive, string path, CopperScreenAdfImage disk, bool markChanged)
    {
        if ((uint)drive >= (uint)_machine.FloppyDriveCount) return Reject("Floppy drive is not connected.");
        if (disk.Format == LightweightFloppyFormat.Adf && !Path.GetExtension(disk.Name).Equals(".adf", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("ADF media must have an .adf filename.");
        var adf = disk; // Fully prepared by the loader before changing the existing medium.
        if (_machine.IsDriveDirty(drive)) return Reject($"DF{drive} has unsaved changes. Save ADF or discard and eject it before replacing the disk.");
        if (markChanged)
        {
            _machine.EjectAdf(drive);
            _pendingAdf[drive] = adf;
            _insertDelay[drive] = 25;
        }
        else { adf.Mount(_machine, drive); _pendingAdf[drive] = null; }
        _diskPaths[drive] = path;
        StatusText = $"Lightweight: DF{drive} " + (markChanged ? "swapping " : "inserted ") + CopperScreenDiskImageArchive.GetDisplayName(path);
        return true;
    }

    public bool EjectDisk(int drive)
    {
        if ((uint)drive >= (uint)_machine.FloppyDriveCount) return Reject("Floppy drive is not connected.");
        if (_machine.IsDriveDirty(drive)) return Reject($"DF{drive} has unsaved changes. Save ADF or use Discard and eject.");
        return DiscardAndEjectDisk(drive);
    }

    public bool DiscardAndEjectDisk(int drive)
    {
        if ((uint)drive >= (uint)_machine.FloppyDriveCount) return Reject("Floppy drive is not connected.");
        _pendingAdf[drive] = null;
        _machine.EjectAdf(drive);
        _diskPaths[drive] = null;
        StatusText = $"Lightweight: DF{drive} ejected";
        return true;
    }
    public bool SetDriveWriteProtected(int drive, bool writeProtected)
    {
        if ((uint)drive >= (uint)_machine.FloppyDriveCount) return Reject("Floppy drive is not connected.");
        if (!writeProtected && (_pendingAdf[drive]?.Format ?? _machine.GetDriveFormat(drive)) == LightweightFloppyFormat.Ipf)
            return Reject("IPF media is permanently read-only.");
        _machine.SetDriveWriteProtected(drive, writeProtected);
        SetStatusText($"DF{drive}: " + (writeProtected ? "write protected" : "writable in memory; use Save ADF to keep changes"));
        return true;
    }

    public byte[] ExportAdf(int drive) => _machine.ExportAdf(drive);
    public void MarkAdfSaved(int drive) => _machine.MarkAdfSaved(drive);
    private bool Reject(string message) { SetStatusText(message); return false; }
    public void SetStatusText(string message) { if (!_faulted) StatusText = message; }
    public void CaptureFatalException(Exception exception)
    { _faulted = IsPaused = true; _audioAvailable = false; StatusText = exception.Message; }
    public bool TogglePaused()
    {
        if (_faulted) return true;
        IsPaused = !IsPaused;
        _audioAvailable = false;
        return IsPaused;
    }
    public void Reset()
    {
        for (var i = 0; i < _machine.FloppyDriveCount; i++)
            if (_pendingAdf[i] is { } adf) adf.Mount(_machine, i);
        _machine.Reset();
        ResetHostState();
    }
    private void ResetHostState()
    {
        Array.Clear(_pendingAdf);
        Array.Clear(_insertDelay);
        _mouseButtons = _joystick0 = _joystick1 = 0;
        _fireFrames = 0;
        _haveFrame = _audioAvailable = _faulted = IsPaused = IsInterlaced = false;
        CompletedInterlaceField = 0;
        Array.Fill(Framebuffer, unchecked((int)0xFF000000));
        ApplyInput();
        StatusText = "Lightweight experimental A500 — native Kickstart 1.3, ADF / read-only IPF / CopperHDF";
    }
    private void ApplyInput(short dx = 0, short dy = 0)
        => _machine.SubmitInput(new((byte)(_joystick0 | (_inputOptions.IsMousePort(0) ? 0 : 128)),
            _joystick1, (byte)(_mouseButtons | (_fireFrames > 0 ? 1 : 0)), dx, dy, 0));
    public void MoveMousePort(int x, int y)
    {
        if (!_inputOptions.IsMousePort(0)) return;
        // Relative counters wrap at eight bits; reducing large host deltas preserves that result.
        ApplyInput(unchecked((short)x), unchecked((short)y));
    }
    // CopperStart-only absolute cursor hints. MainWindow separately submits relative
    // hardware deltas; applying both would double movement on a native-ROM machine.
    public void SetMousePortPosition(int x, int y) { }
    public void SetMousePresentationPosition(int x, int y) { }
    public void SetMouseButtons(bool primary, bool secondary)
    { _mouseButtons = (byte)((primary ? 1 : 0) | (secondary ? 2 : 0)); ApplyInput(); }
    public void PulsePrimaryFire(int frames) { _fireFrames = Math.Max(0, frames); ApplyInput(); }
    public void SetJoystickPort(bool up, bool down, bool left, bool right, bool primary, bool secondary)
        => SetJoystickPort(_inputOptions.JoystickPortIndex, up, down, left, right, primary, secondary);
    public void SetJoystickPort(int port, bool up, bool down, bool left, bool right, bool primary, bool secondary)
    {
        if ((uint)port > 1) throw new ArgumentOutOfRangeException(nameof(port));
        var value = (byte)((up ? 1 : 0) | (down ? 2 : 0) | (left ? 4 : 0) | (right ? 8 : 0) | (primary ? 16 : 0) | (secondary ? 32 : 0));
        if (port == 0) _joystick0 = value; else _joystick1 = value;
        ApplyInput();
    }
    public void SetInputOptions(CopperScreenInputOptions options)
    { ValidateInput(options); _inputOptions = options; _joystick0 = _joystick1 = _mouseButtons = 0; ApplyInput(); }
    public void SetPresentationOptions(CopperScreenPresentationOptions options) { /* Host presenter owns these options. */ }
    public void KeyDown(AmigaRawKey key) => _machine.SubmitKey((byte)key);
    public void KeyUp(AmigaRawKey key) => _machine.SubmitKey((byte)((byte)key | 128));
    public bool ConsumeCopperBenchRequest() => false;
    public bool LaunchCopperBenchPath(string path, out string message)
    { message = "CopperBench/CopperStart services are not available in Lightweight."; return Reject(message); }
    public void QueueHostClipboardText(string text) => SetStatusText("Host clipboard integration is not available in Lightweight.");
    public void QueueHostClipboardImage(ClipboardImage image) => SetStatusText("Host clipboard integration is not available in Lightweight.");
    public bool TryTakeHostClipboardText(out string text) { text = string.Empty; return false; }
    public bool TryTakeHostClipboardImage(out ClipboardImage? image) { image = null; return false; }
    public void Dispose() => _machine.Dispose();
}
