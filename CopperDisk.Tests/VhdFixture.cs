using System.Buffers.Binary;

namespace CopperDisk.Tests;

internal static class VhdFixture
{
    public static byte[] Fixed(ReadOnlySpan<byte> sectors, bool leadingFooter = false)
    {
        var footer = Footer(sectors.Length, 2);
        using var output = new MemoryStream();
        if (leadingFooter) output.Write(footer);
        output.Write(sectors);
        output.Write(footer);
        return output.ToArray();
    }

    public static byte[] Dynamic(long capacity, int blockSize = 512 * 1024, byte[]? firstBlock = null)
    {
        var count = (int)((capacity + blockSize - 1) / blockSize);
        var bat = new byte[(count * 4 + 511) & ~511];
        bat.AsSpan(0, count * 4).Fill(0xff);
        var header = new byte[1024];
        "cxsparse"u8.CopyTo(header);
        BinaryPrimitives.WriteUInt64BigEndian(header.AsSpan(8), ulong.MaxValue);
        BinaryPrimitives.WriteUInt64BigEndian(header.AsSpan(16), 1536);
        Put(header, 24, 0x10000);
        Put(header, 28, (uint)count);
        Put(header, 32, (uint)blockSize);
        Checksum(header, 36);
        var footer = Footer(capacity, 3);
        using var output = new MemoryStream();
        output.Write(footer);
        output.Write(header);
        if (firstBlock != null) Put(bat, 0, (uint)((1536 + bat.Length) / 512));
        output.Write(bat);
        if (firstBlock != null)
        {
            var bitmap = new byte[((blockSize + 4095) / 4096 + 511) & ~511];
            bitmap.AsSpan(0, firstBlock.Length / 512 / 8).Fill(0xff);
            var remainingSectors = firstBlock.Length / 512 % 8;
            if (remainingSectors > 0) bitmap[firstBlock.Length / 512 / 8] = (byte)(0xff << (8 - remainingSectors));
            output.Write(bitmap);
            output.Write(firstBlock);
            output.SetLength(output.Position + blockSize - firstBlock.Length);
            output.Position = output.Length;
        }
        output.Write(footer);
        return output.ToArray();
    }

    private static byte[] Footer(long capacity, uint type)
    {
        var footer = new byte[512];
        "conectix"u8.CopyTo(footer);
        Put(footer, 8, 2);
        Put(footer, 12, 0x10000);
        BinaryPrimitives.WriteUInt64BigEndian(footer.AsSpan(16), type == 2 ? ulong.MaxValue : 512);
        "vpc "u8.CopyTo(footer.AsSpan(28));
        "Wi2k"u8.CopyTo(footer.AsSpan(36));
        BinaryPrimitives.WriteInt64BigEndian(footer.AsSpan(40), capacity);
        BinaryPrimitives.WriteInt64BigEndian(footer.AsSpan(48), capacity);
        Put(footer, 60, type);
        Checksum(footer, 64);
        return footer;
    }

    public static void Checksum(Span<byte> data, int offset)
    {
        data.Slice(offset, 4).Clear();
        uint sum = 0;
        foreach (var b in data) sum += b;
        Put(data, offset, ~sum);
    }

    public static void Put(Span<byte> data, int offset, uint value) => BinaryPrimitives.WriteUInt32BigEndian(data[offset..], value);
}
