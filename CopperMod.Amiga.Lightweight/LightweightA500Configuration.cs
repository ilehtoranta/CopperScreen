using CopperDisk;
using Copper68k;

namespace CopperMod.Amiga.Lightweight;

/// <summary>Configuration for a PAL OCS/ECS board or experimental Alice/Lisa A1200.</summary>
public sealed record LightweightA500Configuration
{
    /// <summary>68000/68010, or an experimental 68EC020/68020/68030/68040 accelerator.
    /// 020/030 use two native clocks per motherboard clock; 040 uses four with
    /// approximate fixed instruction timing. This does not model a physical card.</summary>
    /// <remarks>M68060 uses an eight-clock policy and supports FPU arithmetic and paging.
    /// It needs 060-aware OS task/FPU support; native Kickstart 1.3 initial task
    /// frames are incompatible. Other OS combinations need validation. Fault continuation and physical
    /// accelerator timing remain incomplete.</remarks>
    public M68kCpuModel CpuModel { get; init; } = M68kCpuModel.M68000;
    /// <summary>Optional 68040 JIT with interpreter fallback at unsupported instructions and MMU accesses.</summary>
    public M68kExecutionMode CpuExecutionMode { get; init; } = M68kExecutionMode.Interpreter;
    /// <summary>Connected drives, DF0 through DF3 (1–4). Media starts write protected.</summary>
    public int FloppyDriveCount { get; init; } = 1;
    /// <summary>Fitted drive types in DF0–DF3 order. Unspecified connected drives are DD.</summary>
    public IReadOnlyList<LightweightFloppyDriveType> FloppyDriveTypes { get; init; } = [];
    /// <summary>File-backed CopperHDF units, attached at construction. Changes require a new machine.</summary>
    public IReadOnlyList<AmigaHardfileConfiguration> Hardfiles { get; init; } = [];
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
    /// <summary>Initial raster width: 908 retains hires, 1816 retains ECS SuperHires; 454 is lowres-only.
    /// Guest ECS timing can change completed width/height; read machine dimensions with each field.</summary>
    public int FramebufferWidth { get; init; } = 454;
    public int FramebufferHeight { get; init; } = 313;
    public int AudioSampleRate { get; init; } = 48_000;
    public int AudioChannels { get; init; } = 2;
}
