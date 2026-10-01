using Copper68k;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

public sealed class LightweightAgaTests
{
    private static LightweightA500Machine Create() => new(new() { AgnusModel = LightweightAgnusModel.Mos8374Alice,
        DeniseModel = LightweightDeniseModel.Lisa4203, CpuModel = M68kCpuModel.M68EC020,
        ChipRamBytes = 2097152, SlowRamBytes = 0, FramebufferWidth = 1816 });
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
    public void AlignedChipLongUsesOneGrantAndMisalignedLongKeepsWordPhases()
    {
        using var m = Create(); var bus = new LightweightAcceleratorBus(m, M68kCpuModel.M68EC020);
        long c = 20; bus.WriteLong(0x2000, 0x12345678, ref c, M68kBusAccessKind.CpuDataWrite);
        Assert.Equal(24, c); Assert.Equal(0x1234, m.ReadChipWordDma(0x2000)); Assert.Equal(0x5678, m.ReadChipWordDma(0x2002));
        bus.WriteLong(0x2002, 0xAABBCCDD, ref c, M68kBusAccessKind.CpuDataWrite); Assert.Equal(32, c);
        Assert.Equal(0xAABB, m.ReadChipWordDma(0x2002)); Assert.Equal(0xCCDD, m.ReadChipWordDma(0x2004));
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
    [InlineData(0x100, 0x0C10)] [InlineData(0x100, 0x5800)] [InlineData(0x1FC, 4)] [InlineData(0x106, 0x0C80)] [InlineData(0x10E, 0x40)]
    public void ModesBeyondTheInitialMilestoneReportTheirMissingFeature(ushort register, ushort value)
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
