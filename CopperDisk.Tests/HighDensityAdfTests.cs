using System.IO.Compression;

namespace CopperDisk.Tests;

public sealed class HighDensityAdfTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(79, 1)]
    public void HdSectorsRoundTripAndWritesInvalidateTheCorrectTrack(int cylinder, int head)
    {
        var bytes = new byte[1802240];
        var offset = (cylinder * 2 + head) * 22 * 512;
        for (var i = 0; i < 22 * 512; i++) bytes[offset + i] = (byte)(i * 7 + (i >> 9));
        var media = AmigaDiskLoader.FromAdfBytes(bytes);
        Assert.Equal(bytes.AsSpan(offset + 21 * 512, 512).ToArray(), media.ReadSector(cylinder, head, 21).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => media.ReadSector(cylinder, head, 22));
        var track = media.ReadTrack(cylinder, head);
        Assert.Equal(202688, track.BitLength);
        var decoded = new byte[22 * 512];
        Assert.Equal(22, AmigaDosTrackDecoder.DecodeTrackBestEffort(track, cylinder, head, decoded));
        Assert.Equal(bytes.AsSpan(offset, decoded.Length).ToArray(), decoded);
        Assert.True(media.TryWriteBytes(offset + 21 * 512, new byte[] { 0x73 }));
        Assert.NotSame(track, media.ReadTrack(cylinder, head));
        Assert.Equal(22, AmigaDosTrackDecoder.DecodeTrackBestEffort(media.ReadTrack(cylinder, head), cylinder, head, decoded));
        Assert.Equal(0x73, decoded[21 * 512]);
        var replacement = AmigaDiskLoader.FromAdfBytes(new byte[1802240]);
        Assert.True(replacement.TryWriteTrack(cylinder, head, media.ReadTrack(cylinder, head)));
        Assert.Equal(decoded, replacement.ReadBytes(offset, decoded.Length).ToArray());
    }

    [Fact]
    public void HdAdzRetainsItsGeometryAndReadOnlyContract()
    {
        var bytes = new byte[1802240]; bytes[^1] = 42;
        using var compressed = new MemoryStream();
        using (var gzip = new GZipStream(compressed, CompressionMode.Compress, true)) gzip.Write(bytes);
        var media = AmigaDiskLoader.FromAdzBytes(compressed.ToArray());
        Assert.Equal(bytes, media.SectorData.ToArray());
        Assert.Equal(42, media.ReadSector(79, 1, 21).Span[^1]);
        Assert.False(media is IWritableAmigaSectorDiskMedia);
    }
}
