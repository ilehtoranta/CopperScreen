using Copper68k;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

// HRM ch.8: running reads realign on input matches while WORDSYNC is enabled.
// An ADKCON write is not a match or a fresh DSKLEN start. Bus deadlines here
// exercise the existing bounded FIFO model, not measured Paula DMAL phases.
public sealed class LightweightDiskLiveSyncTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LiveTogglePreservesPartialWordAndDoesNotRearmRead(bool initiallyEnabled)
    {
        using var m = Running(1, initiallyEnabled);
        Bits(m, 7, 0x1234);
        Write(m, 0x09E, initiallyEnabled ? (ushort)0x0400 : (ushort)0x8400);
        Bits(m, 8, 0xBEEF);
        Assert.Equal(long.MaxValue, m.DiskDmaNextCycle);
        Bits(m, 1, 0xBEEF);
        Drain(m);
        Assert.Equal(0xBEEF, m.ReadChipWordDma(0x1000));
        Assert.Equal(0x1002u, m.GetDiskPointer());
        Assert.False(m.DiskDmaActive);
        Assert.Equal(2, m.Intreq & 2);
        Assert.Null(m.UnsupportedActiveFeature);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FutureMatchUsesLiveEnableWithoutLosingBufferedWords(bool enable)
    {
        using var m = Running(3, !enable);
        Bits(m, 16, 0xCAFE);
        Bits(m, 16, 0xFACE);
        Refresh(m);
        m.AdvanceHardwareTo(m.DiskDmaNextCycle); // One accepted, one queued.
        Bits(m, 7, 0);
        Write(m, 0x09E, enable ? (ushort)0x8400 : (ushort)0x0400);
        m.ReceiveDiskBit(0x4489, true, m.Cycle);
        Bits(m, 8, 0x1234);
        if (enable) Bits(m, 8, 0x5678); // Matching input, not ADKCON, realigns.
        Refresh(m);
        m.AdvanceHardwareTo(m.Cycle + 1000);
        Assert.Equal(0xCAFE, m.ReadChipWordDma(0x1000));
        Assert.Equal(0xFACE, m.ReadChipWordDma(0x1002));
        Assert.Equal(enable ? 0x5678 : 0x1234, m.ReadChipWordDma(0x1004));
        Assert.Equal(0x1006u, m.GetDiskPointer());
        Assert.False(m.DiskDmaActive);
        Assert.Equal(2, m.Intreq & 2);
        Assert.Null(m.UnsupportedActiveFeature);
    }

    [Theory]
    [InlineData(0x0010)]
    [InlineData(0x0200)]
    public void ToggleDuringDmaPausePreservesWordPhase(int mask)
    {
        using var m = Running(1, true);
        Bits(m, 7, 0);
        Write(m, 0x096, (ushort)mask);
        Write(m, 0x09E, 0x0400);
        Bits(m, 8, 0);
        Assert.Equal(long.MaxValue, m.DiskDmaNextCycle);
        Write(m, 0x096, (ushort)(0x8000 | mask));
        Bits(m, 1, 0xBEEF);
        Drain(m);
        Assert.Equal(0xBEEF, m.ReadChipWordDma(0x1000));
        Assert.Equal(2, m.Intreq & 2);
        Assert.Null(m.UnsupportedActiveFeature);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancelOrResetAfterToggleRevokesPartialWord(bool reset)
    {
        using var m = Running(1, true);
        Bits(m, 15, 0xAAAA);
        Write(m, 0x09E, 0x0400);
        if (reset) m.ResetExternalDevices(m.Cycle);
        else Write(m, 0x024, 0x4000);
        Write(m, 0x096, 0x8210);
        Write(m, 0x022, 0x1000);
        Write(m, 0x024, 0x8001);
        Write(m, 0x024, 0x8001);
        Bits(m, 15, 0xBEEF);
        Assert.Equal(long.MaxValue, m.DiskDmaNextCycle);
        Bits(m, 1, 0xBEEF);
        Drain(m);
        Assert.Equal(0xBEEF, m.ReadChipWordDma(0x1000));
        Assert.Equal(2, m.Intreq & 2);
        Assert.Null(m.UnsupportedActiveFeature);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void EncodedAdfPreservesFirstDataWordAcrossLiveToggles(int drive)
    {
        using var m = new LightweightA500Machine(new() { FloppyDriveCount = 4 });
        m.MountAdf(drive, new byte[901120]);
        Write(m, 0x096, 0x8210);
        Write(m, 0x09E, 0x8500);
        Write(m, 0x022, 0x1000);
        Write(m, 0x024, 0x8001);
        Write(m, 0x024, 0x8001);
        var cycle = m.Cycle;
        m.WriteByte(0xBFD100, (byte)(0x7F & ~(8 << drive)), ref cycle, M68kBusAccessKind.CpuDataWrite);
        m.WriteByte(0xBFD300, 0xFF, ref cycle, M68kBusAccessKind.CpuDataWrite);
        var sync = m.GetDiskNextCycle(drive) + 47 * 14;
        m.AdvanceHardwareTo(sync + 7 * 14);
        Write(m, 0x09E, 0x0400);
        m.AdvanceHardwareTo(sync + 10 * 14);
        Write(m, 0x09E, 0x8400);
        m.AdvanceHardwareTo(sync + 16 * 14 + 454);
        Assert.Equal(0x4489, m.ReadChipWordDma(0x1000)); // Second sector-header sync.
        Assert.Equal(0x1002u, m.GetDiskPointer());
        Assert.False(m.DiskDmaActive);
        Assert.Equal(2, m.Intreq & 2);
        Assert.Null(m.UnsupportedActiveFeature);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ToggleBeforeFirstSyncStillReportsUnverifiedBehavior(int words)
    {
        using var m = new LightweightA500Machine();
        Write(m, 0x096, 0x8210);
        Write(m, 0x09E, 0x8400);
        Write(m, 0x024, (ushort)(0x8000 | words));
        Write(m, 0x024, (ushort)(0x8000 | words));
        Write(m, 0x09E, 0x0400);
        Assert.Equal("WORDSYNC enable change while disk DMA waits for first sync", m.UnsupportedActiveFeature);
        Assert.Equal(0, m.Intreq & 2);
    }

    private static LightweightA500Machine Running(int words, bool sync)
    {
        var m = new LightweightA500Machine();
        Write(m, 0x096, 0x8210);
        if (sync) Write(m, 0x09E, 0x8400);
        Write(m, 0x022, 0x1000);
        Write(m, 0x024, (ushort)(0x8000 | words));
        Write(m, 0x024, (ushort)(0x8000 | words));
        if (sync) m.ReceiveDiskBit(0x4489, true, m.Cycle);
        return m;
    }

    private static void Bits(LightweightA500Machine m, int count, ushort shift)
    {
        for (var bit = 0; bit < count; bit++) m.ReceiveDiskBit(shift, false, m.Cycle);
    }

    private static void Refresh(LightweightA500Machine m) => Write(m, 0x096, 0x8210);
    private static void Drain(LightweightA500Machine m)
    {
        Refresh(m);
        Assert.NotEqual(long.MaxValue, m.DiskDmaNextCycle);
        m.AdvanceHardwareTo(m.DiskDmaNextCycle + 2);
    }

    private static void Write(LightweightA500Machine m, ushort offset, ushort value)
        => m.WriteCustomRegisterFromCopper(offset, value, m.Cycle);
}
