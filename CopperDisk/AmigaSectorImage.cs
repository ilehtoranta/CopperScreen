using System.Buffers.Binary;
namespace CopperDisk;

/// <summary>A read-only, protected against concurrent writers, sector image for desktop disk tools.</summary>
/// <remarks>Single-consumer API. Supports raw ADF/HDF, RDB partitions and read-only IPF/DMS; contains no filesystem implementation.</remarks>
public sealed class AmigaSectorImage : IDisposable
{
    private readonly FileStream _stream;
    private readonly ValidatedSectorImage? _decoded;
    private readonly byte[]? _dmsSectors;
    private readonly DmsArchive? _dmsArchive;

    private AmigaSectorImage(FileStream stream) => _stream = stream;
    private AmigaSectorImage(FileStream stream, ValidatedSectorImage decoded) => (_stream, _decoded) = (stream, decoded);
    private AmigaSectorImage(FileStream stream, byte[] sectors, DmsArchive archive) => (_stream, _dmsSectors, _dmsArchive) = (stream, sectors, archive);

    /// <summary>Whether decoded sectors must be encoded as DMS before publication.</summary>
    public bool IsDms => _dmsArchive != null;
    /// <summary>Container-level write restriction, independent of host file permissions.</summary>
    public string? WriteUnavailableReason => _decoded != null ? "IPF images are read-only." : _dmsArchive?.WriteUnavailableReason;

    /// <summary>Encode and verify a DMS replacement, preserving archive header metadata. Does not publish it.</summary>
    public void WriteDmsReplacement(Stream sectors, Stream destination, CancellationToken cancellationToken = default)
    {
        if (_dmsArchive == null) throw new InvalidOperationException("This image is not a DMS archive.");
        _dmsArchive.WriteReplacement(sectors, destination, cancellationToken);
    }

    /// <summary>Open a complete, unencrypted standard DD DMS archive as read-only sectors.</summary>
    public static AmigaSectorImage OpenDms(string path)
    {
        var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            if (source.Length > 256 * 1024 * 1024) throw new InvalidDataException("DMS archive exceeds the 256 MiB decoding limit.");
            var bytes = new byte[checked((int)source.Length)];
            source.ReadExactly(bytes);
            byte[] sectors;
            try { sectors = DmsDecoder.Decode(bytes); }
            catch (AmigaDiskException ex) { throw new InvalidDataException(ex.Message, ex); }
            var image = new AmigaSectorImage(source, sectors, new DmsArchive(bytes));
            image.Volumes = Array.AsReadOnly(new[] { new AmigaImageVolume(image, "DMS", 0, sectors.LongLength, 512, 2, null, null) });
            return image;
        }
        catch { source.Dispose(); throw; }
    }

    /// <summary>Open IPF preservation media as a strictly validated read-only sector view.</summary>
    public static AmigaSectorImage OpenIpf(string path)
    {
        var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            if (source.Length > 256 * 1024 * 1024) throw new InvalidDataException("IPF image exceeds the 256 MiB decoding limit.");
            var bytes = new byte[checked((int)source.Length)]; source.ReadExactly(bytes);
            var ipf = IpfDecoder.Decode(bytes);
            if (ipf.MinCylinder < 0 || ipf.MaxCylinder > 83 || ipf.MinHead < 0 || ipf.MaxHead > 1) throw new NotSupportedException("Unsupported IPF geometry.");
            var tracks = new IAmigaTrack[(Math.Max(79, ipf.MaxCylinder) + 1) * 2];
            Array.Fill<IAmigaTrack>(tracks, AmigaEncodedTrack.FromBytes(AmigaDosTrackEncoder.CreateUnformattedTrack()));
            var seen = new HashSet<int>();
            foreach (var track in ipf.Tracks)
            {
                if (track.Cylinder is < 0 or > 83 || track.Head is < 0 or > 1) throw new InvalidDataException("Invalid IPF track coordinates.");
                var number = checked(track.Cylinder * 2 + track.Head);
                if ((uint)number >= tracks.Length || !seen.Add(number)) throw new InvalidDataException("Duplicate or invalid IPF track.");
                tracks[number] = track;
            }
            var decoded = AmigaDosTrackDecoder.DecodeValidated(tracks);
            var image = new AmigaSectorImage(source, decoded);
            image.Volumes = Array.AsReadOnly(new[] { new AmigaImageVolume(image, "IPF", 0, decoded.SectorCount * 512L, 512, 2, null, null) });
            return image;
        }
        catch { source.Dispose(); throw; }
    }

    /// <summary>Discovered volumes, including invalid extents with an explanation.</summary>
    public IReadOnlyList<AmigaImageVolume> Volumes { get; private set; } = [];

    /// <summary>Open an existing sector image without permitting concurrent writers.</summary>
    public static AmigaSectorImage Open(string path)
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var image = new AmigaSectorImage(stream);
        try
        {
            if (stream.Length < 3 * 512 || stream.Length % 512 != 0)
                throw new InvalidDataException("Choose a sector-based ADF or HDF image with complete 512-byte sectors.");
            var rdbMarker = false;
            var probe = new byte[512];
            for (var i = 0; i < Math.Min(16, stream.Length / 512); i++)
            {
                image.Read(i * 512L, probe);
                rdbMarker |= BinaryPrimitives.ReadUInt32BigEndian(probe) == 0x5244534B;
            }
            if (rdbMarker)
            {
                using var hardfile = AmigaHardfile.Open(new(0, path, readOnly: true,
                    mountMode: AmigaHardfileMountMode.RigidDiskBlock));
                var volumes = new List<AmigaImageVolume>();
                foreach (var partition in hardfile.GetMountablePartitions())
                {
                    var e = partition.Environment;
                    long start = 0, length = 0;
                    var blockSize = 512;
                    string? error = null;
                    try
                    {
                        var sectorBytes = checked((long)e[1] * 4);
                        var cylinderBytes = checked(sectorBytes * e[3] * e[5]);
                        if (e[0] < 16 || e[2] != 0 || cylinderBytes == 0 || e[10] < e[9])
                            throw new InvalidDataException("Unsupported partition geometry.");
                        start = checked(cylinderBytes * e[9]);
                        length = checked(cylinderBytes * ((long)e[10] - e[9] + 1));
                        blockSize = checked((int)(sectorBytes * e[4]));
                    }
                    catch (Exception ex) when (ex is OverflowException or InvalidDataException)
                    { error = "Invalid or unsupported partition geometry."; }
                    volumes.Add(new(image, partition.DeviceName, start, length, blockSize, e[6], e.DosType, error));
                }
                if (volumes.Count == 0) throw new InvalidDataException("The RDB contains no mountable partitions.");
                image.Volumes = volumes.AsReadOnly();
            }
            else image.Volumes = Array.AsReadOnly(new[] { new AmigaImageVolume(image, "Image", 0, stream.Length, 512, 2, null, null) });
            return image;
        }
        catch { stream.Dispose(); throw; }
    }

    internal long Length => _dmsSectors?.LongLength ?? (_decoded == null ? _stream.Length : _decoded.SectorCount * 512L);
    /// <summary>Copy protected raw/decoded sectors into an empty working file, using sparse
    /// host storage when supported. Returns whether sparse storage was enabled.</summary>
    public bool CopyToSparse(FileStream destination, CancellationToken cancellationToken = default)
    {
        if (WriteUnavailableReason is { } reason) throw new NotSupportedException(reason);
        if (_dmsSectors != null)
        {
            using var source = new MemoryStream(_dmsSectors, writable: false);
            return AmigaSparseFile.Copy(source, destination, cancellationToken);
        }
        _stream.Position = 0;
        return AmigaSparseFile.Copy(_stream, destination, cancellationToken);
    }
    /// <summary>Copy protected raw/decoded sectors to an unpublished working file.</summary>
    public void CopyTo(Stream destination, CancellationToken cancellationToken = default)
    {
        if (WriteUnavailableReason is { } reason) throw new NotSupportedException(reason);
        if (_dmsSectors != null) { destination.WriteAsync(_dmsSectors, cancellationToken).AsTask().GetAwaiter().GetResult(); return; }
        _stream.Position = 0;
        _stream.CopyToAsync(destination, 65536, cancellationToken).GetAwaiter().GetResult();
    }
    internal void Read(long offset, Span<byte> data)
    {
        if (_dmsSectors != null) { _dmsSectors.AsSpan(checked((int)offset), data.Length).CopyTo(data); return; }
        if (_decoded != null) { _decoded.Read(offset, data); return; }
        _stream.Position = offset;
        _stream.ReadExactly(data);
    }

    /// <summary>Close the image and release its writer exclusion.</summary>
    public void Dispose() => _stream.Dispose();
}

/// <summary>A volume-relative byte view and mount geometry; filesystem contents are interpreted by the consumer.</summary>
public sealed class AmigaImageVolume
{
    private readonly AmigaSectorImage _image;
    /// <summary>Device name from the partition table, or Image for a raw volume.</summary>
    public string DeviceName { get; }
    /// <summary>Start of this volume in the image.</summary>
    public long ByteOffset { get; }
    /// <summary>Length of this volume's byte range.</summary>
    public long Length { get; }
    /// <summary>Filesystem block size in bytes.</summary>
    public uint BlockSize { get; }
    /// <summary>Reserved blocks from the mount geometry.</summary>
    public uint ReservedBlocks { get; }
    /// <summary>Declared RDB DOS type, or null for a raw image.</summary>
    public uint? DosType { get; }
    /// <summary>Reason when this extent cannot be read with the declared geometry.</summary>
    public string? UnsupportedReason { get; }

    internal AmigaImageVolume(AmigaSectorImage image, string device, long start, long length, int blockSize,
        uint reserved, uint? dosType, string? error)
    {
        (_image, DeviceName, ByteOffset, Length, BlockSize, ReservedBlocks, DosType) =
            (image, device, start, length, (uint)blockSize, reserved, dosType);
        if (error == null && (blockSize < 512 || blockSize > 65536 || (blockSize & (blockSize - 1)) != 0 ||
            start < 0 || length <= 0 || start > image.Length - length || length % blockSize != 0 ||
            length / blockSize > uint.MaxValue || reserved < 1 || reserved >= length / blockSize - 1))
            error = "Invalid or unsupported volume geometry.";
        UnsupportedReason = error;
    }

    /// <summary>Read exactly within this volume. Single-consumer access, including sibling volumes.</summary>
    public void Read(long offset, Span<byte> destination)
    {
        if (UnsupportedReason != null) throw new InvalidDataException(UnsupportedReason);
        if (offset < 0 || offset > Length - destination.Length) throw new ArgumentOutOfRangeException(nameof(offset), "Read outside the volume range.");
        _image.Read(ByteOffset + offset, destination);
    }
}
