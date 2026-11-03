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

    public static string[] Choices => CopperStartEnabled
        ? ["MinimalDiskBoot", "KickstartRom", "CopperStart"] : ["MinimalDiskBoot", "KickstartRom"];

    public static CopperScreenBootMethod Method(CopperScreenSettingsDraft draft) => draft.KickstartSource switch
    {
        CopperScreenKickstartSource.MinimalDiskBoot => CopperScreenBootMethod.MinimalDiskBoot,
        CopperScreenKickstartSource.CopperStart => CopperScreenBootMethod.CopperStart,
        _ => CopperScreenBootMethod.KickstartRom
    };

    public static string Label(string method) => method switch
    {
        "MinimalDiskBoot" => "Minimal disk boot — Free",
        "KickstartRom" => "Original Kickstart ROM",
        "CopperStart" => "CopperStart ROM",
        _ => method
    };

    public static string? Unavailable(string method, CopperScreenMachineModel model) => method switch
    {
        _ when !Enum.IsDefined(model) => "Choose a supported Amiga model.",
        "CopperStart" when !CopperStartEnabled => "CopperStart is not enabled in this build.",
        _ => null
    };

    // Preserve existing saved model-0..5 profiles; minimal boot gets its own slots.
    public static int Choice(CopperScreenMachineModel model, CopperScreenBootMethod method) => method switch
    {
        CopperScreenBootMethod.MinimalDiskBoot => 6 + (int)model,
        CopperScreenBootMethod.CopperStart => model == CopperScreenMachineModel.Amiga1200 ? 3 : 4 + (int)model,
        _ => (int)model
    };
}
