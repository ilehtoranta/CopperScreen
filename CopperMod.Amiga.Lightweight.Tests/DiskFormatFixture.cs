using System.Buffers.Binary;
using System.IO.Compression;

namespace CopperMod.Amiga.Lightweight.Tests;

// Original synthetic media; no ROM or copyrighted disk contents.
internal static class DiskFormatFixture
{
    internal static byte[] Adz(byte[] image)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Fastest, true)) gzip.Write(image);
        return output.ToArray();
    }

    internal static byte[] Extended(byte[] sectors, byte[]? raw = null, int bits = 0)
    {
        const int sectorTrack = 11 * 512, table = 12 + 160 * 12;
        var image = new byte[table + sectors.Length - (raw is null ? 0 : sectorTrack) + (raw?.Length ?? 0)];
        "UAE-1ADF"u8.CopyTo(image); Be16(image, 10, 160);
        var position = table;
        for (var t = 0; t < 160; t++)
        {
            var data = t == 0 && raw is not null ? raw.AsSpan() : sectors.AsSpan(t * sectorTrack, sectorTrack);
            Be16(image, 12 + t * 12 + 2, t == 0 && raw is not null ? 1 : 0);
            BinaryPrimitives.WriteInt32BigEndian(image.AsSpan(12 + t * 12 + 4), data.Length);
            BinaryPrimitives.WriteInt32BigEndian(image.AsSpan(12 + t * 12 + 8), t == 0 && raw is not null ? bits : 0);
            data.CopyTo(image.AsSpan(position)); position += data.Length;
        }
        return image;
    }

    internal static byte[] Dms(byte[] sectors)
    {
        const int size = 22 * 512;
        var image = new byte[56 + 80 * (20 + size)];
        "DMS!"u8.CopyTo(image); Be16(image, 18, 79); Be16(image, 46, 111);
        Be24(image, 21, image.Length - 56); Be24(image, 25, sectors.Length);
        Be16(image, 54, Crc(image.AsSpan(4, 50)));
        for (var c = 0; c < 80; c++)
        {
            var header = image.AsSpan(56 + c * (20 + size), 20);
            var data = sectors.AsSpan(c * size, size);
            "TR"u8.CopyTo(header); Be16(header, 2, c);
            Be16(header, 6, size); Be16(header, 8, size); Be16(header, 10, size);
            var sum = 0; foreach (var value in data) sum += value;
            Be16(header, 14, sum & 65535); Be16(header, 16, Crc(data)); Be16(header, 18, Crc(header[..18]));
            data.CopyTo(image.AsSpan(56 + c * (20 + size) + 20));
        }
        return image;
    }

    internal static byte[] Scp(int cylinder = 0, params ushort[] ticks)
    {
        var image = new byte[0x2B0 + 16 + ticks.Length * 2];
        "SCP"u8.CopyTo(image); image[3] = 0x25; image[4] = 4; image[5] = 1;
        image[6] = image[7] = (byte)(cylinder * 2); image[8] = 1;
        BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(0x10 + cylinder * 2 * 4), 0x2B0);
        "TRK"u8.CopyTo(image.AsSpan(0x2B0)); image[0x2B3] = (byte)(cylinder * 2);
        BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(0x2B4), 100000 * 80);
        BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(0x2B8), ticks.Length);
        BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(0x2BC), 16);
        for (var i = 0; i < ticks.Length; i++) Be16(image, 0x2C0 + i * 2, ticks[i]);
        return image;
    }

    internal static byte[] ScpEncoded(byte[] encoded)
    {
        var ticks = new List<ushort>(); var previous = -1;
        for (var bit = 0; bit < encoded.Length * 8; bit++)
            if ((encoded[bit >> 3] & (0x80 >> (bit & 7))) != 0)
            { ticks.Add(checked((ushort)((bit - previous) * 80))); previous = bit; }
        var image = Scp(0, ticks.ToArray());
        BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(0x2B4), encoded.Length * 8 * 80);
        return image;
    }

    private static void Be16(Span<byte> data, int offset, int value)
        => BinaryPrimitives.WriteUInt16BigEndian(data[offset..], (ushort)value);
    private static void Be24(Span<byte> data, int offset, int value)
    { data[offset] = (byte)(value >> 16); data[offset + 1] = (byte)(value >> 8); data[offset + 2] = (byte)value; }
    private static ushort Crc(ReadOnlySpan<byte> data)
    {
        var crc = 0;
        foreach (var value in data) { crc ^= value; for (var b = 0; b < 8; b++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xA001 : crc >> 1; }
        return (ushort)crc;
    }
}
