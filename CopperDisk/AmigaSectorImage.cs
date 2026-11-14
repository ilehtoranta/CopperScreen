using System.Buffers.Binary;
namespace CopperDisk;

/// <summary>A read-only, protected against concurrent writers, sector image for desktop disk tools.</summary>
/// <remarks>Single-consumer API. Supports raw ADF/HDF and RDB partitions; contains no filesystem implementation.</remarks>
public sealed class AmigaSectorImage : IDisposable
{
    private readonly FileStream _stream;

    private AmigaSectorImage(FileStream stream) => _stream = stream;

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

    internal long Length => _stream.Length;
    /// <summary>Copy the protected image bytes to an unpublished host file.</summary>
    public void CopyTo(Stream destination, CancellationToken cancellationToken = default)
    {
        _stream.Position = 0;
        _stream.CopyToAsync(destination, 65536, cancellationToken).GetAwaiter().GetResult();
    }
    internal void Read(long offset, Span<byte> data)
    {
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
