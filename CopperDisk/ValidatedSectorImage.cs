namespace CopperDisk;

/// <summary>Outcome of opt-in preservation-track sector validation.</summary>
public enum SectorValidity
{
    /// <summary>No readable sector header.</summary>
    Missing,
    /// <summary>Sector checksum failed.</summary>
    Corrupt,
    /// <summary>Consistent data with valid checksums.</summary>
    Valid,
    /// <summary>Conflicting valid copies.</summary>
    Ambiguous,
    /// <summary>Sector depends on unstable data.</summary>
    Weak
}

/// <summary>Decoded sectors with explicit validity. Reads never substitute missing bytes.</summary>
public sealed class ValidatedSectorImage
{
    private readonly byte[] _bytes;
    private readonly SectorValidity[] _validity;
    internal ValidatedSectorImage(byte[] bytes, SectorValidity[] validity) => (_bytes, _validity) = (bytes, validity);
    /// <summary>Number of 512-byte sectors.</summary>
    public int SectorCount => _validity.Length;
    /// <summary>Validity of one sector.</summary>
    public SectorValidity GetValidity(int sector) => _validity[sector];
    /// <summary>Read a bounded byte range, rejecting every unvalidated sector it touches.</summary>
    public void Read(long offset, Span<byte> destination)
    {
        if (offset < 0 || offset > _bytes.LongLength - destination.Length) throw new ArgumentOutOfRangeException(nameof(offset));
        if (destination.Length == 0) return;
        for (var i = (int)(offset / 512); i <= (offset + destination.Length - 1) / 512; i++)
            if (_validity[i] != SectorValidity.Valid) throw new IOException($"Sector {i} is {_validity[i]}; no validated sector data is available.");
        _bytes.AsSpan((int)offset, destination.Length).CopyTo(destination);
    }
}

public static partial class AmigaDosTrackDecoder
{
    /// <summary>Strict opt-in decode for filesystem tools; the emulator's best-effort API is unchanged.</summary>
    public static ValidatedSectorImage DecodeValidated<T>(IReadOnlyList<T> tracks) where T : IAmigaTrack
    {
        ArgumentNullException.ThrowIfNull(tracks);
        if (tracks.Count is 0 or > AmigaDiskGeometry.MaximumPreservedTrackCount) throw new ArgumentException("Invalid track count.", nameof(tracks));
        var bytes = new byte[tracks.Count * 11 * 512]; var states = new SectorValidity[tracks.Count * 11];
        var candidate = new byte[11 * 512]; var found = new bool[11];
        for (var number = 0; number < tracks.Count; number++)
        {
            var track = ToEncodedTrack(tracks[number]);
            if (track.BitLength < EncodedSectorBitsAfterSync) continue;
            for (var bit = 0; bit < track.BitLength; bit++)
            {
                if (track.ReadUInt16AtBit(bit) != SyncWord || track.ReadUInt16AtBit(bit + 16) != SyncWord) continue;
                var header = DecodeOddEven(ReadMfmLong(track, bit + 32), ReadMfmLong(track, bit + 64));
                var sector = (int)((header >> 8) & 255);
                if ((header & 0xFF000000) != 0xFF000000 || ((header >> 16) & 255) != number || sector >= 11) continue;
                var index = number * 11 + sector;
                Array.Clear(found); var count = 0;
                TryDecodeSector(track, bit, number, candidate.AsSpan(), found.AsSpan(), ref count);
                if (count == 0) { if (states[index] == SectorValidity.Missing) states[index] = SectorValidity.Corrupt; continue; }
                const AmigaTrackFeatures weakFlags = AmigaTrackFeatures.ApproximateWeakData | AmigaTrackFeatures.WeakData;
                var weak = track.Regions.Any(r => (r.Features & weakFlags) != 0 && Overlaps(bit, EncodedSectorBitsAfterSync + 32, r.StartBit, r.BitLength, track.BitLength));
                // Without bounded weak regions a track-wide weak flag cannot certify any sector.
                weak |= track.Regions.Count == 0 && (track.Features & weakFlags) != 0;
                if (weak) { states[index] = SectorValidity.Weak; continue; }
                var data = candidate.AsSpan(sector * 512, 512); var prior = bytes.AsSpan(index * 512, 512);
                if (states[index] == SectorValidity.Valid && !data.SequenceEqual(prior)) states[index] = SectorValidity.Ambiguous;
                else if (states[index] is SectorValidity.Missing or SectorValidity.Corrupt) { data.CopyTo(prior); states[index] = SectorValidity.Valid; }
            }
        }
        return new(bytes, states);
    }

    private static bool Overlaps(int start, int length, int other, int otherLength, int revolution)
    {
        for (var shift = -1; shift <= 1; shift++)
        {
            var adjusted = other + shift * revolution;
            if ((long)start < (long)adjusted + otherLength && adjusted < (long)start + length) return true;
        }
        return false;
    }
}
