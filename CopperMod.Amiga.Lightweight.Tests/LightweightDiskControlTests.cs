using Copper68k;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

// HRM ch.8 defines enabled-DMA status and WORDSYNC gating. Exact Paula
// request/interrupt propagation phases remain outside these control tests.
public sealed class LightweightDiskControlTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DmaOnRequiresSecondStrobeAndClearsAfterCompletion(bool writing)
    {
        using var m = new LightweightA500Machine();
        Write(m, 0x096, 0x8210);
        Write(m, 0x09E, 0x8100);
        Write(m, 0x022, 0x1000);
        var length = (ushort)(writing ? 0xC001 : 0x8001);
        var direction = writing ? 0x2000 : 0;
        Write(m, 0x024, length);
        Assert.Equal(direction, Status(m) & 0x6000);
        Write(m, 0x024, length);
        Assert.Equal(direction | 0x4000, Status(m) & 0x6000);
        if (!writing)
            for (var bit = 0; bit < 16; bit++) m.ReceiveDiskBit(0xBEEF, false, m.Cycle);
        Write(m, 0x096, 0x8210); // Refresh deadline after direct receiver injection.
        m.AdvanceHardwareTo(m.DiskDmaNextCycle + 2);
        if (writing)
        {
            Assert.Equal(0, m.DiskDmaRemaining); // RAM prefetch is not completion.
            Assert.Equal(0x6000, Status(m) & 0x6000);
            Assert.Equal(0, m.Intreq & 2);
            m.AdvanceHardwareTo(m.Cycle + 500);
        }
        Assert.False(m.DiskDmaActive);
        Assert.Equal(2, m.Intreq & 2);
        Assert.Equal(direction, Status(m) & 0x6000);
        Assert.Null(m.UnsupportedActiveFeature);
    }

    [Theory]
    [InlineData(0x0010)]
    [InlineData(0x0200)]
    public void WaitingDmaStatusTracksEnablePauseCancelAndReset(int mask)
    {
        using var m = new LightweightA500Machine();
        Write(m, 0x096, 0x8210);
        Write(m, 0x09E, 0x8400);
        Write(m, 0x024, 0x8001);
        Write(m, 0x024, 0x8001);
        Assert.Equal(0x4000, Status(m) & 0x6000); // Waiting for sync is enabled.
        Write(m, 0x096, (ushort)mask);
        Assert.Equal(0, Status(m) & 0x4000);
        Assert.True(m.DiskDmaActive);
        Write(m, 0x096, (ushort)(0x8000 | mask));
        Assert.Equal(0x4000, Status(m) & 0x4000);
        Write(m, 0x024, 0x4000);
        Assert.Equal(0x2000, Status(m) & 0x6000);
        Write(m, 0x024, 0x8001);
        Assert.Equal(0, Status(m) & 0x6000); // Cancellation resets the interlock.
        Write(m, 0x024, 0x8001);
        m.ResetExternalDevices(m.Cycle);
        Assert.Equal(0, Status(m) & 0x6000);
        Assert.Equal(0, m.Intreq & 2);
    }

    [Theory]
    [InlineData(0x0010)]
    [InlineData(0x0200)]
    public void ZeroLengthWordsyncReadWaitsForEnabledMatchWithoutRamTransfer(int mask)
    {
        using var m = new LightweightA500Machine();
        Write(m, 0x022, 0x1000);
        m.WriteChipWordDma(0x1000, 0xCAFE);
        Write(m, 0x096, 0x8210);
        Write(m, 0x09E, 0x8400);
        Write(m, 0x024, 0x80FF);
        Write(m, 0x024, 0x8000);
        Assert.True(m.DiskDmaActive);
        Assert.Equal(0, m.Intreq & 2);
        m.ReceiveDiskBit(0x1234, false, m.Cycle);
        Assert.Equal(0, m.Intreq & 2);
        Write(m, 0x096, (ushort)mask);
        m.ReceiveDiskBit(0x4489, true, m.Cycle);
        Assert.True(m.DiskDmaActive);
        Assert.Equal(0, m.Intreq & 2);
        Write(m, 0x096, (ushort)(0x8000 | mask));
        Assert.Equal(0, m.Intreq & 2); // No synthetic match at DMACON write.
        m.ReceiveDiskBit(0x4489, true, m.Cycle);
        Assert.False(m.DiskDmaActive);
        Assert.Equal(2, m.Intreq & 2);
        Assert.Equal(0x1000u, m.GetDiskPointer());
        Assert.Equal(0xCAFE, m.ReadChipWordDma(0x1000));
        Assert.Equal(long.MaxValue, m.DiskDmaNextCycle);
        Write(m, 0x09C, 2);
        Write(m, 0x024, 0x8001);
        Assert.False(m.DiskDmaActive); // Completion requires a fresh double strobe.
        Assert.Equal(0, m.Intreq & 2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancelOrResetRevokesZeroLengthSyncWait(bool reset)
    {
        using var m = new LightweightA500Machine();
        Write(m, 0x096, 0x8210);
        Write(m, 0x09E, 0x8400);
        Write(m, 0x024, 0x8000);
        Write(m, 0x024, 0x8000);
        Assert.Equal(0, m.Intreq & 2);
        if (reset) m.ResetExternalDevices(m.Cycle);
        else Write(m, 0x024, 0x4000);
        m.AdvanceHardwareTo(m.Cycle + 1000);
        Assert.False(m.DiskDmaActive);
        Assert.Equal(0, m.Intreq & 2);
        Assert.Equal(long.MaxValue, m.DiskDmaNextCycle);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ActualAdfSyncCompletesZeroLengthReadOnEachDrive(int drive)
    {
        using var m = new LightweightA500Machine(new() { FloppyDriveCount = 4 });
        m.MountAdf(drive, new byte[901120]);
        Write(m, 0x096, 0x8210);
        Write(m, 0x09E, 0x8500);
        Write(m, 0x022, 0x1000);
        m.WriteChipWordDma(0x1000, 0xCAFE);
        Write(m, 0x024, 0x8000);
        Write(m, 0x024, 0x8000);
        var cycle = m.Cycle;
        m.WriteByte(0xBFD100, (byte)(0x7F & ~(8 << drive)), ref cycle, M68kBusAccessKind.CpuDataWrite);
        m.WriteByte(0xBFD300, 0xFF, ref cycle, M68kBusAccessKind.CpuDataWrite);
        var sync = m.GetDiskNextCycle(drive) + 47 * 14; // Four gap bytes, then $4489.
        m.AdvanceHardwareTo(sync - 2);
        Assert.True(m.DiskDmaActive);
        Assert.Equal(0, m.Intreq & 0x1002);
        m.AdvanceHardwareTo(sync);
        Assert.Equal(0x4489, m.DiskInputShift);
        Assert.Equal(0x1002, m.Intreq & 0x1002);
        Assert.False(m.DiskDmaActive);
        Assert.Equal(0x1000u, m.GetDiskPointer());
        Assert.Equal(0xCAFE, m.ReadChipWordDma(0x1000));
        Assert.Equal(0, Status(m) & 0x4000);
        Assert.Null(m.UnsupportedActiveFeature);
    }

    private static int Status(LightweightA500Machine m)
    {
        var cycle = m.Cycle;
        return m.ReadWord(0xDFF01A, ref cycle, M68kBusAccessKind.CpuDataRead);
    }

    private static void Write(LightweightA500Machine m, ushort offset, ushort value)
        => m.WriteCustomRegisterFromCopper(offset, value, m.Cycle);
}
