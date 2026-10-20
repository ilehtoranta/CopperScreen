using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace CopperScreen;

// A consumer qualification ID, not a claim of a released producer version.
// Firmware remains a guest ROM: no callbacks, host services or Legacy fallback.
internal static class CopperScreenCopperStartRom
{
    private sealed record Artifact(string QualificationId, string ProducerCommit, int Bytes, string Sha256, string Requirements);
    private static readonly Artifact Qualified = LoadArtifact();
    public static string Version => Qualified.QualificationId;
    public static string ProducerCommit => Qualified.ProducerCommit;
    public static string Sha256 => Qualified.Sha256;
    public static string Requirements => Qualified.Requirements;
    public static string Description => $"CopperStart {Version} (experimental). {Requirements}";
    public static string Recovery => $"Choose the qualified CopperStart {Version} standalone ROM (SHA-256 {Sha256}). Older builds and native Kickstart ROMs cannot be used with this boot method.";

    private static Artifact LoadArtifact()
    {
        using var stream = typeof(CopperScreenCopperStartRom).Assembly.GetManifestResourceStream("CopperScreen.Firmware.CopperStart.json")!;
        return JsonSerializer.Deserialize<Artifact>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    public static byte[] Read(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new FileNotFoundException("CopperStart ROM is missing. " + Recovery, path);
        try { return ReadExisting(path); }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        { throw new InvalidOperationException("Could not read the CopperStart ROM: " + ex.Message + " " + Recovery, ex); }
    }

    private static byte[] ReadExisting(string path)
    {
        byte[] bytes;
        if (Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using var archive = ZipFile.OpenRead(path);
            // Never rank by native Kickstart filename/version heuristics.
            var entries = archive.Entries.Where(e => !string.IsNullOrEmpty(e.Name) &&
                new[] { ".rom", ".bin", ".kick" }.Contains(Path.GetExtension(e.Name), StringComparer.OrdinalIgnoreCase)).ToArray();
            if (entries.Length != 1) throw new InvalidOperationException("Choose a ZIP containing exactly one CopperStart ROM, or extract the ROM. " + Recovery);
            if (entries[0].Length != Qualified.Bytes) throw new InvalidOperationException("CopperStart ROM has the wrong size. " + Recovery);
            using var input = entries[0].Open();
            bytes = ReadBounded(input);
        }
        else
        {
            using var input = File.OpenRead(path);
            if (input.Length != Qualified.Bytes) throw new InvalidOperationException("CopperStart ROM has the wrong size. " + Recovery);
            bytes = ReadBounded(input);
        }
        if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("CopperStart ROM is damaged or is an unqualified version. " + Recovery);
        return bytes;
    }

    private static byte[] ReadBounded(Stream stream)
    {
        var bytes = new byte[Qualified.Bytes];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1) throw new InvalidOperationException("CopperStart ROM is too large. " + Recovery);
        return bytes;
    }
}
