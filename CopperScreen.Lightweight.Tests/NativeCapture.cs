using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace CopperScreen.Tests;

// BCL-only lossless captures keep native qualification independent of a desktop renderer.
internal static class NativeCapture
{
    public static void WritePng(string path, ReadOnlySpan<int> pixels, int width, int height)
    {
        using var file = File.Create(path);
        file.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8; header[9] = 6;
        Chunk(file, "IHDR", header);
        using var compressed = new MemoryStream();
        using (var zip = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            var row = new byte[1 + width * 4]; // PNG filter 0, RGBA samples.
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var pixel = (uint)pixels[y * width + x]; var at = 1 + x * 4;
                    row[at] = (byte)(pixel >> 16); row[at + 1] = (byte)(pixel >> 8);
                    row[at + 2] = (byte)pixel; row[at + 3] = (byte)(pixel >> 24);
                }
                zip.Write(row);
            }
        }
        Chunk(file, "IDAT", compressed.ToArray()); Chunk(file, "IEND", []);
    }
    private static void Chunk(Stream file, string type, byte[] data)
    {
        Span<byte> word = stackalloc byte[4]; BinaryPrimitives.WriteInt32BigEndian(word, data.Length); file.Write(word);
        var name = Encoding.ASCII.GetBytes(type); file.Write(name); file.Write(data);
        uint crc = uint.MaxValue;
        foreach (var bytes in new[] { name, data })
            foreach (var b in bytes)
            {
                crc ^= b;
                for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0);
            }
        BinaryPrimitives.WriteUInt32BigEndian(word, ~crc); file.Write(word);
    }
}
