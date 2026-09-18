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
        "CPU backend" => value == "AccurateM68000",
        "Kickstart" => value is "KickstartRom" or "Kickstart13Rom",
        "Connected" => value is "1" or "2" or "3" or "4",
        _ => true
    };

    public static bool IsControllerAvailable(int port, CopperScreenControllerKind kind)
        => port != 2 || kind != CopperScreenControllerKind.Mouse;

    public static string ChoiceLabel(string value) => value switch
    {
        "AccurateM68000" => "Motorola 68000",
        "AccurateM68EC020" => "Motorola 68EC020",
        "AccurateM68020" => "Motorola 68020",
        "AccurateM68030" => "Motorola 68030",
        "AccurateM68040" => "Motorola 68040",
        "JitM68040" => "Motorola 68040 (JIT)",
        "Kickstart13Rom" => "Kickstart 1.3 ROM",
        "KickstartRom" => "Native Kickstart ROM (1.3)",
        "DiagRom" => "Diagnostic ROM",
        "KeyboardJoystick" => "Keyboard joystick",
        _ => value
    };
}
