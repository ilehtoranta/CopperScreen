using CopperDisk;

namespace CopperMod.Amiga.Lightweight;

/// <summary>Owned floppy media decoded and validated before attachment.</summary>
public sealed class LightweightFloppyImage
{
    internal byte[]? Sectors { get; }
    internal LightweightIpfImage? Tracks { get; }
    public LightweightFloppyFormat Format { get; }
    public bool CanWrite => Format == LightweightFloppyFormat.Adf;

    private LightweightFloppyImage(LightweightFloppyFormat format, byte[]? sectors, LightweightIpfImage? tracks)
        => (Format, Sectors, Tracks) = (format, sectors, tracks);

    /// <summary>Prepare ADF (standard or UAE-1ADF), ADZ, DMS, IPF or SCP bytes.
    /// A ZIP entry's filename may be supplied; when given an archive filename,
    /// identifies the extracted single image by signature. Does not unpack ZIPs.
    /// SCP uses CopperDisk's first-revolution DD decoding, not live flux playback.</summary>
    public static LightweightFloppyImage Prepare(ReadOnlySpan<byte> image, string name)
    {
        var extension = Path.GetExtension(name).ToLowerInvariant();
        if (extension == ".zip")
            extension = image.StartsWith("CAPS"u8) ? ".ipf" : image.StartsWith("DMS!"u8) ? ".dms" :
                image.StartsWith("SCP"u8) ? ".scp" : image.Length >= 2 && image[0] == 0x1F && image[1] == 0x8B ? ".adz" : ".adf";
        if (extension == ".ipf") return new(LightweightFloppyFormat.Ipf, null, LightweightIpfImage.Prepare(image));
        if (extension == ".scp")
        {
            var disk = ScpDecoder.Decode(image);
            var tracks = disk.Tracks.Select(t => (t.Cylinder, t.Head, (IAmigaTrack)t)).ToArray();
            return new(LightweightFloppyFormat.Scp, null, LightweightIpfImage.PrepareTracks(tracks, image));
        }
        var extended = image.StartsWith("UAE-1ADF"u8) || image.StartsWith("UAE--ADF"u8);
        if (extension == ".adf" && !extended && image.Length != AmigaDiskGeometry.StandardAdfSize)
            throw new NotSupportedException("ADF requires complete 880 KiB DD sectors or modern UAE-1ADF tracks.");
        IAmigaSectorDiskMedia media = extension switch
        {
            ".adf" when extended => AmigaDiskLoader.FromExtendedAdfBytes(image),
            ".adf" => AmigaDiskLoader.FromAdfBytes(image.ToArray()),
            ".adz" => AmigaDiskLoader.FromAdzBytes(image),
            ".dms" => AmigaDiskLoader.FromDmsBytes(image),
            _ => throw new NotSupportedException("Choose ADF, ADZ, DMS, IPF or SCP floppy media.")
        };
        var format = extension == ".adz" ? LightweightFloppyFormat.Adz :
            extension == ".dms" ? LightweightFloppyFormat.Dms :
            extended ? LightweightFloppyFormat.ExtendedAdf : LightweightFloppyFormat.Adf;
        if (media is IAmigaPreservedTrackDiskMedia)
        {
            var tracks = new List<(int Cylinder, int Head, IAmigaTrack Track)>();
            for (var cylinder = 0; cylinder < media.Cylinders; cylinder++)
                for (var head = 0; head < media.Heads; head++)
                    tracks.Add((cylinder, head, media.ReadTrack(cylinder, head)));
            return new(format, null, LightweightIpfImage.PrepareTracks(tracks, image));
        }
        if (!media.HasCompleteDecodedSectorData || media.SectorData.Length != AmigaDiskGeometry.StandardAdfSize)
            throw new NotSupportedException("Sector media must contain a complete 880 KiB DD image.");
        return new(format, media.SectorData.ToArray(), null);
    }
}
