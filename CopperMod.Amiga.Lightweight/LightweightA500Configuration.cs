using CopperDisk;

namespace CopperMod.Amiga.Lightweight;

/// <summary>Fixed configuration for the first lightweight engine milestone.</summary>
public sealed record LightweightA500Configuration
{
    /// <summary>Connected DD drives, DF0 through DF3 (1–4). Media starts write protected.</summary>
    public int FloppyDriveCount { get; init; } = 1;
    /// <summary>File-backed CopperHDF units, attached at construction. Changes require a new machine.</summary>
    public IReadOnlyList<AmigaHardfileConfiguration> Hardfiles { get; init; } = [];
    public int ChipRamBytes { get; init; } = 512 * 1024;
    public int SlowRamBytes { get; init; } = 512 * 1024;
    /// <summary>908 retains every OCS hires pixel; 454 is a lowres-only output contract.</summary>
    public int FramebufferWidth { get; init; } = 454;
    public int FramebufferHeight { get; init; } = 313;
    public int AudioSampleRate { get; init; } = 48_000;
    public int AudioChannels { get; init; } = 2;
}
