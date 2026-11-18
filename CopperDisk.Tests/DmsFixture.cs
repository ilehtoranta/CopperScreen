using System.Buffers.Binary;

namespace CopperDisk.Tests;

// Original generated DD archives. Modes 0 (stored) and 1 (RLE).
internal static class DmsFixture
{
    internal static byte[] Create(byte[] sectors, byte mode = 1, int cylinders = 80, ushort flags = 0)
    {
        if (sectors.Length != 901120 || mode > 1) throw new ArgumentException("Expected a standard DD image and stored/RLE mode.");
        using var body = new MemoryStream();
        for (var cylinder = 0; cylinder < cylinders; cylinder++)
        {
            var data = sectors.AsSpan(cylinder * 11264, 11264);
            var packed = mode == 0 ? data.ToArray() : Rle(data);
            var header = new byte[20]; "TR"u8.CopyTo(header);
            Be16(header, 2, cylinder); Be16(header, 6, packed.Length);
            Be16(header, 8, packed.Length); Be16(header, 10, data.Length); header[13] = mode;
            var sum = 0; foreach (var value in data) sum += value;
            Be16(header, 14, sum); Be16(header, 16, Crc(packed)); Be16(header, 18, Crc(header.AsSpan(0, 18)));
            body.Write(header); body.Write(packed);
        }
        var image = new byte[56 + body.Length]; "DMS!"u8.CopyTo(image);
        Be16(image, 10, flags); Be16(image, 18, 79); Be16(image, 46, 111); Be16(image, 52, mode);
        Be24(image, 21, (int)body.Length); Be24(image, 25, sectors.Length);
        Be16(image, 54, Crc(image.AsSpan(4, 50))); body.ToArray().CopyTo(image, 56);
        return image;
    }
    private static byte[] Rle(ReadOnlySpan<byte> data)
    {
        using var result = new MemoryStream();
        for (var i = 0; i < data.Length;)
        {
            var count = 1;
            while (i + count < data.Length && data[i + count] == data[i]) count++;
            if (count >= 3)
                result.Write([0x90, 0xff, data[i], (byte)(count >> 8), (byte)count]);
            else
                for (var n = 0; n < count; n++) { result.WriteByte(data[i]); if (data[i] == 0x90) result.WriteByte(0); }
            i += count;
        }
        return result.ToArray();
    }
    internal static byte[] WithAuxiliary(byte[] archive)
    {
        var result = new byte[archive.Length + 24]; archive.CopyTo(result, 0);
        var track = result.AsSpan(archive.Length, 20); "TR"u8.CopyTo(track);
        Be16(track, 2, 80); Be16(track, 6, 4); Be16(track, 8, 4); Be16(track, 10, 4);
        "DIZ!"u8.CopyTo(result.AsSpan(archive.Length + 20));
        Be16(track, 14, 'D' + 'I' + 'Z' + '!'); Be16(track, 16, Crc("DIZ!"u8)); Be16(track, 18, Crc(track[..18]));
        Be16(result, 10, 0x100); Be24(result, 21, result.Length - 56); SealHeader(result);
        return result;
    }
    internal static void SealHeader(byte[] archive) => Be16(archive, 54, Crc(archive.AsSpan(4, 50)));
    private static void Be16(Span<byte> data, int offset, int value) => BinaryPrimitives.WriteUInt16BigEndian(data[offset..], unchecked((ushort)value));
    private static void Be24(Span<byte> data, int offset, int value) { data[offset] = (byte)(value >> 16); data[offset + 1] = (byte)(value >> 8); data[offset + 2] = (byte)value; }
    private static int Crc(ReadOnlySpan<byte> bytes)
    {
        var crc = 0;
        foreach (var value in bytes) { crc ^= value; for (var bit = 0; bit < 8; bit++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xa001 : crc >> 1; }
        return crc;
    }
}
