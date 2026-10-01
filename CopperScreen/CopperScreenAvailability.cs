namespace CopperScreen;

// Configuration-time checks only. The session remains the authority for supported hardware.
internal static class CopperScreenAvailability
{
    public const string NotYetAvailable = "Not yet available";

    public static string? GetUnavailableReason(CopperScreenSettingsDraft draft, string baseDirectory)
    {
        try
        {
            if (draft.Engine != CopperScreenEngine.Lightweight)
                return "Legacy engine and CopperStart are not yet available.";
            CopperScreenLightweightSession.Validate(draft.ToStartupOptions(baseDirectory), requireRomPath: false);
            return null;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException or FormatException or OverflowException)
        {
            return ex.Message;
        }
    }

    public static bool IsChoiceAvailable(string setting, string value) => setting switch
    {
        "Engine" => value == "Lightweight",
        "CPU backend" => value is "AccurateM68000" or "AccurateM68EC020" or "AccurateM68020" or "AccurateM68030" or "AccurateM68040",
        "Kickstart" => value is "KickstartRom" or "Kickstart13Rom" or "Kickstart31Rom",
        "Agnus" => value is "OcsAgnus" or "Agnus8372A" or "Agnus8375Pal2M" or "AgaAlice",
        "Connected" => value is "1" or "2" or "3" or "4",
        _ => true
    };

    public static bool IsControllerAvailable(int port, CopperScreenControllerKind kind)
        => port != 2 || kind != CopperScreenControllerKind.Mouse;

    public static string[] ChipRamChoices(DmaChipModel model) => model switch
    {
        DmaChipModel.AgaAlice => ["2048"],
        DmaChipModel.OcsAgnus => ["512"],
        DmaChipModel.Agnus8372A => ["512", "1024"],
        DmaChipModel.Agnus8375Pal2M => ["1024", "2048"],
        _ => ["512", "1024", "2048"]
    };

    public static string ChoiceLabel(string value) => value switch
    {
        "OcsAgnus" => "8371 · OCS PAL · 512 KiB",
        "Agnus8372A" => "8372A · ECS Agnus · up to 1 MiB",
        "Agnus8375Pal2M" => "8375 (318069-10) · ECS PAL · up to 2 MiB",
        "EcsAgnus" => "ECS Agnus (unspecified revision)",
        "AgaAlice" => "8374 · Alice · initial A1200 AGA",
        "AgaLisa" => "4203 · Lisa · initial AGA",
        "OcsDenise" => "8362 · OCS Denise",
        "EcsDenise" => "8373 · ECS Denise",
        "AccurateM68000" => "Motorola 68000",
        "AccurateM68EC020" => "Motorola 68EC020 (experimental)",
        "AccurateM68020" => "Motorola 68020 (experimental)",
        "AccurateM68030" => "Motorola 68030 (experimental)",
        "AccurateM68040" => "Motorola 68040 (experimental)",
        "AccurateM68060" => "Motorola 68060 (requires 060 OS support)",
        "JitM68040" => "Motorola 68040 (JIT)",
        "Kickstart13Rom" => "Kickstart 1.3 ROM",
        "Kickstart31Rom" => "Kickstart 3.1 ROM (A500)",
        "KickstartRom" => "Native Kickstart ROM (1.3, A500 3.1 or A1200 3.0)",
        "DiagRom" => "Diagnostic ROM",
        "KeyboardJoystick" => "Keyboard joystick",
        _ => value
    };
}
