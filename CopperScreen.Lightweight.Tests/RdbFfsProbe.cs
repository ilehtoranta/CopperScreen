using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace CopperScreen.Tests;

// Independent inspection of the owned native RdbProbe fixture. No guest RAM
// writes, host DOS services or execution shortcuts are used by these checks.
internal static class RdbFfsProbe
{
    internal const uint Complete = 0x52444231, Failed = 0x52444641;
    private const int PartitionBytes = 1802240, Root = 1760;
    private static int Start(int part) => (4 + part * 55) * 32768;
    internal static uint Long(byte[] data, int at) => BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(at));
    private static void Put(byte[] data, int at, uint value) => BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(at), value);
    private static uint Word(byte[] volume, uint block, int at) => Long(volume, checked((int)block * 512 + at));
    private static void PutWord(byte[] volume, uint block, int at, uint value) => Put(volume, checked((int)block * 512 + at), value);
    private static void Checksum(byte[] volume, uint block)
    {
        uint sum = 0; for (var at = 0; at < 512; at += 4) sum = unchecked(sum + Word(volume, block, at));
        Assert.Equal(0u, sum);
    }
    private static void FixChecksum(byte[] volume, uint block, int check)
    {
        PutWord(volume, block, check, 0);
        uint sum = 0; for (var at = 0; at < 512; at += 4) sum = unchecked(sum + Word(volume, block, at));
        PutWord(volume, block, check, unchecked(0u - sum));
    }
    private static IEnumerable<(uint Block, uint Previous, int Bucket)> Entries(byte[] volume, uint directory)
    {
        for (var slot = 0; slot < 72; slot++)
        {
            uint previous = 0;
            for (var block = Word(volume, directory, 24 + slot * 4); block != 0; block = Word(volume, block, 496))
            { yield return (block, previous, slot); previous = block; }
        }
    }
    private static string Name(byte[] volume, uint block) => Encoding.ASCII.GetString(volume, (int)block * 512 + 433, volume[(int)block * 512 + 432]);
    private static uint Find(byte[] volume, uint directory, string name) => Entries(volume, directory)
        .Where(e => Name(volume, e.Block) == name).Select(e => e.Block).SingleOrDefault();
    private static uint[] FileBlocks(byte[] volume, uint header)
    {
        Assert.Equal(0u, Word(volume, header, 504));
        return Enumerable.Range(0, checked((int)Word(volume, header, 8)))
            .Select(i => Word(volume, header, 308 - i * 4)).Prepend(header).ToArray();
    }
    private static byte[] Volume(byte[] image, int part) => image.AsSpan(Start(part), PartitionBytes).ToArray();
    internal static byte[] Pristine()
    {
        var image = File.ReadAllBytes(Environment.GetEnvironmentVariable("COPPERSCREEN_COPPERSTART_RDB")!);
        Assert.Equal("000A3AF5969E52B856F59DDD1D3298CC2FF71C60ADFDEF0136369228D5C2F368", Convert.ToHexString(SHA256.HashData(image)));
        AssertDisk(image, image);
        for (var part = 0; part < 2; part++)
        {
            var volume = Volume(image, part);
            var directory = Find(volume, Root, "Data");
            var saved = Entries(volume, directory).Single(e => Name(volume, e.Block) == "Saved");
            var blocks = FileBlocks(volume, saved.Block); Assert.Equal(6, blocks.Length);
            PutWord(volume, saved.Previous == 0 ? directory : saved.Previous,
                saved.Previous == 0 ? 24 + saved.Bucket * 4 : 496, Word(volume, saved.Block, 496));
            var bitmap = Word(volume, Root, 316);
            foreach (var block in blocks)
            {
                var bit = block - 2;
                PutWord(volume, bitmap, 4 + (int)(bit / 32) * 4, Word(volume, bitmap, 4 + (int)(bit / 32) * 4) | 1u << (int)(bit % 32));
                Array.Clear(volume, (int)block * 512, 512);
            }
            FixChecksum(volume, directory, 20); FixChecksum(volume, bitmap, 0);
            Checksum(volume, directory); Checksum(volume, bitmap);
            Assert.Equal(0u, Find(volume, directory, "Saved"));
            volume.CopyTo(image, Start(part));
        }
        return image;
    }
    internal static byte[] BootFromDh0(byte[] image)
    {
        var copy = image.ToArray();
        // The qualified fixture has PART blocks 2 and 3. Swap the signed
        // priorities (+5 and -128), keeping flags and filesystem bytes intact.
        Assert.Equal(0x50415254u, Word(copy, 2, 0)); Assert.Equal(0x50415254u, Word(copy, 3, 0));
        Assert.Equal(128u, Word(copy, 2, 4)); Assert.Equal(128u, Word(copy, 3, 4));
        PutWord(copy, 2, 188, 5); PutWord(copy, 3, 188, unchecked((uint)-128));
        FixChecksum(copy, 2, 8); FixChecksum(copy, 3, 8);
        Checksum(copy, 2); Checksum(copy, 3);
        return copy;
    }
    internal static void AssertDisk(byte[] original, byte[] image)
    {
        Assert.Equal(original.Length, image.Length);
        Assert.Equal(original[..Start(0)], image[..Start(0)]);
        Assert.Equal(original[(Start(1) + PartitionBytes)..], image[(Start(1) + PartitionBytes)..]);
        for (var part = 0; part < 2; part++)
        {
            var before = Volume(original, part); var volume = Volume(image, part);
            var directory = Find(volume, Root, "Data"); Assert.NotEqual(0u, directory);
            var saved = Find(volume, directory, "Saved"); Assert.NotEqual(0u, saved);
            Assert.Equal(2049u, Word(volume, saved, 324));
            var blocks = FileBlocks(volume, saved); Assert.Equal(6, blocks.Length); Assert.Equal(6, blocks.Distinct().Count());
            Assert.Equal(FfsHardDiskProbe.Expected(), blocks.Skip(1).SelectMany(block => volume.AsSpan((int)block * 512, 512).ToArray()).Take(2049).ToArray());
            var bitmap = Word(volume, Root, 316); Assert.Equal(uint.MaxValue, Word(volume, Root, 312));
            int Free(byte[] data) => Enumerable.Range(2, PartitionBytes / 512 - 2)
                .Count(block => (Word(data, bitmap, 4 + (block - 2) / 32 * 4) & (1u << ((block - 2) % 32))) != 0);
            var allocated = Find(before, Find(before, Root, "Data"), "Saved") == 0 ? 6 : 0;
            Assert.Equal(Free(before) - allocated, Free(volume));
            foreach (var block in blocks)
                Assert.Equal(0u, Word(volume, bitmap, 4 + (int)((block - 2) / 32) * 4) & (1u << (int)((block - 2) % 32)));
            foreach (var block in new[] { (uint)Root, bitmap, directory, saved }) Checksum(volume, block);
            var changed = blocks.Concat(new[] { (uint)Root, bitmap, directory }).ToHashSet();
            for (uint block = 0; block < PartitionBytes / 512; block++)
                if (!changed.Contains(block)) Assert.True(before.AsSpan((int)block * 512, 512).SequenceEqual(volume.AsSpan((int)block * 512, 512)), $"Unexpected DH{part} block {block} change");
        }
    }
    private static uint Read(byte[] chip, byte[] slow, uint address) => address <= chip.Length - 4 ? Long(chip, (int)address)
        : address >= 0xC00000 && address - 0xC00000 <= slow.Length - 4 ? Long(slow, (int)(address - 0xC00000)) : 0;
    private static uint Request(byte[] chip, byte[] slow, int part)
    {
        var port = Read(chip, slow, (uint)(0xBC0 + part * 8));
        // Pinned ROM private diagnostics, not public ABI validation rules.
        return port == 0 ? 0 : Read(chip, slow, Read(chip, slow, port - 92 + 88) + 4);
    }
    internal static bool HasCompletedFlush(byte[] chip, byte[] slow) => Enumerable.Range(0, 2).All(part =>
    {
        var request = Request(chip, slow, part);
        return request != 0 && (Read(chip, slow, request + 28) & 0xFFFF00FF) == 4u << 16 && Read(chip, slow, request + 32) == 0;
    });
    internal static void AssertReceipt(byte[] chip, byte[] slow, uint phase, uint identity = 2)
    {
        Assert.Equal(Complete, Long(chip, 0xB80)); Assert.Equal(identity, Long(chip, 0xB88));
        Assert.NotEqual(0u, Long(chip, 0xB8C)); Assert.NotEqual(Long(chip, 0xB8C), Long(chip, 0xB90));
        for (var part = 0; part < 2; part++)
        {
            Assert.Equal(phase, Long(chip, 0xB98 + part * 4));
            var address = Long(chip, 0xBA0 + part * 4);
            Assert.Equal(FfsHardDiskProbe.Expected(), address < chip.Length ? chip.AsSpan((int)address, 2049).ToArray() : slow.AsSpan((int)(address - 0xC00000), 2049).ToArray());
            Assert.Equal(Long(chip, 0xB8C + part * 4), Long(chip, 0xBC0 + part * 8));
            Assert.NotEqual(0u, Long(chip, 0xBC4 + part * 8));
        }
        Assert.True(HasCompletedFlush(chip, slow));
        Assert.NotEqual(Request(chip, slow, 0), Request(chip, slow, 1));
        Assert.Equal(0u, Read(chip, slow, Read(chip, slow, 0x840) + 20)); // No DF0 boot.
    }
}
