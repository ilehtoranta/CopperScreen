using CopperDisk;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

public sealed class CopperHdfDiscardTests
{
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

    private static void WithDisk(bool supported, bool readOnly, Action<LightweightHdfBus, Hook> test)
    {
        var path = Path.GetTempFileName();
        try
        {
            using (var file = File.OpenWrite(path)) file.SetLength(16 * 512);
            var hook = new Hook { SupportsDiscard = supported };
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

    private static void Execute(LightweightHdfBus bus) => Assert.True(bus.CopperHdf.TryExecuteIoRequest(bus, 0x1100));

    private sealed class Hook : IAmigaHardfileDiscardIoHook
    {
        public bool SupportsDiscard { get; init; }
        public uint MaximumDiscardSectors => 8;
        public bool FlushOnDispose => true;
        public int Discards;
        public void Read(FileStream backing, long offset, Span<byte> destination) { backing.Position = offset; backing.ReadExactly(destination); }
        public void Write(FileStream backing, long offset, ReadOnlySpan<byte> source) { backing.Position = offset; backing.Write(source); }
        public void Flush(FileStream backing) => backing.Flush();
        public void Discard(FileStream backing, long offset, int bytes)
        { Discards++; backing.Position = offset; backing.Write(Enumerable.Repeat((byte)0xA5, bytes).ToArray()); }
    }
}
