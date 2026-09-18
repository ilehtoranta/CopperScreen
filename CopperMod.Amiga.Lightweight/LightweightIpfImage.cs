using CopperDisk;

namespace CopperMod.Amiga.Lightweight;

/// <summary>Immutable preserved tracks prepared outside emulated execution.</summary>
public sealed class LightweightIpfImage
{
    internal byte[][] Tracks { get; }
    internal int[] Lengths { get; }
    internal byte[]?[] WeakMasks { get; }
    internal int[]?[] CellDeadlines { get; }
    internal uint Seed { get; }
    private LightweightIpfImage(byte[][] tracks, int[] lengths, byte[]?[] masks, int[]?[] deadlines, uint seed)
        => (Tracks, Lengths, WeakMasks, CellDeadlines, Seed) = (tracks, lengths, masks, deadlines, seed);

    /// <summary>Decode and validate IPF geometry without changing a mounted drive.</summary>
    public static LightweightIpfImage Prepare(ReadOnlySpan<byte> image)
    {
        var decoded = IpfDecoder.Decode(image, new IpfDecodeOptions { AlignTracksToWord = false, StartAtIndex = true });
        if (decoded.Tracks.Count == 0 || decoded.Tracks.Any(t => t.Cylinder is < 0 or > 83 || t.Head is < 0 or > 1))
            throw new NotSupportedException("IPF requires DD tracks within cylinders 0–83 and heads 0–1.");
        var cylinders = Math.Max(80, decoded.Tracks.Max(t => t.Cylinder) + 1);
        var tracks = new byte[cylinders * 2][];
        var lengths = new int[tracks.Length];
        var masks = new byte[]?[tracks.Length];
        var deadlines = new int[]?[tracks.Length];
        var seen = new bool[tracks.Length];
        for (var i = 0; i < tracks.Length; i++)
        {
            lengths[i] = 100_000;
            tracks[i] = new byte[12_500];

        }
        foreach (var track in decoded.Tracks)
        {
            var index = track.Cylinder * 2 + track.Head;
            if (seen[index]) throw new InvalidDataException("IPF contains duplicate physical tracks.");
            seen[index] = true;
            if (track.BitLength > LightweightPaulaAudio.PalCpuFrequency / 10)
                throw new NotSupportedException("IPF track cells must be at least one CCK apart.");
            tracks[index] = track.EncodedData.ToArray();
            lengths[index] = track.BitLength;
            if (!track.CellWeights.IsEmpty)
            {
                var weights = track.CellWeights.Span;
                long total = 0, elapsed = 0;
                foreach (var weight in weights) total += weight;
                var times = new int[weights.Length + 1];
                for (var bit = 0; bit < weights.Length; bit++)
                {
                    elapsed += weights[bit];
                    times[bit + 1] = (int)((elapsed * (LightweightPaulaAudio.PalCpuFrequency / 10) + total - 1) / total);
                    if (times[bit + 1] <= times[bit])
                        throw new NotSupportedException("IPF density requires cell timing finer than one CCK.");
                }
                deadlines[index] = times;
            }
            byte[]? mask = null;
            foreach (var region in track.Regions)
            {
                if ((region.Features & (AmigaTrackFeatures.WeakData)) == 0) continue;
                mask ??= new byte[tracks[index].Length];
                for (var bit = region.StartBit; bit < region.StartBit + region.BitLength; bit++)
                    mask[bit >> 3] |= (byte)(0x80 >> (bit & 7));
            }
            masks[index] = mask!;
        }
        uint seed = 2166136261;
        foreach (var value in image) seed = unchecked((seed ^ value) * 16777619);
        return new(tracks, lengths, masks, deadlines, seed);
    }
}
