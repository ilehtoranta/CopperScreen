using System;
using System.Collections.Generic;

namespace CopperDisk;

/// <summary>
/// A single decoded IPF floppy track.
/// </summary>
public sealed class IpfTrack : IAmigaTrack
{
    internal IpfTrack(
        int cylinder,
        int head,
        int bitLength,
        int startBit,
        byte[] encodedData,
        AmigaTrackFeatures features,
        IReadOnlyList<AmigaTrackRegion>? regions = null,
        uint densityType = 2,
        ushort[]? cellWeights = null)
    {
        Cylinder = cylinder;
        Head = head;
        BitLength = bitLength;
        StartBit = startBit;
        EncodedData = encodedData;
        Features = features;
        DensityType = densityType;
        CellWeights = cellWeights ?? ReadOnlyMemory<ushort>.Empty;
        Regions = regions == null || regions.Count == 0
            ? Array.Empty<AmigaTrackRegion>()
            : Array.AsReadOnly(new List<AmigaTrackRegion>(regions).ToArray());
    }

    /// <summary>
    /// Gets the physical cylinder number.
    /// </summary>
    public int Cylinder { get; }

    /// <summary>
    /// Gets the physical head number.
    /// </summary>
    public int Head { get; }

    /// <summary>
    /// Gets the number of meaningful bits in <see cref="EncodedData"/>.
    /// </summary>
    public int BitLength { get; }

    /// <summary>
    /// Gets the decoded stream start bit.
    /// </summary>
    public int StartBit { get; }

    /// <summary>SPS density selector. 1 denotes unformatted media, 2 automatic
    /// cell size; other selectors require their corresponding density profile.</summary>
    public uint DensityType { get; }

    /// <summary>Index-oriented relative cell durations; empty means uniform.
    /// The nominal weight is 1000. Normalize the sum to one revolution.</summary>
    public ReadOnlyMemory<ushort> CellWeights { get; }

    /// <summary>
    /// Gets the decoded encoded-track bytes.
    /// </summary>
    /// <remarks>The returned memory is a read-only view over decoder-owned backing storage.</remarks>
    public ReadOnlyMemory<byte> EncodedData { get; }

    /// <summary>
    /// Gets feature flags exposed by the decoder for this track.
    /// </summary>
    public AmigaTrackFeatures Features { get; }

    /// <summary>
    /// Gets feature-marked bit ranges exposed by the decoder for this track.
    /// </summary>
    public IReadOnlyList<AmigaTrackRegion> Regions { get; }
}
