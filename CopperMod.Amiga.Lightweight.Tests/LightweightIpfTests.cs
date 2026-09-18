using CopperDisk;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

public sealed class LightweightIpfTests
{
    [Theory]
    [InlineData(100003)]
    [InlineData(110000)]
    public void PreservedLongTrackRecoversAllSectorsThroughReceiverAndDma(int bits)
    {
        var adf = new byte[AmigaDiskGeometry.StandardAdfSize];
        new Random(0x68_000).NextBytes(adf.AsSpan(0, 11 * 512));
        var encoded = AmigaDosTrackEncoder.EncodeTrack(new AdfDiskMedia(adf), 0, 0);
        var data = Enumerable.Repeat((byte)0xAA, (bits + 7) / 8).ToArray();
        encoded.AsSpan(0, Math.Min(encoded.Length, data.Length)).CopyTo(data);
        using var machine = new LightweightA500Machine();
        machine.MountIpf(IpfFixture.Create(new IpfFixture.Track(bits, data, Start: bits - 7)));
        // Capture the raw recovered stream. WORDSYNC deliberately discards a
        // partial DMA word when a sync arrives at a different alignment after
        // a non-word-sized revolution; the sector decoder needs both syncs.
        machine.WriteCustomRegisterFromCopper(0x09E, 0x8100, machine.Cycle);
        machine.WriteCustomRegisterFromCopper(0x096, 0x8210, machine.Cycle);
        WritePins(machine, 0x77);
        machine.AdvanceHardwareTo(machine.Cycle + LightweightFloppyDrive.MotorSpinUpCycles + 300000);
        machine.WriteCustomRegisterFromCopper(0x020, 0, machine.Cycle);
        machine.WriteCustomRegisterFromCopper(0x022, 0x2000, machine.Cycle);
        // More than two revolutions, so a sector straddling capture ends is
        // represented completely without relying on a circular DMA buffer.
        const int words = 15000;
        machine.WriteCustomRegisterFromCopper(0x024, (ushort)(0x8000 | words), machine.Cycle);
        machine.WriteCustomRegisterFromCopper(0x024, (ushort)(0x8000 | words), machine.Cycle);
        machine.AdvanceHardwareTo(machine.Cycle + 4 * 1418758);
        Assert.False(machine.DiskDmaActive);
        var recovered = new byte[11 * 512];
        var track = new AmigaEncodedTrack(machine.ChipRam.Slice(0x2000, words * 2), words * 16);
        Assert.Equal(11, AmigaDosTrackDecoder.DecodeTrackBestEffort(track, 0, 0, recovered));
        Assert.Equal(adf.AsSpan(0, recovered.Length).ToArray(), recovered);
        Assert.Null(machine.UnsupportedActiveFeature);
    }

    [Fact]
    public void ImpossibleCellGeometryIsRejectedBeforeReplacingMedia()
    {
        using var machine = new LightweightA500Machine();
        var adf = new byte[901120]; machine.MountAdf(adf);
        var ipf = IpfFixture.Create(new IpfFixture.Track(800000, new byte[100000]));
        Assert.Throws<NotSupportedException>(() => machine.MountIpf(0, ipf));
        Assert.Equal(LightweightFloppyFormat.Adf, machine.GetDriveFormat(0));
        Assert.Equal(adf, machine.ExportAdf());
    }

    [Theory]
    [InlineData(3, 1000, 1000, 1000, 1000, 945, 995, 1045)]
    [InlineData(4, 945, 995, 1045, 1000, 1000, 1000, 1000)]
    [InlineData(5, 1000, 1000, 1000, 1000, 1000, 1050, 1000)]
    [InlineData(6, 1000, 1100, 900, 1000, 1000, 1000, 1000)]
    [InlineData(7, 1000, 1050, 1000, 1000, 1000, 1000, 1000)]
    [InlineData(8, 1000, 1100, 1050, 1000, 950, 900, 850)]
    [InlineData(9, 1000, 1050, 950, 1050, 950, 1050, 950)]
    public void SpsDensityProfilesPreserveBoundariesAndIndependentIndex(int profile, params int[] expected)
    {
        const int start = 83997, bits = 84000;
        var ipf = IpfFixture.DensityTrack((uint)profile, start);
        var track = Assert.Single(IpfDecoder.Decode(ipf, new() { AlignTracksToWord = false }).Tracks);
        var weights = track.CellWeights.Span;
        for (var block = 0; block < 7; block++)
        {
            Assert.Equal(expected[block], weights[(start + block * 12000) % bits]);
            Assert.Equal(expected[block], weights[(start + block * 12000 + 9999) % bits]);
            var gapWeight = profile is 3 or 4 ? expected[(block + 1) % 7] : 1000;
            Assert.Equal(gapWeight, weights[(start + block * 12000 + 10000) % bits]);
        }
        var prepared = LightweightIpfImage.Prepare(ipf);
        var times = prepared.CellDeadlines[0]!;
        Assert.Equal(709379, times[^1]);
        for (var i = 1; i < times.Length; i++) Assert.True(times[i] > times[i - 1]);
        var drive = new LightweightFloppyDrive(); drive.MountIpf(prepared); Start(drive);
        var origin = (drive.ReadyCycle + 1) & ~1L;
        for (var bit = 0; bit < bits; bit++)
        {
            Assert.Equal(origin + 2L * times[bit + 1], drive.NextBitCycle);
            drive.AdvanceRotation();
        }
        Assert.Equal(origin + 2L * (709379 + times[1]), drive.NextBitCycle);
    }

    [Fact]
    public void DensitySeekKeepsElapsedSpindleTimeWithoutAllocations()
    {
        var drive = new LightweightFloppyDrive(); drive.MountIpf(IpfFixture.DensityTrack(6)); Start(drive);
        for (var i = 0; i < 21000; i++) drive.AdvanceRotation();
        var cycle = drive.NextBitCycle - 2;
        drive.WriteControlPins(0x73, cycle); // Missing side has uniform unformatted timing.
        Assert.True(drive.NextBitCycle > cycle);
        drive.WriteControlPins(0x77, cycle);
        Assert.Equal(21000, drive.BitPosition);
        for (var i = 0; i < 1000; i++) drive.AdvanceRotation();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 84000; i++) { drive.ReadBit(); drive.AdvanceRotation(); }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Theory]
    [InlineData(100003)]
    [InlineData(110000)]
    public void ExactLengthAndIndexOrientationSurviveMount(int bits)
    {
        var data = new byte[(bits + 7) / 8]; data[0] = 0xDE; data[1] = 0xAD;
        var image = IpfFixture.Create(new IpfFixture.Track(bits, data, Start: bits - 7));
        var drive = new LightweightFloppyDrive(); drive.MountIpf(image);
        Assert.Equal(bits, drive.TrackBitLength);
        Start(drive);
        var origin = (drive.ReadyCycle + 1) & ~1L;
        var captured = new int[bits];
        for (var i = 0; i < bits; i++)
        {
            captured[i] = drive.ReadBit();
            Assert.Equal(i == bits - 1, drive.AdvanceRotation());
        }
        for (var i = 0; i < 16; i++) Assert.Equal((data[i / 8] >> (7 - i % 8)) & 1, captured[(bits - 7 + i) % bits]);
        Assert.Equal(1u, drive.Revolution);
        Assert.Equal(origin + 2L * 709379 + 2 * ((709379L + bits - 1) / bits), drive.NextBitCycle);
    }

    [Fact]
    public void WeakDataVariesPerRevolutionButRepeatsForSameMediaAndDrive()
    {
        var image = IpfFixture.Create(new IpfFixture.Track(100000, [], Weak: true));
        var a = new LightweightFloppyDrive(2); a.MountIpf(image); Start(a, 2);
        var b = new LightweightFloppyDrive(2); b.MountIpf(image); Start(b, 2);
        var first = new int[256];
        for (var i = 0; i < 100000; i++)
        {
            var bit = a.ReadBit(); Assert.Equal(bit, b.ReadBit());
            if (i < first.Length) first[i] = bit;
            a.AdvanceRotation(); b.AdvanceRotation();
        }
        var different = false;
        for (var i = 0; i < 256; i++)
        {
            var bit = a.ReadBit(); Assert.Equal(bit, b.ReadBit()); different |= first[i] != bit;
            a.AdvanceRotation(); b.AdvanceRotation();
        }
        Assert.True(different);
    }

    [Fact]
    public void NoFluxTrackHasNoInventedTransitionsAndRetainsOuterCylinder()
    {
        var ipf = IpfFixture.Create(new IpfFixture.Track(0, [], Cylinder: 83, Density: 1));
        var decoded = IpfDecoder.Decode(ipf);
        Assert.Equal(83, Assert.Single(decoded.Tracks).Cylinder);
        var drive = new LightweightFloppyDrive(); drive.MountIpf(ipf);
        for (var i = 0; i < 83; i++) { drive.WriteControlPins(0x75, 0); drive.WriteControlPins(0x74, 0); }
        drive.UpdateRotation(0, false);
        Assert.Equal(83, drive.Cylinder);
        for (var i = 0; i < 100100; i++) { Assert.Equal(0, drive.ReadBit()); drive.AdvanceRotation(); }
    }

    [Fact]
    public void DifferentLengthSideChangeKeepsPhysicalRevolutionAndResetSelectsSideZero()
    {
        var drive = new LightweightFloppyDrive();
        drive.MountIpf(IpfFixture.Create(new IpfFixture.Track(100000, new byte[12500]), new(110000, new byte[13750], Head: 1)));
        Start(drive); var origin = (drive.ReadyCycle + 1) & ~1L;
        for (var i = 0; i < 25000; i++) drive.AdvanceRotation();
        var cycle = drive.NextBitCycle - 2;
        drive.WriteControlPins(0x73, cycle);
        Assert.InRange(drive.BitPosition, 27500, 27501);
        Assert.True(drive.NextBitCycle > cycle);
        while (drive.BitPosition != 109999) drive.AdvanceRotation();
        Assert.Equal(origin + 2L * 709379, drive.NextBitCycle);
        drive.ResetControl(); Assert.Equal(0, drive.Head); Assert.Equal(100000, drive.TrackBitLength);
    }

    [Fact]
    public void AllDrivesRejectWritingExportAndInvalidReplacementWithoutLosingMedia()
    {
        using var machine = new LightweightA500Machine(new() { FloppyDriveCount = 4 });
        var ipf = IpfFixture.Create(new IpfFixture.Track(100003, new byte[12501]));
        for (var i = 0; i < 4; i++)
        {
            machine.MountIpf(i, ipf);
            Assert.Equal(LightweightFloppyFormat.Ipf, machine.GetDriveFormat(i));
            Assert.True(machine.IsDriveWriteProtected(i)); Assert.False(machine.CanWriteDrive(i));
            var index = i;
            Assert.Throws<InvalidOperationException>(() => machine.SetDriveWriteProtected(index, false));
            Assert.Throws<InvalidOperationException>(() => machine.ExportAdf(index));
            Assert.Throws<IpfDecodeException>(() => machine.MountIpf(index, new byte[5]));
            Assert.True(machine.IsDriveMounted(i));
        }
    }

    [Fact]
    public void WeakTrackExecutionAllocatesNothing()
    {
        var drive = new LightweightFloppyDrive(); drive.MountIpf(IpfFixture.Create(new IpfFixture.Track(100000, [], Weak: true))); Start(drive);
        for (var i = 0; i < 1000; i++) { drive.ReadBit(); drive.AdvanceRotation(); }
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 200000; i++) { drive.ReadBit(); drive.AdvanceRotation(); }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static void Start(LightweightFloppyDrive drive, int index = 0)
    {
        drive.WriteControlPins((byte)(0x7F & ~(8 << index)), 0);
        drive.UpdateRotation(0, false);
    }

    [Fact]
    public void ReceiverAndDmaCarrySyncAcrossIndexAndCancellationStopsTransfers()
    {
        const int bits = 100000;
        var data = Enumerable.Repeat((byte)0xAA, bits / 8).ToArray();
        data[^3] = 0x44; data[^2] = 0x89; data[^1] = 0x44; data[0] = 0x89;
        using var machine = new LightweightA500Machine();
        machine.MountIpf(IpfFixture.Create(new IpfFixture.Track(bits, data)));
        machine.WriteCustomRegisterFromCopper(0x09E, 0x8500, machine.Cycle);
        machine.WriteCustomRegisterFromCopper(0x096, 0x8210, machine.Cycle);
        WritePins(machine, 0x77);
        machine.AdvanceHardwareTo(machine.Cycle + LightweightFloppyDrive.MotorSpinUpCycles + 300000);
        machine.WriteCustomRegisterFromCopper(0x020, 0, machine.Cycle);
        machine.WriteCustomRegisterFromCopper(0x022, 0x2000, machine.Cycle);
        machine.WriteCustomRegisterFromCopper(0x024, 0x8001, machine.Cycle);
        machine.WriteCustomRegisterFromCopper(0x024, 0x8001, machine.Cycle);
        machine.AdvanceHardwareTo(machine.Cycle + 2 * 1418758);
        Assert.False(machine.DiskDmaActive);
        Assert.Equal(0x4489, System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(machine.ChipRam.Span[0x2000..]));
        machine.WriteCustomRegisterFromCopper(0x022, 0x2100, machine.Cycle);
        machine.WriteCustomRegisterFromCopper(0x024, 0x8008, machine.Cycle);
        machine.WriteCustomRegisterFromCopper(0x024, 0x8008, machine.Cycle);
        machine.WriteCustomRegisterFromCopper(0x024, 0, machine.Cycle);
        machine.AdvanceHardwareTo(machine.Cycle + 1418758);
        Assert.All(machine.ChipRam.Span.Slice(0x2100, 16).ToArray(), b => Assert.Equal(0, b));
        Assert.Null(machine.UnsupportedActiveFeature);
    }

    [Fact]
    public void FourRunningPreservedDrivesKeepSpindlePhaseThroughSelection()
    {
        using var machine = new LightweightA500Machine(new() { FloppyDriveCount = 4 });
        var image = LightweightIpfImage.Prepare(IpfFixture.Create(new IpfFixture.Track(100003, new byte[12501])));
        for (var drive = 0; drive < 4; drive++)
        {
            machine.MountIpf(drive, image);
            WritePins(machine, (byte)(0x7F & ~(8 << drive)));
        }
        machine.AdvanceHardwareTo(machine.Cycle + LightweightFloppyDrive.MotorSpinUpCycles + 400000);
        var next = Enumerable.Range(0, 4).Select(machine.GetDiskNextCycle).ToArray();
        WritePins(machine, 0x7F);
        for (var drive = 0; drive < 4; drive++)
        {
            var before = machine.GetDiskBitPosition(drive);
            WritePins(machine, (byte)(0x7F & ~(8 << drive)));
            Assert.InRange(machine.GetDiskBitPosition(drive) - before, 0, 4);
            Assert.True(machine.GetDiskNextCycle(drive) >= next[drive]);
            Assert.True(machine.GetDriveState(drive).MotorOn);
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        machine.AdvanceHardwareTo(machine.Cycle + 1418758);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - allocated);
    }

    private static void WritePins(LightweightA500Machine machine, byte pins)
    {
        var cycle = machine.Cycle;
        machine.WriteByte(0xBFD100, pins, ref cycle, Copper68k.M68kBusAccessKind.CpuDataWrite);
        machine.WriteByte(0xBFD300, 255, ref cycle, Copper68k.M68kBusAccessKind.CpuDataWrite);
    }
}
