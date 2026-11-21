using CopperDisk;

namespace CopperDisk.Tests;

public sealed class ValidatedSectorTests
{
    private static byte[] Track(byte fill)
    {
        var adf = AmigaDiskLoader.FromAdfBytes(Enumerable.Repeat(fill, 901120).ToArray());
        return AmigaDosTrackEncoder.EncodeTrack(adf, 0, 0);
    }

    [Fact]
    public void StrictReadsDistinguishMissingDataFromRealZeroSectors()
    {
        var valid = AmigaEncodedTrack.FromBytes(Track(0));
        var missing = AmigaEncodedTrack.FromBytes(AmigaDosTrackEncoder.CreateUnformattedTrack());
        var result = AmigaDosTrackDecoder.DecodeValidated<IAmigaTrack>([valid, missing]);
        var bytes = new byte[512]; result.Read(0, bytes); Assert.All(bytes, b => Assert.Equal(0, b));
        Assert.Equal(SectorValidity.Valid, result.GetValidity(0)); Assert.Equal(SectorValidity.Missing, result.GetValidity(11));
        Assert.Throws<IOException>(() => result.Read(11 * 512, bytes));
        Assert.Throws<ArgumentOutOfRangeException>(() => result.Read(-1, bytes));
    }

    [Fact]
    public void ConflictingValidCopiesAreAmbiguousRatherThanLastCopyWins()
    {
        var first = Track(3); var second = Track(9); var combined = first.Concat(second).ToArray();
        var result = AmigaDosTrackDecoder.DecodeValidated<IAmigaTrack>([AmigaEncodedTrack.FromBytes(combined)]);
        Assert.Equal(SectorValidity.Ambiguous, result.GetValidity(0));
        Assert.Throws<IOException>(() => result.Read(0, new byte[512]));
    }

    [Fact]
    public void WeakSectorsAreNeverCertifiedEvenWhenTheirDeterministicBytesPassChecksums()
    {
        var bytes = Track(8); var track = new AmigaEncodedTrack(bytes, bytes.Length * 8, 0, AmigaTrackFeatures.WeakData);
        var strict = AmigaDosTrackDecoder.DecodeValidated<IAmigaTrack>([track]);
        Assert.Equal(SectorValidity.Weak, strict.GetValidity(0));
        Assert.Throws<IOException>(() => strict.Read(0, new byte[512]));
        var ordinary = AmigaDosTrackDecoder.DecodeBestEffort<IAmigaTrack>([track], out var complete);
        Assert.True(complete); Assert.All(ordinary, b => Assert.Equal(8, b));
    }

    [Fact]
    public void UnusedWeakGapDoesNotInvalidateSoundSectors()
    {
        var bytes = Track(5);
        // Synthetic track starts with a gap before its first sync word.
        var track = new AmigaEncodedTrack(bytes, bytes.Length * 8, 0, AmigaTrackFeatures.WeakData,
            [new AmigaTrackRegion(0, 8, AmigaTrackFeatures.WeakData)]);
        var strict = AmigaDosTrackDecoder.DecodeValidated<IAmigaTrack>([track]);
        Assert.Equal(SectorValidity.Valid, strict.GetValidity(0));
        strict.Read(0, new byte[512]);
    }
}
