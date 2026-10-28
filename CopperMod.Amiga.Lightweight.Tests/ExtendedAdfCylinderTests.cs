using System.Buffers.Binary;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

public sealed class ExtendedAdfCylinderTests
{
    [Fact]
    public void PreparedExtendedAdfRetainsCylinder82Head1ThroughDriveSelection()
    {
        const int count = 166;
        var image = new byte[12 + count * 12 + count * 4];
        "UAE-1ADF"u8.CopyTo(image);
        BinaryPrimitives.WriteUInt16BigEndian(image.AsSpan(10), count);
        for (var track = 0; track < count; track++)
        {
            var header = image.AsSpan(12 + track * 12);
            BinaryPrimitives.WriteUInt16BigEndian(header[2..], 1);
            BinaryPrimitives.WriteUInt32BigEndian(header[4..], 4);
            BinaryPrimitives.WriteUInt32BigEndian(header[8..], 20);
            image[12 + count * 12 + track * 4] = (byte)track;
        }

        var media = LightweightFloppyImage.Prepare(image, "extra-cylinders.adf");
        Assert.Equal(LightweightFloppyFormat.ExtendedAdf, media.Format);
        Assert.False(media.CanWrite);
        var drive = new LightweightFloppyDrive();
        drive.MountPreserved(media.Tracks!, media.Format);
        for (var cylinder = 0; cylinder < 82; cylinder++)
        {
            drive.WriteControlPins(0x71, cylinder * 20); // Head 1, inward, selected.
            drive.WriteControlPins(0x70, cylinder * 20 + 10);
        }
        Assert.Equal(82, drive.Cylinder);
        Assert.Equal(1, drive.Head);
        Assert.Equal(20, drive.TrackBitLength);
        Assert.Equal(new byte[] { 165, 0, 0 }, drive.Track.ToArray());
    }
}
