using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

public sealed class LightweightCiaPinsTests
{
    [Fact]
    public void SuccessivePrbAccessesPreserveEveryDelayedPcPulseAndTheGaps()
    {
        var c = Timer(0);
        c.WriteRegister(1, 1, 10, out _);
        c.ReadRegister(1, 255, 20, out _);
        c.WriteRegister(1, 2, 40, out _);
        Assert.False(c.PcHigh(40));
        c.WriteRegister(1, 3, 50, out _);
        Assert.False(c.PcHigh(59));
        Assert.True(c.PcHigh(60));
        Assert.False(c.PcHigh(70));
        Assert.False(c.PcHigh(89));
        Assert.True(c.PcHigh(90));
        c.Reset();
        Assert.True(c.PcHigh(90));
    }

    [Fact]
    public void KeyboardClockFeedsTimerAndTimerPortControlsDf3Selection()
    {
        using var m = new LightweightA500Machine(new() { FloppyDriveCount = 4 });
        void WriteB(int reg, byte value)
        {
            var cycle = m.Cycle;
            m.WriteByte(0xBFD000u + (uint)(reg << 8), value, ref cycle, Copper68k.M68kBusAccessKind.CpuDataWrite);
        }
        WriteB(4, 20);
        WriteB(5, 0);
        WriteB(14, 0x17); // PB6 toggle; overrides input DDR, /SEL3 initially high.
        Assert.False(m.GetDriveState(3).Selected);
        var underflow = (m.Cycle / 10 + 20) * 10;
        m.AdvanceHardwareTo(underflow);
        Assert.True(m.GetDriveState(3).Selected);
        m.AdvanceHardwareTo(underflow + 200);
        Assert.False(m.GetDriveState(3).Selected);
        Assert.Null(m.UnsupportedActiveFeature);
    }

    [Fact]
    public void MachineCiaBInputInterruptReachesPaulaAfterEightCycles()
    {
        using var m = new LightweightA500Machine();
        var cycle = m.Cycle;
        m.WriteByte(0xBFDD00, 0x88, ref cycle, Copper68k.M68kBusAccessKind.CpuDataWrite);
        m.WriteCustomRegisterFromCopper(0x09A, 0xE000, m.Cycle);
        for (var bit = 0; bit < 8; bit++)
        {
            m.SetCiaBSerialPins(true, false);
            m.AdvanceHardwareTo(m.Cycle + 10);
            m.SetCiaBSerialPins(true, true);
            if (bit < 7) m.AdvanceHardwareTo(m.Cycle + 10);
        }
        Assert.Equal(0x2000, m.Intreq & 0x2000);
        Assert.Equal(0, m.InterruptPinLevel);
        m.AdvanceHardwareTo(m.Cycle + 8);
        Assert.Equal(6, m.InterruptPinLevel);
    }

    [Fact]
    public void ExternalInputLevelsSurviveCpuResetAndParallelDdrSelectsPins()
    {
        using var m = new LightweightA500Machine();
        m.SetParallelDataPins(0xA5);
        LightweightInputTests.WriteCia(m, 1, 0x3C);
        LightweightInputTests.WriteCia(m, 3, 0x0F);
        Assert.Equal(0xAC, LightweightInputTests.ReadCia(m, 1));
        m.SetCiaBSerialPins(false, false);
        m.SetParallelAcknowledgePin(false);
        m.ResetExternalDevices(m.Cycle);
        Assert.False(m.CiaBSerialDataHigh);
        Assert.False(m.CiaBSerialClockHigh);
        Assert.Equal(0xA5, LightweightInputTests.ReadCia(m, 1));
        m.SetParallelAcknowledgePin(false); // Still low, not another falling edge.
        Assert.Equal(0, m.CiaAPendingInterrupts & 0x10);
        m.SetParallelAcknowledgePin(true);
        m.SetParallelAcknowledgePin(false);
        Assert.Equal(0x10, m.CiaAPendingInterrupts & 0x10);
    }

    [Fact]
    public void ExternalCountSerialOutputCannotClockThroughItsOwnLowCntDrive()
    {
        var c = Timer(0x71, latchA: 1);
        c.WriteRegister(12, 0xFF, 0, out _);
        c.SetSerialPins(true, false, 10);
        c.SetSerialPins(true, true, 20);
        Assert.False(c.SerialClockPinHigh);
        Assert.True(c.SerialSpHigh);
        c.SetSerialPins(true, false, 30);
        c.SetSerialPins(true, true, 40);
        Assert.False(c.SerialClockPinHigh);
        Assert.Equal(0, c.PendingInterrupts & 8);
    }

    [Fact]
    public void SerialOutputAndCntTimerSteadyStateAllocateNothing()
    {
        var c = Timer(controlB: 0x21);
        for (var run = 0; run < 500; run++)
        {
            c.WriteRegister(12, 0xAA, run * 1000, out _);
            c.AdvanceTo(run * 1000 + 999);
        }
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var run = 500; run < 1000; run++)
        {
            c.WriteRegister(12, 0xAA, run * 1000, out _);
            c.AdvanceTo(run * 1000 + 999);
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static LightweightCia Timer(byte controlA = 0x51, byte controlB = 0, byte latchA = 2, byte latchB = 2)
    {
        var c = new LightweightCia();
        c.Reset();
        c.WriteRegister(4, latchA, 0, out _);
        c.WriteRegister(5, 0, 0, out _);
        c.WriteRegister(6, latchB, 0, out _);
        c.WriteRegister(7, 0, 0, out _);
        c.WriteRegister(14, controlA, 0, out _);
        c.WriteRegister(15, controlB, 0, out _);
        return c;
    }

    [Fact]
    public void SerialOutputIsMsbFirstAndInterruptsOnSixteenthUnderflow()
    {
        var c = Timer();
        c.WriteRegister(13, 0x88, 0, out _);
        c.WriteRegister(12, 0xA6, 0, out _);
        for (var bit = 0; bit < 8; bit++)
        {
            var fall = 20 + bit * 40;
            c.AdvanceTo(fall - 1);
            Assert.True(c.SerialCntHigh);
            Assert.Equal(long.MaxValue, c.AdvanceTo(fall));
            Assert.False(c.SerialCntHigh);
            Assert.Equal((0xA6 & (0x80 >> bit)) != 0, c.SerialSpHigh);
            Assert.Equal(0, c.PendingInterrupts & 8);
            var irq = c.AdvanceTo(fall + 20);
            Assert.True(c.SerialCntHigh);
            Assert.Equal(bit == 7 ? fall + 20 : long.MaxValue, irq);
        }
        Assert.Equal(8, c.PendingInterrupts & 8);
        c.ReadRegister(13, 255, 321, out _);
        c.AdvanceTo(10000);
        Assert.False(c.SerialSpHigh); // Last bit remains driven while idle.
        Assert.True(c.SerialCntHigh);
        Assert.Equal(0, c.PendingInterrupts & 8);
    }

    [Fact]
    public void SerialHoldingBufferDoesNotCorruptInFlightByteAndLatestWriteWins()
    {
        var c = Timer();
        c.WriteRegister(12, 0x80, 0, out _);
        c.AdvanceTo(20);
        c.WriteRegister(12, 0x11, 25, out _);
        c.WriteRegister(12, 0x55, 30, out _);
        c.AdvanceTo(320);
        Assert.False(c.SerialSpHigh);
        c.ReadRegister(13, 255, 321, out _);
        byte value = 0;
        for (var bit = 0; bit < 8; bit++)
        {
            c.AdvanceTo(340 + bit * 40);
            value = (byte)((value << 1) | (c.SerialSpHigh ? 1 : 0));
        }
        Assert.Equal(0x55, value);
        c.AdvanceTo(640);
        Assert.Equal(8, c.PendingInterrupts & 8);
    }

    [Fact]
    public void SerialCoarseAndFineAdvancementAgreeIncludingBufferedBytes()
    {
        var a = Timer(controlB: 0x61, latchB: 3);
        var b = Timer(controlB: 0x61, latchB: 3);
        foreach (var c in new[] { a, b })
        {
            c.WriteRegister(12, 0xAA, 0, out _);
            c.AdvanceTo(20);
            c.WriteRegister(12, 0x5A, 21, out _);
        }
        a.AdvanceTo(1000);
        for (var cycle = 22; cycle <= 1000; cycle++) b.AdvanceTo(cycle);
        Assert.Equal(a.TimerACounter, b.TimerACounter);
        Assert.Equal(a.TimerBCounter, b.TimerBCounter);
        Assert.Equal(a.PendingInterrupts, b.PendingInterrupts);
        Assert.Equal(a.SerialSpHigh, b.SerialSpHigh);
        Assert.Equal(a.SerialCntHigh, b.SerialCntHigh);
    }

    [Theory]
    [InlineData(0x21, 0x21)]
    [InlineData(0x21, 0x41)]
    [InlineData(0x21, 0x61)]
    public void ExternalCntCountsOnlyRisingEdgesAndCascades(int cra, int crb)
    {
        var c = Timer((byte)cra, (byte)crb);
        c.AdvanceTo(1000);
        Assert.Equal(2, c.TimerACounter);
        Assert.Equal(2, c.TimerBCounter);
        for (var i = 0; i < 4; i++)
        {
            c.SetSerialPins(true, false, 1100 + i * 40);
            c.SetSerialPins(true, true, 1110 + i * 40);
            c.SetSerialPins(false, true, 1120 + i * 40); // No new clock.
        }
        Assert.Equal(3, c.PendingInterrupts & 3);
        Assert.Equal(2, c.TimerACounter);
        Assert.Equal(2, c.TimerBCounter);
    }

    [Fact]
    public void TimerBUnderflowGateUsesActualCntLevel()
    {
        var c = Timer(0x11, 0x61);
        c.SetSerialPins(true, false, 1);
        c.AdvanceTo(100);
        Assert.Equal(2, c.TimerBCounter);
        c.SetSerialPins(true, true, 101);
        c.AdvanceTo(120);
        Assert.Equal(1, c.TimerBCounter);
        c.SetSerialPins(true, false, 121);
        c.AdvanceTo(200);
        Assert.Equal(1, c.TimerBCounter);
        c.SetSerialPins(true, true, 201);
        c.AdvanceTo(220);
        Assert.Equal(2, c.TimerBCounter);
        Assert.Equal(2, c.PendingInterrupts & 2);
    }

    [Fact]
    public void SwitchingRunningTimerFromCntToEClockDoesNotCountThePast()
    {
        var c = Timer(0x21);
        c.SetSerialPins(true, false, 1000);
        c.SetSerialPins(true, true, 1001);
        Assert.Equal(1, c.TimerACounter);
        c.WriteRegister(14, 1, 1005, out _);
        c.AdvanceTo(1009);
        Assert.Equal(0, c.PendingInterrupts & 1);
        c.AdvanceTo(1010);
        Assert.Equal(1, c.PendingInterrupts & 1);
    }

    [Fact]
    public void ExternalSerialInputSamplesAtRisingCntAndLatchesOnlyWholeBytes()
    {
        var c = Timer(0, 0);
        for (var bit = 0; bit < 8; bit++)
        {
            c.SetSerialPins(false, false, bit * 30);
            c.SetSerialPins((0x95 & (0x80 >> bit)) != 0, false, bit * 30 + 10);
            Assert.Equal(0, c.PendingInterrupts & 8);
            c.SetSerialPins((0x95 & (0x80 >> bit)) != 0, true, bit * 30 + 20);
        }
        Assert.Equal(0x95, c.ReadRegister(12, 255, 240, out _));
        Assert.Equal(8, c.PendingInterrupts & 8);
    }

    [Theory]
    [InlineData(0x17, 0x40)]
    [InlineData(0x13, 0)]
    public void TimerPortOverridesDirectionAndLatch(int cra, int initial)
    {
        var c = Timer((byte)cra);
        c.WriteRegister(1, 0xFF, 0, out _);
        c.WriteRegister(3, 0, 0, out _); // PBON overrides input DDR.
        Assert.Equal(initial, c.ReadPort(1, 0, 0) & 0x40);
        c.AdvanceTo(20);
        Assert.Equal(initial ^ 0x40, c.ReadPort(1, 0, 20) & 0x40);
        c.AdvanceTo(30);
        Assert.Equal(0, c.ReadPort(1, 0, 30) & 0x40);
        c.AdvanceTo(40);
        Assert.Equal(0x40, c.ReadPort(1, 0, 40) & 0x40);
    }

    [Fact]
    public void OneShotPulseFallsOneEClockAfterStoppedUnderflow()
    {
        var c = Timer(0x1B);
        Assert.Equal(20, c.GetNextActiveInterruptCycle());
        c.AdvanceTo(20);
        Assert.Equal(0x40, c.ReadPort(1, 0, 20) & 0x40);
        Assert.Equal(30, c.GetNextActiveInterruptCycle());
        c.AdvanceTo(30);
        Assert.Equal(0, c.ReadPort(1, 0, 30) & 0x40);
        Assert.Equal(long.MaxValue, c.GetNextActiveInterruptCycle());
    }

    [Fact]
    public void CascadedPortPulseDeadlineNeverRemainsInPast()
    {
        var c = Timer(0x11, 0x43);
        c.AdvanceTo(40);
        Assert.Equal(0x80, c.ReadPort(1, 0, 40) & 0x80);
        Assert.Equal(50, c.GetNextActiveInterruptCycle());
        c.AdvanceTo(50);
        Assert.Equal(80, c.GetNextActiveInterruptCycle());
    }

    [Fact]
    public void FlagIsFallingEdgeSensitiveAndPrbAccessProducesPcPulse()
    {
        var c = Timer(0);
        c.SetFlagPin(false, 10);
        Assert.Equal(0x10, c.ReadRegister(13, 255, 11, out _));
        c.SetFlagPin(false, 12);
        Assert.Equal(0, c.ReadRegister(13, 255, 13, out _));
        c.SetFlagPin(true, 14);
        c.SetFlagPin(false, 15);
        Assert.Equal(0x10, c.PendingInterrupts);
        c.WriteRegister(1, 0xAA, 20, out _);
        Assert.True(c.PcHigh(49));
        Assert.False(c.PcHigh(50));
        Assert.False(c.PcHigh(59));
        Assert.True(c.PcHigh(60));
        c.ReadRegister(1, 255, 70, out _);
        Assert.False(c.PcHigh(100));
    }
}
