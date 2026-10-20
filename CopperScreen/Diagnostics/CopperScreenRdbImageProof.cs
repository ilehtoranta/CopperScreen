#if COPPERSCREEN_INSTALLED_TESTS || COPPERSCREEN_INSTALLED_MEDIA_TESTS
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace CopperScreen;

// Independent inspection of the owned native RdbProbe fixture. No guest RAM
// writes, host DOS services or execution shortcuts are used by these checks.
internal static class CopperScreenRdbImageProof
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
        Check.Equal(0u, sum);
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
        Check.Equal(0u, Word(volume, header, 504));
        return Enumerable.Range(0, checked((int)Word(volume, header, 8)))
            .Select(i => Word(volume, header, 308 - i * 4)).Prepend(header).ToArray();
    }
    private static byte[] Volume(byte[] image, int part) => image.AsSpan(Start(part), PartitionBytes).ToArray();
    internal static byte[] Pristine(string fixture)
    {
        var image = File.ReadAllBytes(fixture);
        Check.Equal("000A3AF5969E52B856F59DDD1D3298CC2FF71C60ADFDEF0136369228D5C2F368", Convert.ToHexString(SHA256.HashData(image)));
        AssertDisk(image, image);
        for (var part = 0; part < 2; part++)
        {
            var volume = Volume(image, part);
            var directory = Find(volume, Root, "Data");
            var saved = Entries(volume, directory).Single(e => Name(volume, e.Block) == "Saved");
            var blocks = FileBlocks(volume, saved.Block); Check.Equal(6, blocks.Length);
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
            Check.Equal(0u, Find(volume, directory, "Saved"));
            volume.CopyTo(image, Start(part));
        }
        return image;
    }
    internal static byte[] BootFromDh0(byte[] image)
    {
        var copy = image.ToArray();
        // The qualified fixture has PART blocks 2 and 3. Swap the signed
        // priorities (+5 and -128), keeping flags and filesystem bytes intact.
        Check.Equal(0x50415254u, Word(copy, 2, 0)); Check.Equal(0x50415254u, Word(copy, 3, 0));
        Check.Equal(128u, Word(copy, 2, 4)); Check.Equal(128u, Word(copy, 3, 4));
        PutWord(copy, 2, 188, 5); PutWord(copy, 3, 188, unchecked((uint)-128));
        FixChecksum(copy, 2, 8); FixChecksum(copy, 3, 8);
        Checksum(copy, 2); Checksum(copy, 3);
        return copy;
    }
    internal static byte[] ForRestoredWrites(string fixture)
    {
        var original = File.ReadAllBytes(fixture);
        Check.Equal("000A3AF5969E52B856F59DDD1D3298CC2FF71C60ADFDEF0136369228D5C2F368", Convert.ToHexString(SHA256.HashData(original)));
        AssertDisk(original, original);
        var image = original.ToArray();
        for (var part = 0; part < 2; part++)
        {
            var volume = Volume(image, part);
            var command = Find(volume, Find(volume, Root, "C"), "RdbProbe");
            Check.NotEqual(0u, command);
            var blocks = FileBlocks(volume, command).Skip(1).ToArray();
            var bytes = blocks.SelectMany(block => volume.AsSpan((int)block * 512, 512).ToArray()).ToArray();
            for (var unit = 0; unit < 2; unit++)
            {
                var oldName = Encoding.ASCII.GetBytes($"DH{unit}:Data/Saved\0");
                var newName = Encoding.ASCII.GetBytes($"DH{unit}:Data/After\0");
                var at = bytes.AsSpan().IndexOf(oldName);
                Check.True(at >= 0 && bytes.AsSpan(at + oldName.Length).IndexOf(oldName) < 0, "Expected one native probe filename.");
                newName.CopyTo(bytes, at);
            }
            for (var i = 0; i < blocks.Length; i++) bytes.AsSpan(i * 512, 512).CopyTo(volume.AsSpan((int)blocks[i] * 512));
            volume.CopyTo(image, Start(part));
        }
        return image;
    }
    internal static void AssertDisk(byte[] original, byte[] image, string fileName = "Saved")
    {
        Check.Equal(original.Length, image.Length);
        Check.Equal(original[..Start(0)], image[..Start(0)]);
        Check.Equal(original[(Start(1) + PartitionBytes)..], image[(Start(1) + PartitionBytes)..]);
        for (var part = 0; part < 2; part++)
        {
            var before = Volume(original, part); var volume = Volume(image, part);
            var directory = Find(volume, Root, "Data"); Check.NotEqual(0u, directory);
            var saved = Find(volume, directory, fileName); Check.NotEqual(0u, saved);
            Check.Equal(2049u, Word(volume, saved, 324));
            var blocks = FileBlocks(volume, saved); Check.Equal(6, blocks.Length); Check.Equal(6, blocks.Distinct().Count());
            Check.Equal(Expected(), blocks.Skip(1).SelectMany(block => volume.AsSpan((int)block * 512, 512).ToArray()).Take(2049).ToArray());
            var bitmap = Word(volume, Root, 316); Check.Equal(uint.MaxValue, Word(volume, Root, 312));
            int Free(byte[] data) => Enumerable.Range(2, PartitionBytes / 512 - 2)
                .Count(block => (Word(data, bitmap, 4 + (block - 2) / 32 * 4) & (1u << ((block - 2) % 32))) != 0);
            var allocated = Find(before, Find(before, Root, "Data"), fileName) == 0 ? 6 : 0;
            Check.Equal(Free(before) - allocated, Free(volume));
            foreach (var block in blocks)
                Check.Equal(0u, Word(volume, bitmap, 4 + (int)((block - 2) / 32) * 4) & (1u << (int)((block - 2) % 32)));
            foreach (var block in new[] { (uint)Root, bitmap, directory, saved }) Checksum(volume, block);
            var changed = blocks.Concat(new[] { (uint)Root, bitmap, directory }).ToHashSet();
            for (uint block = 0; block < PartitionBytes / 512; block++)
                if (!changed.Contains(block)) Check.True(before.AsSpan((int)block * 512, 512).SequenceEqual(volume.AsSpan((int)block * 512, 512)), $"Unexpected DH{part} block {block} change");
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
        Check.Equal(Complete, Long(chip, 0xB80)); Check.Equal(identity, Long(chip, 0xB88));
        Check.NotEqual(0u, Long(chip, 0xB8C)); Check.NotEqual(Long(chip, 0xB8C), Long(chip, 0xB90));
        for (var part = 0; part < 2; part++)
        {
            Check.Equal(phase, Long(chip, 0xB98 + part * 4));
            var address = Long(chip, 0xBA0 + part * 4);
            Check.Equal(Expected(), address < chip.Length ? chip.AsSpan((int)address, 2049).ToArray() : slow.AsSpan((int)(address - 0xC00000), 2049).ToArray());
            Check.Equal(Long(chip, 0xB8C + part * 4), Long(chip, 0xBC0 + part * 8));
            Check.NotEqual(0u, Long(chip, 0xBC4 + part * 8));
        }
        Check.True(HasCompletedFlush(chip, slow));
        Check.NotEqual(Request(chip, slow, 0), Request(chip, slow, 1));
        Check.Equal(0u, Read(chip, slow, Read(chip, slow, 0x840) + 20)); // No DF0 boot.
    }
    internal static byte[] Expected()
    {
        var bytes = Enumerable.Range(0, 2049).Select(i => (byte)(i * 37 + 11)).ToArray();
        Enumerable.Range(0, 32).Select(i => (byte)(255 - i)).ToArray().CopyTo(bytes, 509);
        return bytes;
    }
    internal static byte[] Backing(string path)
    {
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var bytes = new byte[input.Length]; input.ReadExactly(bytes); return bytes;
    }
    private static class Check
    {
        internal static void True(bool value, string message = "RDB image/receipt check failed")
        { if (!value) throw new InvalidDataException(message); }
        internal static void Equal<T>(T expected, T actual)
        {
            var equal = expected is Array a && actual is Array b ? a.Cast<object>().SequenceEqual(b.Cast<object>()) : EqualityComparer<T>.Default.Equals(expected, actual);
            True(equal, $"RDB image/receipt mismatch: expected {expected}, actual {actual}");
        }
        internal static void NotEqual<T>(T unexpected, T actual) => True(!EqualityComparer<T>.Default.Equals(unexpected, actual), $"Unexpected RDB value {actual}");
    }
}
#endif
