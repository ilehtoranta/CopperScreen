using System.Buffers.Binary;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

public sealed class ExtendedAdfCylinderTests
{
    [Theory]
    [InlineData(82)]
    [InlineData(83)]
    public void PreparedExtendedAdfRetainsWritableOuterTracks(int lastCylinder)
    {
        var count = (lastCylinder + 1) * 2;
        var image = new byte[12 + count * 12 + count * 4];
        "UAE-1ADF"u8.CopyTo(image);
        BinaryPrimitives.WriteUInt16BigEndian(image.AsSpan(10), (ushort)count);
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
        Assert.True(media.CanWrite);
        var drive = new LightweightFloppyDrive();
        drive.MountPreserved(media.Tracks!, media.Format);
        for (var cylinder = 0; cylinder < lastCylinder; cylinder++)
        {
            drive.WriteControlPins(0x71, cylinder * 20); // Head 1, inward, selected.
            drive.WriteControlPins(0x70, cylinder * 20 + 10);
        }
        Assert.Equal(lastCylinder, drive.Cylinder);
        Assert.Equal(1, drive.Head);
        Assert.Equal(20, drive.TrackBitLength);
        Assert.Equal(new byte[] { (byte)(count - 1), 0, 0 }, drive.Track.ToArray());
        Assert.True(drive.WriteProtected);
        drive.WriteProtected = false;
        drive.UpdateRotation(drive.ReadyCycle, false);
        drive.WriteBit(0, drive.ReadyCycle, false);
        Assert.True(drive.IsDirty);
        var reopened = LightweightFloppyImage.Prepare(drive.ExportAdf(allowExtended: true), "reopened.adf");
        Assert.Equal(20, reopened.Tracks!.Lengths[^1]);
        Assert.True(drive.Track.SequenceEqual(reopened.Tracks.Tracks[^1]));
    }
}
