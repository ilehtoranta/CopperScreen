using Copper68k;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

// HRM ch.8 specifies enabled WORDSYNC alignment and independent sync IRQs.
// The latched start gate and live alignment distinction are a bounded model
// cross-checked against pinned WinUAE, not physical coincident-edge evidence.
public sealed class LightweightDiskInitialSyncTests
{
    public static IEnumerable<object[]> WordPhases()
    {
        for (var offset = 0; offset < 16; offset++)
            foreach (var reenable in new[] { false, true })
                yield return [offset, reenable];
    }

    [Theory]
    [MemberData(nameof(WordPhases))]
    public void FirstReceivedMatchUsesLiveAlignmentAtEveryWordPhase(int offset, bool reenable)
    {
        using var m = Waiting(1);
        var drive = Pattern(offset);
        var serial = new LightweightDiskSerial();
        serial.OnDriveChanged(drive, 0);
        Sample(ref serial, drive, m, offset + 8);
        Write(m, 0x09E, 0x0400); // In the middle of the first sync word.
        Sample(ref serial, drive, m, 3);
        if (reenable) Write(m, 0x09E, 0x8400);
        Assert.Equal(0, m.Intreq & 0x1002); // Register writes supply no input.
        Sample(ref serial, drive, m, 5);
        Assert.Equal(0x4489, serial.Shift);
        Assert.Equal(0x1000, m.Intreq & 0x1002);
        Assert.True(m.DiskDmaActive);
        Assert.Equal(1, m.DiskDmaRemaining);
        Assert.Equal(0x1000u, m.GetDiskPointer());
        Assert.Equal(long.MaxValue, m.DiskDmaNextCycle); // Match is not queued.

        var followingBits = reenable || offset == 0 ? 16 : 16 - offset;
        Sample(ref serial, drive, m, followingBits - 1);
        Assert.Equal(long.MaxValue, m.DiskDmaNextCycle);
        Sample(ref serial, drive, m, 1);
        Assert.Equal(0, m.ReadChipWordDma(0x1000));
        Assert.Equal(0, m.Intreq & 2);
        var addressCycle = m.DiskDmaNextCycle;
        Assert.NotEqual(long.MaxValue, addressCycle);
        m.AdvanceHardwareTo(addressCycle);
        Assert.Equal(0x1002u, m.GetDiskPointer());
        Assert.Equal(1, m.DiskDmaRemaining);
        Assert.Equal(0, m.ReadChipWordDma(0x1000));
        m.AdvanceHardwareTo(addressCycle + 2);
        // Disabled alignment retains the word phase seeded by DSKLEN; the
        // first stored word can include the tail of the matching sync.
        var expected = (ushort)((0x4489u << followingBits) | (0xBEEFu >> (16 - followingBits)));
        Assert.Equal(expected, m.ReadChipWordDma(0x1000));
        Assert.Equal(0, m.DiskDmaRemaining);
        Assert.False(m.DiskDmaActive);
        Assert.Equal(2, m.Intreq & 2);
        Assert.Null(m.UnsupportedActiveFeature);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void RegisterOnlyMatchAndClearReenableDoNotOpenInitialGate(int words)
    {
        using var m = Waiting(words);
        Write(m, 0x07E, 0); // Matches the shared receiver's reset shift.
        Assert.Equal(0x1000, m.Intreq & 0x1002);
        Write(m, 0x09C, 0x1000);
        Write(m, 0x09E, 0x0400);
        Write(m, 0x09E, 0x8400);
        Assert.Equal(0, m.Intreq & 0x1002);
        Assert.True(m.DiskDmaActive);
        Assert.Equal(words, m.DiskDmaRemaining);
        Assert.Equal(0x1000u, m.GetDiskPointer());
        Assert.Equal(long.MaxValue, m.DiskDmaNextCycle);
        Assert.Null(m.UnsupportedActiveFeature);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CpuAndCopperWaitingTogglesPreservePartialPhase(bool cpu)
    {
        using var m = Waiting(1);
        Bits(m, 5);
        Toggle(0x0400);
        Bits(m, 3);
        Toggle(0x8400);
        Bits(m, 2);
        Toggle(0x0400);
        m.ReceiveDiskBit(0x4489, true, m.Cycle);
        Bits(m, 4);
        Assert.Equal(long.MaxValue, m.DiskDmaNextCycle);
        Bits(m, 1, 0xBEEF);
        Drain(m);
        Assert.Equal(0xBEEF, m.ReadChipWordDma(0x1000));
        Assert.Equal(2, m.Intreq & 2);
        Assert.Null(m.UnsupportedActiveFeature);

        void Toggle(ushort value)
        {
            if (!cpu) Write(m, 0x09E, value);
            else
            {
                var cycle = m.Cycle;
                m.WriteWord(0xDFF09E, value, ref cycle, M68kBusAccessKind.CpuDataWrite);
            }
            Assert.Equal(0, m.Intreq & 0x1002);
            Assert.True(m.DiskDmaActive);
            Assert.Equal(1, m.DiskDmaRemaining);
        }
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public void EncodedAdfWaitingTogglesPreservePartialCpuByteAndFollowingDmaWord(int drive, bool reenable)
    {
        using var m = new LightweightA500Machine(new() { FloppyDriveCount = 4 });
        m.MountAdf(drive, new byte[LightweightFloppyDrive.StandardAdfBytes]);
        m.WriteChipWordDma(0x1000, 0);
        Write(m, 0x096, 0x8210);
        Write(m, 0x09E, 0x8500);
        Write(m, 0x022, 0x1000);
        Write(m, 0x024, 0x8001);
        Write(m, 0x024, 0x8001);
        var cycle = m.Cycle;
        m.WriteByte(0xBFD100, (byte)(0x7F & ~(8 << drive)), ref cycle, M68kBusAccessKind.CpuDataWrite);
        m.WriteByte(0xBFD300, 0xFF, ref cycle, M68kBusAccessKind.CpuDataWrite);
        var first = m.GetDiskNextCycle(drive);
        m.AdvanceHardwareTo(first + 6 * 14); // Seven bits of the first gap byte.
        Write(m, 0x09E, 0x0400);
        Write(m, 0x09E, 0x8400);
        Assert.Equal(0, ReadStatus(m) & 0x8000);
        m.AdvanceHardwareTo(first + 7 * 14);
        Assert.Equal(0x80AA, ReadStatus(m) & 0x80FF);
        Assert.True(m.DiskDmaActive);
        Assert.Equal(0, m.Intreq & 0x1002);

        var sync = first + 47 * 14;
        m.AdvanceHardwareTo(sync - 2);
        Write(m, 0x09E, 0x0400);
        if (reenable) Write(m, 0x09E, 0x8400);
        Assert.Equal(0, m.Intreq & 0x1002);
        m.AdvanceHardwareTo(sync);
        Assert.Equal(0x4489, m.DiskInputShift);
        Assert.Equal(0x1000, m.Intreq & 0x1002);
        Assert.Equal(0x1000u, m.GetDiskPointer());
        Assert.Equal(0, m.ReadChipWordDma(0x1000));
        m.AdvanceHardwareTo(sync + 16 * 14 + 454);
        Assert.Equal(0x4489, m.ReadChipWordDma(0x1000)); // Second header sync.
        Assert.Equal(0x1002u, m.GetDiskPointer());
        Assert.False(m.DiskDmaActive);
        Assert.Equal(2, m.Intreq & 2);
        Assert.Null(m.UnsupportedActiveFeature);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ZeroLengthCompletesOnInputWithSyncIrqAndNoRamTransfer(bool reenable)
    {
        using var m = Waiting(0);
        m.WriteChipWordDma(0x1000, 0xCAFE);
        var drive = Pattern(7);
        var serial = new LightweightDiskSerial();
        serial.OnDriveChanged(drive, 0);
        Sample(ref serial, drive, m, 15);
        Write(m, 0x09E, 0x0400);
        if (reenable) Write(m, 0x09E, 0x8400);
        Sample(ref serial, drive, m, 7);
        Assert.True(m.DiskDmaActive);
        Assert.Equal(0, m.Intreq & 0x1002);
        Sample(ref serial, drive, m, 1);
        Assert.Equal(0x1002, m.Intreq & 0x1002);
        Assert.False(m.DiskDmaActive);
        Assert.Equal(long.MaxValue, m.DiskDmaNextCycle);
        Assert.Equal(0x1000u, m.GetDiskPointer());
        Assert.Equal(0xCAFE, m.ReadChipWordDma(0x1000));
        Assert.Null(m.UnsupportedActiveFeature);
        Write(m, 0x09C, 0x1002);
        Write(m, 0x024, 0x8001);
        Assert.False(m.DiskDmaActive); // A fresh double strobe is required.
        Assert.Equal(0, m.Intreq & 2);
    }

    [Theory]
    [InlineData(0x0010, false, 0)]
    [InlineData(0x0200, false, 0)]
    [InlineData(0x0010, true, 0)]
    [InlineData(0x0200, true, 0)]
    [InlineData(0x0010, false, 1)]
    [InlineData(0x0200, false, 1)]
    [InlineData(0x0010, true, 1)]
    [InlineData(0x0200, true, 1)]
    public void PausedMatchesAlignOnlyWhenEnabledAndCannotOpenGate(int mask, bool reenable, int words)
    {
        using var m = Waiting(words);
        Bits(m, 5);
        Write(m, 0x096, (ushort)mask);
        Write(m, 0x09E, 0x0400);
        if (reenable) Write(m, 0x09E, 0x8400);
        m.ReceiveDiskBit(0x4489, true, m.Cycle);
        Bits(m, 3);
        Write(m, 0x09E, 0x0400);
        Write(m, 0x096, (ushort)(0x8000 | mask));
        Assert.True(m.DiskDmaActive);
        Assert.Equal(words, m.DiskDmaRemaining);
        Assert.Equal(0, m.Intreq & 2);
        Assert.Equal(long.MaxValue, m.DiskDmaNextCycle);
        m.ReceiveDiskBit(0x4489, true, m.Cycle); // New input after resume.
        if (words == 0)
        {
            Assert.False(m.DiskDmaActive);
            Assert.Equal(2, m.Intreq & 2);
        }
        else
        {
            // The paused enabled match reset phase; the disabled one did not.
            var followingBits = reenable ? 12 : 6;
            Bits(m, followingBits - 1);
            Assert.Equal(long.MaxValue, m.DiskDmaNextCycle);
            Bits(m, 1, 0xBEEF);
            Drain(m);
            Assert.Equal(0xBEEF, m.ReadChipWordDma(0x1000));
            Assert.Equal(2, m.Intreq & 2);
        }
        Assert.Equal(words == 0 ? 0x1000u : 0x1002u, m.GetDiskPointer());
        Assert.Null(m.UnsupportedActiveFeature);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AcceptedOldWordSurvivesCancelRearmAndWaitingToggles(bool zeroLength)
    {
        using var m = Waiting(1);
        m.ReceiveDiskBit(0x4489, true, m.Cycle);
        Bits(m, 16, 0xCAFE);
        Refresh(m);
        m.AdvanceHardwareTo(14);
        Write(m, 0x024, 0);
        Write(m, 0x022, 0x2000);
        var length = (ushort)(zeroLength ? 0x8000 : 0x8001);
        Write(m, 0x024, length);
        Write(m, 0x024, length);
        Write(m, 0x09E, 0x0400);
        Write(m, 0x09E, 0x8400);
        m.AdvanceHardwareTo(16);
        Assert.Equal(0xCAFE, m.ReadChipWordDma(0x1000));
        Assert.Equal(0, m.ReadChipWordDma(0x2000));
        Assert.Equal(0x2000u, m.GetDiskPointer());
        Assert.Equal(zeroLength ? 0 : 1, m.DiskDmaRemaining);
        Assert.True(m.DiskDmaActive);
        Assert.Equal(0, m.Intreq & 2);
        m.ReceiveDiskBit(0x4489, true, m.Cycle);
        if (!zeroLength)
        {
            Bits(m, 16, 0xBEEF);
            Drain(m);
            Assert.Equal(0xBEEF, m.ReadChipWordDma(0x2000));
        }
        Assert.False(m.DiskDmaActive);
        Assert.Equal(2, m.Intreq & 2);
        Assert.Null(m.UnsupportedActiveFeature);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancelOrResetAfterWaitingToggleRevokesGateAndPartialPhase(bool reset)
    {
        using var m = Waiting(1);
        Write(m, 0x09E, 0x0400);
        Bits(m, 15);
        if (reset) m.ResetExternalDevices(m.Cycle);
        else Write(m, 0x024, 0);
        Write(m, 0x09E, 0x0400);
        Write(m, 0x096, 0x8210);
        Write(m, 0x022, 0x1000);
        Write(m, 0x024, 0x8001);
        Write(m, 0x024, 0x8001);
        Bits(m, 15);
        Assert.Equal(long.MaxValue, m.DiskDmaNextCycle);
        Bits(m, 1, 0xBEEF);
        Drain(m);
        Assert.Equal(0xBEEF, m.ReadChipWordDma(0x1000));
        Assert.Equal(2, m.Intreq & 2);
        Assert.Null(m.UnsupportedActiveFeature);
    }

    private static LightweightA500Machine Waiting(int words)
    {
        var m = new LightweightA500Machine();
        m.WriteChipWordDma(0x1000, 0);
        m.WriteChipWordDma(0x2000, 0);
        Write(m, 0x096, 0x8210);
        Write(m, 0x09E, 0x8500);
        Write(m, 0x022, 0x1000);
        Write(m, 0x024, (ushort)(0x8000 | words));
        Write(m, 0x024, (ushort)(0x8000 | words));
        return m;
    }

    private static LightweightFloppyDrive Pattern(int prefix)
    {
        var drive = new LightweightFloppyDrive();
        drive.Mount(new byte[LightweightFloppyDrive.StandardAdfBytes]);
        drive.WriteControlPins(0x77, 0);
        drive.WriteProtected = false;
        var cycle = drive.ReadyCycle;
        for (var i = 0; i < prefix; i++) drive.WriteBit(0, cycle += 14, false);
        for (var i = 31; i >= 0; i--) drive.WriteBit((int)((0x4489BEEFu >> i) & 1), cycle += 14, false);
        return drive;
    }

    private static void Sample(ref LightweightDiskSerial serial, LightweightFloppyDrive drive,
        LightweightA500Machine m, int bits)
    {
        for (var i = 0; i < bits; i++)
        {
            m.AdvanceHardwareTo(drive.NextBitCycle);
            serial.Step(m.Cycle, drive, m);
            Refresh(m); // Refresh canonical deadline after external serial input.
        }
    }

    private static void Bits(LightweightA500Machine m, int count, ushort shift = 0x1234)
    {
        for (var i = 0; i < count; i++) m.ReceiveDiskBit(shift, false, m.Cycle);
    }

    private static void Drain(LightweightA500Machine m)
    {
        Refresh(m);
        Assert.NotEqual(long.MaxValue, m.DiskDmaNextCycle);
        m.AdvanceHardwareTo(m.DiskDmaNextCycle + 2);
    }

    private static void Refresh(LightweightA500Machine m) => Write(m, 0x096, 0x8210);

    private static ushort ReadStatus(LightweightA500Machine m)
    {
        var cycle = m.Cycle;
        return m.ReadWord(0xDFF01A, ref cycle, M68kBusAccessKind.CpuDataRead);
    }

    private static void Write(LightweightA500Machine m, ushort offset, ushort value)
        => m.WriteCustomRegisterFromCopper(offset, value, m.Cycle);
}
