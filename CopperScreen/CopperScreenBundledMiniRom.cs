namespace CopperScreen;

internal static class CopperScreenBundledMiniRom
{
    private const string ResourceName = "CopperScreen.Bundled.CopperStart-Mini.rom";
    internal static bool Included { get; } = typeof(CopperScreenBundledMiniRom).Assembly.GetManifestResourceInfo(ResourceName) != null;

    internal static byte[] Read()
    {
        using var stream = typeof(CopperScreenBundledMiniRom).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("This build does not include CopperStart Mini.");
        var rom = new byte[stream.Length];
        stream.ReadExactly(rom);
        return rom;
    }
}
