using System.Security.Cryptography;

namespace CopperScreen;

internal static class CopperScreenBundledMiniRom
{
    private const string HashResource = "CopperScreen.Firmware.CopperStartMini.sha256";
    private const string InvalidRomMessage = "ROM/CopperStart-Mini.rom does not match the qualified CopperStart Mini ROM. Restore it from the release package.";
    private static readonly string RomPath = Path.Combine(AppContext.BaseDirectory, "ROM", "CopperStart-Mini.rom");
    private static readonly bool Enabled = typeof(CopperScreenBundledMiniRom).Assembly.GetManifestResourceInfo(HashResource) != null;

    internal const string UnavailableMessage = "CopperStart Mini is unavailable. Re-extract a release that includes it or choose an original Kickstart ROM.";
    internal static bool Included => Enabled && File.Exists(RomPath);

    internal static byte[] Read()
    {
        using var hash = typeof(CopperScreenBundledMiniRom).Assembly.GetManifestResourceStream(HashResource)
            ?? throw new InvalidOperationException(UnavailableMessage);
        using var reader = new StreamReader(hash);
        var expectedHash = reader.ReadToEnd().Trim();
        using var stream = File.OpenRead(RomPath);
        const int romSize = 512 * 1024;
        if (stream.Length != romSize) throw new InvalidOperationException(InvalidRomMessage);
        var rom = new byte[romSize];
        stream.ReadExactly(rom);
        if (!Convert.ToHexString(SHA256.HashData(rom)).Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(InvalidRomMessage);
        return rom;
    }
}
