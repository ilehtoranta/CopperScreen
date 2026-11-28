using CopperDisk;
using Copper68k;

namespace CopperMod.Amiga.Lightweight;

public enum LightweightCd32Region { Pal, Ntsc }

/// <summary>Configuration for a Lightweight motherboard.</summary>
public sealed record LightweightA500Configuration
{
    /// <summary>Fitted stock CD32 board and oscillator. Null selects the existing floppy motherboard.</summary>
    public LightweightCd32Region? Cd32Region { get; init; }

    public static LightweightA500Configuration CreateCd32(LightweightCd32Region region = LightweightCd32Region.Pal) => new()
    {
        Cd32Region = region,
        AgnusModel = LightweightAgnusModel.Mos8374Alice,
        DeniseModel = LightweightDeniseModel.Lisa4203,
        CpuModel = M68kCpuModel.M68EC020,
        ChipRamBytes = 2 * 1024 * 1024,
        SlowRamBytes = 0,
        FloppyDriveCount = 0,
        FramebufferWidth = 1816,
        FramebufferHeight = region == LightweightCd32Region.Ntsc ? 263 : 313
    };

    /// <summary>68000/68010, or an experimental 68EC020/68020/68030/68040 accelerator.
    /// 020/030 use two native clocks per motherboard clock; 040 uses four with
    /// approximate fixed instruction timing. This does not model a physical card.</summary>
    /// <remarks>M68060 uses an eight-clock policy and supports FPU arithmetic and paging.
    /// It needs 060-aware OS task/FPU support; native Kickstart 1.3 initial task
    /// frames are incompatible. Other OS combinations need validation. Fault continuation and physical
    /// accelerator timing remain incomplete.</remarks>
    public M68kCpuModel CpuModel { get; init; } = M68kCpuModel.M68000;
    /// <summary>Boot a 68060 with its FPU disabled until guest software enables it.</summary>
    public bool DisableM68060FpuOnReset { get; init; }
    /// <summary>Optional 68040 JIT with interpreter fallback at unsupported instructions and MMU accesses.</summary>
    public M68kExecutionMode CpuExecutionMode { get; init; } = M68kExecutionMode.Interpreter;
    /// <summary>
    /// Number of CPU timing units per motherboard cycle for the 68040 JIT.
    /// Powers of two greater than one select experimental zero-wait Fast RAM and ROM.
    /// </summary>
    public int CpuClockMultiplier { get; init; } = 1;
    /// <summary>Connected drives, DF0 through DF3 (1–4). Stock CD32 has zero drives.</summary>
    public int FloppyDriveCount { get; init; } = 1;
    /// <summary>Fitted drive types in DF0–DF3 order. Unspecified connected drives are DD.</summary>
    public IReadOnlyList<LightweightFloppyDriveType> FloppyDriveTypes { get; init; } = [];
    /// <summary>
    /// Initial bit cell for deterministic PAL OCS standard DD ADF replay.
    /// When specified, the drive uses seven CCK per bit cell, rotates during
    /// spin-up and varies the synthetic read gap. Null retains nominal ADF timing.
    /// </summary>
    public int? StandardAdfReplayBitCell { get; init; }
    /// <summary>File-backed CopperHDF units, attached at construction. Changes require a new machine.</summary>
    public IReadOnlyList<AmigaHardfileConfiguration> Hardfiles { get; init; } = [];
    /// <summary>Optional synthetic timing per CopperHDF unit. Omitted units remain synchronous.</summary>
    public IReadOnlyDictionary<int, LightweightDriveTimingProfile> HardfileTimings { get; init; } =
        new Dictionary<int, LightweightDriveTimingProfile>();
    /// <summary>Optional read-only ATA master. Requires an A1200 profile.</summary>
    public LightweightIdeDriveConfiguration? IdeMaster { get; init; }
    /// <summary>Optional storage observations; absent from ordinary device execution.</summary>
    public ILightweightStorageObserver? StorageObserver { get; init; }
    /// <summary>Live host-folder DOS volumes. Attach/detach requires a new machine.</summary>
    public IReadOnlyList<LightweightHostDirectoryConfiguration> HostDirectories { get; init; } = [];
    public LightweightAgnusModel AgnusModel { get; init; } = LightweightAgnusModel.Mos8371;
    public LightweightDeniseModel DeniseModel { get; init; } = LightweightDeniseModel.Mos8362;
    /// <summary>Fitted Chip RAM, bounded by Agnus and the selected motherboard layout.</summary>
    public int ChipRamBytes { get; init; } = 512 * 1024;
    public int SlowRamBytes { get; init; } = 512 * 1024;
    /// <summary>Optional CPU-only RAM: 0, 512 KiB, 1, 2, 4 or 8 MiB (Zorro II),
    /// or 16, 32, 64, 128, 256, 512 or 1024 MiB (Zorro III, full 32-bit CPU).
    /// Kickstart assigns its address through Autoconfig. Changes require a new machine.</summary>
    public int FastRamBytes { get; init; }

    /// <summary>Maximum fitted Fast RAM bank for the CPU's physical address width.</summary>
    public static int MaximumFastRamBytes(M68kCpuModel model) => model switch
    {
        M68kCpuModel.M68000 or M68kCpuModel.M68010 or M68kCpuModel.M68EC020 => 8 * 1024 * 1024,
        M68kCpuModel.M68020 or M68kCpuModel.M68030 or M68kCpuModel.M68040 or M68kCpuModel.M68060 => 1024 * 1024 * 1024,
        _ => throw new ArgumentOutOfRangeException(nameof(model))
    };

    /// <summary>Whether a byte count is one of the implemented fitted bank sizes.</summary>
    public static bool IsSupportedFastRamSize(int bytes) => LightweightFastRam.IsSupportedSize(bytes);
    /// <summary>Optional Legacy-compatible A501 RTC at $DC0000; disabled by default.
    /// Host local time plus a guest-set offset, retained through resets/restarts.
    /// A new independent machine starts from host time; no battery file is stored.</summary>
    public bool RtcEnabled { get; init; }
    /// <summary>Optional owner-thread clock source for reproducible RTC callers.
    /// Defaults to DateTimeOffset.Now. Only sampled on RTC register access.</summary>
    public Func<DateTimeOffset>? RtcNowProvider { get; init; }
    /// <summary>Fit the RoboCop 3 dongle to the second controller port.</summary>
    public bool RoboCop3DongleEnabled { get; init; }
    /// <summary>Initial raster width: 908 retains hires, 1816 retains ECS SuperHires; 454 is lowres-only.
    /// Guest ECS timing can change completed width/height; read machine dimensions with each field.</summary>
    public int FramebufferWidth { get; init; } = 454;
    public int FramebufferHeight { get; init; } = 313;
    public int AudioSampleRate { get; init; } = 48_000;
    public int AudioChannels { get; init; } = 2;
}
