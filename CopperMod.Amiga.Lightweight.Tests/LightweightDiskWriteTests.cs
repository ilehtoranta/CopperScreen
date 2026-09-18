using CopperDisk;
using Copper68k;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

public sealed class LightweightDiskWriteTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void GuestDmaWritesTrackAndExportReopensWithAllSectors(int drive)
    {
        using var m = new LightweightA500Machine(new() { FloppyDriveCount = 4 });
        var original = new byte[901120];
        var expected = new byte[901120];
        for (var i = 0; i < 11 * 512; i++) expected[i] = (byte)(i * 13 + 37);
        m.MountAdf(drive, original);
        m.SetDriveWriteProtected(drive, false);
        StartMotor(m, drive);
        var track = AmigaDosTrackEncoder.EncodeTrack(AmigaDiskLoader.FromAdfBytes(expected), 0, 0);
        for (var i = 0; i < track.Length; i += 2)
            m.WriteChipWordDma((uint)(0x1000 + i), (ushort)((track[i] << 8) | track[i + 1]));
        m.WriteChipWordDma((uint)(0x1000 + track.Length), 0xAAAA); // HRM write padding.
        Start(m, track.Length / 2 + 1);
        m.AdvanceHardwareTo(m.Cycle + (track.Length * 8 + 16) * 14 + 1000);
        Assert.False(m.DiskDmaActive);
        Assert.Equal(2, m.Intreq & 2);
        Assert.Equal((uint)(0x1000 + track.Length + 2), m.GetDiskPointer());
        Assert.True(m.IsDriveDirty(drive));
        Assert.Equal(expected, m.ExportAdf(drive));
        Assert.All(original, value => Assert.Equal(0, value));
        m.Reset(); // Reset preserves the magnetic medium.
        Assert.Equal(expected, m.ExportAdf(drive));
        using var reopened = new LightweightA500Machine();
        reopened.MountAdf(m.ExportAdf(drive));
        Assert.Equal(expected, reopened.ExportAdf());
        Assert.Null(m.UnsupportedActiveFeature);
    }

    [Fact]
    public void ProtectedMediaIgnoresWriteButDmaCompletesAndDefaultIsProtected()
    {
        using var m = new LightweightA500Machine();
        var original = new byte[901120];
        m.MountAdf(original);
        StartMotor(m, 0);
        Assert.True(m.IsDriveWriteProtected(0));
        m.WriteChipWordDma(0x1000, 0);
        Start(m, 1);
        m.AdvanceHardwareTo(m.Cycle + 1000);
        Assert.Equal(2, m.Intreq & 2);
        Assert.False(m.IsDriveDirty(0));
        Assert.Equal(original, m.ExportAdf());
        Assert.Null(m.UnsupportedActiveFeature);
    }

    [Fact]
    public void BusAcceptsAddressThenReadsRamAndIrqWaitsForSerializer()
    {
        using var m = new LightweightA500Machine();
        m.WriteChipWordDma(0x1000, 0x1234);
        Start(m, 1);
        m.AdvanceHardwareTo(14);
        Assert.Equal(0x1002u, m.GetDiskPointer());
        Assert.False(m.CanCopperOwnOutputSlot(16));
        Custom(m, 0x022, 0x2000); // Cannot retarget accepted read.
        m.AdvanceHardwareTo(16);
        Assert.Equal(0, m.DiskDmaRemaining);
        Assert.Equal(0, m.Intreq & 2);
        Assert.True(m.DiskDmaActive);
        m.AdvanceHardwareTo(16 + 13 * 14 - 2);
        Assert.Equal(0, m.Intreq & 2);
        m.AdvanceHardwareTo(16 + 13 * 14);
        Assert.Equal(2, m.Intreq & 2);
        Assert.False(m.DiskDmaActive);
        Assert.Equal(0x1234, m.ReadChipWordDma(0x1000));
    }

    [Fact]
    public void CancellationPreservesAcceptedBusSlotButCannotFeedRearmedWrite()
    {
        using var m = new LightweightA500Machine();
        Start(m, 1);
        m.AdvanceHardwareTo(14);
        Custom(m, 0x024, 0x4000);
        Custom(m, 0x022, 0x2000);
        Custom(m, 0x024, 0xC001);
        Custom(m, 0x024, 0xC001);
        m.AdvanceHardwareTo(16);
        Assert.True(m.IsHigherPriorityOutputOwned(16));
        Assert.Equal(1, m.DiskDmaRemaining);
        Assert.Equal(0, m.Intreq & 2);
        m.AdvanceHardwareTo(20);
        Assert.Equal(0, m.DiskDmaRemaining);
        Assert.Equal(0x2002u, m.GetDiskPointer());
        Custom(m, 0x024, 0x4000);
        m.AdvanceHardwareTo(1000);
        Assert.Equal(0, m.Intreq & 2);
    }

    [Fact]
    public void DmaDisablePausesSerializerWithoutLosingQueuedWords()
    {
        using var m = new LightweightA500Machine();
        Start(m, 2);
        m.AdvanceHardwareTo(40);
        Custom(m, 0x096, 0x0010);
        m.AdvanceHardwareTo(2000);
        Assert.Equal(0, m.Intreq & 2);
        Assert.True(m.DiskDmaActive);
        Custom(m, 0x096, 0x8010);
        m.AdvanceHardwareTo(3000);
        Assert.Equal(2, m.Intreq & 2);
        Assert.False(m.DiskDmaActive);
    }

    [Fact]
    public void DamagedSectorCannotBeSilentlyExportedAsOldData()
    {
        var drive = new LightweightFloppyDrive();
        drive.Mount(new byte[901120]);
        drive.WriteProtected = false;
        drive.WriteControlPins(0x77, 0);
        drive.UpdateRotation(0, false);
        var cycle = drive.ReadyCycle;
        for (var i = 0; i < 9000; i++) drive.WriteBit(0, cycle += 14, false);
        Assert.True(drive.IsDirty);
        Assert.Throws<InvalidOperationException>(() => drive.ExportAdf());
    }

    [Fact]
    public void ActiveWriteExecutionAllocatesNoManagedMemory()
    {
        using var m = new LightweightA500Machine();
        m.MountAdf(new byte[901120]);
        m.SetDriveWriteProtected(0, false);
        StartMotor(m, 0);
        Start(m, 1000);
        m.AdvanceHardwareTo(m.Cycle + 1000); // Warm the active path.
        var before = GC.GetAllocatedBytesForCurrentThread();
        m.AdvanceHardwareTo(m.Cycle + 16 * 14 * 1000);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.False(m.DiskDmaActive);
        Assert.True(m.IsDriveDirty(0));
        Assert.Null(m.UnsupportedActiveFeature);
    }

    private static void StartMotor(LightweightA500Machine m, int drive)
    {
        var cycle = m.Cycle;
        m.WriteByte(0xBFD100, (byte)(0x7F & ~(8 << drive)), ref cycle, M68kBusAccessKind.CpuDataWrite);
        m.WriteByte(0xBFD300, 0xFF, ref cycle, M68kBusAccessKind.CpuDataWrite);
        m.AdvanceHardwareTo(m.GetDiskNextCycle(drive));
    }

    private static void Start(LightweightA500Machine m, int words)
    {
        Custom(m, 0x09E, 0x8100);
        Custom(m, 0x096, 0x8210);
        Custom(m, 0x022, 0x1000);
        Custom(m, 0x024, (ushort)(0xC000 | words));
        Custom(m, 0x024, (ushort)(0xC000 | words));
    }

    private static void Custom(LightweightA500Machine m, ushort offset, ushort value)
        => m.WriteCustomRegisterFromCopper(offset, value, m.Cycle);
}
