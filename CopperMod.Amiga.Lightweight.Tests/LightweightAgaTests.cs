using Copper68k;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

public sealed class LightweightAgaTests
{
    private static LightweightA500Machine Create(int fastRamBytes = 0) => new(new() { AgnusModel = LightweightAgnusModel.Mos8374Alice,
        DeniseModel = LightweightDeniseModel.Lisa4203, CpuModel = M68kCpuModel.M68EC020,
        ChipRamBytes = 2097152, SlowRamBytes = 0, FastRamBytes = fastRamBytes, FramebufferWidth = 1816 });
    private static void Write(LightweightA500Machine m, ushort r, ushort v)
    { m.WriteCustomRegisterFromCopper(r, v, m.Cycle); m.AdvanceHardwareTo(m.Cycle + 4); }
    private static void Configure(LightweightA500Machine m, int mode, int index, short odd = 0, short even = 0)
    {
        m.WriteChipWordDma(0x1000, 0x4E72); m.WriteChipWordDma(0x1002, 0x2700); m.Reset();
        for (var p = 0; p < 8; p++)
        {
            var pointer = 0x20000u + (uint)p * 0x4000;
            for (uint a = pointer; a < pointer + 0x4000; a += 2) m.WriteChipWordDma(a, (ushort)((index & (1 << p)) != 0 ? 0xFFFF : 0));
            Write(m, (ushort)(0xE0 + p * 4), (ushort)(pointer >> 16)); Write(m, (ushort)(0xE2 + p * 4), (ushort)pointer);
        }
        Write(m, 0x08E, 0x2C81); Write(m, 0x090, 0x2CC1);
        Write(m, 0x092, 0x38); Write(m, 0x094, (ushort)(mode == 0 ? 0xD0 : mode == 3 ? 0xB8 : 0xC8));
        Write(m, 0x108, (ushort)odd); Write(m, 0x10A, (ushort)even);
        Write(m, 0x1FC, (ushort)mode); Write(m, 0x100, 0x0210); Write(m, 0x096, 0x8300);
    }

    [Fact]
    public void CpuReadClearsOnlyTheSelectedBitplanePointerHalves()
    {
        using var m = Create();
        long cycle = m.Cycle;
        m.WriteLong(0xDFF0FC, 0x00012344, ref cycle, M68kBusAccessKind.CpuDataWrite);
        Assert.Equal(0x00012344u, m.GetBitplanePointer(7));
        m.ReadByte(0xDFF0FD, ref cycle, M68kBusAccessKind.CpuDataRead);
        Assert.Equal(0x2344u, m.GetBitplanePointer(7));
        m.WriteLong(0xDFF0FC, 0x00012344, ref cycle, M68kBusAccessKind.CpuDataWrite);
        m.WriteLong(0x4000, 0xABCD5678, ref cycle, M68kBusAccessKind.CpuDataWrite);
        // First word selects the high CPU bus half. The second sees the preceding word read.
        Assert.Equal(0xABCDABCDu, m.ReadLong(0xDFF0FC, ref cycle, M68kBusAccessKind.CpuDataRead));
        Assert.Equal(0u, m.GetBitplanePointer(7));
        Assert.Equal(0u, m.GetLiveBitplanePointer(7));
    }

    [Theory]
    [InlineData(0x080, 0)]
    [InlineData(0x084, 1)]
    [InlineData(0x120, 0)]
    [InlineData(0x13C, 7)]
    public void Cd32CpuReadStrobesCopperAndSpritePointerHalves(int offset, int index)
    {
        using var m = new LightweightA500Machine(LightweightA500Configuration.CreateCd32());
        long cycle = m.Cycle;
        uint address = 0xDFF000u + (uint)offset;
        uint Pointer() => offset < 0x120 ? m.GetCopperListPointer(index == 1) : m.GetSpritePointer(index);
        m.WriteLong(address, 0x00012344, ref cycle, M68kBusAccessKind.CpuDataWrite);
        Assert.Equal(0x00012344u, Pointer());
        m.ReadByte(address + 1, ref cycle, M68kBusAccessKind.CpuDataRead);
        Assert.Equal(0x2344u, Pointer());
        m.WriteLong(address, 0x00012344, ref cycle, M68kBusAccessKind.CpuDataWrite);
        m.WriteLong(0x4000, 0xABCD5678, ref cycle, M68kBusAccessKind.CpuDataWrite);
        Assert.Equal(0xABCDABCDu, m.ReadLong(address, ref cycle, M68kBusAccessKind.CpuDataRead));
        Assert.Equal(0u, Pointer());
    }

    [Fact]
    public void AliceAndLisaMustBePairedWithTheInitialA1200MemoryAndCpu()
    {
        using var m = Create(); Assert.True(m.IsAga); Assert.Equal(2097152, m.ChipRam.Length);
        Assert.Throws<ArgumentException>(() => new LightweightA500Machine(new() { AgnusModel = LightweightAgnusModel.Mos8374Alice }));
        Assert.Throws<ArgumentException>(() => LightweightAgnus.ValidateMemory(LightweightAgnusModel.Mos8374Alice, 1048576, 0));
        long c = 0; Assert.Equal(0x00F8, m.ReadWord(0xDFF07C, ref c, M68kBusAccessKind.CpuDataRead));
        Assert.Equal(0x2300, m.ReadWord(0xDFF004, ref c, M68kBusAccessKind.CpuDataRead) & 0x7F00);
        Assert.Equal(0x0C00, m.GetCustomRegister(0x106)); Assert.Equal(0x0011, m.GetCustomRegister(0x10C));
    }
    [Theory]
    [InlineData(0, 46u)] [InlineData(1, 46u)] [InlineData(2, 46u)] [InlineData(3, 46u)]
    public void AllEightPlanesAdvanceOnePayloadPerFetchAndOneModuloPerLine(int mode, uint oddAdvance)
    {
        using var m = Create(); Configure(m, mode, 255, 6, 10);
        m.AdvanceHardwareTo(45L * 454);
        for (var p = 0; p < 8; p++) Assert.Equal(0x20000u + (uint)p * 0x4000 + (p % 2 == 0 ? oddAdvance : 50), m.GetLiveBitplanePointer(p));
    }

    [Theory]
    [InlineData(0, false, 48u)]
    [InlineData(0, true, 16u)]
    [InlineData(1, false, 128u)]
    [InlineData(1, true, 24u)]
    public void TerminalFetchLatchesModuloBeforeRegisterWritesOnItsAddressCck(
        int plane, bool writeOnAddressCck, uint advance)
    {
        using var m = Create();
        Configure(m, mode: 3, index: 0, even: 8);
        Write(m, LightweightRegisters.Ddfstop, 0x50);
        Write(m, LightweightRegisters.Bplcon0, 0x2200);

        var lineStart = 44L * LightweightClock.CpuCyclesPerLine;
        var outputCycle = lineStart + (plane == 0 ? 0x60 : 0x5C) * 2;
        var writeCycle = outputCycle - (writeOnAddressCck ? 2 : 4);
        m.AdvanceHardwareTo(writeCycle);
        var register = plane == 0 ? LightweightRegisters.Bpl1mod : LightweightRegisters.Bpl2mod;
        m.WriteCustomRegisterFromCopper(register, (ushort)(plane == 0 ? 0x20 : 0x70), writeCycle);

        m.AdvanceHardwareTo(outputCycle);
        Assert.Equal(outputCycle, m.BitplaneLastOutputCycle);
        Assert.Equal(plane, m.BitplaneLastPlane);
        Assert.Equal(0x20000u + (uint)plane * 0x4000 + advance, m.GetLiveBitplanePointer(plane));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void EightPlanesSelectBankSevenAndPreserveFullRgb24(int mode)
    {
        using var m = Create(); Configure(m, mode, 255);
        Write(m, 0x106, 0xEC00); Write(m, 0x1BE, 0x0123);
        Write(m, 0x106, 0xEE00); Write(m, 0x1BE, 0x0456);
        Write(m, 0x106, 0x0C00); Write(m, 0x180, 0x0F00);
        m.ExecuteFrame(); m.ExecuteFrame();
        Assert.Equal(unchecked((int)0xFF142536), m.Framebuffer.Span[100 * 1816 + 700]);
        Assert.Null(m.UnsupportedActiveFeature);
    }
    [Fact]
    public void HighNibbleWritesInitializeLowNibblesAndBankWritesDoNotAlias()
    {
        using var m = Create(); Configure(m, 3, 32);
        Write(m, 0x106, 0x2C00); Write(m, 0x180, 0x0A5C);
        Write(m, 0x106, 0x0C00); Write(m, 0x180, 0x0FFF);
        m.ExecuteFrame(); m.ExecuteFrame();
        Assert.Equal(unchecked((int)0xFFAA55CC), m.Framebuffer.Span[100 * 1816 + 700]);
    }
    [Theory]
    [InlineData(1, 0u, 0x11223344UL)] [InlineData(1, 2u, 0x33443344UL)]
    [InlineData(2, 0u, 0x11221122UL)] [InlineData(2, 2u, 0x33443344UL)]
    [InlineData(3, 0u, 0x1122334455667788UL)] [InlineData(3, 2u, 0x3344334477887788UL)]
    [InlineData(3, 4u, 0x5566778855667788UL)] [InlineData(3, 6u, 0x7788778877887788UL)]
    public void WideFetchAlignmentAndReplicationUseTheAcceptedPointer(int mode, uint offset, ulong expected)
    {
        using var m = Create(); m.WriteChipWordDma(0x2000, 0x1122); m.WriteChipWordDma(0x2002, 0x3344);
        m.WriteChipWordDma(0x2004, 0x5566); m.WriteChipWordDma(0x2006, 0x7788);
        Assert.Equal(expected, m.ReadAgaBitplane(0x2000 + offset, mode));
    }
    [Fact]
    public void AcceptedWideFetchKeepsItsAddressAndModeAndSamplesRamAtOutput()
    {
        using var m = Create(); Configure(m, 1, 0);
        var input = 44L * 454 + 0x38 * 2;
        m.AdvanceHardwareTo(input);
        const uint oldAddress = 0x3C000; // plane eight, first accepted lores slot
        m.WriteCustomRegisterFromCopper(0x0FC, 6, input);
        m.WriteCustomRegisterFromCopper(0x0FE, 0, input);
        m.WriteCustomRegisterFromCopper(0x1FC, 3, input);
        m.WriteChipWordDma(oldAddress, 0x1234); m.WriteChipWordDma(oldAddress + 2, 0xABCD);
        m.AdvanceHardwareTo(input + 2);
        Assert.Equal(7, m.BitplaneLastPlane); Assert.Equal(oldAddress, m.BitplaneLastAddress);
        Assert.Equal(0x1234, m.GetBitplaneDataLatch(7)); Assert.Equal(oldAddress + 4, m.GetLiveBitplanePointer(7));
    }

    [Fact]
    public void EarlyDdfStartWaitsForAliceAndFetchesEveryRasterline()
    {
        using var m = Create();
        Configure(m, 3, 127);
        Write(m, 0x100, 0x7201);
        Write(m, 0x092, 0x0010);
        Write(m, 0x094, 0x00A0);
        Write(m, 0x106, 0x6C00);
        Write(m, 0x1BE, 0x0F00);
        Write(m, 0x106, 0x0C00);

        var firstLine = 44L * 454;
        m.AdvanceHardwareTo(firstLine + 0x18 * 2);
        Assert.Equal(long.MinValue, m.BitplaneLastOutputCycle);
        for (var line = 1; line <= 3; line++)
        {
            m.AdvanceHardwareTo(firstLine + line * 454);
            Assert.Equal(0x20000u + (uint)line * 48, m.GetLiveBitplanePointer(0));
        }

        m.ExecuteFrame();
        m.ExecuteFrame();
        var pixels = m.Framebuffer.Span;
        Assert.Equal(unchecked((int)0xFFFF0000), pixels[44 * 1816 + 800]);
        Assert.Equal(pixels[44 * 1816 + 800], pixels[45 * 1816 + 800]);
    }

    [Fact]
    public void AlignedChipLongUsesOneGrantAndMisalignedLongKeepsWordPhases()
    {
        using var m = Create(); var bus = new LightweightAcceleratorBus(m, M68kCpuModel.M68EC020);
        long c = 20; bus.WriteLong(0x2000, 0x12345678, ref c, M68kBusAccessKind.CpuDataWrite);
        Assert.Equal(24, c); Assert.Equal(0x1234, m.ReadChipWordDma(0x2000)); Assert.Equal(0x5678, m.ReadChipWordDma(0x2002));
        bus.WriteLong(0x2002, 0xAABBCCDD, ref c, M68kBusAccessKind.CpuDataWrite); Assert.Equal(32, c);
        Assert.Equal(0xAABB, m.ReadChipWordDma(0x2002)); Assert.Equal(0xCCDD, m.ReadChipWordDma(0x2004));
    }

    [Theory]
    [InlineData(1, 4)]
    [InlineData(2, 4)]
    [InlineData(3, 8)]
    public void MisalignedBitplaneFetchAdvancesFromTheAlignedAddress(int mode, int stride)
    {
        using var m = Create();
        Configure(m, mode, 0);
        const uint bank = 0x3C000;
        Write(m, 0xFE, 0xC002);
        for (uint offset = 0; offset < 16; offset += 2)
            m.WriteChipWordDma(bank + offset, (ushort)(0xA000 + offset));
        var first = 44L * 454 + 0x38 * 2 + 2;
        m.AdvanceHardwareTo(first);
        Assert.Equal(bank + 2, m.BitplaneLastAddress);
        Assert.Equal(0xA002, m.GetBitplaneDataLatch(7));
        Assert.Equal(bank + (uint)stride, m.GetLiveBitplanePointer(7));
        m.AdvanceHardwareTo(first + stride * 8);
        Assert.Equal(bank + (uint)stride, m.BitplaneLastAddress);
        Assert.Equal(0xA000 + stride, m.GetBitplaneDataLatch(7));
    }

    [Fact]
    public void GayleRegisterBanksShareWritesAndCardStatusReadback()
    {
        using var m = Create();
        long cycle = 20;
        const M68kBusAccessKind read = M68kBusAccessKind.CpuDataRead, write = M68kBusAccessKind.CpuDataWrite;
        m.WriteLong(0xDBA000, 0x85000000, ref cycle, write);
        Assert.Equal(0x85000000u, m.ReadLong(0xDAA000, ref cycle, read));
        m.WriteLong(0xDA8000, 0x42000000, ref cycle, write);
        Assert.Equal(0x42000000u, m.ReadLong(0xDB8000, ref cycle, read));
        m.WriteByte(0xDB8000, 0, ref cycle, write);
        Assert.Equal(0, m.ReadByte(0xDA8000, ref cycle, read));
    }

    [Theory]
    [InlineData(false, 0xD1)]
    [InlineData(true, 0xF1)]
    public void IdentificationBanksShareEightSerialBitsAndAnyWriteRestartsThem(bool cd32, byte id)
    {
        using var m = cd32 ? new LightweightA500Machine(LightweightA500Configuration.CreateCd32()) : Create();
        long cycle = 20;
        for (var i = 0; i < 10; i++)
        {
            var expected = (i < 8 && (id & (0x80 >> i)) != 0) ? 0x80000000u : 0;
            Assert.Equal(expected, m.ReadLong(i % 2 == 0 ? 0xDD1000u : 0xDE1000u, ref cycle, M68kBusAccessKind.CpuDataRead));
        }
        m.WriteLong(0xDDFFFC, 0, ref cycle, M68kBusAccessKind.CpuDataWrite);
        Assert.Equal(0x80000000u, m.ReadLong(0xDE1000, ref cycle, M68kBusAccessKind.CpuDataRead));
    }
    [Fact]
    public void GayleIdPeeksDoNotStrobeAndAnEmptyAtaBusFloatsHigh()
    {
        using var m = Create(); long c = 20; var io = new LightweightA1200Io();
        foreach (var expected in new byte[] { 0x80, 0x80, 0, 0x80, 0, 0, 0, 0x80 })
        {
            Assert.Equal(expected, io.PeekByte(0xDE1000));
            Assert.Equal(expected, io.PeekByte(0xDE1000));
            Assert.Equal(expected, io.ReadByte(0xDE1000));
            Assert.Equal(expected, m.ReadByte(0xDE1000, ref c, M68kBusAccessKind.CpuDataRead));
        }
        Assert.Equal(0xFFFF, m.ReadWord(0xDA0000, ref c, M68kBusAccessKind.CpuDataRead));
        Assert.Equal(0xFFFF, m.ReadWord(0x600000, ref c, M68kBusAccessKind.CpuDataRead));
        m.WriteByte(0xDE1000, 0, ref c, M68kBusAccessKind.CpuDataWrite);
        Assert.Equal(0x80, m.ReadByte(0xDE1000, ref c, M68kBusAccessKind.CpuDataRead));
    }

    [Theory]
    [InlineData(0x2001u, 4, 0x23456789u, 0x01CAFE67u)]
    [InlineData(0x2003u, 8, 0x6789ABCDu, 0x45CAFEABu)]
    public void OddChipOperandsUseOneGrantPerAlignedLongword(uint address, int wordCycles, uint expectedLong, uint afterWrite)
    {
        using var machine = Create();
        var bus = new LightweightAcceleratorBus(machine, M68kCpuModel.M68EC020);
        machine.WriteChipWordDma(0x2000, 0x0123);
        machine.WriteChipWordDma(0x2002, 0x4567);
        machine.WriteChipWordDma(0x2004, 0x89AB);
        machine.WriteChipWordDma(0x2006, 0xCDEF);
        long cycle = 20;
        Assert.Equal(expectedLong, bus.ReadLong(address, ref cycle, M68kBusAccessKind.CpuDataRead));
        Assert.Equal(28, cycle);
        Assert.Equal((ushort)(expectedLong >> 16), bus.ReadWord(address, ref cycle, M68kBusAccessKind.CpuDataRead));
        Assert.Equal(28 + wordCycles, cycle);
        bus.WriteWord(address, 0xCAFE, ref cycle, M68kBusAccessKind.CpuDataWrite);
        Assert.Equal(28 + 2 * wordCycles, cycle);
        Assert.Equal((ushort)(afterWrite >> 16), machine.ReadChipWordDma(address - 1));
        Assert.Equal((ushort)afterWrite, machine.ReadChipWordDma(address + 1));
    }

    [Theory]
    [InlineData(4, 0xFFFFFFFFu)]
    [InlineData(8, 0x12345678u)]
    public void ConfiguredFastRamTakesPriorityOverEmptyPcmciaMemory(int fastRamMiB, uint expected)
    {
        const uint address = 0x9FFFFC;
        const M68kBusAccessKind read = M68kBusAccessKind.CpuDataRead;
        const M68kBusAccessKind write = M68kBusAccessKind.CpuDataWrite;
        using var machine = Create(fastRamMiB * 1024 * 1024);
        var bus = new LightweightAcceleratorBus(machine, M68kCpuModel.M68EC020);
        long cycle = 0;
        Assert.Equal(0xFFFFFFFFu, bus.ReadLong(address, ref cycle, read));

        bus.WriteByte(0xE8004A, 0, ref cycle, write);
        bus.WriteByte(0xE80048, 0x20, ref cycle, write);
        Assert.Equal(0x200000u, machine.FastRamBase);
        bus.WriteLong(address, 0x12345678, ref cycle, write);
        Assert.Equal(expected, bus.ReadLong(address, ref cycle, read));
        Assert.Equal((ushort)expected, bus.ReadWord(address + 2, ref cycle, read));
        Assert.Equal((byte)expected, bus.ReadByte(address + 3, ref cycle, read));

        bus.WriteByte(address + 3, 0x9A, ref cycle, write);
        Assert.Equal(expected == uint.MaxValue ? 0xFFFFu : 0x569Au, bus.ReadWord(address + 2, ref cycle, read));
        Assert.Equal(0xFFFF, bus.ReadWord(address + 4, ref cycle, read));

        machine.Reset();
        cycle = 0;
        Assert.Equal(0xFFFFFFFFu, bus.ReadLong(address, ref cycle, read));
    }

    [Theory]
    [InlineData(0x100, 0x0C10)] [InlineData(0x100, 0x5800)]
    public void UnverifiedNonstandardModesReportTheirMissingFeature(ushort register, ushort value)
    {
        using var m = Create(); Write(m, register, value); Assert.NotNull(m.UnsupportedActiveFeature);
    }

    [Fact]
    public void ExplicitLowResolutionSpritesRemainAvailableAfterChipsetActivation()
    {
        using var m = Create();
        Write(m, 0x106, 0x0C40);
        Assert.Equal(0x0C40, m.GetCustomRegister(0x106));
        Assert.Null(m.UnsupportedActiveFeature);
    }

    [Fact]
    public void StableEightPlaneOutputDoesNotAllocateDuringReleaseExecution()
    {
        using var m = Create(); Configure(m, 3, 255);
        m.ExecuteFrame(); m.ExecuteFrame();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 4; i++) m.ExecuteFrame();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
