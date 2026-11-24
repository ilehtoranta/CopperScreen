using CopperMod.Amiga.Lightweight;

namespace CopperScreen;

internal sealed record CopperScreenCd32Launch(
    string KickstartPath, string? ExtendedRomPath, string? DiscPath, LightweightCd32Region Region, int FastRamKiB);
