using CopperDisk;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

public sealed class LightweightDiskFormatTests
{
    [Theory]
    [InlineData("adz", false, false)]
    [InlineData("dms", false, false)]
    [InlineData("adf", true, false)]
    [InlineData("adz", true, false)]
    [InlineData("adf", true, true)]
    [InlineData("adz", true, true)]
    [InlineData("scp", false, false)]
    public void AddedSectorAndExtendedFormatsRecoverSectorsThroughPaulaDma(string extension, bool extended, bool raw)
    {
        var sectors = new byte[901120]; new Random(68000).NextBytes(sectors.AsSpan(0, 5632));
        var encoded = AmigaDosTrackEncoder.EncodeTrack(new AdfDiskMedia(sectors), 0, 0);
        var rawTrack = Enumerable.Repeat((byte)0xAA, 13750).ToArray(); encoded.CopyTo(rawTrack, 0);
        var image = extended ? DiskFormatFixture.Extended(sectors, raw ? rawTrack : null, 110000) : sectors;
        if (extension == "adz") image = DiskFormatFixture.Adz(image);
        if (extension == "dms") image = DiskFormatFixture.Dms(image);
        if (extension == "scp") image = DiskFormatFixture.ScpEncoded(encoded);
        using var machine = new LightweightA500Machine();
        machine.MountDisk(0, image, "disk." + extension);
        Assert.False(machine.CanWriteDrive(0)); Assert.True(machine.IsDriveWriteProtected(0));
        Assert.Throws<InvalidOperationException>(() => machine.SetDriveWriteProtected(0, false));
        Assert.Throws<InvalidOperationException>(() => machine.ExportAdf(0));
        Capture(machine);
        var recovered = new byte[5632];
        var track = new AmigaEncodedTrack(machine.ChipRam.Slice(0x2000, 30000), 240000);
        Assert.Equal(11, AmigaDosTrackDecoder.DecodeTrackBestEffort(track, 0, 0, recovered));
        Assert.Equal(sectors.AsSpan(0, 5632).ToArray(), recovered);
        Assert.Null(machine.UnsupportedActiveFeature);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RawNonWordSizedExtendedTrackSurvivesWithoutSectorReconstruction(bool compressed)
    {
        var raw = new byte[12502]; new Random(123).NextBytes(raw);
        var bytes = DiskFormatFixture.Extended(new byte[901120], raw, 100003);
        if (compressed) bytes = DiskFormatFixture.Adz(bytes);
        var image = LightweightFloppyImage.Prepare(bytes, compressed ? "raw.adz" : "raw.adf");
        Assert.Null(image.Sectors); Assert.Equal(100003, image.Tracks!.Lengths[0]);
        Assert.Equal(raw.AsSpan(0, 12501).ToArray(), image.Tracks.Tracks[0]);
        using var machine = new LightweightA500Machine(); machine.MountDisk(0, image);
        Capture(machine); Assert.False(machine.DiskDmaActive);
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        machine.AdvanceHardwareTo(machine.Cycle + 1418758);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - allocated);
    }

    [Fact]
    public void ScpRetainsDecodedPulsesOuterCylindersAndNoFluxGaps()
    {
        var image = LightweightFloppyImage.Prepare(DiskFormatFixture.Scp(83, 80, 160), "outer.scp");
        Assert.Equal(LightweightFloppyFormat.Scp, image.Format);
        Assert.Equal(168, image.Tracks!.Tracks.Length);
        Assert.Equal(0xA0, image.Tracks.Tracks[166][0]);
        Assert.All(image.Tracks.Tracks[0], b => Assert.Equal(0, b));
        var gap = LightweightFloppyImage.Prepare(DiskFormatFixture.Scp(0, 0, 80), "gap.scp");
        Assert.All(gap.Tracks!.Tracks[0].Take(100), b => Assert.Equal(0, b));
        // The NoFlux region includes the terminating pulse in CopperDisk's
        // bounded decoder. Preserve that pulse instead of clearing the region.
        Assert.Equal(Assert.Single(ScpDecoder.Decode(DiskFormatFixture.Scp(0, 0, 80)).Tracks).EncodedData.ToArray(), gap.Tracks.Tracks[0]);
        Assert.Throws<ScpDecodeException>(() => LightweightFloppyImage.Prepare(DiskFormatFixture.Scp(84, 80), "invalid.scp"));
    }

    [Theory]
    [InlineData("adf")]
    [InlineData("adz")]
    [InlineData("dms")]
    [InlineData("scp")]
    public void MalformedReplacementDoesNotEjectExistingMedia(string extension)
    {
        using var machine = new LightweightA500Machine(); var original = new byte[901120]; machine.MountAdf(original);
        Assert.ThrowsAny<Exception>(() => machine.MountDisk(0, new byte[12], "bad." + extension));
        Assert.Equal(LightweightFloppyFormat.Adf, machine.GetDriveFormat(0));
        Assert.Equal(original, machine.ExportAdf());
    }

    private static void Capture(LightweightA500Machine machine)
    {
        machine.WriteCustomRegisterFromCopper(0x09E, 0x8100, machine.Cycle);
        machine.WriteCustomRegisterFromCopper(0x096, 0x8210, machine.Cycle);
        var cycle = machine.Cycle;
        machine.WriteByte(0xBFD100, 0x77, ref cycle, Copper68k.M68kBusAccessKind.CpuDataWrite);
        machine.WriteByte(0xBFD300, 255, ref cycle, Copper68k.M68kBusAccessKind.CpuDataWrite);
        machine.AdvanceHardwareTo(machine.Cycle + LightweightFloppyDrive.MotorSpinUpCycles + 300000);
        machine.WriteCustomRegisterFromCopper(0x020, 0, machine.Cycle);
        machine.WriteCustomRegisterFromCopper(0x022, 0x2000, machine.Cycle);
        machine.WriteCustomRegisterFromCopper(0x024, 0xBA98, machine.Cycle);
        machine.WriteCustomRegisterFromCopper(0x024, 0xBA98, machine.Cycle);
        machine.AdvanceHardwareTo(machine.Cycle + 4 * 1418758);
        Assert.False(machine.DiskDmaActive);
    }
}
