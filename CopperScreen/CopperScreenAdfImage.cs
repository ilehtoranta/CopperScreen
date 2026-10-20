using System.IO.Compression;
using CopperMod.Amiga.Lightweight;

namespace CopperScreen;

// Owned mount-time media. Retains the existing internal name for host API compatibility.
internal sealed class CopperScreenAdfImage
{
    public const int StandardAdfSize = 901120;
    internal const int MaximumImageSize = 32 * 1024 * 1024;
    private CopperScreenAdfImage(byte[] data, string name, LightweightFloppyImage image)
        => (Data, Name, Image) = (data, name, image);
    public byte[] Data { get; }
    public string Name { get; }
    private LightweightFloppyImage Image { get; }
    public LightweightFloppyFormat Format => Image.Format;
    public bool RequiresHighDensityDrive => Image.IsHighDensity;
    public bool CanWrite => Image.CanWrite;
    public static CopperScreenAdfImage FromAdfBytes(byte[] data, string name)
    {
        if (data.Length != StandardAdfSize && data.Length != StandardAdfSize * 2)
            throw new NotSupportedException("Lightweight requires a complete 880 KiB DD or 1760 KiB HD ADF.");
        return new((byte[])data.Clone(), name, LightweightFloppyImage.Prepare(data, "disk.adf"));
    }
    public static CopperScreenAdfImage FromBytes(byte[] data, string name)
    {
        try { return new((byte[])data.Clone(), name, LightweightFloppyImage.Prepare(data, name)); }
        catch (Exception ex) when (ex is CopperDisk.IpfDecodeException or CopperDisk.ScpDecodeException or CopperDisk.AmigaDiskException or OverflowException)
        { throw new InvalidDataException("Invalid floppy image: " + ex.Message, ex); }
    }
    public void Mount(LightweightA500Machine machine, int drive)
    {
        machine.MountDisk(drive, Image);
    }
    internal static bool IsSupported(string name) => Path.GetExtension(name).ToLowerInvariant() is ".adf" or ".adz" or ".dms" or ".ipf" or ".scp";
    internal static CopperScreenAdfImage Read(Stream input, long length, string name)
    {
        if (length <= 0 || length > MaximumImageSize) throw new NotSupportedException("Floppy image is empty or exceeds 32 MiB.");
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
                throw new NotSupportedException("Select one supported floppy entry explicitly from this ZIP archive.");
            using var input = entries[0].Open();
            return Read(input, entries[0].Length, entries[0].Name);
        }
        if (!IsSupported(path)) throw new NotSupportedException("Choose an ADF, ADZ, DMS, IPF, SCP or ZIP file.");
        using var stream = File.OpenRead(path);
        return Read(stream, stream.Length, Path.GetFileName(path));
    }
}
