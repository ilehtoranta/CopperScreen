using Copper68k;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

// Commodore HRM ch.8 UART framing, buffering, SERPER and SERDATR/INTREQ contract.
// Exact pin synchronization/IRQ latency uses the engine's declared CCK model.
public sealed class LightweightPaulaSerialTests
{
    [Theory]
    [InlineData(0)] [InlineData(3)] [InlineData(371)]
    public void TransmitterSendsAutomaticStartDataLsbFirstAndFullStopPeriod(int divisor)
    {
        using var m = Create();
        W(m, 0x032, (ushort)divisor); W(m, 0x030, 0x01A5);
        m.AdvanceHardwareTo(2);
        Assert.False(m.SerialTransmitHigh);
        Assert.Equal(0x2000, Status(m) & 0x3000); // Holding empty, shifter busy.
        Assert.Equal(1, m.Intreq & 1);
        var period = 2L * (divisor + 1);
        for (var bit = 0; bit < 9; bit++)
        {
            m.AdvanceHardwareTo(2 + (bit + 1) * period);
            Assert.Equal(((0x01A5 >> bit) & 1) != 0, m.SerialTransmitHigh);
            Assert.Equal(0, Status(m) & 0x1000);
        }
        m.AdvanceHardwareTo(2 + 10 * period);
        Assert.True(m.SerialTransmitHigh);
        Assert.Equal(0x3000, Status(m) & 0x3000);
    }

    [Fact]
    public void HoldingRegisterStartsNextWordOnlyAfterTheStopBit()
    {
        using var m = Create();
        W(m, 0x032, 3); W(m, 0x030, 0x01A5);
        m.AdvanceHardwareTo(2);
        W(m, 0x09C, 1); W(m, 0x030, 0x015A);
        Assert.Equal(0, Status(m) & 0x3000);
        m.AdvanceHardwareTo(80);
        Assert.True(m.SerialTransmitHigh);
        Assert.Equal(0, m.Intreq & 1);
        m.AdvanceHardwareTo(82);
        Assert.False(m.SerialTransmitHigh);
        Assert.Equal(0x2000, Status(m) & 0x3000);
        Assert.Equal(1, m.Intreq & 1);
        m.AdvanceHardwareTo(162);
        Assert.Equal(0x3000, Status(m) & 0x3000);
    }

    [Fact]
    public void ZeroDataDoesNotStartAndBreakOnlyForcesTheOutputPin()
    {
        using var m = Create();
        W(m, 0x030, 0);
        m.AdvanceHardwareTo(200);
        Assert.Equal(0, m.Intreq & 1);
        Assert.Equal(0x3800, Status(m));
        W(m, 0x09E, 0x8800);
        Assert.False(m.SerialTransmitHigh);
        W(m, 0x030, 0x0100);
        m.AdvanceHardwareTo(224);
        Assert.Equal(0x3000, Status(m) & 0x3000);
        Assert.False(m.SerialTransmitHigh);
        W(m, 0x09E, 0x0800);
        Assert.True(m.SerialTransmitHigh);
    }

    [Theory]
    [InlineData(false, 0xA5, 0x01A5)] [InlineData(true, 0x155, 0x0355)]
    public void ReceiverSamplesEightOrNineBitsAndDoesNotAcknowledgeOnRead(bool nine, int value, int expected)
    {
        using var m = Create();
        W(m, 0x032, (ushort)(19 | (nine ? 0x8000 : 0)));
        Receive(m, value, nine ? 9 : 8);
        Assert.Equal((ushort)(0x7800 | expected), Status(m));
        Assert.Equal(0x0800, m.Intreq & 0x0800);
        var cycle = m.Cycle;
        var first = m.ReadWord(0xDFF018, ref cycle, M68kBusAccessKind.CpuDataRead);
        Assert.Equal(first, m.ReadWord(0xDFF018, ref cycle, M68kBusAccessKind.CpuDataRead));
        Assert.Equal(0x0800, m.Intreq & 0x0800);
        W(m, 0x09C, 0x0800);
        Assert.Equal((ushort)(0x3800 | expected), Status(m));
    }

    [Fact]
    public void UnacknowledgedReceiveSetsOverrunAndAckClearsStatusNotData()
    {
        using var m = Create();
        W(m, 0x032, 19);
        Receive(m, 0x31, 8); Receive(m, 0x72, 8);
        Assert.Equal(0xF972, Status(m));
        W(m, 0x09C, 0x0800);
        Assert.Equal(0x3972, Status(m));
        Receive(m, 0x23, 8);
        Assert.Equal(0x7923, Status(m));
    }

    [Fact]
    public void ShortStartPulseIsRejectedAndReceiverCanRestart()
    {
        using var m = Create();
        W(m, 0x032, 19);
        m.SetSerialReceivePin(false);
        m.AdvanceHardwareTo(10);
        m.SetSerialReceivePin(true);
        m.AdvanceHardwareTo(400);
        Assert.Equal(0, m.Intreq & 0x0800);
        Receive(m, 0x5A, 8);
        Assert.Equal(0x795A, Status(m));
    }

    [Fact]
    public void PeriodChangesAffectFollowingBitsWithoutMovingThePendingEdge()
    {
        using var m = Create();
        W(m, 0x032, 9); W(m, 0x030, 3);
        m.AdvanceHardwareTo(2);
        W(m, 0x032, 19);
        m.AdvanceHardwareTo(20);
        Assert.False(m.SerialTransmitHigh);
        m.AdvanceHardwareTo(22);
        Assert.True(m.SerialTransmitHigh);
        m.AdvanceHardwareTo(100);
        Assert.Equal(0, Status(m) & 0x1000);
        m.AdvanceHardwareTo(102);
        Assert.Equal(0x1000, Status(m) & 0x1000);
    }

    [Fact]
    public void DeviceResetCancelsBothDirectionsAndClearsBreak()
    {
        using var m = Create();
        W(m, 0x032, 19); W(m, 0x030, 0x01FF); W(m, 0x09E, 0x8800);
        m.SetSerialReceivePin(false);
        m.AdvanceHardwareTo(10);
        m.ResetExternalDevices(m.Cycle);
        m.AdvanceHardwareTo(1000);
        Assert.True(m.SerialTransmitHigh);
        Assert.Equal(0x3800, Status(m));
        Assert.Equal(0, m.Intreq & 0x0801);
    }

    [Fact]
    public void SteadyTransmitAndReceiveAllocateNothing()
    {
        using var m = Create();
        W(m, 0x032, 19);
        // Warm both directions and their interrupt writes through the same
        // method as the measurement. Receive-only warmup leaves transmit cold.
        _ = MeasureDuplex(m);
        Assert.Equal(0, MeasureDuplex(m));
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static long MeasureDuplex(LightweightA500Machine m)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++)
        {
            W(m, 0x030, (ushort)(0x100 | i));
            W(m, 0x09C, 0x0801);
            Receive(m, i, 8);
        }
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    private static void Receive(LightweightA500Machine m, int value, int bits)
    {
        var start = m.Cycle;
        m.SetSerialReceivePin(false);
        for (var bit = 0; bit < bits; bit++)
        {
            m.AdvanceHardwareTo(start + (bit + 1) * 40);
            m.SetSerialReceivePin(((value >> bit) & 1) != 0);
        }
        m.AdvanceHardwareTo(start + (bits + 1) * 40);
        m.SetSerialReceivePin(true);
        m.AdvanceHardwareTo(start + (bits + 2) * 40);
    }

    private static LightweightA500Machine Create()
    {
        var m = new LightweightA500Machine();
        m.Cpu.Stopped = true;
        return m;
    }
    private static ushort Status(LightweightA500Machine m) => m.GetCustomRegister(0x018);
    private static void W(LightweightA500Machine m, ushort register, ushort value)
        => m.WriteCustomRegisterFromCopper(register, value, m.Cycle);
}
