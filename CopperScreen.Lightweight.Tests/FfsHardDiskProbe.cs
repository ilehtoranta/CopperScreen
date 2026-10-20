using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace CopperScreen.Tests;

// Independent host inspection of the original producer fixture and its native
// WriteProbe receipts. This never writes guest RAM or services guest DOS calls.
internal static class FfsHardDiskProbe
{
    internal const int Start = 131072, Bytes = 1802240, Root = 1760;
    internal const uint Complete = 0x46465357, Failed = 0x4641494C;
    internal static uint Long(byte[] data, int offset) => BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset));
    private static void Put(byte[] data, int offset, uint value) => BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(offset), value);
    private static uint Word(byte[] data, uint block, int offset) => Long(data, Start + checked((int)block * 512) + offset);
    private static void PutWord(byte[] data, uint block, int offset, uint value) => Put(data, Start + checked((int)block * 512) + offset, value);
    private static void Checksum(byte[] data, uint block)
    {
        uint sum = 0; for (var offset = 0; offset < 512; offset += 4) sum = unchecked(sum + Word(data, block, offset));
        Assert.Equal(0u, sum);
    }
    private static void FixChecksum(byte[] data, uint block, int check)
    {
        PutWord(data, block, check, 0);
        uint sum = 0; for (var offset = 0; offset < 512; offset += 4) sum = unchecked(sum + Word(data, block, offset));
        PutWord(data, block, check, unchecked(0u - sum));
    }
    private static IEnumerable<(uint Block, uint Previous, int Bucket)> Entries(byte[] data, uint directory)
    {
        for (var slot = 0; slot < 72; slot++)
        {
            uint previous = 0;
            for (var block = Word(data, directory, 24 + slot * 4); block != 0; block = Word(data, block, 496))
            {
                yield return (block, previous, slot);
                previous = block;
            }
        }
    }
    private static string Name(byte[] data, uint block)
    {
        var at = Start + (int)block * 512 + 432;
        return Encoding.ASCII.GetString(data, at + 1, data[at]);
    }
    private static uint Find(byte[] data, uint directory, string name) => Entries(data, directory)
        .Where(e => Name(data, e.Block) == name).Select(e => e.Block).SingleOrDefault();
    private static uint[] FileBlocks(byte[] data, uint header)
    {
        Assert.Equal(0u, Word(data, header, 504)); // This 2049-byte probe needs no extension block.
        return Enumerable.Range(0, checked((int)Word(data, header, 8)))
            .Select(i => Word(data, header, 308 - i * 4)).Prepend(header).ToArray();
    }
    internal static byte[] Pristine()
    {
        var data = File.ReadAllBytes(Environment.GetEnvironmentVariable("COPPERSCREEN_COPPERSTART_HDF")!);
        Assert.Equal("74E8A6F56EC6941E66AFF1B653CB13975213026526E4F16BDD8915E55A750046", Convert.ToHexString(SHA256.HashData(data)));
        AssertDisk(data, data);
        // Reuse the exact qualified original WriteProbe HUNK. Remove its saved
        // result and release/clear those six blocks to force real guest writes.
        var directory = Find(data, Root, "Data");
        var saved = Entries(data, directory).Single(e => Name(data, e.Block) == "Saved");
        var blocks = FileBlocks(data, saved.Block);
        Assert.Equal(6, blocks.Length);
        PutWord(data, saved.Previous == 0 ? directory : saved.Previous,
            saved.Previous == 0 ? 24 + saved.Bucket * 4 : 496, Word(data, saved.Block, 496));
        var bitmap = Word(data, Root, 316);
        foreach (var block in blocks)
        {
            var bit = block - 2;
            PutWord(data, bitmap, 4 + (int)(bit / 32) * 4, Word(data, bitmap, 4 + (int)(bit / 32) * 4) | 1u << (int)(bit % 32));
            Array.Clear(data, Start + (int)block * 512, 512);
        }
        FixChecksum(data, directory, 20); FixChecksum(data, bitmap, 0);
        Checksum(data, directory); Checksum(data, bitmap);
        Assert.Equal(0u, Find(data, directory, "Saved"));
        return data;
    }
    internal static byte[] Expected()
    {
        var bytes = Enumerable.Range(0, 2049).Select(i => (byte)(i * 37 + 11)).ToArray();
        Enumerable.Range(0, 32).Select(i => (byte)(255 - i)).ToArray().CopyTo(bytes, 509);
        return bytes;
    }
    internal static void AssertDisk(byte[] original, byte[] data)
    {
        Assert.Equal(original.Length, data.Length);
        Assert.Equal(original[..Start], data[..Start]);
        Assert.Equal(original[(Start + Bytes)..], data[(Start + Bytes)..]);
        var directory = Find(data, Root, "Data"); Assert.NotEqual(0u, directory);
        var saved = Find(data, directory, "Saved"); Assert.NotEqual(0u, saved);
        Assert.Equal(0u, Find(data, directory, "Native")); Assert.Equal(0u, Find(data, directory, "Trash"));
        Assert.Equal(2049u, Word(data, saved, 324)); Assert.Equal(0x20u, Word(data, saved, 320));
        Assert.Equal(1234u, Word(data, saved, 420)); Assert.Equal(56u, Word(data, saved, 424)); Assert.Equal(78u, Word(data, saved, 428));
        Assert.Equal("notes", Encoding.ASCII.GetString(data, Start + (int)saved * 512 + 329, data[Start + (int)saved * 512 + 328]));
        var blocks = FileBlocks(data, saved); Assert.Equal(6, blocks.Length);
        var stored = blocks.Skip(1).SelectMany(block => data.AsSpan(Start + (int)block * 512, 512).ToArray()).Take(2049).ToArray();
        Assert.Equal(Expected(), stored);
        var bitmap = Word(data, Root, 316);
        Assert.Equal(uint.MaxValue, Word(data, Root, 312));
        int Free(byte[] image) => Enumerable.Range(2, Bytes / 512 - 2)
            .Count(block => (Word(image, bitmap, 4 + (block - 2) / 32 * 4) & (1u << ((block - 2) % 32))) != 0);
        var allocated = Find(original, Find(original, Root, "Data"), "Saved") == 0 ? 6 : 0;
        Assert.Equal(Free(original) - allocated, Free(data));
        foreach (var block in blocks) Assert.Equal(0u, Word(data, bitmap, 4 + (int)((block - 2) / 32) * 4) & (1u << (int)((block - 2) % 32)));
        foreach (var block in new[] { (uint)Root, bitmap, directory, saved }) Checksum(data, block);
    }
    internal static byte[] Backing(string path)
    {
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var bytes = new byte[input.Length]; input.ReadExactly(bytes); return bytes;
    }
    internal static void AssertReceipt(byte[] chip, byte[] slow, uint phase, ulong start = Start, uint blocks = 3520)
    {
        uint Read(uint address) => address < chip.Length ? Long(chip, (int)address) : Long(slow, checked((int)(address - 0xC00000)));
        Assert.Equal(Complete, Read(0xB00)); Assert.Equal(phase, Read(0xB08));
        Assert.Equal(2049u, Read(0xB14)); Assert.Equal(0u, Read(0xB18));
        Assert.Equal(0x20u, Read(0xB1C)); Assert.Equal(1234u, Read(0xB20)); Assert.Equal(56u, Read(0xB24)); Assert.Equal(78u, Read(0xB28));
        Assert.NotEqual(0u, Read(0xB38)); Assert.NotEqual(0u, Read(0xB3C)); // DeviceProc + successful ACTION_FLUSH.
        var address = Read(0xB04);
        var payload = address < chip.Length ? chip.AsSpan((int)address, 2049) : slow.AsSpan((int)(address - 0xC00000), 2049);
        Assert.Equal(Expected(), payload.ToArray());
        // Pinned-ROM private diagnostic layout; not a public ABI requirement.
        var state = Read(Read(0x880) + 12); var request = Read(state + 4);
        Assert.Equal(4u << 16, Read(request + 28) & 0xFFFF00FF); // CMD_UPDATE, io_Error=0.
        Assert.Equal(0u, Read(request + 32)); Assert.Equal(blocks, Read(state + 1600));
        Assert.Equal((uint)start, Read(state + 1620)); Assert.Equal((uint)(start >> 32), Read(state + 1616));
        Assert.Equal(0u, Read(Read(0x840) + 20)); // No DF0 bootstrap attempt.
    }
    internal static bool HasCompletedFlush(byte[] chip, byte[] slow)
    {
        uint Read(uint address) => address <= chip.Length - 4 ? Long(chip, (int)address)
            : address >= 0xC00000 && address - 0xC00000 <= slow.Length - 4 ? Long(slow, (int)(address - 0xC00000)) : 0;
        var control = Read(0x880); if (control == 0) return false;
        var state = Read(control + 12); if (state == 0) return false;
        var request = Read(state + 4); if (request == 0) return false;
        return (Read(request + 28) & 0xFFFF00FF) == 4u << 16 && Read(request + 32) == 0;
    }
}
