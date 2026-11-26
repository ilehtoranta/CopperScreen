using Copper68k;
using CopperMod.Amiga.Lightweight;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

public sealed class LightweightCopperReadStrobeTests
{
    // HRM chapter 2, Starting the Copper After Reset: MOVE.W COPJMP1(a1),d0.
    [Theory]
    [InlineData(0x88, false, 0x2000)]
    [InlineData(0x8A, false, 0x3000)]
    [InlineData(0x88, true, 0x2000)]
    [InlineData(0x89, true, 0x2000)]
    [InlineData(0x8A, true, 0x3000)]
    [InlineData(0x8B, true, 0x3000)]
    public void CpuReadSelectsCopperListWhileDmaIsDisabled(int offset, bool byteRead, uint expected)
    {
        using var m = new LightweightA500Machine();
        long cycle = 0;
        m.WriteLong(0xDFF080, 0x2000, ref cycle, M68kBusAccessKind.CpuDataWrite);
        m.WriteLong(0xDFF084, 0x3000, ref cycle, M68kBusAccessKind.CpuDataWrite);
        Assert.Equal(0u, m.CopperProgramCounter);
        if (byteRead) _ = m.ReadByte(0xDFF000u + (uint)offset, ref cycle, M68kBusAccessKind.CpuDataRead);
        else _ = m.ReadWord(0xDFF000u + (uint)offset, ref cycle, M68kBusAccessKind.CpuDataRead);
        Assert.Equal(expected, m.CopperProgramCounter);
        Assert.Equal(long.MaxValue, m.CopperPendingOutputCycle);
        // Strobing a disabled Copper must not fetch or execute the list yet.
        m.AdvanceHardwareTo(cycle + 40);
        Assert.Equal(expected, m.CopperProgramCounter);
    }

    [Fact]
    public void LongReadStrobesBothListsInWordOrder()
    {
        using var m = new LightweightA500Machine();
        long cycle = 0;
        m.WriteLong(0xDFF080, 0x2000, ref cycle, M68kBusAccessKind.CpuDataWrite);
        m.WriteLong(0xDFF084, 0x3000, ref cycle, M68kBusAccessKind.CpuDataWrite);
        _ = m.ReadLong(0xDFF088, ref cycle, M68kBusAccessKind.CpuDataRead);
        Assert.Equal(0x3000u, m.CopperProgramCounter);
    }

    [Theory]
    [InlineData(true, 16, false, 5)]
    [InlineData(false, 16, false, 5)]
    [InlineData(true, 17, true, 6)]
    [InlineData(false, 17, true, 6)]
    public void EnabledCopperJumpRestartsBeforeFetchingTheNewList(
        bool readStrobe, int horizontal, bool readsDummyWord, int firstFetchCcks)
    {
        // WinUAE 6.0.3 PAL OCS, old list waiting ahead of line 110.
        using var machine = new LightweightA500Machine();
        machine.WriteChipWordDma(0x50000, 0xFF01);
        machine.WriteChipWordDma(0x50002, 0xFFFE);
        machine.WriteChipWordDma(0x50004, 0xFFFF);
        machine.WriteChipWordDma(0x50100, 0x0180);
        machine.WriteChipWordDma(0x50102, 0x001F);
        machine.WriteChipWordDma(0x50104, 0xFFFF);
        machine.WriteChipWordDma(0x50106, 0xFFFE);
        long cycle = 32;
        machine.WriteLong(0xDFF080, 0x50000, ref cycle, M68kBusAccessKind.CpuDataWrite);
        machine.WriteWord(0xDFF096, 0x8280, ref cycle, M68kBusAccessKind.CpuDataWrite);
        machine.WriteWord(0xDFF088, 0, ref cycle, M68kBusAccessKind.CpuDataWrite);
        machine.AdvanceHardwareTo(49_000);
        cycle = machine.Cycle;
        machine.WriteLong(0xDFF080, 0x50100, ref cycle, M68kBusAccessKind.CpuDataWrite);
        Assert.True(machine.CopperWaiting);
        var previousOutput = machine.CopperLastOutputCycle;

        var strobeCycle = (110L * LightweightClock.CpuCyclesPerLine) + horizontal * 2;
        cycle = strobeCycle;
        if (readStrobe)
            _ = machine.ReadWord(0xDFF088, ref cycle, M68kBusAccessKind.CpuDataRead);
        else
            machine.WriteWord(0xDFF088, 0, ref cycle, M68kBusAccessKind.CpuDataWrite);

        var firstFetch = strobeCycle + firstFetchCcks * 2;
        machine.AdvanceHardwareTo(firstFetch - 2);
        Assert.Equal(readsDummyWord ? firstFetch - 4 : previousOutput, machine.CopperLastOutputCycle);
        machine.AdvanceHardwareTo(firstFetch);
        Assert.Equal(firstFetch, machine.CopperLastOutputCycle);
        Assert.Equal(0, machine.GetCustomRegister(0x180));
        machine.AdvanceHardwareTo(firstFetch + 4);
        Assert.Equal(0x001F, machine.GetCustomRegister(0x180));
    }
}
