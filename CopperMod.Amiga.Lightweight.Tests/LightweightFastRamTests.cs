using Copper68k;
using CopperDisk;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

public sealed class LightweightFastRamTests
{
    private const uint Config = 0xE80000;
    private const M68kBusAccessKind Read = M68kBusAccessKind.CpuDataRead;
    private const M68kBusAccessKind Write = M68kBusAccessKind.CpuDataWrite;

    private static void Assign(LightweightA500Machine machine, uint address)
    {
        long cycle = machine.Cycle;
        // Commodore Autoconfig: low nibble first, then high nibble.
        machine.WriteByte(Config + 0x4A, (byte)(address >> 12), ref cycle, Write);
        machine.WriteByte(Config + 0x48, (byte)(address >> 16), ref cycle, Write);
    }

    [Fact]
    public void DefaultHasNoExpansionMemoryOrAutoconfigBoard()
    {
        using var m = new LightweightA500Machine();
        long cycle = 0;
        Assert.True(m.FastRam.IsEmpty);
        Assert.Null(m.FastRamBase);
        Assert.Equal(255, m.ReadByte(Config, ref cycle, Read));
        m.WriteLong(0x200000, 0x12345678, ref cycle, Write);
        Assert.Equal(uint.MaxValue, m.ReadLong(0x200000, ref cycle, Read));
    }

    [Theory]
    [InlineData(512, 4, 0x200000u)]
    [InlineData(1024, 5, 0x200000u)]
    [InlineData(2048, 6, 0x200000u)]
    [InlineData(4096, 7, 0x200000u)]
    [InlineData(8192, 0, 0x200000u)]
    public void GuestConfiguresSizeAndRamListFlagThenUsesWholeIndependentBank(int kib, int sizeCode, uint address)
    {
        using var m = new LightweightA500Machine(new() { FastRamBytes = kib * 1024 });
        long cycle = 0;
        Assert.Equal(0xE0, m.ReadByte(Config, ref cycle, Read));
        Assert.Equal(sizeCode << 4, m.ReadByte(Config + 2, ref cycle, Read));
        Assert.Equal(0x7F, m.ReadByte(Config + 8, ref cycle, Read)); // inverted 8 MiB-space flag
        Assert.Null(m.FastRamBase);
        m.WriteWord(address, 0xBEEF, ref cycle, Write);
        Assert.Equal(0xFFFF, m.ReadWord(address, ref cycle, Read));
        Assign(m, address);
        Assert.Equal(address, m.FastRamBase);
        Assert.Equal(kib * 1024, m.FastRam.Length);
        Assert.Equal(255, m.ReadByte(Config, ref cycle, Read));
        Assert.Equal(0, m.ReadWord(address, ref cycle, Read));
        var end = address + (uint)m.FastRam.Length;
        m.WriteLong(address, 0x12345678, ref cycle, Write);
        m.WriteLong(end - 4, 0x89ABCDEF, ref cycle, Write);
        m.WriteByte(address + 1, 0x55, ref cycle, Write);
        Assert.Equal(0x12555678u, m.ReadLong(address, ref cycle, Read));
        Assert.Equal(0x89ABCDEFu, m.ReadLong(end - 4, ref cycle, Read));
        Assert.Equal(0xCDEFFFFFu, m.ReadLong(end - 2, ref cycle, Read));
        Assert.Equal(0xFFFF, m.ReadWord(end, ref cycle, Read));
        Assert.Equal(new byte[] { 0x12, 0x55, 0x56, 0x78 }, m.FastRam.Span[..4].ToArray());
        Assert.True(m.TryPeekInstructionWord(address, out var peek));
        Assert.Equal(0x1255, peek);
        Assert.False(m.TryPeekInstructionWord(end, out _));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(256 * 1024)]
    [InlineData(3 * 1024 * 1024)]
    [InlineData(16 * 1024 * 1024)]
    public void UnsupportedSizesFailBeforeExecution(int bytes)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new LightweightA500Machine(new() { FastRamBytes = bytes }));

    [Theory]
    [InlineData(0x100000u)]
    [InlineData(0x990000u)]
    [InlineData(0xA00000u)]
    [InlineData(0xC00000u)]
    public void AssignmentCannotAliasMotherboardOrMisalignRam(uint address)
    {
        using var m = new LightweightA500Machine(new() { FastRamBytes = 2 * 1024 * 1024 });
        Assert.Throws<ArgumentOutOfRangeException>(() => Assign(m, address));
        Assert.Null(m.FastRamBase);
    }

    [Fact]
    public void ShutUpAndResetsRemoveMappingButRetainRamContents()
    {
        using var m = new LightweightA500Machine(new() { FastRamBytes = 512 * 1024 });
        long cycle = 0;
        m.WriteByte(Config + 0x4C, 0, ref cycle, Write);
        Assert.Equal(255, m.ReadByte(Config, ref cycle, Read));
        Assign(m, 0x200000);
        Assert.Null(m.FastRamBase);
        m.Reset();
        Assign(m, 0x200000);
        m.WriteWord(0x200000, 0xBEEF, ref cycle, Write);
        m.ResetExternalDevices(m.Cycle);
        Assert.Null(m.FastRamBase);
        Assert.Equal(0xE0, m.ReadByte(Config, ref cycle, Read));
        Assert.Equal(0xFFFF, m.ReadWord(0x200000, ref cycle, Read));
        Assign(m, 0x280000);
        Assert.Equal(0xBEEF, m.ReadWord(0x280000, ref cycle, Read));
        m.Reset();
        Assert.Null(m.FastRamBase);
        Assign(m, 0x200000);
        Assert.Equal(0xBEEF, m.ReadWord(0x200000, ref cycle, Read));
    }

    [Fact]
    public void FastRamAvoidsRefreshContentionAndDmaAlwaysUsesChipRam()
    {
        using var m = new LightweightA500Machine(new() { FastRamBytes = 512 * 1024 });
        Assign(m, 0x200000);
        long cycle = 454;
        m.WriteWord(0x200100, 0xABCD, ref cycle, Write);
        Assert.Equal(454, cycle); // No bus wait added; core supplies its transfer clocks.
        m.WriteChipWordDma(0x200100, 0x1234);
        Assert.Equal(0x1234, m.ReadChipWordDma(0x100));
        Assert.Equal(0x1234, m.ReadChipWordDma(0x200100));
        Assert.Equal(0xABCD, m.ReadWord(0x200100, ref cycle, Read));
        m.WriteWord(0xC00100, 0x5678, ref cycle, Write);
        Assert.Equal(458, cycle); // Slow RAM still waits for Agnus refresh.
    }

    [Theory]
    [InlineData(M68kCpuModel.M68020)]
    [InlineData(M68kCpuModel.M68EC020)]
    public void AcceleratorHandlesUnalignedOperandsAndAddressWidth(M68kCpuModel model)
    {
        using var m = new LightweightA500Machine(new() { FastRamBytes = 512 * 1024, CpuModel = model });
        Assign(m, 0x200000);
        var bus = new LightweightAcceleratorBus(m, model);
        long cycle = m.Cycle;
        bus.WriteLong(0x200000, uint.MaxValue, ref cycle, Write);
        bus.WriteLong(0x200004, uint.MaxValue, ref cycle, Write);
        var start = cycle;
        bus.WriteLong(0x200001, 0x12345678, ref cycle, Write);
        Assert.Equal(12, cycle - start);
        Assert.Equal(0xFF123456u, bus.ReadLong(0x200000, ref cycle, Read));
        Assert.Equal(0x78FFFFFFu, bus.ReadLong(0x200004, ref cycle, Read));
        Assert.Equal(0x12345678u, bus.ReadLong(0x200001, ref cycle, Read));
        Assert.Equal(model == M68kCpuModel.M68EC020 ? 0xFF12 : 0xFFFF, bus.ReadHostWord(0x1200000));
        Assert.Equal(cycle, m.Cycle);
    }

    [Theory]
    [InlineData(M68kCpuModel.M68020, true)]
    [InlineData(M68kCpuModel.M68020, false)]
    [InlineData(M68kCpuModel.M68EC020, true)]
    [InlineData(M68kCpuModel.M68EC020, false)]
    public void FastCodeStaysCachedUntilGuestFlush(M68kCpuModel model, bool batch)
    {
        using var m = new LightweightA500Machine(new() { FastRamBytes = 512 * 1024, CpuModel = model }, batch);
        ushort[] code = [0x7001, 0x4E7B, 0x0002,
            0x4EB9, 0x20, 0x0180, 0x2401,
            0x33FC, 0x7207, 0x20, 0x0180, 0x4EB9, 0x20, 0x0180, 0x2601,
            0x7009, 0x4E7B, 0x0002, 0x4EB9, 0x20, 0x0180, 0x2801, 0x4E72, 0x2700];
        for (var i = 0; i < code.Length; i++) m.WriteChipWordDma(0x1000u + (uint)i * 2, code[i]);
        m.Reset();
        Assign(m, 0x200000);
        long cycle = m.Cycle;
        m.WriteLong(0x200180, 0x72014E75, ref cycle, Write);
        m.ExecuteFrame();
        Assert.True(m.Cpu.Stopped);
        Assert.Null(m.UnsupportedActiveFeature);
        Assert.Equal(1u, m.Cpu.D[2]);
        Assert.Equal(1u, m.Cpu.D[3]);
        Assert.Equal(7u, m.Cpu.D[4]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RamAndCopperHdfShareAutoconfigAndGuestBuffers(bool shutUpRam)
    {
        var path = Path.Combine(Path.GetTempPath(), "copper-fast-" + Guid.NewGuid().ToString("N") + ".hdf");
        try
        {
            AmigaHardfile.CreateBlank(path, 1024);
            using var m = new LightweightA500Machine(new()
                { FastRamBytes = 512 * 1024, Hardfiles = [new(0, path)] });
            long cycle = 0;
            if (shutUpRam) m.WriteByte(Config + 0x4C, 0, ref cycle, Write);
            else Assign(m, 0x200000);
            Assert.Equal(0xD0, m.ReadByte(Config, ref cycle, Read));
            Assign(m, 0xEA0000);
            var bus = m.HardfileBus!;
            Assert.True(bus.CopperHdf.IsConfigured);
            Assert.Equal(0xEA0000u, bus.CopperHdf.ConfiguredBase);
            Assert.Equal((byte)'c', bus.ReadByte(0xEA0100));
            if (!shutUpRam)
            {
                Assert.True(bus.IsMappedMemoryRange(0x200000, 512 * 1024));
                Assert.False(bus.IsMappedMemoryRange(0x27FFFF, 2));
                Assert.False(bus.IsMappedMemoryRange(0xFFFFFFF0, 32));
                bus.CopyToMemory(0x200000, new byte[] { 1, 2, 3, 4 });
                Assert.Equal(0x01020304u, m.ReadLong(0x200000, ref cycle, Read));
                var copy = new byte[4];
                bus.CopyFromMemory(0x200000, copy);
                Assert.Equal(new byte[] { 1, 2, 3, 4 }, copy);
                bus.ClearMemory(0x200000, 4);
                Assert.Equal(0u, bus.ReadLong(0x200000));
            }
            m.Reset();
            Assert.Null(m.FastRamBase);
            Assert.False(bus.CopperHdf.IsConfigured);
            Assert.Equal(0xE0, m.ReadByte(Config, ref cycle, Read));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void CopperHdfTransfersSectorsWithRequestUnitAndDataInFastRam()
    {
        var path = Path.Combine(Path.GetTempPath(), "copper-fast-io-" + Guid.NewGuid().ToString("N") + ".hdf");
        try
        {
            AmigaHardfile.CreateBlank(path, 1024);
            using var m = new LightweightA500Machine(new()
                { FastRamBytes = 512 * 1024, Hardfiles = [new(0, path)] });
            Assign(m, 0x200000);
            var bus = m.HardfileBus!;
            const uint unit = 0x200000, request = 0x200100, data = 0x200200;
            bus.WriteLong(unit, 0);
            bus.WriteLong(request + 0x18, unit);
            bus.WriteLong(request + 0x24, 512);
            bus.WriteLong(request + 0x28, data);
            bus.WriteLong(request + 0x2C, 512);
            var expected = Enumerable.Range(0, 512).Select(i => (byte)(i ^ 0xA5)).ToArray();
            bus.CopyToMemory(data, expected);
            bus.WriteWord(request + 0x1C, 3);
            Assert.True(bus.CopperHdf.TryExecuteIoRequest(bus, request));
            Assert.Equal(0, bus.ReadByte(request + 0x1F));
            Assert.Equal(512u, bus.ReadLong(request + 0x20));
            bus.ClearMemory(data, 512);
            bus.WriteWord(request + 0x1C, 2);
            Assert.True(bus.CopperHdf.TryExecuteIoRequest(bus, request));
            Assert.Equal(0, bus.ReadByte(request + 0x1F));
            Assert.Equal(expected, m.FastRam.Span.Slice(512, 512).ToArray());
            bus.WriteLong(request + 0x28, 0x27FF00); // Crosses the mapped bank end.
            Assert.True(bus.CopperHdf.TryExecuteIoRequest(bus, request));
            Assert.Equal(unchecked((byte)-5), bus.ReadByte(request + 0x1F));
            Assert.Equal(0u, bus.ReadLong(request + 0x20));
        }
        finally { File.Delete(path); }
    }
}
