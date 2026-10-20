using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace CopperScreen.Tests;

// Independent sparse DOS1 formatter. Startup files are above 4 GiB; an optional
// stress layout fills all lower data blocks with ordinary files.
internal sealed class LargeFfsFixture
{
    internal const uint Blocks = 0x800000 + 262144, Root = Blocks / 2, Upper = 0x800000;
    internal const long Start = 131072;
    internal const string Geometry = "DH0:2:32:4:135171";
    private const uint Maps = (Blocks - 2 + 4063) / 4064, Extensions = (Maps - 25 + 126) / 127;
    private readonly string path;
    private readonly bool forceUpperAllocation, existingSaved;
    private readonly byte[] prefix;
    private readonly uint freeBefore;
    private static uint Get(byte[] b, int at) => BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(at));
    private static void Put(byte[] b, int at, uint value) => BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(at), value);
    private static void Checksum(byte[] b, int at) { Put(b, at, 0); uint sum = 0; for (int i = 0; i < 512; i += 4) sum = unchecked(sum + Get(b, i)); Put(b, at, unchecked(0u - sum)); }
    private static uint Relocate(uint block) => block == 0 ? 0 : block == FfsHardDiskProbe.Root ? Root : Upper + block;
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(SafeFileHandle file, uint code, IntPtr input, int inputBytes, IntPtr output, int outputBytes, out int returned, IntPtr overlapped);
    internal LargeFfsFixture(string path, bool forceUpperAllocation = false, bool existingSaved = false)
    {
        this.path = path;
        this.forceUpperAllocation = forceUpperAllocation; this.existingSaved = existingSaved;
        var original = FfsHardDiskProbe.Pristine();
        if (existingSaved)
        {
            original = File.ReadAllBytes(Environment.GetEnvironmentVariable("COPPERSCREEN_COPPERSTART_HDF")!);
            Assert.Equal("74E8A6F56EC6941E66AFF1B653CB13975213026526E4F16BDD8915E55A750046", Convert.ToHexString(SHA256.HashData(original)));
        }
        var seed = original.AsSpan(FfsHardDiskProbe.Start, FfsHardDiskProbe.Bytes).ToArray();
        AddGuestCacheCommand(seed);
        byte[] Old(uint key) => seed.AsSpan((int)key * 512, 512).ToArray();
        var headers = new HashSet<uint>();
        void Directory(uint key)
        {
            if (!headers.Add(key)) return;
            var b = Old(key);
            for (int slot = 0; slot < 72; slot++)
                for (var entry = Get(b, 24 + slot * 4); entry != 0; entry = Get(Old(entry), 496))
                {
                    var e = Old(entry);
                    if (Get(e, 508) == 2) Directory(entry);
                    else { headers.Add(entry); for (var ext = Get(e, 504); ext != 0; ext = Get(Old(ext), 504)) headers.Add(ext); }
                }
        }
        Directory(FfsHardDiskProbe.Root);
        var allocated = new HashSet<uint>(); var oldMap = Old(FfsHardDiskProbe.Root + 1);
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite);
        if (OperatingSystem.IsWindows() && !DeviceIoControl(file.SafeFileHandle, 0x900C4, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        file.SetLength(Start + (long)Blocks * 512 + 512);
        void Write(uint key, byte[] b) { file.Position = Start + (long)key * 512; file.Write(b); }
        var boot = seed[..1024]; Put(boot, 8, Root); Put(boot, 4, 0); uint bootSum = 0;
        for (int i = 0; i < 1024; i += 4) { var n = unchecked(bootSum + Get(boot, i)); if (n < bootSum) n++; bootSum = n; }
        Put(boot, 4, ~bootSum); file.Position = Start; file.Write(boot);
        byte[] root = null!;
        for (uint key = 2; key < 3520; key++)
        {
            if (key == FfsHardDiskProbe.Root + 1) continue;
            var bit = key - 2; if ((Get(oldMap, 4 + (int)(bit / 32) * 4) & (1u << (int)(bit % 32))) != 0) continue;
            var b = Old(key); var target = Relocate(key); allocated.Add(target);
            if (headers.Contains(key))
            {
                if (key != FfsHardDiskProbe.Root) { Put(b, 4, target); Put(b, 16, Relocate(Get(b, 16))); }
                for (int i = 24; i <= 308; i += 4) Put(b, i, Relocate(Get(b, i)));
                foreach (var at in new[] { 496, 500, 504 }) Put(b, at, Relocate(Get(b, at)));
                if (key == FfsHardDiskProbe.Root) root = b;
                else Checksum(b, 20);
            }
            Write(target, b);
        }
        // Two <2 GiB filler files own all lower data sectors except volume metadata.
        uint cursor = 2, next = Upper + 4096;
        for (int f = 0; forceUpperAllocation && cursor < Upper; f++)
        {
            var header = next++; var chain = header; var first = true; uint bytes = 0;
            do
            {
                var b = new byte[512]; Put(b, 0, first ? 2u : 16u); Put(b, 4, chain); Put(b, 500, first ? Root : header); Put(b, 508, 0xFFFFFFFD);
                uint count = 0;
                for (; count < 72 && cursor < Upper && bytes <= 0x7FFFFC00;)
                {
                    if (cursor >= Root && cursor < Root + 1 + Maps + Extensions) { cursor = Root + 1 + Maps + Extensions; continue; }
                    Put(b, 308 - (int)count * 4, cursor++); count++; bytes += 512;
                }
                Put(b, 8, count);
                var more = cursor < Upper && bytes <= 0x7FFFFC00;
                var following = more ? next++ : 0; Put(b, 504, following);
                if (first)
                {
                    var name = Encoding.ASCII.GetBytes($"Lower{f}"); b[432] = (byte)name.Length; name.CopyTo(b, 433);
                    uint hash = (uint)name.Length; foreach (var c in name) hash = (hash * 13 + (uint)char.ToUpperInvariant((char)c)) & 0x7FF; hash %= 72;
                    Put(b, 496, Get(root, 24 + (int)hash * 4)); Put(root, 24 + (int)hash * 4, header);
                }
                allocated.Add(chain); Checksum(b, 20); Write(chain, b); first = false; chain = following;
            } while (chain != 0);
            file.Position = Start + (long)header * 512; var head = new byte[512]; file.ReadExactly(head); Put(head, 324, bytes); Checksum(head, 20); Write(header, head);
        }
        Array.Clear(root, 316, 104);
        for (uint i = 0; i < 25; i++) Put(root, 316 + (int)i * 4, Root + 1 + i);
        Put(root, 416, Root + 1 + Maps); Checksum(root, 20); Write(Root, root);
        for (uint i = 0; i < Extensions; i++)
        {
            var b = new byte[512]; for (uint j = 0; j < 127 && 25 + i * 127 + j < Maps; j++) Put(b, (int)j * 4, Root + 1 + 25 + i * 127 + j);
            if (i + 1 < Extensions) Put(b, 508, Root + 1 + Maps + i + 1); Write(Root + 1 + Maps + i, b);
        }
        for (uint key = Root; key < Root + 1 + Maps + Extensions; key++) allocated.Add(key);
        uint free = 0;
        for (uint page = 0; page < Maps; page++)
        {
            var b = new byte[512];
            for (uint bit = 0; bit < 4064; bit++)
            {
                var key = 2 + page * 4064 + bit; if ((forceUpperAllocation && key < Upper) || key >= Blocks || allocated.Contains(key)) continue;
                Put(b, 4 + (int)(bit / 32) * 4, Get(b, 4 + (int)(bit / 32) * 4) | 1u << (int)(bit % 32)); free++;
            }
            Checksum(b, 0); Write(Root + 1 + page, b);
        }
        freeBefore = free; file.Flush(true); file.Position = 0; prefix = new byte[forceUpperAllocation ? 4 * 1024 * 1024 : (int)Start]; file.ReadExactly(prefix);
    }
    // Original 68000 HUNK: use public DOS AddBuffers("DH0:", 32), record its
    // Boolean return at $B40, and return normally. This runs from Startup-Sequence
    // before the unchanged WriteProbe; it never bypasses a filesystem operation.
    private static void AddGuestCacheCommand(byte[] seed)
    {
        byte[] Block(uint key) => seed.AsSpan((int)key * 512, 512).ToArray();
        void Save(uint key, byte[] bytes) => bytes.CopyTo(seed, (int)key * 512);
        uint Find(uint parent, string name)
        {
            var directory = Block(parent);
            for (int slot = 0; slot < 72; slot++)
                for (var key = Get(directory, 24 + slot * 4); key != 0; key = Get(Block(key), 496))
                {
                    var entry = Block(key);
                    if (Encoding.ASCII.GetString(entry, 433, entry[432]) == name) return key;
                }
            throw new InvalidDataException($"Missing seed entry {name}.");
        }
        var map = Block(FfsHardDiskProbe.Root + 1);
        uint Allocate()
        {
            for (uint key = 2; key < 3520; key++)
            {
                var bit = key - 2; var at = 4 + (int)(bit / 32) * 4; var mask = 1u << (int)(bit % 32);
                if ((Get(map, at) & mask) == 0) continue;
                Put(map, at, Get(map, at) & ~mask); return key;
            }
            throw new InvalidDataException("Seed has no space for cache command.");
        }
        ushort[] instructions = [0x48E7, 0x3F3E, 0x2C78, 4, 0x43FA, 44, 0x7000,
            0x4EAE, 0xFDD8, 0x2C40, 0x41FA, 44, 0x2208, 0x7420, 0x4EAE, 0xFD24,
            0x21C0, 0x0B40, 0x224E, 0x2C78, 4, 0x4EAE, 0xFE62, 0x4CDF, 0x7CFC, 0x7000, 0x4E75];
        var code = new byte[72];
        for (int i = 0; i < instructions.Length; i++) BinaryPrimitives.WriteUInt16BigEndian(code.AsSpan(i * 2), instructions[i]);
        Encoding.ASCII.GetBytes("dos.library\0DH0:\0").CopyTo(code, 54);
        var hunk = new byte[108];
        uint[] layout = [0x3F3, 0, 1, 0, 0, 18, 0x3E9, 18];
        for (int i = 0; i < layout.Length; i++) Put(hunk, i * 4, layout[i]);
        code.CopyTo(hunk, 32); Put(hunk, 104, 0x3F2);
        var directoryKey = Find(FfsHardDiskProbe.Root, "C"); var directory = Block(directoryKey);
        var headerKey = Allocate(); var dataKey = Allocate(); var header = new byte[512];
        Put(header, 0, 2); Put(header, 4, headerKey); Put(header, 8, 1); Put(header, 16, dataKey);
        Put(header, 308, dataKey); Put(header, 324, (uint)hunk.Length); Put(header, 500, directoryKey); Put(header, 508, 0xFFFFFFFD);
        header[432] = 5; Encoding.ASCII.GetBytes("Cache").CopyTo(header, 433);
        uint hash = 5; foreach (var c in "CACHE") hash = (hash * 13 + c) & 0x7FF; hash %= 72;
        Put(header, 496, Get(directory, 24 + (int)hash * 4)); Put(directory, 24 + (int)hash * 4, headerKey);
        Checksum(header, 20); Checksum(directory, 20); Checksum(map, 0);
        Save(headerKey, header); hunk.CopyTo(seed, (int)dataKey * 512); Save(directoryKey, directory); Save(FfsHardDiskProbe.Root + 1, map);
        var startupKey = Find(Find(FfsHardDiskProbe.Root, "S"), "Startup-Sequence"); var startup = Block(startupKey);
        var script = Encoding.ASCII.GetBytes("Cache\nWriteProbe\n");
        script.CopyTo(seed, (int)Get(startup, 308) * 512); Put(startup, 324, (uint)script.Length);
        Checksum(startup, 20); Save(startupKey, startup);
    }
    internal string Observe()
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        byte[] Block(uint key) { file.Position = Start + (long)key * 512; var b = new byte[512]; file.ReadExactly(b); return b; }
        void Valid(byte[] b) { uint sum = 0; for (int i = 0; i < 512; i += 4) sum = unchecked(sum + Get(b, i)); Assert.Equal(0u, sum); }
        uint Find(uint dir, string name)
        {
            var b = Block(dir); Valid(b);
            for (int i = 0; i < 72; i++) for (var key = Get(b, 24 + i * 4); key != 0; key = Get(Block(key), 496))
            { var e = Block(key); if (Encoding.ASCII.GetString(e, 433, e[432]) == name) return key; }
            return 0;
        }
        var data = Find(Root, "Data"); Assert.NotEqual(0u, data); var saved = Find(data, "Saved"); Assert.InRange(saved, (forceUpperAllocation || existingSaved) ? Upper : 2u, Blocks - 1);
        Assert.Equal(0u, Find(data, "Native")); Assert.Equal(0u, Find(data, "Trash"));
        var h = Block(saved); Valid(h); Assert.Equal(2049u, Get(h, 324)); Assert.Equal(0x20u, Get(h, 320));
        Assert.Equal(1234u, Get(h, 420)); Assert.Equal(56u, Get(h, 424)); Assert.Equal(78u, Get(h, 428));
        Assert.Equal("notes", Encoding.ASCII.GetString(h, 329, h[328]));
        var payload = new List<byte>(); for (int i = 0; i < 5; i++) { var key = Get(h, 308 - i * 4); Assert.InRange(key, (forceUpperAllocation || existingSaved) ? Upper : 2u, Blocks - 1); payload.AddRange(Block(key)); }
        Assert.Equal(FfsHardDiskProbe.Expected(), payload.Take(2049).ToArray());
        // Independently walk the large filler files: they must still own each
        // lower data sector exactly once, excluding the bitmap/root records.
        uint lower = forceUpperAllocation ? 2u : Upper;
        for (int f = 0; lower < Upper; f++)
        {
            var head = Find(Root, $"Lower{f}"); Assert.InRange(head, Upper, Blocks - 1);
            var declared = Get(Block(head), 324); Assert.InRange(declared, 1u, (uint)int.MaxValue);
            uint bytes = 0;
            for (var table = head; table != 0;)
            {
                var b = Block(table); Valid(b); Assert.Equal(table, Get(b, 4));
                Assert.Equal(table == head ? Root : head, Get(b, 500));
                var count = Get(b, 8); Assert.InRange(count, 1u, 72u);
                for (int i = 0; i < count; i++)
                {
                    if (lower == Root) lower = Root + 1 + Maps + Extensions;
                    Assert.Equal(lower++, Get(b, 308 - i * 4)); bytes += 512;
                }
                table = Get(b, 504);
            }
            Assert.Equal(declared, bytes);
        }
        Assert.Equal(Upper, lower);
        uint free = 0; for (uint i = 0; i < Maps; i++) { var b = Block(Root + 1 + i); Valid(b); for (int j = 4; j < 512; j += 4) free += (uint)System.Numerics.BitOperations.PopCount(Get(b, j)); }
        Assert.Equal(freeBefore - (existingSaved ? 0u : 6u), free); Assert.Equal(uint.MaxValue, Get(Block(Root), 312));
        file.Position = 0; var actual = new byte[prefix.Length]; file.ReadExactly(actual); Assert.Equal(prefix, actual);
        file.Position = 0; return Convert.ToHexString(SHA256.HashData(file));
    }
}
