
using CopperMod.Amiga.Lightweight;

namespace CopperScreen;

// One owner, one machine. Host commands run on CopperScreenRuntime's thread.
internal sealed class CopperScreenLightweightSession : ICopperScreenSession
{
    private readonly LightweightA500Machine _machine;
    private readonly IReadOnlyList<LightweightFloppyDriveType> _driveTypes;
    private readonly CopperScreenMinimalBoot? _minimalBoot;
    private readonly bool _copperStart;
    private readonly CopperOsWorkspace? _workspace;
    private int _checkpointFields;
    private bool _disposed;
    private bool _workspaceSaveFailed, _discardWorkspaceChanges;
    private CopperScreenInputOptions _inputOptions;
    private byte _mouseButtons, _joystick0, _joystick1;
    private int _fireFrames;
    private bool _haveFrame, _audioAvailable, _faulted;
    private int _nativeWidth, _nativeHeight;
    private readonly CopperScreenAdfImage?[] _pendingAdf = new CopperScreenAdfImage?[4];
    private readonly int[] _insertDelay = new int[4];
    private readonly string?[] _diskPaths = new string?[4];

    public CopperScreenLightweightSession(CopperScreenStartupOptions options, CopperScreenLightweightSession? previous = null)
    {
        Validate(options);
        _driveTypes = options.Profile.FloppyDriveTypes;
        _copperStart = options.Profile.KickstartSource == CopperScreenKickstartSource.CopperStart;
        // Media decoding is mount-time only; none of the old execution machinery is created.
        CopperScreenAdfImage? bootDisk = null;
        byte[] rom;
        if (options.Profile.KickstartSource == CopperScreenKickstartSource.MinimalDiskBoot)
        {
            bootDisk = CopperScreenDiskImageArchive.LoadDiskImage(options.DriveDiskPaths[0]!);
            _minimalBoot = new(bootDisk, options.Profile.ExpansionRamSize != 0);
            rom = _minimalBoot.Rom;
        }
        else if (_copperStart) rom = CopperScreenCopperStartRom.Read(options.KickstartRomPath, options.Profile.Chipset.DisplayChip == DisplayChipModel.AgaLisa);
        else rom = CopperScreenKickstartRomArchive.ReadNativeRom(options.KickstartRomPath!,
            options.Profile.KickstartSource, options.Profile.KickstartVersion, options.Profile.Chipset.DisplayChip == DisplayChipModel.AgaLisa);
        BaseDirectory = options.BaseDirectory;
        var backend = options.CpuBackendOverride ?? options.Profile.CpuBackend;
        ProfileName = _minimalBoot != null ? "Amiga 500 · Minimal disk boot [Lightweight]" : options.Profile.DisplayName + (backend == M68kBackendKind.AccurateM68000
            ? " [Lightweight]" : $" [Lightweight, {CopperScreenAvailability.ChoiceLabel(backend.ToString())}]");
        FloppyDriveAudioOptions = options.FloppyDriveAudio;
        _inputOptions = options.Input;
        var configuration = new LightweightA500Configuration
            { FramebufferWidth = options.Profile.Chipset.DisplayChip != DisplayChipModel.OcsDenise ? 1816 : 908,
                DeniseModel = options.Profile.Chipset.DisplayChip switch { DisplayChipModel.AgaLisa => LightweightDeniseModel.Lisa4203, DisplayChipModel.EcsDenise => LightweightDeniseModel.Mos8373, _ => LightweightDeniseModel.Mos8362 }, FloppyDriveCount = options.Profile.FloppyDriveCount,
                FloppyDriveTypes = _driveTypes.Take(options.Profile.FloppyDriveCount).ToArray(),
                CpuModel = GetCpuModel(options.CpuBackendOverride ?? options.Profile.CpuBackend),
                CpuExecutionMode = backend == M68kBackendKind.JitM68040 ? Copper68k.M68kExecutionMode.Jit : Copper68k.M68kExecutionMode.Interpreter,
                AgnusModel = GetAgnusModel(options.Profile.Chipset.DmaChip),
                ChipRamBytes = options.Profile.ChipRamSize, SlowRamBytes = options.Profile.ExpansionRamSize,
                FastRamBytes = options.Profile.RealFastRamSize,
                RtcEnabled = options.Profile.RtcEnabled,
                Hardfiles = options.HardDrives.Select(h => new CopperDisk.AmigaHardfileConfiguration(h.Unit, h.Path, h.ReadOnly, h.CreateSizeBytes, (CopperDisk.AmigaHardfileMountMode)h.Mode, ConvertPartition(h.Partition))).ToArray(),
                HostDirectories = options.HostDirectories };
        if (previous is not null && !previous.IsPaused) throw new InvalidOperationException("Pause before preparing a replacement machine.");
        _machine = previous?._machine.CreateRestartCandidate(configuration) ?? new(configuration);
        try
        {
            if (_copperStart && CopperOsWorkspace.IsWorkspace(options.DriveDiskPaths[0]))
            {
                if (previous?._workspace?.ImagePath.Equals(options.DriveDiskPaths[0], StringComparison.OrdinalIgnoreCase) == true)
                {
                    if (previous._machine.IsDriveDirty(0) || previous._machine.DriveState is { ActiveDma: true } or { MotorOn: true })
                        throw new InvalidOperationException("Save CopperOS changes on the running session before replacing the Amiga.");
                    _workspace = previous._workspace.Retain();
                }
                else _workspace = CopperOsWorkspace.Open(options.DriveDiskPaths[0]!);
            }
            var integratedExtension = _copperStart && _machine.IsAga && rom.Length == 1048576;
            _machine.LoadKickstart(integratedExtension ? rom.AsSpan(524288) : rom);
            if (integratedExtension)
                _machine.LoadExtensionRom(rom.AsSpan(0, 524288));
            else if (_copperStart && !_machine.IsAga && !string.IsNullOrWhiteSpace(options.KickstartRomPath))
            {
                var romDirectory = Path.GetDirectoryName(Path.GetFullPath(options.KickstartRomPath))!;
                foreach (var name in new[] { "CopperStart-SfsExtension.rom", "CopperStart-DesktopExtension.rom" })
                {
                    var companion = Path.Combine(romDirectory, name);
                    if (!File.Exists(companion)) continue;
                    _machine.LoadExtensionRom(File.ReadAllBytes(companion));
                    break;
                }
            }
            for (var i = 0; i < _machine.FloppyDriveCount; i++)
            {
                var path = options.DriveDiskPaths[i];
                if (path != null) (i == 0 && bootDisk != null ? bootDisk : CopperScreenDiskImageArchive.LoadDiskImage(path)).Mount(_machine, i);
                _machine.SetDriveWriteProtected(i, (_machine.IsDriveMounted(i) && !_machine.CanWriteDrive(i)) || (options.DriveWriteProtected[i] ?? true));
                _diskPaths[i] = path;
            }
            ResetHostState();
        }
        catch { _workspace?.Dispose(); _machine.Dispose(); throw; }
    }

    internal static CopperDisk.AmigaHardfilePartitionMetadata? ConvertPartition(AmigaHardfilePartitionMetadata? p)
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
        if (GetValidationError(options, requireRomPath) is { } error) throw new NotSupportedException(error);
    }

    // Preset menus ask this for unsupported configurations during ordinary refreshes.
    // Keep the same checks as launch validation without throwing for expected choices.
    internal static string? GetValidationError(CopperScreenStartupOptions options, bool requireRomPath = true)
    {
        if (options.Error != null) return options.Error;
        var p = options.Profile;
        var cpu = options.CpuBackendOverride ?? p.CpuBackend;
        var minimal = p.KickstartSource == CopperScreenKickstartSource.MinimalDiskBoot;
        var copperStart = p.KickstartSource == CopperScreenKickstartSource.CopperStart;
        var copperStartA1200 = p.Chipset == new AmigaChipset(DmaChipModel.AgaAlice, DisplayChipModel.AgaLisa, VideoStandard.Pal) &&
            cpu == M68kBackendKind.AccurateM68EC020 && p.ChipRamSize == 2097152 && p.ExpansionRamSize == 0 &&
            options.HardDrives.Count == 1 && options.DriveDiskPaths.All(path => path == null);
        var copperStartA500 = p.Chipset == AmigaChipset.OcsPal && cpu == M68kBackendKind.AccurateM68000 &&
            p.ChipRamSize == 524288 && p.ExpansionRamSize == 524288;
        if (copperStart && ((!copperStartA500 && !copperStartA1200) || p.RealFastRamSize != 0 ||
            p.FloppyDriveCount != 1 || (options.DriveWriteProtected[0] == false && !CopperOsWorkspace.IsWorkspace(options.DriveDiskPaths[0])) || p.RtcEnabled || options.HardDrives.Count > 1 || options.HostDirectories.Count != 0))
            return "Use the A500 + CopperStart standalone ROM preset, or PAL A1200 with 68EC020, 2 MiB Chip RAM and one CopperOS HDF with empty floppy drives. " + CopperScreenCopperStartRom.Requirements + " Other configurations are not qualified.";
        if (copperStart && CopperOsWorkspace.IsWorkspace(options.DriveDiskPaths[0]))
            CopperOsWorkspace.ValidateOwnership(options.DriveDiskPaths[0]!);
        if (copperStart && options.HardDrives.Count == 1)
            options.HardDrives[0].ValidateCopperStart(checkImage: requireRomPath);

        if (minimal && (p.Chipset != new AmigaChipset(DmaChipModel.OcsAgnus, DisplayChipModel.OcsDenise, VideoStandard.Pal) || p.ChipRamSize != 524288 ||
            (options.CpuBackendOverride ?? p.CpuBackend) != M68kBackendKind.AccurateM68000 ||
            p.RealFastRamSize != 0 || options.HardDrives.Count != 0 || options.HostDirectories.Count != 0))
            return "Minimal disk boot currently requires a PAL OCS Amiga 500, Motorola 68000, 512 KiB Chip RAM, optional 512 KiB slow RAM and no Fast RAM, hard drives or shared folders.";
        var aga = p.Chipset.DisplayChip == DisplayChipModel.AgaLisa;
        if (aga != (p.Chipset.DmaChip == DmaChipModel.AgaAlice) ||
            aga && (cpu is not (M68kBackendKind.AccurateM68EC020 or M68kBackendKind.AccurateM68020 or M68kBackendKind.AccurateM68030 or M68kBackendKind.AccurateM68040 or M68kBackendKind.JitM68040) ||
                (!copperStart && (p.KickstartSource != CopperScreenKickstartSource.KickstartRom || p.KickstartVersion != KickstartVersion.Kickstart30))))
            return "A1200 requires PAL Alice/Lisa, 68EC020 or a 68020/030/040 upgrade, 2 MiB Chip RAM, no slow RAM and native A1200 Kickstart 3.0 (39.106).";
        if (p.Chipset.DisplayChip is not (DisplayChipModel.OcsDenise or DisplayChipModel.EcsDenise or DisplayChipModel.AgaLisa) || p.Chipset.VideoStandard != VideoStandard.Pal ||
            p.ExpansionRamBase != 0xC00000 ||
            p.RtgVramSize != 0 ||
            p.FloppyDriveCount is < 1 or > 4 ||
            (options.CpuBackendOverride ?? p.CpuBackend) is not (M68kBackendKind.AccurateM68000 or M68kBackendKind.AccurateM68010 or M68kBackendKind.AccurateM68EC020 or M68kBackendKind.AccurateM68020 or M68kBackendKind.AccurateM68030 or M68kBackendKind.AccurateM68040 or M68kBackendKind.JitM68040) ||
            p.KickstartSource is not (CopperScreenKickstartSource.KickstartRom or CopperScreenKickstartSource.Kickstart13Rom or CopperScreenKickstartSource.Kickstart31Rom or CopperScreenKickstartSource.MinimalDiskBoot or CopperScreenKickstartSource.CopperStart) ||
            (!aga && !minimal && !copperStart && p.KickstartVersion is not (KickstartVersion.Kickstart13 or KickstartVersion.Kickstart20 or KickstartVersion.Kickstart31)) ||
            (p.KickstartSource == CopperScreenKickstartSource.Kickstart13Rom && p.KickstartVersion != KickstartVersion.Kickstart13) ||
            (p.KickstartSource == CopperScreenKickstartSource.Kickstart31Rom && p.KickstartVersion != KickstartVersion.Kickstart31))
            return "Lightweight supports PAL / OCS or ECS Denise, or the initial A1200 Alice/Lisa profile / 68000 or experimental 68010/68EC020/68020/68030/68040 (optional 040 JIT) / explicit PAL Agnus and compatible Chip/slow RAM / native Kickstart 1.3, A500+ 2.04 or A500 3.1 / one to four floppy drives / optional A501 RTC; no RTG. The 68060 requires separately validated 060-aware OS task/FPU support.";
        if (p.Chipset.DmaChip is not (DmaChipModel.OcsAgnus or DmaChipModel.Agnus8372A or DmaChipModel.Agnus8375Pal2M or DmaChipModel.AgaAlice))
            return "Select an explicit supported PAL Agnus: 8371, 8372A, 8375 (318069-10) or A1200 Alice.";
        try { LightweightAgnus.ValidateMemory(GetAgnusModel(p.Chipset.DmaChip), p.ChipRamSize, p.ExpansionRamSize); }
        catch (ArgumentException ex) { return ex.Message; }
        if (requireRomPath && minimal && string.IsNullOrWhiteSpace(options.DriveDiskPaths[0]))
            return "Choose a standard ADF startup disk in DF0 for minimal disk boot.";
        if (requireRomPath && !minimal && string.IsNullOrWhiteSpace(options.KickstartRomPath))
            return copperStart ? CopperScreenCopperStartRom.RecoveryFor(aga) : "Choose the matching Kickstart 1.3, A500+ 2.04, A500 3.1 or A1200 3.0 ROM in Settings > Setup, or supply --kickstart <path> with the matching profile.";
        var maxFast = LightweightA500Configuration.MaximumFastRamBytes(GetCpuModel(cpu));
        if (!LightweightA500Configuration.IsSupportedFastRamSize(p.RealFastRamSize) || p.RealFastRamSize > maxFast)
            return $"Fast RAM must use a standard size up to {maxFast / 1048576} MiB for the selected CPU.";
        if (p.RealFastRamSize > 8388608 && p.KickstartVersion is not (KickstartVersion.Kickstart30 or KickstartVersion.Kickstart31))
            return "More than 8 MiB Fast RAM requires a full 32-bit CPU and native Kickstart 3.x Zorro III Autoconfig. Choose A500 Kickstart 3.1.";
        if (p.RealFastRamSize != 0 && p.RealFastRamBase != CopperScreenDefaults.GetDefaultFastRamBase(p.RealFastRamSize))
            return "Fast RAM uses guest Autoconfig; restore the default assignment hint for its size.";
        if (options.DriveDiskPaths.Skip(p.FloppyDriveCount).Any(path => path != null))
            return "Lightweight accepts media in connected floppy drives only.";
        if (options.AgnusBusArbitration != AgnusBusArbitrationMode.Legacy ||
            options.CopperQuiescentFastPath || options.CopperQuiescentFastPathVerify || options.CopperQuiescentDiagnostics ||
            options.DeferredCpuBusBatchConfigured || options.DeferredCpuChipWriteJournalConfigured ||
            options.DeferredCpuChipReadSegmentsConfigured || options.DeferredCpuChipInstructionFetchBatchConfigured ||
            options.DeferredCpuChipInstructionFetchShadowConfigured || options.DeferredCpuCustomPointerWritesConfigured ||
            options.DeferredCpuCustomCompositionWritesConfigured || options.CpuWaitSlotReference || !options.HardwareSpecialization)
            return "Legacy execution overrides do not apply to Lightweight.";
        if (options.Input.IsMousePort(1)) return "Lightweight currently supports a mouse on port 1 only.";
        return null;
    }

    private static LightweightAgnusModel GetAgnusModel(DmaChipModel model) => model switch
    {
        DmaChipModel.AgaAlice => LightweightAgnusModel.Mos8374Alice,
        DmaChipModel.OcsAgnus => LightweightAgnusModel.Mos8371,
        DmaChipModel.Agnus8372A => LightweightAgnusModel.Mos8372A,
        DmaChipModel.Agnus8375Pal2M => LightweightAgnusModel.Mos8375Pal2M,
        _ => throw new NotSupportedException("Select an explicit supported PAL Agnus: 8371, 8372A, 8375 (318069-10) or A1200 Alice.")
    };

    internal static Copper68k.M68kCpuModel GetCpuModel(M68kBackendKind backend) => backend switch
    {
        M68kBackendKind.AccurateM68000 => Copper68k.M68kCpuModel.M68000,
        M68kBackendKind.AccurateM68010 => Copper68k.M68kCpuModel.M68010,
        M68kBackendKind.AccurateM68EC020 => Copper68k.M68kCpuModel.M68EC020,
        M68kBackendKind.AccurateM68020 => Copper68k.M68kCpuModel.M68020,
        M68kBackendKind.AccurateM68030 => Copper68k.M68kCpuModel.M68030,
        M68kBackendKind.AccurateM68040 => Copper68k.M68kCpuModel.M68040,
        M68kBackendKind.JitM68040 => Copper68k.M68kCpuModel.M68040,
        M68kBackendKind.AccurateM68060 => Copper68k.M68kCpuModel.M68060,
        _ => throw new NotSupportedException($"CPU backend {backend} is unavailable in Lightweight.")
    };

    private static void ValidateInput(CopperScreenInputOptions input)
    {
        if (input.IsMousePort(1))
            throw new NotSupportedException("Lightweight currently supports a mouse on port 1 only.");
    }

    public int Width => _nativeWidth;
    public int Height => _nativeHeight * 2;
    public int[] Framebuffer { get; private set; } = [];
    public int AudioSampleRate => 48_000;
    public double VideoVBlankHz => _machine.VideoVBlankHz;
    internal static CopperScreenPresentationGeometry NativePresentationGeometry =>
        new(356, 285, 320, 256, 2, 2, true, false)
        {
            // The engine stores the complete beam: visible samples start at
            // lowres x=98, line=26. Standard PAL DIW is ($81,$2C), 320x256.
            FullViewport = new(196, 52, 712, 570),
            StandardViewport = new(258, 88, 640, 512)
        };
    public CopperScreenPresentationGeometry PresentationGeometry
    {
        get
        {
            if (!_machine.ProgrammableBeamEnabled && Width == 908 && Height == 626) return NativePresentationGeometry;
            var scale = _machine.DeniseModel != LightweightDeniseModel.Mos8362 ? 4 : 2;
            var variable = _machine.ProgrammableBeamEnabled;
            var productivity = variable && Width == 912 && Height is 1050 or 1052 && scale == 4;
            var pal = Height >= 626;
            var x = variable ? 0 : 98 * scale;
            var y = variable ? 0 : pal ? 52 : 42;
            return new(Width / scale - x / scale, Height / 2 - y / 2,
                Math.Min(320, Width / scale), Math.Min(pal ? 256 : 200, Height / 2), scale, 2, pal, scale == 4)
            { // Standard 31 kHz Productivity samples are square before progressive row doubling.
              HorizontalPixelAspectOverride = productivity ? (IsInterlaced ? 1.0 : 2.0) : null,
              FullViewport = new(x, y, Width - x, Height - y),
              StandardViewport = variable ? new(0, 0, Width, Height) :
                new(129 * scale, 88, Math.Min(320 * scale, Width - 129 * scale), Math.Min(pal ? 512 : 400, Height - 88)) };
        }
    }
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
    internal string? WorkspaceSeedPairId => _workspace?.PairId;

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
            _machine.SetDriveWriteProtected(i, true);
            _pendingAdf[i] = null;
            StatusText = $"Lightweight: DF{i} inserted " + CopperScreenDiskImageArchive.GetDisplayName(_diskPaths[i]);
        }
        var field = _machine.IsLongField ? 0 : 1;
        _machine.ExecuteFrame();
        if (_workspace != null && !_discardWorkspaceChanges && ++_checkpointFields >= 50)
        {
            _checkpointFields = 0;
            try
            {
                if (_workspace.Checkpoint(_machine) && _workspaceSaveFailed)
                { _workspaceSaveFailed = false; SetStatusText("CopperOS workspace - changes saved automatically"); }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { _workspaceSaveFailed = true; SetStatusText("CopperOS changes are not saved: " + ex.Message); }
        }
        if (_minimalBoot is { } boot && _machine.Cpu.ProgramCounter >= boot.FaultAddress && _machine.Cpu.ProgramCounter < boot.FaultAddress + 4)
        {
            var address = (int)CopperScreenMinimalBoot.StateAddress;
            var ram = _machine.ChipRam.Span;
            CaptureFatalException(new NotSupportedException(CopperScreenMinimalBoot.DescribeFault(
                System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(ram.Slice(address, 2)),
                System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(ram.Slice(address + 4, 4)),
                System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(ram.Slice(address + 52, 4)))));
            throw new NotSupportedException(StatusText);
        }
        if (_machine.UnsupportedActiveFeature is { } feature)
        {
            CaptureFatalException(new NotSupportedException("Lightweight unsupported: " + feature));
            throw new NotSupportedException(StatusText);
        }
        // Preserve all native pixels. Vertical duplication/weaving is host presentation only.
        var interlaced = _machine.InterlaceEnabled;
        var pixels = _machine.Framebuffer.Span;
        if (_nativeWidth != _machine.FramebufferWidth || _nativeHeight != _machine.FramebufferHeight)
        {
            _nativeWidth = _machine.FramebufferWidth;
            _nativeHeight = _machine.FramebufferHeight;
            _haveFrame = false;
        }
        if (Framebuffer.Length != Width * Height)
        {
            Framebuffer = new int[Width * Height];
            _haveFrame = false;
        }
        for (var y = 0; y < _machine.FramebufferHeight; y++)
        {
            var row = pixels.Slice(y * Width, Width);
            row.CopyTo(Framebuffer.AsSpan((y * 2 + (interlaced ? field : 0)) * Width, Width));
            if (!interlaced || !_haveFrame || !IsInterlaced)
                row.CopyTo(Framebuffer.AsSpan((y * 2 + (interlaced ? 1 - field : 1)) * Width, Width));
        }
        IsInterlaced = interlaced;
        CompletedInterlaceField = field;
        _haveFrame = _audioAvailable = true;
        if (!ReferenceEquals(destination, Framebuffer) && destination.Length == Framebuffer.Length) Framebuffer.CopyTo(destination, 0);
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
        if (_workspace != null && drive == 0) return Reject("Close this CopperOS session before changing its workspace disk.");
        if ((uint)drive >= (uint)_machine.FloppyDriveCount) return Reject("Floppy drive is not connected.");
        if (disk.Format == LightweightFloppyFormat.Adf && !Path.GetExtension(disk.Name).Equals(".adf", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("ADF media must have an .adf filename.");
        if (disk.RequiresHighDensityDrive && _driveTypes[drive] != LightweightFloppyDriveType.HighDensity)
            return Reject($"DF{drive} is a DD drive. Select HD in Floppy Drives settings and restart the Amiga before inserting HD media.");
        var adf = disk; // Fully prepared by the loader before changing the existing medium.
        if (_machine.IsDriveDirty(drive)) return Reject($"DF{drive} has unsaved changes. Save ADF or discard and eject it before replacing the disk.");
        if (markChanged)
        {
            _machine.EjectAdf(drive);
            _pendingAdf[drive] = adf;
            _insertDelay[drive] = 25;
        }
        else { adf.Mount(_machine, drive); _pendingAdf[drive] = null; }
        _machine.SetDriveWriteProtected(drive, true);
        _diskPaths[drive] = path;
        StatusText = $"Lightweight: DF{drive} " + (markChanged ? "swapping " : "inserted ") + CopperScreenDiskImageArchive.GetDisplayName(path);
        return true;
    }

    public bool EjectDisk(int drive)
    {
        if (_workspace != null && drive == 0) return Reject("Close this CopperOS session before changing its workspace disk.");
        if ((uint)drive >= (uint)_machine.FloppyDriveCount) return Reject("Floppy drive is not connected.");
        if (_machine.IsDriveDirty(drive)) return Reject($"DF{drive} has unsaved changes. Save ADF or use Discard and eject.");
        return DiscardAndEjectDisk(drive);
    }

    public bool DiscardAndEjectDisk(int drive)
    {
        if (_workspace != null && drive == 0) return Reject("Close this CopperOS session before changing its workspace disk.");
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
        if (!writeProtected && _pendingAdf[drive] != null) return Reject("Wait for the disk swap to finish before enabling writes.");
        if (_copperStart && !writeProtected && _workspace == null) return Reject("CopperStart writes require a user-owned CopperOS workspace. The seed stays read-only.");
        if (!writeProtected && (_pendingAdf[drive]?.Format ?? _machine.GetDriveFormat(drive)) is not (LightweightFloppyFormat.None or LightweightFloppyFormat.Adf))
            return Reject("This disk format is permanently read-only.");
        _machine.SetDriveWriteProtected(drive, writeProtected);
        SetStatusText($"DF{drive}: " + (writeProtected ? "write protected" : "writable in memory; use Save ADF to keep changes"));
        return true;
    }

    internal bool OwnsWorkspacePath(string path) => _workspace?.ImagePath.Equals(Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase) == true;
    internal void ExportWorkspaceBackup(string destination)
    {
        if (_workspace == null) throw new NotSupportedException("Start a CopperOS workspace before exporting a workspace backup.");
        _workspace.ExportBackup(destination, ExportAdf(0));
    }
    internal bool IsInsideWorkspace(string path) => _workspace != null && Path.GetFullPath(path).StartsWith(
        _workspace.DirectoryPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    public byte[] ExportAdf(int drive)
    {
        if (_workspace == null || drive != 0) return _machine.ExportAdf(drive);
        var paused = IsPaused;
        try
        {
            IsPaused = false;
            for (var n=0;n<6000;n++)
            {
                if (_machine.DriveState is not ({ActiveDma:true} or {MotorOn:true}))
                {
                    var bytes = _machine.ExportAdf(0);
                    try { CopperOsWorkspace.ValidateImage(bytes); return bytes; } catch (InvalidDataException) { }
                }
                if (_faulted) break;
                RenderNextFrame(Framebuffer);
            }
            throw new IOException("CopperOS disk activity is unfinished. Keep the session open and retry after the guest finishes writing.");
        }
        finally { IsPaused=paused; }
    }
    // Exporting elsewhere must not acknowledge an unsaved primary workspace.
    public void MarkAdfSaved(int drive) { if (_workspace == null || drive != 0) _machine.MarkAdfSaved(drive); }
    public void DiscardPersistentStorage() { _discardWorkspaceChanges = true; } // Direct host writes already made cannot be rolled back.
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
        _nativeWidth = _machine.FramebufferWidth; _nativeHeight = _machine.FramebufferHeight;
        Framebuffer = new int[Width * Height];
        Array.Fill(Framebuffer, unchecked((int)0xFF000000));
        ApplyInput();
        StatusText = _workspace != null ? "CopperOS workspace - changes saved automatically"
            : _minimalBoot != null ? "Lightweight A500 — minimal disk boot (experimental; no DOS or Workbench)"
            : _copperStart ? CopperScreenCopperStartRom.DescriptionFor(_machine.IsAga)
            : _machine.IsAga ? "Lightweight experimental A1200 — native Kickstart 3.0, initial AGA display"
            : "Lightweight experimental A500 — native Kickstart, ADF / read-only IPF / CopperHDF";
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
    public void KeyDown(AmigaRawKey key) => _machine.SetKeyState((byte)key, true);
    public void KeyUp(AmigaRawKey key) => _machine.SetKeyState((byte)key, false);
    public bool ConsumeCopperBenchRequest() => false;
    public bool LaunchCopperBenchPath(string path, out string message)
    { message = "Legacy CopperBench host services are unavailable. Standalone CopperStart uses guest floppy services only."; return Reject(message); }
    public void QueueHostClipboardText(string text) => SetStatusText("Host clipboard integration is not available in Lightweight.");
    public void QueueHostClipboardImage(ClipboardImage image) => SetStatusText("Host clipboard integration is not available in Lightweight.");
    public bool TryTakeHostClipboardText(out string text) { text = string.Empty; return false; }
    public bool TryTakeHostClipboardImage(out ClipboardImage? image) { image = null; return false; }
    public void SavePersistentStorage()
    {
        _machine.FlushPersistentStorage();
        if (_workspace == null) return;
        var paused = IsPaused;
        try
        {
            IsPaused = false;
            for (var n = 0; n < 6000; n++)
            {
                if (_workspace.Checkpoint(_machine)) return;
                if (_faulted) break;
                RenderNextFrame(Framebuffer);
            }
            throw new IOException("CopperOS disk activity did not finish. The last complete workspace is retained; keep the app open and retry after the guest finishes writing.");
        }
        finally { IsPaused = paused; }
    }
    public void Dispose()
    {
        if (_disposed) return;
        // On failure retain both machine and lease so callers can retry/export.
        if (!_discardWorkspaceChanges) SavePersistentStorage();
        try { _machine.Dispose(); }
        finally { _workspace?.Dispose(); _disposed = true; }
    }
}
