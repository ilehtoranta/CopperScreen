using System;
using System.Collections.Generic;

namespace CopperDisk;

internal sealed class TrackBackedDiskMedia : IAmigaSectorDiskMedia, IAmigaPreservedTrackDiskMedia
{
    private readonly AmigaEncodedTrack[] _encodedTracks;

    public TrackBackedDiskMedia(IReadOnlyList<IAmigaTrack> encodedTracks)
    {
        _encodedTracks = BuildTrackSet(encodedTracks);
        DataBytes = AmigaDosTrackDecoder.DecodeBestEffort(_encodedTracks, out var hasCompleteDecodedSectorData);
        HasCompleteDecodedSectorData = hasCompleteDecodedSectorData;
    }

    public TrackBackedDiskMedia(IReadOnlyList<AmigaEncodedTrack> encodedTracks)
    {
        _encodedTracks = BuildTrackSet(encodedTracks);
        DataBytes = AmigaDosTrackDecoder.DecodeBestEffort(_encodedTracks, out var hasCompleteDecodedSectorData);
        HasCompleteDecodedSectorData = hasCompleteDecodedSectorData;
    }

    public int Cylinders => _encodedTracks.Length / AmigaDiskGeometry.HeadCount;

    public int Heads => AmigaDiskGeometry.HeadCount;

    public bool HasCompleteDecodedSectorData { get; }

    public byte[] DataBytes { get; }

    public ReadOnlyMemory<byte> SectorData => DataBytes;

    public ReadOnlyMemory<byte> BootBlock => DataBytes.AsMemory(0, 1024);

    public IAmigaTrack ReadTrack(int cylinder, int head)
    {
        var index = GetTrackIndex(cylinder, head);
        return _encodedTracks[index];
    }

    public ReadOnlyMemory<byte> ReadSector(int cylinder, int head, int sector)
    {
        var track = GetTrackIndex(cylinder, head);
        if ((uint)sector >= AmigaDiskGeometry.SectorsPerTrack)
            throw new ArgumentOutOfRangeException(nameof(sector));
        return ReadSector(track * AmigaDiskGeometry.SectorsPerTrack + sector);
    }

    public ReadOnlyMemory<byte> ReadSector(int logicalSector)
    {
        var offset = checked(logicalSector * AmigaDiskGeometry.SectorSize);
        if (logicalSector < 0 || offset + AmigaDiskGeometry.SectorSize > DataBytes.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(logicalSector));
        }

        return DataBytes.AsMemory(offset, AmigaDiskGeometry.SectorSize);
    }

    public ReadOnlyMemory<byte> ReadBytes(int byteOffset, int byteCount)
    {
        if (byteOffset < 0 || byteCount < 0 || byteOffset + byteCount > DataBytes.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(byteOffset), "Requested disk byte range is outside the decoded sector view.");
        }

        return DataBytes.AsMemory(byteOffset, byteCount);
    }

    private static AmigaEncodedTrack[] BuildTrackSet<T>(IReadOnlyList<T> encodedTracks)
        where T : IAmigaTrack
    {
        ArgumentNullException.ThrowIfNull(encodedTracks);
        if (encodedTracks.Count is 0 or > AmigaDiskGeometry.MaximumPreservedTrackCount)
        {
            throw new ArgumentException($"Between 1 and {AmigaDiskGeometry.MaximumPreservedTrackCount} encoded tracks are required.", nameof(encodedTracks));
        }

        var tracks = new AmigaEncodedTrack[(encodedTracks.Count + 1) / 2 * 2];
        for (var index = 0; index < encodedTracks.Count; index++)
        {
            var track = encodedTracks[index];
            tracks[index] = new AmigaEncodedTrack(
                track.EncodedData,
                track.BitLength,
                track.StartBit,
                track.Features | AmigaTrackFeatures.PreservedTrackData,
                track.Regions);
        }

        if (tracks.Length != encodedTracks.Count)
            tracks[^1] = AmigaEncodedTrack.FromBytes(AmigaDosTrackEncoder.CreateUnformattedTrack());

        return tracks;
    }

    private int GetTrackIndex(int cylinder, int head)
    {
        if ((uint)cylinder >= Cylinders) throw new ArgumentOutOfRangeException(nameof(cylinder));
        if ((uint)head >= Heads) throw new ArgumentOutOfRangeException(nameof(head));
        return cylinder * Heads + head;
    }
}
