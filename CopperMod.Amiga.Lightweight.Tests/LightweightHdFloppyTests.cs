using CopperDisk;
using Copper68k;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

public sealed class LightweightHdFloppyTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void SerialIdTracksMediaAndRestartsAfterMotorCycle(int unit)
    {
        var drive = new LightweightFloppyDrive(unit, LightweightFloppyDriveType.HighDensity);
        Assert.Equal(0u, Identify(drive, unit));
        drive.Mount(new byte[1802240]);
        Assert.Equal(0xAAAAAAAAu, Identify(drive, unit));
        Assert.Equal(0xAAAAAAAAu, Identify(drive, unit));
        drive.Mount(new byte[901120]);
        Assert.Equal(0u, Identify(drive, unit));
        drive.Eject();
        Assert.Equal(0u, Identify(drive, unit));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HdMediaDoublesIndexPeriodAndCellsAtTheSamePaulaRate(bool hd)
    {
        var drive = new LightweightFloppyDrive(0, LightweightFloppyDriveType.HighDensity);
        drive.Mount(new byte[hd ? 1802240 : 901120]);
        drive.WriteControlPins(0x77, 0);
        drive.UpdateRotation(0, false);
        var expectedCells = 12668 * 8 * (hd ? 2 : 1);
        Assert.Equal(expectedCells, drive.TrackBitLength);
        Assert.Equal(14, FollowingCellCycles(drive));
        while (!drive.AdvanceRotation()) { }
        var firstIndex = drive.NextBitCycle;
        var cells = 0;
        do { cells++; } while (!drive.AdvanceRotation());
        Assert.Equal(expectedCells, cells);
        // 300 RPM = 200 ms, 150 RPM = 400 ms, on the canonical CPU clock.
        var expectedPeriod = 2L * (LightweightPaulaAudio.PalCpuFrequency / (hd ? 5 : 10));
        Assert.InRange(drive.NextBitCycle - firstIndex, expectedPeriod - 2, expectedPeriod + 2);
        // A side change maps angular phase into the other surface.
        var bit = drive.BitPosition;
        var next = drive.NextBitCycle;
        drive.WriteControlPins(0x73, next - 2);
        Assert.Equal(bit, drive.BitPosition);
        Assert.Equal(next, drive.NextBitCycle);
    }

    [Fact]
    public void DdDriveRejectsHdMediaBeforeReplacingItsDisk()
    {
        var drive = new LightweightFloppyDrive();
        var dd = new byte[901120]; dd[0] = 42;
        drive.Mount(dd);
        Assert.Throws<ArgumentException>(() => drive.Mount(new byte[1802240]));
        Assert.Equal(42, drive.Image[0]);
        using var machine = new LightweightA500Machine(new() {
            FloppyDriveTypes = [LightweightFloppyDriveType.HighDensity] });
        machine.MountDisk(0, LightweightFloppyImage.Prepare(new byte[1802240], "hd.adf"));
        Assert.Equal(1802240, machine.ExportAdf().Length);
        Assert.Null(machine.UnsupportedActiveFeature);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IndexReachesCiaFlagAtTheMediaSpindleDeadline(bool hd)
    {
        using var machine = new LightweightA500Machine(new() {
            FloppyDriveTypes = [LightweightFloppyDriveType.HighDensity] });
        machine.MountAdf(new byte[hd ? 1802240 : 901120]);
        var cycle = machine.Cycle;
        machine.WriteByte(0xBFD100, 0x77, ref cycle, M68kBusAccessKind.CpuDataWrite);
        machine.WriteByte(0xBFD300, 0xFF, ref cycle, M68kBusAccessKind.CpuDataWrite);
        var origin = machine.GetDiskNextCycle(0) - 14;
        var period = 2L * (LightweightPaulaAudio.PalCpuFrequency / (hd ? 5 : 10));
        machine.AdvanceHardwareTo(origin + period - 2);
        Assert.Equal(0, machine.CiaBPendingInterrupts & 0x10);
        machine.AdvanceHardwareTo(origin + period);
        Assert.NotEqual(0, machine.CiaBPendingInterrupts & 0x10);
        cycle = machine.Cycle;
        Assert.Equal(0x10, machine.ReadByte(0xBFDD00, ref cycle, M68kBusAccessKind.CpuDataRead));
        machine.AdvanceHardwareTo(origin + period * 2 - 2);
        Assert.Equal(0, machine.CiaBPendingInterrupts & 0x10);
        machine.AdvanceHardwareTo(origin + period * 2);
        Assert.NotEqual(0, machine.CiaBPendingInterrupts & 0x10);
        Assert.Null(machine.UnsupportedActiveFeature);
    }

    private static long FollowingCellCycles(LightweightFloppyDrive drive)
    {
        var previous = drive.NextBitCycle;
        drive.AdvanceRotation();
        return drive.NextBitCycle - previous;
    }

    private static uint Identify(LightweightFloppyDrive drive, int unit)
    {
        var selectedOn = (byte)(0x7F & ~(8 << unit));
        var selectedOff = (byte)(0xFF & ~(8 << unit));
        drive.WriteControlPins(0xFF, 0);
        drive.WriteControlPins(0x7F, 0);
        drive.WriteControlPins(selectedOn, 0);
        drive.WriteControlPins(0x7F, 0);
        drive.WriteControlPins(0xFF, 0);
        drive.WriteControlPins(selectedOff, 0);
        drive.WriteControlPins(0xFF, 0);
        uint id = 0;
        for (var bit = 0; bit < 32; bit++)
        {
            drive.WriteControlPins(selectedOff, 0);
            id = (id << 1) | (uint)((drive.ReadInputPins(0) >> 5) & 1);
            drive.WriteControlPins(0xFF, 0);
            Assert.Equal(0xFF, drive.ReadInputPins(0));
        }
        return id;
    }
}
