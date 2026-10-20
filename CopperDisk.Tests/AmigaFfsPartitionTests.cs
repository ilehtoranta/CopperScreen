namespace CopperDisk.Tests;

public sealed class AmigaFfsPartitionTests
{
    [Theory]
    [InlineData("DH0:2:32:4:58", 131072, 1802240)]
    [InlineData("Work:2:32:4:259", 131072, 8388608)]
    [InlineData("DH0:1:1:0:8388606", 0, 4294966784)]
    [InlineData("DH0:2:32:0:131071", 0, 4294967296)]
    [InlineData("DH0:2:32:131076:131130", 4295098368, 1802240)]
    [InlineData("DH0:2:32:131068:131122", 4294836224, 1802240)]
    [InlineData("DH0:2:32:4:262147", 131072, 8589934592)]
    [InlineData("DH0:1:1:0:4294967294", 0, 2199023255040)]
    public void ExplicitGeometryUsesInclusiveCylindersAndByteOffsets(string geometry, long start, long length)
    {
        var p = AmigaFfsPartition.ParseGeometry(geometry);
        Assert.Equal((start, length), AmigaFfsPartition.GetByteRange(p));
        Assert.Equal(0x444F5301u, p.DosType);
        Assert.Equal(0xFFFFFFFEu, p.Mask);
    }

    [Theory]
    [InlineData("DH0:2:32:4")]
    [InlineData("DH0:2:32:-1:58")]
    [InlineData("DH0:0:32:4:58")]
    [InlineData("DH0:2:32:58:4")]
    [InlineData("DH0:1:1:0:4294967295")]
    [InlineData("DH0:4194305:1:4294967294:4294967295")]
    [InlineData("DH0:4294967295:4294967295:0:4294967295")]
    [InlineData("DH0:2:32:0:4294967296")]
    public void InvalidOrUnaddressablePartitionIsRejected(string geometry)
        => Assert.Throws<ArgumentException>(() => AmigaFfsPartition.ParseGeometry(geometry));
}
