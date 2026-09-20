using Copper68k;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

public sealed class LightweightCopperTests
{
    private const uint ListAddress = 0x2000;

    // vAmigaTS Agnus/Copper/lc (0489f55), lc1/lc2/lc3 and A500 hardware
    // photographs: a VSYNC restart held by disabled DMA follows later COP1LC
    // writes until Copper can execute. lc4 distinguishes a later ordinary pause.
    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    public void FrameRestartWhileDmaDisabledUsesLatestList(int disabledFrames, bool masterOff)
    {
        using var machine = new LightweightA500Machine();
        var cycle = StartCopper(machine, (0x0180, 0x0F00), (0xFFFF, 0xFFFE));
        machine.AdvanceHardwareTo(cycle + 64);
        WriteControl(machine, LightweightRegisters.DmaconWrite, masterOff ? (ushort)0x0200 : (ushort)0x0080);
        machine.AdvanceHardwareTo(disabledFrames * (long)LightweightClock.PalLongFieldCycles + 128);
        machine.WriteChipWordDma(0x3000, 0x0180);
        machine.WriteChipWordDma(0x3002, 0x00F0);
        machine.WriteChipWordDma(0x3004, 0xFFFF);
        machine.WriteChipWordDma(0x3006, 0xFFFE);
        WriteControl(machine, LightweightRegisters.Cop1lcl, 0x4000);
        WriteControl(machine, LightweightRegisters.Cop1lcl, 0x3000);
        Assert.Equal((ushort)0x0F00, machine.GetCustomRegister(0x180));
        WriteControl(machine, LightweightRegisters.DmaconWrite, 0x8280);
        machine.AdvanceHardwareTo(machine.Cycle + 80);
        Assert.Equal((ushort)0x00F0, machine.GetCustomRegister(0x180));
        Assert.Equal(0x3008u, machine.CopperProgramCounter);
    }

    [Fact]
    public void OrdinaryDmaPauseAfterExecutionDoesNotReloadChangedList()
    {
        using var machine = new LightweightA500Machine();
        var cycle = StartCopper(machine, (0x0180, 0x0F00), (0xFFFF, 0xFFFE));
        machine.AdvanceHardwareTo(cycle + 64);
        WriteControl(machine, LightweightRegisters.DmaconWrite, 0x0080);
        machine.WriteChipWordDma(0x3000, 0x0180);
        machine.WriteChipWordDma(0x3002, 0x00F0);
        machine.WriteChipWordDma(0x3004, 0xFFFF);
        machine.WriteChipWordDma(0x3006, 0xFFFE);
        WriteControl(machine, LightweightRegisters.Cop1lcl, 0x3000);
        WriteControl(machine, LightweightRegisters.DmaconWrite, 0x8080);
        machine.AdvanceHardwareTo(machine.Cycle + 80);
        Assert.Equal((ushort)0x0F00, machine.GetCustomRegister(0x180));
        Assert.Equal(ListAddress + 8, machine.CopperProgramCounter);
    }

    private static void WriteControl(LightweightA500Machine machine, ushort register, ushort value)
    {
        var cycle = machine.Cycle;
        machine.WriteWord(LightweightA500Machine.CustomBase + register, value,
            ref cycle, M68kBusAccessKind.CpuDataWrite);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TerminalWaitRetainsBusStateAndReactivatesOnFrameOrJump(bool jump)
    {
        using var machine = new LightweightA500Machine();
        var cycle = StartCopper(machine, (0x0180, 0x0F00), (0xFFFF, 0xFFFE), (0x0180, 0x00F0));
        machine.AdvanceHardwareTo(cycle + 64);
        Assert.True(machine.CopperWaiting);
        Assert.Equal(ListAddress + 8, machine.CopperProgramCounter);
        Assert.Equal(long.MaxValue, machine.CopperNextCycle);
        Assert.Equal((ushort)0x0F00, machine.GetCustomRegister(0x180));
        machine.WriteChipWordDma(ListAddress + 2, 0x000F);
        if (jump)
        {
            cycle = machine.Cycle;
            machine.WriteWord(LightweightA500Machine.CustomBase + LightweightRegisters.Copjmp1,
                0, ref cycle, M68kBusAccessKind.CpuDataWrite);
            machine.AdvanceHardwareTo(cycle + 32);
        }
        else
        {
            machine.AdvanceHardwareTo(LightweightClock.PalLongFieldCycles + 32);
        }
        Assert.Equal((ushort)0x000F, machine.GetCustomRegister(0x180));
        Assert.Equal(ListAddress + 8, machine.CopperProgramCounter);
        Assert.Equal(long.MaxValue, machine.CopperNextCycle);
    }

    [Fact]
    public void MoveCommitsAtRetainedSecondWordOutputPhase()
    {
        using var machine = new LightweightA500Machine();
        var cycle = StartCopper(
            machine,
            (0x0180, 0x0F00),
            (0xFFFF, 0xFFFE));
        var firstInput = machine.CopperLastInstructionFirstInputCycle;

        Assert.Equal(cycle, firstInput);
        Assert.Equal(firstInput + 2, machine.CopperPendingOutputCycle);

        machine.AdvanceHardwareTo(firstInput + 5);
        Assert.Equal((ushort)0, machine.GetCustomRegister(0x180));

        machine.AdvanceHardwareTo(firstInput + 6);

        Assert.Equal(firstInput + 6, machine.CopperLastMoveCycle);
        Assert.Equal((ushort)0x0F00, machine.GetCustomRegister(0x180));
        Assert.Equal(ListAddress + 4, machine.CopperProgramCounter);
    }

    [Fact]
    public void AcceptedCopperOutputSlotDelaysCpuWithoutBlockingConcurrentInput()
    {
        using var machine = new LightweightA500Machine();
        _ = StartCopper(
            machine,
            (0x0180, 0x0123),
            (0xFFFF, 0xFFFE));
        var outputCycle = machine.CopperPendingOutputCycle;
        var cpuCycle = outputCycle;

        _ = machine.ReadWord(
            0x3000,
            ref cpuCycle,
            M68kBusAccessKind.CpuDataRead);

        Assert.Equal(outputCycle + 4, cpuCycle);
        Assert.Equal(outputCycle + 4, machine.CopperLastOutputCycle);
        Assert.Equal(outputCycle + 2, machine.CopperLastAcceptedInputCycle);
        Assert.Equal((ushort)0x0123, machine.GetCustomRegister(0x180));
    }

    [Fact]
    public void WaitUsesTwoControlInputsThenContinuesAfterLiveBeamMatch()
    {
        using var machine = new LightweightA500Machine();
        _ = StartCopper(
            machine,
            (0x0101, 0xFFFE),
            (0x0180, 0x00F0),
            (0xFFFF, 0xFFFE));
        var lineOne = LightweightClock.CpuCyclesPerLine;

        machine.AdvanceHardwareTo(lineOne + 9);
        Assert.Equal((ushort)0, machine.GetCustomRegister(0x180));

        machine.AdvanceHardwareTo(lineOne + 10);

        Assert.Equal((ushort)0x00F0, machine.GetCustomRegister(0x180));
        Assert.Equal(lineOne + 10, machine.CopperLastMoveCycle);
    }

    [Theory]
    [InlineData(0, 0x43)]
    [InlineData(4, 0x43)]
    [InlineData(5, 0x45)]
    [InlineData(6, 0x49)]
    public void WaitWakeupYieldsToBitplaneDmaBeforeFetchingTheFollowingMove(int planes, int outputCck)
    {
        // HRM chapter 2: WAIT needs a memory cycle to wake up. At DDF=$38,
        // BPL5 owns output $3F for five/six planes. The WAIT $3C41 cannot
        // wake on its matching input $3E; its first free wake input is $40.
        // With six planes the MOVE uses outputs $45/$49 instead of $41/$45;
        // with five, BPL6's free slot permits the first word at output $43.
        using var machine = new LightweightA500Machine();
        long cycle = machine.Cycle;
        foreach (var (offset, value) in new (ushort, ushort)[]
        {
            (0x08E, 0x2C90), (0x090, 0xF4B0), (0x092, 0x0038), (0x094, 0x00D0),
            (0x100, (ushort)(planes << 12)), (0x096, 0x8300)
        })
            machine.WriteWord(LightweightA500Machine.CustomBase + offset, value,
                ref cycle, M68kBusAccessKind.CpuDataWrite);
        _ = StartCopper(machine, (0x3C41, 0xFFFE), (0x0180, 0x0678), (0xFFFF, 0xFFFE));
        var expectedCycle = 60L * LightweightClock.CpuCyclesPerLine + outputCck * 2;

        machine.AdvanceHardwareTo(expectedCycle - 1);
        Assert.Equal((ushort)0, machine.GetCustomRegister(0x180));
        machine.AdvanceHardwareTo(expectedCycle);
        Assert.Equal((ushort)0x0678, machine.GetCustomRegister(0x180));
        Assert.Equal(expectedCycle, machine.CopperLastMoveCycle);
    }

    [Fact]
    public void SatisfiedSkipSuppressesMoveButDoesNotSkipFollowingWait()
    {
        using var machine = new LightweightA500Machine();
        _ = StartCopper(
            machine,
            (0x0001, 0xFFFF),
            (0x0180, 0x0F00),
            (0x0001, 0xFFFE),
            (0x0180, 0x00F0),
            (0xFFFF, 0xFFFE));

        machine.AdvanceHardwareTo(160);

        Assert.Equal((ushort)0x00F0, machine.GetCustomRegister(0x180));
        Assert.True(machine.CopperWaiting);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void JumpInvalidatesDecodeButRetainsAlreadyAcceptedRamOutput(bool readStrobe)
    {
        using var machine = new LightweightA500Machine();
        long cycle = machine.Cycle;
        machine.WriteWord(
            LightweightA500Machine.CustomBase + LightweightRegisters.Cop2lch,
            0,
            ref cycle,
            M68kBusAccessKind.CpuDataWrite);
        machine.WriteWord(
            LightweightA500Machine.CustomBase + LightweightRegisters.Cop2lcl,
            0x3000,
            ref cycle,
            M68kBusAccessKind.CpuDataWrite);
        _ = StartCopper(
            machine,
            (0x0180, 0x0F00),
            (0xFFFF, 0xFFFE));
        var retainedOutput = machine.CopperPendingOutputCycle;
        cycle = machine.Cycle;
        if (readStrobe)
            _ = machine.ReadWord(LightweightA500Machine.CustomBase + LightweightRegisters.Copjmp2,
                ref cycle, M68kBusAccessKind.CpuDataRead);
        else machine.WriteWord(
            LightweightA500Machine.CustomBase + LightweightRegisters.Copjmp2,
            0,
            ref cycle,
            M68kBusAccessKind.CpuDataWrite);

        Assert.Equal(retainedOutput, machine.CopperLastOutputCycle);
        Assert.Equal(0x3000u, machine.CopperProgramCounter & 0x7FFEu);
        Assert.Equal((ushort)0, machine.GetCustomRegister(0x180));
    }

    // OCS PAL: HRM chapter 2, "Copper Loops and Branches and Comparison
    // Enable": VP7 cannot be masked; IR2 bit 15 is BFD, not a compare enable.
    [Theory]
    [InlineData(0xCC01, 0xFF00, 204)]
    [InlineData(0xCC01, 0x7F00, 204)]
    [InlineData(0x8001, 0x8000, 128)]
    [InlineData(0x8001, 0x0000, 128)]
    public void WaitAlwaysComparesVerticalHighBit(int first, int second, int targetLine)
    {
        using var machine = new LightweightA500Machine();
        _ = StartCopper(machine,
            ((ushort)first, (ushort)second),
            (0x0180, 0x00F0),
            (0xFFFF, 0xFFFE));
        var targetCycle = (long)targetLine * LightweightClock.CpuCyclesPerLine;

        machine.AdvanceHardwareTo(targetCycle - 2);
        Assert.Equal((ushort)0, machine.GetCustomRegister(0x180));

        machine.AdvanceHardwareTo(targetCycle + 40);
        Assert.Equal((ushort)0x00F0, machine.GetCustomRegister(0x180));
        Assert.InRange(machine.CopperLastMoveCycle, targetCycle, targetCycle + 40);
    }

    [Theory]
    [InlineData(76, 0xFF01, false)]
    [InlineData(76, 0x7F01, false)]
    [InlineData(204, 0xFF01, true)]
    [InlineData(204, 0x7F01, true)]
    public void SkipAlwaysComparesVerticalHighBit(int line, int second, bool skips)
    {
        using var machine = new LightweightA500Machine();
        machine.AdvanceHardwareTo((long)line * LightweightClock.CpuCyclesPerLine);
        var cycle = StartCopper(machine,
            (0xCC01, (ushort)second),
            (0x0180, 0x00F0),
            (0xFFFF, 0xFFFE));

        machine.AdvanceHardwareTo(cycle + 160);

        Assert.Equal((ushort)(skips ? 0 : 0x00F0), machine.GetCustomRegister(0x180));
    }

    [Theory]
    [InlineData(0x8F00)]
    [InlineData(0x0F00)]
    public void ObservedVerticalHighBitMakesLowerMaskedTargetAlreadySatisfied(int second)
    {
        using var machine = new LightweightA500Machine();
        machine.AdvanceHardwareTo(128L * LightweightClock.CpuCyclesPerLine);
        var cycle = StartCopper(machine,
            (0x0F01, (ushort)second),
            (0x0180, 0x00F0),
            (0xFFFF, 0xFFFE));

        machine.AdvanceHardwareTo(cycle + 160);

        Assert.Equal((ushort)0x00F0, machine.GetCustomRegister(0x180));
        Assert.Equal(128, machine.BeamLine);
    }

    private static long StartCopper(
        LightweightA500Machine machine,
        params (ushort First, ushort Second)[] instructions)
    {
        long cycle = machine.Cycle;
        for (var index = 0; index < instructions.Length; index++)
        {
            machine.WriteWord(
                ListAddress + (uint)(index * 4),
                instructions[index].First,
                ref cycle,
                M68kBusAccessKind.CpuDataWrite);
            machine.WriteWord(
                ListAddress + (uint)(index * 4) + 2,
                instructions[index].Second,
                ref cycle,
                M68kBusAccessKind.CpuDataWrite);
        }

        machine.WriteWord(
            LightweightA500Machine.CustomBase + LightweightRegisters.Cop1lch,
            (ushort)(ListAddress >> 16),
            ref cycle,
            M68kBusAccessKind.CpuDataWrite);
        machine.WriteWord(
            LightweightA500Machine.CustomBase + LightweightRegisters.Cop1lcl,
            (ushort)ListAddress,
            ref cycle,
            M68kBusAccessKind.CpuDataWrite);
        machine.WriteWord(
            LightweightA500Machine.CustomBase + LightweightRegisters.DmaconWrite,
            0x8280,
            ref cycle,
            M68kBusAccessKind.CpuDataWrite);
        machine.WriteWord(
            LightweightA500Machine.CustomBase + LightweightRegisters.Copjmp1,
            0,
            ref cycle,
            M68kBusAccessKind.CpuDataWrite);
        return cycle;
    }
}
