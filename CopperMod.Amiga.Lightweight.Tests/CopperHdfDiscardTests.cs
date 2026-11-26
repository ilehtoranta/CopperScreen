using CopperDisk;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

public sealed class CopperHdfDiscardTests
{
    [Fact]
    public void ProductionUnmapReclaimsStorageAboveFourGiBAndPreservesAdjacentData()
    {
        var path = Path.GetTempFileName();
        const long offset = 5L * 1024 * 1024 * 1024;
        try
        {
            using (var file = File.OpenWrite(path))
            {
                Assert.True(AmigaSparseFile.TryEnable(file));
                file.SetLength(offset + 1024 * 1024);
                file.Position = offset; file.Write(Enumerable.Repeat((byte)0xA7, 1024 * 1024).ToArray());
                file.Flush(flushToDisk: true);
            }
            var before = AmigaSparseFile.GetAllocatedBytes(path);
            using (var machine = new LightweightA500Machine(new() { Hardfiles = [new(0, path) { SparseStorage = true }] }))
            {
                var bus = machine.HardfileBus!;
                bus.WriteLong(0x1000, 0); bus.WriteLong(0x1118, 0x1000);
                Command(bus, 0x12, 6, 64);
                bus.WriteByte(0x1401, 1); bus.WriteByte(0x1402, 0xB2); bus.WriteByte(0x1404, 64);
                Execute(bus); Assert.Equal(0x80, bus.ReadByte(0x1505));
                Unmap(bus, (uint)(offset / 512 + 128), 1024); Execute(bus);
                Assert.Equal(0, bus.ReadByte(0x111F));
                Assert.Equal(offset + 1024 * 1024, bus.CopperHdf.Hardfiles[0].Length);
                var after = AmigaSparseFile.GetAllocatedBytes(path);
                Assert.True(after < before, $"Host allocation before={before}, after={after}");
                Assert.All(bus.CopperHdf.Hardfiles[0].ReadBytes(offset, 65536), value => Assert.Equal(0xA7, value));
                Assert.All(bus.CopperHdf.Hardfiles[0].ReadBytes(offset + 589824, 65536), value => Assert.Equal(0xA7, value));
            }
            using var reopened = File.OpenRead(path); Assert.Equal(offset + 1024 * 1024, reopened.Length);
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void UnmapRequiresExplicitProviderAndWritableDevice(bool supported, bool readOnly)
    {
        WithDisk(supported, readOnly, (bus, hook) =>
        {
            Command(bus, 0x12, 6, 64);
            bus.WriteByte(0x1401, 1); bus.WriteByte(0x1402, 0xB2); bus.WriteByte(0x1404, 64);
            Execute(bus);
            Assert.Equal(supported && !readOnly ? 0x80 : 0, bus.ReadByte(0x1505));
            Unmap(bus, 2, 3); Execute(bus);
            Assert.Equal(supported && !readOnly ? 0 : -3, unchecked((sbyte)bus.ReadByte(0x111F)));
            Assert.Equal(supported && !readOnly ? 1 : 0, hook.Discards);
            Assert.All(bus.CopperHdf.Hardfiles[0].ReadBytes(2 * 512, 3 * 512),
                value => Assert.Equal(supported && !readOnly ? 0xA5 : 0, value));
        });
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void InvalidUnmapNeverReachesProvider(int fault)
    {
        WithDisk(true, false, (bus, hook) =>
        {
            Unmap(bus, 2, 3);
            if (fault == 0) bus.WriteWord(0x1310, 6);
            if (fault == 1) bus.WriteByte(0x1401, 1); // unsupported ANCHOR
            if (fault == 2) bus.WriteByte(0x1408, 7);
            if (fault == 3) bus.WriteWord(0x1502, 32);
            if (fault == 4) bus.WriteLong(0x150C, 15); // partition end
            if (fault == 5) bus.WriteLong(0x1510, 9); // advertised maximum
            Execute(bus);
            Assert.NotEqual(0, bus.ReadByte(0x111F)); Assert.Equal(0, hook.Discards);
        });
    }

    [Fact]
    public void VpdLimitsAndCapacityDescribeProviderWithoutPromisingZeroReads()
    {
        WithDisk(true, false, (bus, hook) =>
        {
            Command(bus, 0x12, 6, 64);
            bus.WriteByte(0x1401, 1); bus.WriteByte(0x1402, 0xB0); bus.WriteByte(0x1404, 64);
            Execute(bus); Assert.Equal(8u, bus.ReadLong(0x1514)); Assert.Equal(1u, bus.ReadLong(0x1518));
            Command(bus, 0x9E, 16, 32); bus.WriteByte(0x1401, 0x10); bus.WriteLong(0x140A, 32);
            Execute(bus); Assert.Equal(15u, bus.ReadLong(0x1504)); Assert.Equal(512u, bus.ReadLong(0x1508));
            Assert.Equal(0x80, bus.ReadByte(0x150E)); Assert.Equal(0, hook.Discards);
        });
    }

    [Theory]
    [InlineData(1u)] [InlineData(4u)] [InlineData(5000u)]
    public void VpdReportsTheProvidersDescriptorLimit(uint descriptors)
    {
        WithDisk(true, false, (bus, hook) =>
        {
            Command(bus, 0x12, 6, 64);
            bus.WriteByte(0x1401, 1); bus.WriteByte(0x1402, 0xB0); bus.WriteByte(0x1404, 64);
            Execute(bus); Assert.Equal(Math.Min(descriptors, 4095u), bus.ReadLong(0x1518));
        }, descriptors);
    }

    // An opted-in provider accepts several ranges in one UNMAP; each reaches
    // Discard separately and data between the ranges is left alone.
    [Fact]
    public void MultiRangeUnmapDiscardsEveryRangeAndPreservesGaps()
    {
        WithDisk(true, false, (bus, hook) =>
        {
            bus.CopperHdf.Hardfiles[0].Write(0, Enumerable.Repeat((byte)0x3C, 16 * 512).ToArray());
            UnmapRanges(bus, (1, 2), (5, 1), (9, 3)); Execute(bus);
            Assert.Equal(0, bus.ReadByte(0x111F));
            Assert.Equal(3, hook.Discards);
            Assert.Equal(new long[] { 512, 5 * 512, 9 * 512 }, hook.Offsets);
            var data = bus.CopperHdf.Hardfiles[0].ReadBytes(0, 16 * 512);
            for (var sector = 0; sector < 16; sector++)
            {
                var discarded = sector is 1 or 2 or 5 or 9 or 10 or 11;
                Assert.All(data.AsSpan(sector * 512, 512).ToArray(), value => Assert.Equal(discarded ? 0xA5 : 0x3C, value));
            }
        }, descriptors: 4);
    }

    // Each case is refused before any range is discarded: too many ranges for
    // the provider, more than the default single range, a sum over MAXIMUM
    // UNMAP LBA COUNT although every range fits, and a bad last range.
    [Theory]
    [InlineData(4u, 5)] [InlineData(1u, 2)] [InlineData(4u, -1)] [InlineData(4u, -2)]
    public void MultiRangeUnmapIsValidatedBeforeAnyDiscard(uint descriptors, int ranges)
    {
        WithDisk(true, false, (bus, hook) =>
        {
            if (ranges > 0) UnmapRanges(bus, Enumerable.Range(0, ranges).Select(i => ((uint)i * 2, 1u)).ToArray());
            else if (ranges == -1) UnmapRanges(bus, (0, 5), (8, 4)); // 9 sectors, maximum 8
            else UnmapRanges(bus, (0, 2), (4, 2), (15, 2)); // last range past the end
            Execute(bus);
            Assert.NotEqual(0, bus.ReadByte(0x111F)); Assert.Equal(0, hook.Discards);
        }, descriptors);
    }

    private static void WithDisk(bool supported, bool readOnly, Action<LightweightHdfBus, Hook> test, uint descriptors = 1)
    {
        var path = Path.GetTempFileName();
        try
        {
            using (var file = File.OpenWrite(path)) file.SetLength(16 * 512);
            var hook = new Hook { SupportsDiscard = supported, Descriptors = descriptors };
            using var machine = new LightweightA500Machine(new() { Hardfiles = [new(0, path, readOnly: readOnly) { IoHook = hook }] });
            var bus = machine.HardfileBus!;
            bus.WriteLong(0x1000, 0); bus.WriteLong(0x1118, 0x1000);
            test(bus, hook);
        }
        finally { File.Delete(path); }
    }

    private static void Command(LightweightHdfBus bus, byte opcode, ushort size, uint dataBytes)
    {
        bus.ClearMemory(0x1300, 0x300); bus.WriteWord(0x111C, 28);
        bus.WriteLong(0x1128, 0x1300); bus.WriteLong(0x1124, 30);
        bus.WriteLong(0x1300, 0x1500); bus.WriteLong(0x1304, dataBytes);
        bus.WriteLong(0x130C, 0x1400); bus.WriteWord(0x1310, size); bus.WriteByte(0x1400, opcode);
    }

    private static void Unmap(LightweightHdfBus bus, uint block, uint count)
    {
        Command(bus, 0x42, 10, 24); bus.WriteByte(0x1408, 24);
        bus.WriteWord(0x1500, 22); bus.WriteWord(0x1502, 16);
        bus.WriteLong(0x150C, block); bus.WriteLong(0x1510, count);
    }

    private static void UnmapRanges(LightweightHdfBus bus, params (uint Block, uint Count)[] ranges)
    {
        var bytes = 8 + ranges.Length * 16;
        Command(bus, 0x42, 10, (uint)bytes); bus.WriteByte(0x1407, (byte)(bytes >> 8)); bus.WriteByte(0x1408, (byte)bytes);
        bus.WriteWord(0x1500, (ushort)(bytes - 2)); bus.WriteWord(0x1502, (ushort)(ranges.Length * 16));
        for (var i = 0; i < ranges.Length; i++)
        {
            bus.WriteLong(0x150C + (uint)i * 16, ranges[i].Block); bus.WriteLong(0x1510 + (uint)i * 16, ranges[i].Count);
        }
    }

    private static void Execute(LightweightHdfBus bus) => Assert.True(bus.CopperHdf.TryExecuteIoRequest(bus, 0x1100));

    private sealed class Hook : IAmigaHardfileDiscardIoHook
    {
        public bool SupportsDiscard { get; init; }
        public uint Descriptors { get; init; } = 1;
        public uint MaximumDiscardSectors => 8;
        public uint MaximumDiscardDescriptors => Descriptors;
        public bool FlushOnDispose => true;
        public int Discards;
        public readonly List<long> Offsets = [];
        public void Read(FileStream backing, long offset, Span<byte> destination) { backing.Position = offset; backing.ReadExactly(destination); }
        public void Write(FileStream backing, long offset, ReadOnlySpan<byte> source) { backing.Position = offset; backing.Write(source); }
        public void Flush(FileStream backing) => backing.Flush();
        public void Discard(FileStream backing, long offset, int bytes)
        { Discards++; Offsets.Add(offset); backing.Position = offset; backing.Write(Enumerable.Repeat((byte)0xA5, bytes).ToArray()); }
    }
}
