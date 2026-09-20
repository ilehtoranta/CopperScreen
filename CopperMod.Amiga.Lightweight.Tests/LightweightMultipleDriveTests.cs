using System.Buffers.Binary;
using Copper68k;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

public sealed class LightweightMultipleDriveTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void EachSelectLineReadsItsOwnMediaThroughTheSharedDma(int selected)
    {
        using var m = FourDrives();
        for (var i = 0; i < 4; i++)
        {
            var image = new byte[LightweightFloppyDrive.StandardAdfBytes];
            Array.Fill(image, (byte)(0x31 + i * 0x23));
            m.MountAdf(i, image);
        }
        Pins(m, (byte)(0x7F & ~(8 << selected)));
        Custom(m, 0x09E, 0x8100); // FAST; no WORDSYNC, capture from physical bit zero.
        Custom(m, 0x096, 0x8210);
        Custom(m, 0x022, 0x1000);
        Custom(m, 0x024, 0x8220); // One complete encoded sector, 1088 bytes.
        Custom(m, 0x024, 0x8220);
        for (var i = 0; i < 4; i++)
            Assert.Equal(i == selected, m.GetDriveState(i).ActiveDma);
        m.AdvanceHardwareTo(m.DiskSerialNextCycle + 150_000);
        Assert.False(m.DiskDmaActive);
        Assert.Equal(2, m.Intreq & 2);
        var encoded = m.ChipRam.Span.Slice(0x1000, 1088);
        Assert.Equal(0x44894489u, U32(encoded.Slice(4)));
        Assert.Equal(0xFF00090Bu, Decode(encoded.Slice(8), encoded.Slice(12)));
        var value = (uint)(0x31 + selected * 0x23) * 0x01010101u;
        for (var offset = 0; offset < 512; offset += 4)
            Assert.Equal(value, Decode(encoded.Slice(0x40 + offset), encoded.Slice(0x240 + offset)));
        Assert.Null(m.UnsupportedActiveFeature);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void MotorOffIdentificationDistinguishesConnectedExternalDrives(int count)
    {
        using var m = new LightweightA500Machine(new() { FloppyDriveCount = count });
        for (var i = 1; i < 4; i++)
        {
            // The standard DD identifier is 32 zero bits, independent of media.
            uint identifier = 0;
            for (var bit = 0; bit < 32; bit++)
            {
                Pins(m, 0xFF);
                Pins(m, (byte)(0xFF & ~(8 << i)));
                identifier = (identifier << 1) | ((ReadPins(m) & 0x20) != 0 ? 1u : 0u);
            }
            Assert.Equal(i < count ? 0u : uint.MaxValue, identifier);
        }
        Pins(m, 0xF7); // Internal empty DF0 retains its existing READY behavior.
        Assert.NotEqual(0, ReadPins(m) & 0x20);
        Assert.Null(m.UnsupportedActiveFeature);
    }

    [Fact]
    public void StepMotorChangeAndStatusAreIndependentAndStatusLinesAreWiredTogether()
    {
        using var m = FourDrives();
        m.MountAdf(2, new byte[LightweightFloppyDrive.StandardAdfBytes]);
        Pins(m, 0xDD); // DF2, motor off, step inward.
        Pins(m, 0xDC);
        Assert.Equal(1, m.GetDriveState(2).Cylinder);
        Assert.Equal(0x14, ReadPins(m) & 0x3C); // Changed cleared, off track zero, DD id and WP low.
        Pins(m, 0xD7); // DF0 and DF2: track zero from DF0 pulls shared line low.
        Assert.Equal(0, ReadPins(m) & 0x1C);
        Pins(m, 0xFF);
        Pins(m, 0x6F); // Motor on only for DF1.
        Assert.True(m.GetDriveState(1).MotorOn);
        Assert.False(m.GetDriveState(2).MotorOn);
        Pins(m, 0xFF);
        Assert.True(m.GetDriveState(1).MotorOn);
        Assert.Equal(0, m.GetDriveState(0).Cylinder);
        Assert.Equal(0, m.GetDriveState(3).Cylinder);
        m.Reset();
        Assert.True(m.IsDriveMounted(2));
        Assert.Equal(1, m.GetDriveState(2).Cylinder);
        for (var i = 0; i < 4; i++) Assert.False(m.GetDriveState(i).MotorOn);
    }

    [Fact]
    public void UnselectedSpindlesKeepIndependentPhaseAndEjectOnlyStopsItsOwnSource()
    {
        using var m = FourDrives();
        for (var i = 0; i < 4; i++) m.MountAdf(i, new byte[LightweightFloppyDrive.StandardAdfBytes]);
        Pins(m, 0x77);
        m.AdvanceHardwareTo(1000);
        Pins(m, 0x6F);
        m.AdvanceHardwareTo(2000);
        Pins(m, 0x5F);
        m.AdvanceHardwareTo(3000);
        Pins(m, 0x3F);
        Pins(m, 0xFF);
        Custom(m, 0x09E, 0x8100);
        m.AdvanceHardwareTo(m.GetDiskNextCycle(3) + 1000);
        for (var i = 0; i < 3; i++) Assert.True(m.GetDiskBitPosition(i) > m.GetDiskBitPosition(i + 1));
        var next = m.GetDiskNextCycle(1);
        Pins(m, 0x6F); // Re-select still-running DF1; phase must not restart.
        Assert.InRange(m.GetDiskNextCycle(1) - next, 0, 100);
        Assert.True(m.GetDiskBitPosition(1) > 50);
        var shift = m.DiskInputShift;
        m.EjectAdf(0);
        Assert.Equal(long.MaxValue, m.GetDiskNextCycle(0));
        Assert.Equal(shift, m.DiskInputShift);
        Assert.True(m.IsDriveMounted(1));
        m.AdvanceHardwareTo(m.GetDiskNextCycle(1) + 100);
        Assert.NotEqual(shift, m.DiskInputShift);
        Assert.Null(m.UnsupportedActiveFeature);
    }

    [Fact]
    public void SerialByteAssemblyBelongsToControllerAcrossDriveSwitch()
    {
        using var m = new LightweightA500Machine();
        Custom(m, 0x09E, 0x8100);
        var a = new LightweightFloppyDrive(0);
        var b = new LightweightFloppyDrive(3);
        a.Mount(new byte[LightweightFloppyDrive.StandardAdfBytes]);
        b.Mount(new byte[LightweightFloppyDrive.StandardAdfBytes]);
        a.WriteControlPins(0x77, 0);
        b.WriteControlPins(0x3F, 1000);
        var serial = new LightweightDiskSerial();
        serial.OnDriveChanged(a, 0);
        serial.OnDriveChanged(b, 1000);
        ushort expected = 0;
        for (var i = 0; i < 8; i++)
        {
            var drive = i < 5 ? a : b;
            var p = drive.BitPosition;
            expected = (ushort)((expected << 1) | ((drive.Track[p >> 3] >> (7 - (p & 7))) & 1));
            m.AdvanceHardwareTo(drive.NextBitCycle);
            serial.Step(m.Cycle, drive, m);
        }
        Assert.Equal(5, a.BitPosition);
        Assert.Equal(3, b.BitPosition);
        Assert.Equal(expected, serial.Shift);
        Assert.Equal(0x8000 | expected, serial.ReadByteStatus(new LightweightRegisters(), false));
    }

    [Fact]
    public void MultiSelectControlIsSupportedButConflictingReadStreamsAreReported()
    {
        using var m = FourDrives();
        Pins(m, 0x85); // All four selected, motor off; inward step.
        Pins(m, 0x84);
        for (var i = 0; i < 4; i++) Assert.Equal(1, m.GetDriveState(i).Cylinder);
        Assert.Null(m.UnsupportedActiveFeature);
        m.MountAdf(0, new byte[LightweightFloppyDrive.StandardAdfBytes]);
        m.MountAdf(3, new byte[LightweightFloppyDrive.StandardAdfBytes]);
        Pins(m, 0xFF);
        Pins(m, 0x37);
        m.AdvanceHardwareTo(m.DiskSerialNextCycle);
        Assert.Contains("simultaneous disk read streams", m.UnsupportedActiveFeature!);
    }

    [Fact]
    public void FourSpinningDrivesDoNotAllocateDuringHardwareExecution()
    {
        using var m = FourDrives();
        for (var i = 0; i < 4; i++)
        {
            m.MountAdf(i, new byte[LightweightFloppyDrive.StandardAdfBytes]);
            Pins(m, (byte)(0x7F & ~(8 << i)));
        }
        Custom(m, 0x09E, 0x8100);
        m.AdvanceHardwareTo(m.GetDiskNextCycle(3) + 1000);
        var target = m.Cycle + LightweightPaulaAudio.PalCpuFrequency;
        var before = GC.GetAllocatedBytesForCurrentThread();
        m.AdvanceHardwareTo(target);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Null(m.UnsupportedActiveFeature);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void InvalidConnectedCountsAreRejected(int count)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new LightweightA500Machine(new() { FloppyDriveCount = count }));

    [Fact]
    public void DisconnectedAndInvalidIndicesNeverAliasDf0()
    {
        using var m = new LightweightA500Machine();
        foreach (var i in new[] { -1, 1, 4 })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => m.MountAdf(i, new byte[901120]));
            Assert.Throws<ArgumentOutOfRangeException>(() => m.EjectAdf(i));
            Assert.Throws<ArgumentOutOfRangeException>(() => m.GetDriveState(i));
        }
        Assert.False(m.IsAdfMounted);
    }

    private static LightweightA500Machine FourDrives() => new(new() { FloppyDriveCount = 4 });
    private static void Pins(LightweightA500Machine m, byte pins)
    {
        var cycle = m.Cycle;
        m.WriteByte(0xBFD100, pins, ref cycle, M68kBusAccessKind.CpuDataWrite);
        m.WriteByte(0xBFD300, 0xFF, ref cycle, M68kBusAccessKind.CpuDataWrite);
    }
    private static byte ReadPins(LightweightA500Machine m)
    {
        var cycle = m.Cycle;
        return m.ReadByte(0xBFE001, ref cycle, M68kBusAccessKind.CpuDataRead);
    }
    private static void Custom(LightweightA500Machine m, int register, ushort value)
        => m.WriteCustomRegisterFromCopper((ushort)register, value, m.Cycle);
    private static uint U32(ReadOnlySpan<byte> data) => BinaryPrimitives.ReadUInt32BigEndian(data);
    private static uint Decode(ReadOnlySpan<byte> odd, ReadOnlySpan<byte> even)
        => ((U32(odd) & 0x55555555) << 1) | (U32(even) & 0x55555555);
}
