using System.Buffers.Binary;

namespace CopperDisk;

// DMS layout/RLE semantics: public-domain xDMS src/pfile.c and src/u_rle.c.
// Auxiliary records can depend on compression state; keep those archives read-only.
internal sealed class DmsArchive
{
    private const int HeaderBytes = 56, TrackHeaderBytes = 20, CylinderBytes = 11264;
    private readonly byte[] header;
    internal string? WriteUnavailableReason { get; }

    // Called only after the maintained decoder has validated the complete archive.
    internal DmsArchive(ReadOnlySpan<byte> validatedArchive)
    {
        header = validatedArchive[..HeaderBytes].ToArray();
        if ((Read16(header, 10) & ~0x81) != 0 || Read16(header, 16) != 0 || Read16(header, 18) != 79)
            WriteUnavailableReason = "DMS archives with auxiliary records, appended data or unsupported flags are read-only.";
        var offset = HeaderBytes; var count = 0;
        while (offset + TrackHeaderBytes <= validatedArchive.Length && validatedArchive[offset..].StartsWith("TR"u8))
        {
            var track = validatedArchive.Slice(offset, TrackHeaderBytes);
            if (Read16(track, 2) >= 80 || Read16(track, 10) != CylinderBytes || track[4] != 0 || track[5] != 0 || (track[12] & ~15) != 0)
                WriteUnavailableReason = "DMS archives with auxiliary or unsupported track records are read-only.";
            offset += TrackHeaderBytes + Read16(track, 6); count++;
        }
        if (count != 80 || offset != validatedArchive.Length)
            WriteUnavailableReason = "DMS archives with auxiliary records or trailing data are read-only.";
    }

    internal void WriteReplacement(Stream sectors, Stream destination, CancellationToken token)
    {
        if (WriteUnavailableReason is { } reason) throw new NotSupportedException(reason);
        ArgumentNullException.ThrowIfNull(sectors); ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanRead || !destination.CanWrite || !destination.CanSeek || destination.Length != 0 || destination.Position != 0)
            throw new ArgumentException("Use an empty, seekable read/write staging stream.", nameof(destination));
        if (!sectors.CanSeek || sectors.Length != AmigaDiskGeometry.StandardAdfSize)
            throw new InvalidDataException("A DMS replacement requires exactly 901,120 decoded sector bytes.");
        token.ThrowIfCancellationRequested(); sectors.Position = 0;
        var decoded = new byte[AmigaDiskGeometry.StandardAdfSize]; sectors.ReadExactly(decoded);
        using var archive = new MemoryStream(HeaderBytes + 80 * TrackHeaderBytes + decoded.Length);
        archive.Write(header);
        var compressedTracks = 0;
        Span<byte> track = stackalloc byte[TrackHeaderBytes];
        for (var cylinder = 0; cylinder < 80; cylinder++)
        {
            token.ThrowIfCancellationRequested();
            var data = decoded.AsSpan(cylinder * CylinderBytes, CylinderBytes);
            var packed = Rle(data); var compress = packed.Length < data.Length;
            ReadOnlySpan<byte> payload = compress ? packed : data;
            if (compress) compressedTracks++;
            track.Clear();
            "TR"u8.CopyTo(track); Put16(track, 2, cylinder);
            Put16(track, 6, payload.Length); Put16(track, 8, payload.Length); Put16(track, 10, data.Length);
            track[13] = compress ? (byte)1 : (byte)0;
            Put16(track, 14, DmsDecoder.CalcChecksum(data)); Put16(track, 16, DmsDecoder.CreateCrc(payload));
            Put16(track, 18, DmsDecoder.CreateCrc(track[..18]));
            archive.Write(track); archive.Write(payload);
        }
        var bytes = archive.ToArray();
        Put24(bytes, 21, bytes.Length - HeaderBytes); Put24(bytes, 25, decoded.Length);
        Put16(bytes, 52, compressedTracks >= 40 ? 1 : 0);
        Put16(bytes, 54, DmsDecoder.CreateCrc(bytes.AsSpan(4, 50)));
        destination.WriteAsync(bytes, token).AsTask().GetAwaiter().GetResult();
        destination.Flush(); destination.Position = 0;
        var written = new byte[bytes.Length]; destination.ReadExactly(written);
        token.ThrowIfCancellationRequested();
        if (destination.Length != bytes.Length || !written.AsSpan().SequenceEqual(bytes) ||
            !DmsDecoder.Decode(written, token).AsSpan().SequenceEqual(decoded))
            throw new InvalidDataException("DMS replacement verification failed.");
    }

    private static byte[] Rle(ReadOnlySpan<byte> data)
    {
        using var result = new MemoryStream(data.Length);
        for (var i = 0; i < data.Length;)
        {
            var count = 1;
            while (i + count < data.Length && data[i + count] == data[i]) count++;
            if (count >= 4 || data[i] == 0x90 && count >= 2)
            {
                if (count < 255) result.Write([0x90, (byte)count, data[i]]);
                else result.Write([0x90, 0xff, data[i], (byte)(count >> 8), (byte)count]);
            }
            else for (var n = 0; n < count; n++) { result.WriteByte(data[i]); if (data[i] == 0x90) result.WriteByte(0); }
            i += count;
        }
        return result.ToArray();
    }
    private static ushort Read16(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16BigEndian(bytes[offset..]);
    private static void Put16(Span<byte> bytes, int offset, int value) => BinaryPrimitives.WriteUInt16BigEndian(bytes[offset..], unchecked((ushort)value));
    private static void Put24(Span<byte> bytes, int offset, int value) { bytes[offset] = (byte)(value >> 16); bytes[offset + 1] = (byte)(value >> 8); bytes[offset + 2] = (byte)value; }
}
