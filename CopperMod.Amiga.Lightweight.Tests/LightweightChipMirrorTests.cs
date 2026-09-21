using Copper68k;
using CopperMod.Amiga.Lightweight;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

public sealed class LightweightChipMirrorTests
{
    // A500 schematic 312511 sheet 2: default JP2 feeds CPU A23 to Agnus
    // A19. Within Gary's low 2 MiB decode, A19/A20 do not select more RAM.
    [Theory]
    [InlineData(0x080000u)]
    [InlineData(0x100000u)]
    [InlineData(0x180000u)]
    [InlineData(0x1180000u)]
    public void CpuReadsAndWritesSharePhysicalChipRam(uint mirror)
    {
        using var m = new LightweightA500Machine();
        long cycle = 0;
        m.WriteLong(mirror + 0x2200, 0x12345678, ref cycle, M68kBusAccessKind.CpuDataWrite);
        Assert.Equal(0x12345678u, m.ReadLong(0x2200, ref cycle, M68kBusAccessKind.CpuDataRead));
        m.WriteByte(0x2201, 0xAB, ref cycle, M68kBusAccessKind.CpuDataWrite);
        Assert.Equal((ushort)0x12AB, m.ReadWord(mirror + 0x2200, ref cycle, M68kBusAccessKind.CpuInstructionFetch));
        Assert.Equal((byte)0x78, m.ReadByte(mirror + 0x2203, ref cycle, M68kBusAccessKind.CpuDataRead));
    }

    [Fact]
    public void LongwordCrossesPhysicalChipBoundaryWithoutLosingSecondWord()
    {
        using var m = new LightweightA500Machine();
        long cycle = 0;
        m.WriteLong(0x0FFFFE, 0x12345678, ref cycle, M68kBusAccessKind.CpuDataWrite);
        Assert.Equal((ushort)0x1234, m.ReadChipWordDma(0x7FFFE));
        Assert.Equal((ushort)0x5678, m.ReadChipWordDma(0));
        Assert.Equal(0x12345678u, m.ReadLong(0x7FFFE, ref cycle, M68kBusAccessKind.CpuDataRead));
        m.WriteWord(0x200000, 0xABCD, ref cycle, M68kBusAccessKind.CpuDataWrite);
        Assert.Equal((ushort)0x5678, m.ReadChipWordDma(0));
        Assert.Equal((ushort)0xFFFF, m.ReadWord(0x200000, ref cycle, M68kBusAccessKind.CpuDataRead));
    }

    [Theory]
    [InlineData(0x080000u)]
    [InlineData(0x100000u)]
    [InlineData(0x180000u)]
    public void MirrorAccessWaitsForTheRefreshContendedAgnusBus(uint mirror)
    {
        using var m = new LightweightA500Machine();
        long cycle = LightweightClock.CpuCyclesPerLine;
        m.WriteWord(mirror + 0x2200, 0x5AA5, ref cycle, M68kBusAccessKind.CpuDataWrite);
        Assert.Equal(LightweightClock.CpuCyclesPerLine + 4, cycle);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CpuExecutesCodeThroughTheMirror(bool batch)
    {
        using var m = new LightweightA500Machine(null, enableConservativeCpuLoopBatch: batch);
        m.WriteChipWordDma(0x1000, 0x4EF9); // JMP $080002
        m.WriteChipWordDma(0x1002, 0x0008);
        m.WriteChipWordDma(0x1004, 0x0002);
        m.WriteChipWordDma(2, 0x702A); // MOVEQ #42,D0
        m.WriteChipWordDma(4, 0x4E72); // STOP #$2700
        m.WriteChipWordDma(6, 0x2700);
        m.ExecuteFrame();
        Assert.True(m.Cpu.Stopped);
        Assert.Equal(42u, m.Cpu.D[0]);
        Assert.Equal(0x080008u, m.Cpu.ProgramCounter);
    }
}
