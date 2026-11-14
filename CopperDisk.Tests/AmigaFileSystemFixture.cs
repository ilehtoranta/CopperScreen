using System.Buffers.Binary;
using System.Text;

namespace CopperDisk.Tests;

// Generated media only: no ROMs, guest execution or copyrighted disk contents.
internal sealed class AmigaFileSystemFixture
{
    internal readonly byte[] Bytes;
    internal readonly byte[] Content;
    internal readonly int BlockSize, Root;
    internal readonly int Dos;

    internal AmigaFileSystemFixture(int dos = 1, int blockSize = 512, int blocks = 1760, int contentBlocks = 75)
    {
        Dos = dos; BlockSize = blockSize; Root = (blocks + 1) / 2;
        Bytes = new byte[blocks * blockSize];
        Put(0, 0, 0x444F5300u + (uint)dos);
        Put(0, 8, 880); // HD boot code can still contain 880; filesystem root is calculated.
        Header(Root, 1, "Fixture"); Put(Root, 4, 0); Put(Root, 12, (uint)(blockSize / 4 - 56));
        Put(Root, 24, 10); Fix(Root);
        Header(10, -3, "ReadMe"); Put(10, blockSize - 12, (uint)Root); Put(10, blockSize - 16, 11);
        BString(10, blockSize - 184, "Amiga comment"); Put(10, blockSize - 192, 0x40);
        Header(11, -3, "EmptyFile"); Put(11, blockSize - 12, (uint)Root); Put(11, blockSize - 16, 20); Fix(11);
        Header(20, 2, "Tools"); Put(20, blockSize - 12, (uint)Root); Put(20, 24, 30); Fix(20);
        Header(30, -3, "Nested.bin"); Put(30, blockSize - 12, 20); Put(30, blockSize - 16, 31);
        Header(31, 2, "Empty"); Put(31, blockSize - 12, 20); Fix(31);
        FileData(30, 40, [1, 3, 5, 7]);
        var payload = dos % 2 == 0 ? blockSize - 24 : blockSize;
        Content = Enumerable.Range(0, payload * (contentBlocks - 1) + 17).Select(i => (byte)(i * 13 + i / 31)).ToArray();
        FileData(10, 100, Content);
    }

    private void FileData(int header, int start, byte[] content)
    {
        var payload = Dos % 2 == 0 ? BlockSize - 24 : BlockSize;
        var blocks = (content.Length + payload - 1) / payload;
        var slots = BlockSize / 4 - 56;
        Put(header, BlockSize - 188, (uint)content.Length);
        if (Dos % 2 == 0) Put(header, 16, (uint)start);
        var table = header;
        for (var i = 0; i < blocks; i++)
        {
            if (i != 0 && i % slots == 0)
            {
                Put(table, BlockSize - 8, 60); Fix(table);
                table = 60; Header(table, -3, ""); Put(table, 0, 16); Put(table, BlockSize - 12, (uint)header);
            }
            var slot = i % slots;
            Put(table, 8, (uint)(slot + 1));
            Put(table, BlockSize - 204 - slot * 4, (uint)(start + i));
            var count = Math.Min(payload, content.Length - i * payload);
            content.AsSpan(i * payload, count).CopyTo(Bytes.AsSpan((start + i) * BlockSize + BlockSize - payload));
            if (Dos % 2 == 0)
            {
                Put(start + i, 0, 8); Put(start + i, 4, (uint)header); Put(start + i, 8, (uint)(i + 1));
                Put(start + i, 12, (uint)count); Put(start + i, 16, i + 1 == blocks ? 0 : (uint)(start + i + 1)); Fix(start + i);
            }
        }
        Fix(table);
    }

    internal void Header(int block, int type, string name)
    {
        Put(block, 0, 2); Put(block, 4, (uint)block); Put(block, BlockSize - 4, unchecked((uint)type));
        Put(block, BlockSize - 92, 1); Put(block, BlockSize - 88, 2); Put(block, BlockSize - 84, 3);
        BString(block, BlockSize - 80, name);
    }
    internal void BString(int block, int offset, string value)
    {
        Bytes[block * BlockSize + offset] = (byte)value.Length;
        Encoding.Latin1.GetBytes(value).CopyTo(Bytes, block * BlockSize + offset + 1);
    }
    internal void Put(int block, int offset, uint value) => BinaryPrimitives.WriteUInt32BigEndian(Bytes.AsSpan(block * BlockSize + offset), value);
    internal void Fix(int block, int checksumOffset = 20, int? words = null)
    {
        Put(block, checksumOffset, 0); uint sum = 0;
        for (var offset = 0; offset < (words ?? BlockSize / 4) * 4; offset += 4)
            sum = unchecked(sum + BinaryPrimitives.ReadUInt32BigEndian(Bytes.AsSpan(block * BlockSize + offset)));
        Put(block, checksumOffset, unchecked(0u - sum));
    }

    internal byte[] Rdb(bool secondUnsupported = false)
    {
        const int start = 44 * 512;
        var factor = BlockSize / 512;
        var disk = new AmigaFileSystemFixture(Dos, 512, 44 + 1760 * factor * (secondUnsupported ? 2 : 1), 1);
        Array.Clear(disk.Bytes);
        disk.Put(0, 0, 0x5244534B); disk.Put(0, 4, 64); disk.Put(0, 16, 512);
        disk.Put(0, 24, uint.MaxValue); disk.Put(0, 28, 1); disk.Put(0, 32, uint.MaxValue); disk.Fix(0, 8, 64);
        void Partition(int key, string name, uint low, uint high, uint dos, uint next)
        {
            disk.Put(key, 0, 0x50415254); disk.Put(key, 4, 64); disk.Put(key, 16, next); disk.Put(key, 20, 1);
            disk.BString(key, 36, name);
            uint[] env = [16, 128, 0, 2, (uint)factor, 11, 2, 0, 0, low, high, 30, 1, 0x1FE00, 0xFFFFFFFE, 0, dos];
            for (var i = 0; i < env.Length; i++) disk.Put(key, 128 + i * 4, env[i]);
            disk.Fix(key, 8, 64);
        }
        Partition(1, "DH0", 2, (uint)(2 + 80 * factor - 1), 0x444F5300u + (uint)Dos, secondUnsupported ? 2u : uint.MaxValue);
        if (secondUnsupported) Partition(2, "DH1", (uint)(2 + 80 * factor), (uint)(2 + 160 * factor - 1), 0x53465300, uint.MaxValue);
        Bytes.CopyTo(disk.Bytes, start);
        return disk.Bytes;
    }
}
