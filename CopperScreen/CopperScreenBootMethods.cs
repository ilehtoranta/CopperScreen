namespace CopperScreen;

// Product choices are independent of the native Kickstart version in a profile.
internal enum CopperScreenBootMethod { MinimalDiskBoot, KickstartRom, CopperStart }

internal static class CopperScreenBootMethods
{
    public static bool CopperStartEnabled =>
#if COPPERSCREEN_ENABLE_COPPERSTART
        true;
#else
        false;
#endif

    public static CopperScreenBootMethod Method(CopperScreenSettingsDraft draft) => draft.KickstartSource switch
    {
        CopperScreenKickstartSource.MinimalDiskBoot => CopperScreenBootMethod.MinimalDiskBoot,
        CopperScreenKickstartSource.CopperStart => CopperScreenBootMethod.CopperStart,
        _ => CopperScreenBootMethod.KickstartRom
    };

    // Preserve existing saved model-0..5 profiles; minimal boot gets its own slots.
    public static int Choice(CopperScreenMachineModel model, CopperScreenBootMethod method) => method switch
    {
        CopperScreenBootMethod.MinimalDiskBoot => 6 + (int)model,
        CopperScreenBootMethod.CopperStart => model == CopperScreenMachineModel.Amiga1200 ? 3 : 4 + (int)model,
        _ => (int)model
    };
}
