using System.IO.Compression;
using CopperMod.Amiga.Lightweight;

namespace CopperScreen;

// Owned mount-time media. Retains the existing internal name for host API compatibility.
internal sealed class CopperScreenAdfImage
{
    public const int StandardAdfSize = 901120;
    internal const int MaximumIpfSize = 32 * 1024 * 1024;
    private CopperScreenAdfImage(byte[] data, string name, LightweightIpfImage? ipf = null)
        => (Data, Name, Ipf) = (data, name, ipf);
    public byte[] Data { get; }
    public string Name { get; }
    public LightweightIpfImage? Ipf { get; }
    public LightweightFloppyFormat Format => Ipf is null ? LightweightFloppyFormat.Adf : LightweightFloppyFormat.Ipf;
    public bool CanWrite => Ipf is null;
    public static CopperScreenAdfImage FromAdfBytes(byte[] data, string name)
    {
        if (data.Length != StandardAdfSize)
            throw new NotSupportedException("Lightweight requires a complete standard 880 KiB ADF.");
        _ = CopperDisk.AmigaDiskLoader.FromAdfBytes(data);
        return new((byte[])data.Clone(), name);
    }
    public static CopperScreenAdfImage FromBytes(byte[] data, string name)
    {
        try { return Path.GetExtension(name).Equals(".ipf", StringComparison.OrdinalIgnoreCase)
            ? new((byte[])data.Clone(), name, LightweightIpfImage.Prepare(data))
            : Path.GetExtension(name).Equals(".adf", StringComparison.OrdinalIgnoreCase)
                ? FromAdfBytes(data, name)
                : throw new NotSupportedException("Choose a standard ADF or preserved IPF image."); }
        catch (Exception ex) when (ex is CopperDisk.IpfDecodeException or OverflowException)
        { throw new InvalidDataException("Invalid IPF: " + ex.Message, ex); }
    }
    public void Mount(LightweightA500Machine machine, int drive)
    {
        if (Ipf is { } ipf) machine.MountIpf(drive, ipf);
        else machine.MountAdf(drive, Data);
    }
    internal static bool IsSupported(string name) => Path.GetExtension(name).ToLowerInvariant() is ".adf" or ".ipf";
    internal static CopperScreenAdfImage Read(Stream input, long length, string name)
    {
        if (length <= 0 || length > MaximumIpfSize) throw new NotSupportedException("Floppy image is empty or exceeds 32 MiB.");
        var data = new byte[(int)length];
        input.ReadExactly(data);
        return FromBytes(data, name);
    }
    public static CopperScreenAdfImage Load(string path)
    {
        if (Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using var archive = ZipFile.OpenRead(path);
            var entries = archive.Entries.Where(e => !string.IsNullOrEmpty(e.Name) && IsSupported(e.Name)).ToArray();
            if (entries.Length != 1)
                throw new NotSupportedException("Select one ADF or IPF entry explicitly from this ZIP archive.");
            using var input = entries[0].Open();
            return Read(input, entries[0].Length, entries[0].Name);
        }
        if (!IsSupported(path)) throw new NotSupportedException("Choose an ADF, IPF or ZIP file.");
        using var stream = File.OpenRead(path);
        return Read(stream, stream.Length, Path.GetFileName(path));
    }
}
