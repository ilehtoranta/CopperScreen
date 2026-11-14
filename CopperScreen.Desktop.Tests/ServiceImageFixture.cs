using System.Buffers.Binary;
using System.Text;
using CopperStart.FileSystem;

namespace CopperScreen.Tests;

// Generated-media fixture shared from CopperStart service tests (MIT), copied for independent package-consumer integration checks.
internal sealed class ServiceImageFixture : IReadOnlyBlockSource
{
    internal readonly Dictionary<uint, byte[]> Blocks = [];
    internal FileSystemGeometry Geometry;
    internal byte[] Expected = [];
    internal bool FailReads { get; set; }
    internal Action? OnRead { get; set; }
    internal uint PatternStart, PatternEnd;
    public long Length => (long)Geometry.BlockCount * Geometry.BlockBytes;
    public void Read(long offset, Span<byte> destination)
    {
        OnRead?.Invoke();
        if (FailReads) throw new IOException("Injected source error");
        if (offset < 0 || offset > Length - destination.Length || offset % Geometry.BlockBytes != 0 || destination.Length % Geometry.BlockBytes != 0)
            throw new IOException("Read outside fixture.");
        for (var i = 0; i < destination.Length; i += (int)Geometry.BlockBytes)
        {
            var block = (uint)((offset + i) / Geometry.BlockBytes); var target = destination.Slice(i, (int)Geometry.BlockBytes);
            if (Blocks.TryGetValue(block, out var data)) data.CopyTo(target);
            else if (block >= PatternStart && block < PatternEnd)
                for (var j = 0; j < target.Length; j++) target[j] = Pattern((ulong)block * Geometry.BlockBytes + (uint)j);
            else target.Clear();
        }
    }
    internal static byte Pattern(ulong offset) => (byte)(offset * 13 + offset / 31);
    internal static void Put(byte[] b, int at, uint value) => BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(at), value);
    internal static void Short(byte[] b, int at, ushort value) => BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(at), value);
    internal byte[] Block(uint key, uint type = 0)
    { var b = new byte[Geometry.BlockBytes]; Put(b, 0, type); Blocks[key] = b; return b; }
    internal static void Seal(byte[] b, int checksum, uint seed = 0)
    {
        Put(b, checksum, 0); uint sum = seed;
        for (var i = 0; i < b.Length; i += 4) sum = unchecked(sum + BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(i)));
        Put(b, checksum, unchecked(0u - sum));
    }
    internal byte[] Bytes(uint? blocks = null)
    {
        if (blocks is { } count && count != Geometry.BlockCount)
        {
            Blocks.Remove(Geometry.BlockCount - 1);
            Geometry = new(count, Geometry.BlockBytes, Geometry.ReservedBlocks);
            var root = Blocks[0]; Put(root, 48, count);
            var backup = (byte[])root.Clone(); Put(backup, 8, count - 1); Blocks[count - 1] = backup;
            var seed = BinaryPrimitives.ReadUInt32BigEndian(root) == 0x53465302 ? 2u : 1u;
            Seal(root, 4, seed); Seal(backup, 4, seed);
        }
        var bytes = new byte[checked((int)Length)]; Read(0, bytes); return bytes;
    }
    private static void BString(byte[] b, int at, string text)
    { b[at] = (byte)text.Length; Encoding.Latin1.GetBytes(text).CopyTo(b, at + 1); }

    internal static ServiceImageFixture Ffs(int dos = 1, int children = 140, uint bytes = 512, bool link = false, bool extended = true)
    {
        var f = new ServiceImageFixture { Geometry = new(4096, bytes, 2) };
        const uint root = 2048;
        var boot = f.Block(0, 0x444F5300u + (uint)dos);
        byte[] Header(uint key, int type, string name, uint parent)
        {
            var b = f.Block(key, 2); Put(b, 4, key); Put(b, (int)bytes - 4, unchecked((uint)type));
            Put(b, (int)bytes - 12, parent); BString(b, (int)bytes - 80, name);
            Put(b, (int)bytes - 92, 1); return b;
        }
        var r = Header(root, 1, "Fixture", 0); Put(r, 4, 0); Put(r, 12, bytes / 4 - 56); Put(r, 24, 10);
        var dir = Header(10, 2, "Docs", root); Put(dir, 24, 20); Put(dir, (int)bytes - 16, 11);
        Header(11, 2, "Empty", root);
        for (uint i = 0; i < children; i++)
        {
            var key = 20 + i; var file = Header(key, -3, i == 0 ? "Payload" : $"File{i}", 10);
            Put(file, (int)bytes - 16, i + 1 < children ? key + 1 : 0);
        }
        if (link) { var l = Header(12, -4, "Link", root); Put(l, (int)bytes - 44, 20); Put(f.Blocks[11], (int)bytes - 16, 12); }
        var payload = (dos & 1) != 0 ? (int)bytes : (int)bytes - 24;
        var dataBlocks = extended ? (int)(bytes / 4 - 56) + 3 : 3;
        f.Expected = Enumerable.Range(0, payload * (dataBlocks - 1) + 17).Select(i => Pattern((ulong)i)).ToArray();
        var table = f.Blocks[20]; Put(table, (int)bytes - 188, (uint)f.Expected.Length);
        BString(table, (int)bytes - 184, "Comment"); Put(table, (int)bytes - 192, 0x40);
        if ((dos & 1) == 0) Put(table, 16, 500);
        var slots = (int)(bytes / 4 - 56);
        for (var i = 0; i < dataBlocks; i++)
        {
            if (i == slots)
            {
                Put(table, (int)bytes - 8, 400); table = Header(400, -3, "", 20); Put(table, 0, 16);
            }
            Put(table, 8, (uint)(i % slots + 1)); var key = (uint)(500 + i * 2); // deliberately fragmented
            Put(table, (int)bytes - 204 - i % slots * 4, key);
            var b = f.Block(key); var count = Math.Min(payload, f.Expected.Length - i * payload);
            f.Expected.AsSpan(i * payload, count).CopyTo(b.AsSpan((int)bytes - payload));
            if ((dos & 1) == 0)
            { Put(b, 0, 8); Put(b, 4, 20); Put(b, 8, (uint)(i + 1)); Put(b, 12, (uint)count); Put(b, 16, i + 1 == dataBlocks ? 0 : key + 2); Seal(b, 20); }
        }
        foreach (var b in f.Blocks.Values)
            if (BinaryPrimitives.ReadUInt32BigEndian(b) is 2 or 16) Seal(b, 20);
        return f;
    }

    internal static ServiceImageFixture Smart(bool sfs2, bool wide = false, bool link = false)
    {
        var f = new ServiceImageFixture { Geometry = new(wide ? 0x1000100u : 256u, 512, 1) };
        var seed = sfs2 ? 2u : 1u;
        byte[] Header(uint n, uint id) { var b = f.Block(n, id); Put(b, 8, n); return b; }
        int Object(byte[] b, int at, uint node, uint value, ulong size, byte flags, string name, string comment = "")
        {
            var extra = sfs2 ? 2 : 0;
            Put(b, at + 4, node); Put(b, at + 8, 0x4F); Put(b, at + 12, value);
            if ((flags & 128) != 0 || !sfs2) Put(b, at + 16, (uint)size);
            else { Put(b, at + 16, (uint)(size >> 16)); Short(b, at + 20, (ushort)size); }
            Put(b, at + 20 + extra, 123456789); b[at + 24 + extra] = flags;
            Encoding.Latin1.GetBytes(name).CopyTo(b, at + 25 + extra);
            Encoding.Latin1.GetBytes(comment).CopyTo(b, at + 26 + extra + name.Length);
            return (at + 28 + extra + name.Length + comment.Length) & ~1;
        }
        foreach (var n in new[] { 0u, f.Geometry.BlockCount - 1 })
        {
            var b = Header(n, sfs2 ? 0x53465302u : 0x53465300u); Short(b, 12, (ushort)(sfs2 ? 4 : 3));
            Put(b, 48, f.Geometry.BlockCount); Put(b, 52, 512); Put(b, 104, 3); Put(b, 108, 6); Put(b, 112, 7);
        }
        var root = Header(3, 0x4F424A43); Object(root, 24, 1, 4, 8, 128, "Fixture"); Put(root, 484, 90);
        Header(5, 0x54524F4B);
        var entries = Header(8, 0x4F424A43); Put(entries, 12, 1);
        var at = Object(entries, 24, 2, 0, 0, 129, ".recycled");
        at = Object(entries, at, 7, 9, 10, 128, "Docs");
        at = Object(entries, at, 9, 0, 0, 128, "Empty");
        if (link) Object(entries, at, 10, 0, 0, 64, "Link");
        var payload = Header(10, 0x4F424A43); Put(payload, 12, 7);
        var fileLength = wide ? 0x100000000ul + 277 : 1301ul;
        var payloadEnd = Object(payload, 24, 8, 40, fileLength, 0, "Payload", "Comment");
        Object(payload, payloadEnd, 11, 0, 0, 0, "EmptyFile");
        var nodes = Header(7, 0x4E444320); Put(nodes, 12, 1); Put(nodes, 16, 1);
        Put(nodes, 20, 3); Put(nodes, 30, 8); Put(nodes, 80, 8); Put(nodes, 90, 10); Put(nodes, 100, 8); Put(nodes, 110, 8); Put(nodes, 120, 10);
        var tree = Header(6, 0x424E4443); Short(tree, 12, (ushort)(wide ? 1 : 2)); tree[14] = 1; tree[15] = (byte)(sfs2 ? 16 : 14);
        Put(tree, 16, 40); Put(tree, 24, 0x80000008);
        var count = (uint)((fileLength + 511) / 512);
        if (sfs2) Put(tree, 28, wide ? count : 1u); else Short(tree, 28, (ushort)(wide ? count : 1));
        if (!wide)
        {
            Put(tree, 20, 60);
            var second = 16 + tree[15]; Put(tree, second, 60); Put(tree, second + 8, 40);
            if (sfs2) Put(tree, second + 12, 2); else Short(tree, second + 12, 2);
        }
        foreach (var b in f.Blocks.Values) Seal(b, 4, seed);
        f.PatternStart = 40; f.PatternEnd = 40 + count;
        f.Expected = Enumerable.Range(0, 1301).Select(i => Pattern(40ul * 512 + (uint)i)).ToArray();
        if (!wide)
            for (uint index = 0; index < 3; index++)
                f.Blocks[index == 0 ? 40 : 59 + index] = Enumerable.Range(0, 512).Select(i => Pattern(40ul * 512 + index * 512 + (uint)i)).ToArray();
        return f;
    }

    internal static ServiceImageFixture SmartMany(bool sfs2)
    {
        var f = Smart(sfs2); var oldLeaf = f.Blocks[7];
        for (uint i = 0; i < 4; i++)
        {
            var leaf = f.Block(200 + i, 0x4E444320);
            if (i == 0) oldLeaf.CopyTo(leaf, 0);
            Put(leaf, 8, 200 + i); Put(leaf, 12, i * 49 + 1); Put(leaf, 16, 1);
        }
        var branch = f.Block(7, 0x4E444320); Put(branch, 8, 7); Put(branch, 12, 1); Put(branch, 16, 49);
        for (var i = 0; i < 4; i++) Put(branch, 20 + i * 4, (uint)(200 + i) << (sfs2 ? 1 : 4));
        Put(f.Blocks[8], 16, 100);
        const int children = 140;
        for (var i = 0; i < children; i++)
        {
            var group = i / 8; var blockKey = (uint)(100 + group); var node = (uint)(16 + i);
            byte[] objects;
            if (i % 8 == 0)
            {
                objects = f.Block(blockKey, 0x4F424A43); Put(objects, 8, blockKey); Put(objects, 12, 1);
                Put(objects, 16, i + 8 < children ? blockKey + 1 : 0); Put(objects, 20, group == 0 ? 8 : blockKey - 1);
            }
            else objects = f.Blocks[blockKey];
            var extra = sfs2 ? 2 : 0; var name = $"Item{node:D3}"; var stride = (28 + extra + name.Length) & ~1;
            var at = 24 + i % 8 * stride;
            Put(objects, at + 4, node); Put(objects, at + 8, 15); Put(objects, at + 20 + extra, 123456789);
            Encoding.Latin1.GetBytes(name).CopyTo(objects, at + 25 + extra);
            var delta = node - 1; Put(f.Blocks[200 + delta / 49], 20 + (int)(delta % 49) * 10, blockKey);
        }
        foreach (var b in f.Blocks.Values)
            if (BinaryPrimitives.ReadUInt32BigEndian(b) is 0x4E444320 or 0x4F424A43) Seal(b, 4, sfs2 ? 2u : 1u);
        return f;
    }
}
