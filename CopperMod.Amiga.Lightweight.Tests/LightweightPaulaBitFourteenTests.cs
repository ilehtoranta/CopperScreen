using Copper68k;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

public sealed class LightweightPaulaBitFourteenTests
{
    [Fact]
    public void SoftwareRequestBitFourteenIsLatchedReadableAndClearedIndependently()
    {
        var registers = new LightweightRegisters();
        registers.Write(LightweightRegisters.IntreqWrite, 0xC004);
        Assert.Equal(0x4004, registers.Read(LightweightRegisters.Intreqr));
        Assert.Equal(0, registers.GetHighestEnabledInterruptLevel());
        registers.Write(LightweightRegisters.IntenaWrite, 0xC004);
        Assert.Equal(6, registers.GetHighestEnabledInterruptLevel());
        registers.Write(LightweightRegisters.IntreqWrite, 0x4000);
        Assert.Equal(4, registers.Intreq);
        Assert.Equal(1, registers.GetHighestEnabledInterruptLevel());
    }

    [Fact]
    public void MasterEnableAloneDoesNotCreateAnInterruptRequest()
    {
        var registers = new LightweightRegisters();
        registers.Write(LightweightRegisters.IntenaWrite, 0xC000);
        Assert.Equal(0, registers.Intreq);
        Assert.Equal(0, registers.GetHighestEnabledInterruptLevel());
    }

    [Fact]
    public void CopperBitFourteenSetClearAndMasterMaskUseExistingVisibilityDelay()
    {
        using var machine = new LightweightA500Machine();
        machine.AdvanceHardwareTo(16);
        machine.WriteCustomRegisterFromCopper(LightweightRegisters.IntenaWrite, 0xC004, 16);
        machine.AdvanceHardwareTo(18);
        machine.WriteCustomRegisterFromCopper(LightweightRegisters.IntreqWrite, 0xC004, 18);
        Assert.Equal(0x4004, machine.Intreq);
        Assert.Equal(0, machine.InterruptPinLevel);
        machine.AdvanceHardwareTo(19);
        Assert.Equal(0, machine.InterruptPinLevel);
        machine.AdvanceHardwareTo(20);
        Assert.Equal(6, machine.InterruptPinLevel);
        Assert.Equal(20, machine.InterruptPinChangeCycle);
        machine.WriteCustomRegisterFromCopper(LightweightRegisters.IntreqWrite, 0x4000, 20);
        Assert.Equal(4, machine.Intreq);
        Assert.Equal(6, machine.InterruptPinLevel);
        machine.AdvanceHardwareTo(22);
        Assert.Equal(1, machine.InterruptPinLevel);
        machine.WriteCustomRegisterFromCopper(LightweightRegisters.IntenaWrite, 0x4000, 22);
        machine.AdvanceHardwareTo(24);
        Assert.Equal(0, machine.InterruptPinLevel);
        Assert.Equal(4, machine.Intreq);
    }

    [Fact]
    public void CpuBitFourteenRequestWakesStoppedCpuThroughLevelSixAutovector()
    {
        using var machine = new LightweightA500Machine();
        machine.WriteChipWordDma(0x1000, 0x4E72); // STOP #$2000
        machine.WriteChipWordDma(0x1002, 0x2000);
        machine.WriteChipWordDma(0x0078, 0);
        machine.WriteChipWordDma(0x007A, 0x1100);
        machine.WriteChipWordDma(0x1100, 0x4E71);
        machine.WriteChipWordDma(0x1102, 0x60FC);
        machine.Reset();
        machine.ExecuteFrame();
        Assert.True(machine.Cpu.Stopped);
        long cycle = machine.Cpu.Cycles;
        machine.WriteWord(LightweightA500Machine.CustomBase + LightweightRegisters.IntenaWrite,
            0xC000, ref cycle, M68kBusAccessKind.CpuDataWrite);
        machine.WriteWord(LightweightA500Machine.CustomBase + LightweightRegisters.IntreqWrite,
            0xC000, ref cycle, M68kBusAccessKind.CpuDataWrite);
        machine.Cpu.Cycles = cycle;
        machine.ExecuteFrame();
        Assert.False(machine.Cpu.Stopped);
        Assert.Equal(6, (machine.Cpu.StatusRegister >> 8) & 7);
        Assert.InRange(machine.Cpu.ProgramCounter, 0x1100u, 0x1104u);
    }
}
