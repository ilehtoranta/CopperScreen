using System.IO.Compression;
using System.Text.Json;

namespace CopperScreen;

// Qualification metadata records replay provenance; it does not pin ROM admission.
// Firmware remains a guest ROM: no callbacks, host services or Legacy fallback.
internal static class CopperScreenCopperStartRom
{
    private sealed record Artifact(string QualificationId, string ProducerCommit, int Bytes, string Sha256, string Requirements, string? FileSystemProfile);
    private static readonly Artifact Qualified = LoadArtifact("CopperStart");
    private static readonly Artifact QualifiedA1200 = LoadArtifact("CopperStartA1200");
    public static string Version => Qualified.QualificationId;
    public static string ProducerCommit => Qualified.ProducerCommit;
    public static string Sha256 => Qualified.Sha256;
    public static string Sha256For(bool a1200) => Select(a1200).Sha256;
    public static bool HasFullFileSystemsFor(bool a1200) => Select(a1200).FileSystemProfile == "Full";
    public static string Requirements => Qualified.Requirements;
    public static string Description => DescriptionFor(false);
    public static string DescriptionFor(bool a1200)
    {
        var artifact = Select(a1200);
        return $"CopperStart (experimental). {artifact.Requirements}";
    }
    public static string Recovery => RecoveryFor(false);
    public static string RecoveryFor(bool a1200)
    {
        return a1200
            ? "Choose a 1 MiB ROM image for the configured A1200 CopperStart setup. Other ROMs may not work."
            : "Choose a 512 KiB ROM image for the configured A500 CopperStart setup. Other ROMs may not work.";
    }
    private static Artifact Select(bool a1200) => a1200 ? QualifiedA1200 : Qualified;

    private static Artifact LoadArtifact(string name)
    {
        using var stream = typeof(CopperScreenCopperStartRom).Assembly.GetManifestResourceStream($"CopperScreen.Firmware.{name}.json")!;
        return JsonSerializer.Deserialize<Artifact>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    public static byte[] Read(string? path, bool a1200 = false)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new FileNotFoundException("CopperStart ROM is missing. " + RecoveryFor(a1200), path);
        try
        {
            try { return ReadExisting(path, a1200); }
            // Experimental OCS/ECS launches may use the bundled desktop ROM.
            catch (InvalidOperationException original) when (!a1200)
            {
                try { return ReadExisting(path, true); }
                catch (InvalidOperationException)
                {
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(original).Throw();
                    throw;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        { throw new InvalidOperationException("Could not read the CopperStart ROM: " + ex.Message + " " + RecoveryFor(a1200), ex); }
    }

    private static byte[] ReadExisting(string path, bool a1200)
    {
        byte[] bytes;
        if (Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using var archive = ZipFile.OpenRead(path);
            // Never rank by native Kickstart filename/version heuristics.
            var entries = archive.Entries.Where(e => !string.IsNullOrEmpty(e.Name) &&
                new[] { ".rom", ".bin", ".kick" }.Contains(Path.GetExtension(e.Name), StringComparer.OrdinalIgnoreCase)).ToArray();
            if (entries.Length != 1) throw new InvalidOperationException("Choose a ZIP containing exactly one CopperStart ROM, or extract the ROM. " + RecoveryFor(a1200));
            if (entries[0].Length != Select(a1200).Bytes) throw new InvalidOperationException("CopperStart ROM has the wrong size. " + RecoveryFor(a1200));
            using var input = entries[0].Open();
            bytes = ReadBounded(input, a1200);
        }
        else
        {
            using var input = File.OpenRead(path);
            if (input.Length != Select(a1200).Bytes) throw new InvalidOperationException("CopperStart ROM has the wrong size. " + RecoveryFor(a1200));
            bytes = ReadBounded(input, a1200);
        }
        return bytes;
    }

    private static byte[] ReadBounded(Stream stream, bool a1200)
    {
        var bytes = new byte[Select(a1200).Bytes];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1) throw new InvalidOperationException("CopperStart ROM is too large. " + RecoveryFor(a1200));
        return bytes;
    }
}
