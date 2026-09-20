using Xunit;
using static CopperMod.Amiga.Lightweight.Tests.LightweightInputTests;

namespace CopperMod.Amiga.Lightweight.Tests;

public sealed class LightweightKeyboardProtocolTests
{
    private static byte Raw(byte wire)
    {
        var bits = (byte)~wire;
        return (byte)((bits >> 1) | (bits << 7));
    }

    private static byte FinishByte(LightweightA500Machine m)
    {
        m.AdvanceHardwareTo(m.Cycle + 4000);
        Assert.True(m.KeyboardWaitingForHandshake);
        var raw = Raw(ReadCia(m, 12));
        Assert.Equal(8, ReadCia(m, 13) & 8);
        return raw;
    }

    private static void Recover(LightweightA500Machine m)
    {
        m.AdvanceHardwareTo(m.KeyboardNextCycle + 286);
        Assert.True(m.KeyboardWaitingForHandshake);
        Assert.Null(m.UnsupportedActiveFeature);
        Handshake(m);
    }

    [Fact]
    public void TimeoutResynchronizesThenSendsF9OriginalByteAndQueuedRelease()
    {
        using var m = new LightweightA500Machine();
        m.SubmitKey(0x35);
        m.SubmitKey(0xB5);
        Assert.Equal(0x35, FinishByte(m));
        Recover(m);
        Assert.Equal(0xF9, FinishByte(m));
        Handshake(m);
        Assert.Equal(0x35, FinishByte(m));
        Handshake(m);
        Assert.Equal(0xB5, FinishByte(m));
        Handshake(m);
        Assert.Equal(long.MaxValue, m.KeyboardNextCycle);
    }

    [Fact]
    public void LostF9RetainsOriginalByteAcrossAnotherRecovery()
    {
        using var m = new LightweightA500Machine();
        m.SubmitKey(0x44);
        FinishByte(m);
        Recover(m);
        Assert.Equal(0xF9, FinishByte(m));
        Recover(m);
        Assert.Equal(0xF9, FinishByte(m));
        Handshake(m);
        Assert.Equal(0x44, FinishByte(m));
    }

    [Fact]
    public void SlowSynchronizationPulsesDoNotInventWholeKeyBytes()
    {
        using var m = new LightweightA500Machine();
        m.SubmitKey(0x35);
        FinishByte(m);
        m.AdvanceHardwareTo(m.KeyboardNextCycle + 286);
        Assert.Equal(0, ReadCia(m, 13) & 8);
        var next = m.KeyboardNextCycle;
        m.AdvanceHardwareTo(next - 1);
        Assert.True(m.KeyboardWaitingForHandshake);
        m.AdvanceHardwareTo(next + 286);
        Assert.Equal(0, ReadCia(m, 13) & 8);
    }

    [Fact]
    public void ColdStartupReportsFdCurrentHeldKeysFeWithoutF9()
    {
        using var m = new LightweightA500Machine();
        m.LoadKickstart(new byte[256 * 1024]);
        m.SetKeyState(0x35, true);
        m.SetKeyState(0x36, true);
        m.SetKeyState(0x36, false); // Not held at synchronization.
        m.AdvanceHardwareTo(286);
        Assert.Equal(0, m.CiaAPendingInterrupts & 8);
        Handshake(m);
        Assert.Equal(0xFD, FinishByte(m));
        // A transition after the snapshot follows FE, never vanishes.
        m.SetKeyState(0x35, false);
        Handshake(m);
        Assert.Equal(0x35, FinishByte(m));
        Handshake(m);
        Assert.Equal(0xFE, FinishByte(m));
        Handshake(m);
        Assert.Equal(0xB5, FinishByte(m));
        Handshake(m);
        Assert.Equal(long.MaxValue, m.KeyboardNextCycle);
    }

    [Fact]
    public void UnacknowledgedColdStartupContinuesSlowPulsesUntilCiaCanInterrupt()
    {
        using var m = new LightweightA500Machine();
        m.LoadKickstart(new byte[256 * 1024]);
        long cycle = 286;
        for (var pulse = 0; pulse < 8; pulse++)
        {
            m.AdvanceHardwareTo(cycle);
            Assert.Equal(pulse == 7 ? 8 : 0, m.CiaAPendingInterrupts & 8);
            cycle += LightweightKeyboard.TimeoutCycles + 286;
        }
        Assert.Equal(0, ReadCia(m, 12)); // Active-low logical ones, not FF.
        Handshake(m);
        Assert.Equal(0xFD, FinishByte(m));
    }

    [Fact]
    public void PhysicalCapsLockTogglesOnPressAndSuppressesRepeatAndRelease()
    {
        using var m = new LightweightA500Machine();
        m.SetKeyState(0x62, true);
        m.SetKeyState(0x62, true);
        m.SetKeyState(0x62, false);
        Assert.True(m.KeyboardCapsLockOn);
        Assert.Equal(0x62, FinishByte(m));
        Handshake(m);
        Assert.Equal(long.MaxValue, m.KeyboardNextCycle);
        m.SetKeyState(0x62, true);
        Assert.False(m.KeyboardCapsLockOn);
        Assert.Equal(0xE2, FinishByte(m));
    }

    [Fact]
    public void RepeatedPhysicalDownAndUnknownUpDoNotCreateExtraBytes()
    {
        using var m = new LightweightA500Machine();
        m.SetKeyState(0x35, false);
        Assert.Equal(long.MaxValue, m.KeyboardNextCycle);
        m.SetKeyState(0x35, true);
        m.SetKeyState(0x35, true);
        m.SetKeyState(0x35, false);
        Assert.Equal(0x35, FinishByte(m));
        Handshake(m);
        Assert.Equal(0xB5, FinishByte(m));
        Handshake(m);
        Assert.Equal(long.MaxValue, m.KeyboardNextCycle);
        Assert.Throws<ArgumentOutOfRangeException>(() => m.SetKeyState(0x80, true));
    }

    [Fact]
    public void PhysicalQueueOverflowReportsFaAfterRetainedKeys()
    {
        using var m = new LightweightA500Machine();
        for (byte key = 0; key < 12; key++) m.SetKeyState(key, true);
        for (byte key = 0; key < 11; key++)
        {
            Assert.Equal(key, FinishByte(m));
            Handshake(m);
        }
        Assert.Equal(0xFA, FinishByte(m));
        Handshake(m);
        Assert.Equal(long.MaxValue, m.KeyboardNextCycle);
    }

    [Fact]
    public void A500ResetChordResetsCpuDevicesOnceWithoutRewindingClockOrErasingRam()
    {
        using var m = new LightweightA500Machine();
        m.AdvanceHardwareTo(5000);
        m.WriteChipWordDma(0x2000, 0xCAFE);
        m.Cpu.ProgramCounter = 0x1234;
        m.Cpu.StatusRegister = 0x2000;
        m.Cpu.Stopped = true;
        m.WriteCustomRegisterFromCopper(0x096, 0x83FF, m.Cycle);
        m.SetKeyState(0x63, true);
        m.SetKeyState(0x66, true);
        Assert.Equal(0x1234u, m.Cpu.ProgramCounter);
        m.SetKeyState(0x67, true);
        Assert.Equal(5000, m.Cycle);
        Assert.Equal(5000, m.Cpu.Cycles);
        Assert.Equal(0x1000u, m.Cpu.ProgramCounter);
        Assert.Equal(0x2700, m.Cpu.StatusRegister);
        Assert.False(m.Cpu.Stopped);
        Assert.Equal(0, m.Dmacon);
        Assert.Equal(0xCAFE, (m.ChipRam.Span[0x2000] << 8) | m.ChipRam.Span[0x2001]);
        m.Cpu.ProgramCounter = 0x1100;
        m.SetKeyState(0x67, true);
        Assert.Equal(0x1100u, m.Cpu.ProgramCounter);
        m.SetKeyState(0x67, false);
        m.SetKeyState(0x67, true);
        Assert.Equal(0x1000u, m.Cpu.ProgramCounter);
        Assert.Null(m.UnsupportedActiveFeature);
    }

    [Fact]
    public void OutputModeDuringKeyboardByteDoesNotStopKeyboardPhysicalClock()
    {
        using var m = new LightweightA500Machine();
        m.SubmitKey(0x35);
        m.AdvanceHardwareTo(1000);
        WriteCia(m, 14, 0x40);
        m.AdvanceHardwareTo(4000);
        Assert.True(m.KeyboardWaitingForHandshake);
        Assert.Equal(0, m.CiaAPendingInterrupts & 8);
        WriteCia(m, 14, 0);
        Assert.False(m.KeyboardWaitingForHandshake);
        Assert.Null(m.UnsupportedActiveFeature);
    }
}
