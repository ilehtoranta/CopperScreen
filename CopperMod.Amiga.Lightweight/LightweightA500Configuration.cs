using CopperDisk;
using Copper68k;

namespace CopperMod.Amiga.Lightweight;

/// <summary>Configuration for the PAL OCS machine.</summary>
public sealed record LightweightA500Configuration
{
    /// <summary>68000, or an experimental 68EC020/68020 OCS accelerator.
    /// The accelerator runs at two native clocks per motherboard clock.</summary>
    public M68kCpuModel CpuModel { get; init; } = M68kCpuModel.M68000;
    /// <summary>Connected DD drives, DF0 through DF3 (1–4). Media starts write protected.</summary>
    public int FloppyDriveCount { get; init; } = 1;
    /// <summary>File-backed CopperHDF units, attached at construction. Changes require a new machine.</summary>
    public IReadOnlyList<AmigaHardfileConfiguration> Hardfiles { get; init; } = [];
    public int ChipRamBytes { get; init; } = 512 * 1024;
    public int SlowRamBytes { get; init; } = 512 * 1024;
    /// <summary>Optional CPU-only Zorro II RAM: 0, 512 KiB, 1, 2, 4 or 8 MiB.
    /// Kickstart assigns its address through Autoconfig. Changes require a new machine.</summary>
    public int FastRamBytes { get; init; }
    /// <summary>908 retains every OCS hires pixel; 454 is a lowres-only output contract.</summary>
    public int FramebufferWidth { get; init; } = 454;
    public int FramebufferHeight { get; init; } = 313;
    public int AudioSampleRate { get; init; } = 48_000;
    public int AudioChannels { get; init; } = 2;
}
