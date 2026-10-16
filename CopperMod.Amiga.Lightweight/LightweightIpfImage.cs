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

    // Shared immutable track storage for additional CopperDisk formats. IPF's
    // established density/orientation preparation above is deliberately retained.
    internal static LightweightIpfImage PrepareTracks(
        IReadOnlyList<(int Cylinder, int Head, IAmigaTrack Track)> source, ReadOnlySpan<byte> image)
    {
        if (source.Count == 0 || source.Any(t => t.Cylinder is < 0 or > 83 || t.Head is < 0 or > 1))
            throw new NotSupportedException("Preserved media requires DD cylinders 0–83 and heads 0–1.");
        var count = Math.Max(80, source.Max(t => t.Cylinder) + 1) * 2;
        var tracks = new byte[count][];
        var lengths = new int[count];
        var masks = new byte[]?[count];
        var deadlines = new int[]?[count];
        var seen = new bool[count];
        for (var i = 0; i < count; i++) { tracks[i] = new byte[12_500]; lengths[i] = 100_000; }
        foreach (var entry in source)
        {
            var index = entry.Cylinder * 2 + entry.Head;
            var track = entry.Track;
            if (seen[index]) throw new InvalidDataException("Media contains duplicate physical tracks.");
            seen[index] = true;
            var bits = track.BitLength;
            if (bits <= 0 || bits > LightweightPaulaAudio.PalCpuFrequency / 10 ||
                (long)bits > (long)track.EncodedData.Length * 8 || track.StartBit < 0 || track.StartBit >= bits)
                throw new NotSupportedException("Preserved track length/index is invalid or its cells are finer than one CCK.");
            var data = new byte[(bits + 7) / 8];
            var input = track.EncodedData.Span;
            if (track.StartBit == 0) input[..data.Length].CopyTo(data);
            else
                for (var bit = 0; bit < bits; bit++)
                {
                    var original = (bit + track.StartBit) % bits;
                    if ((input[original >> 3] & (0x80 >> (original & 7))) != 0)
                        data[bit >> 3] |= (byte)(0x80 >> (bit & 7));
                }
            foreach (var region in track.Regions)
            {
                if (region.StartBit < 0 || region.BitLength < 0 || (long)region.StartBit + region.BitLength > bits)
                    throw new InvalidDataException("Media contains an out-of-range track region.");
                if ((region.Features & AmigaTrackFeatures.WeakData) == 0) continue;
                for (var bit = region.StartBit; bit < region.StartBit + region.BitLength; bit++)
                {
                    var rotated = (bit - track.StartBit + bits) % bits;
                    var mask = (byte)(0x80 >> (rotated & 7));
                    masks[index] ??= new byte[data.Length];
                    masks[index]![rotated >> 3] |= mask;
                }
            }
            tracks[index] = data; lengths[index] = bits;
        }
        uint seed = 2166136261;
        foreach (var value in image) seed = unchecked((seed ^ value) * 16777619);
        return new(tracks, lengths, masks, deadlines, seed);
    }
}
