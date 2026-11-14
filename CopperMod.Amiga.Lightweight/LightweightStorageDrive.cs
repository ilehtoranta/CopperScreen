using CopperDisk;

namespace CopperMod.Amiga.Lightweight;

/// <summary>One owner, one scheduled sector, and bounded read-ahead. All timestamps use machine cycles.</summary>
internal sealed class LightweightStorageDrive
{
    internal const long ClockHz = 7_093_790;
    private readonly AmigaHardfile _media;
    private readonly LightweightDriveTimingProfile _profile;
    private readonly ILightweightStorageObserver? _observer;
    private readonly byte[] _cache;
    private readonly ulong[] _keys;
    private readonly bool[] _consumed;
    private readonly byte[] _sector = new byte[512];
    private int _replacement;
    private ulong _scheduled, _prefetchNext, _streamFirst;
    private long _seekStart, _seekEnd;
    private long _seekFromCylinder, _seekToCylinder;
    private uint _head;
    private bool _foreground, _readAhead;
    internal long NextCycle { get; private set; } = long.MaxValue;
    internal bool Ready { get; private set; }
    internal bool Failed { get; private set; }
    internal ulong ReadySector { get; private set; }
    internal long SectorCount => _media.SectorCount;
    internal LightweightDriveTimingProfile Profile => _profile;
    internal long CommandCycles => Microseconds(_profile.CommandMicroseconds);
    internal long InterfaceCycles(int bytes) => Ratio((Int128)bytes * ClockHz, _profile.TransferBytesPerSecond);
    internal static long Microseconds(long value) => Ratio((Int128)value * ClockHz, 1_000_000);
    private static long Ratio(Int128 numerator, long denominator) => checked((long)((numerator + denominator - 1) / denominator));

    internal LightweightStorageDrive(AmigaHardfile media, LightweightDriveTimingProfile profile, ILightweightStorageObserver? observer)
    {
        profile.Validate();
        _media = media; _profile = profile; _observer = observer;
        _cache = new byte[profile.ReadAheadBytes]; _keys = new ulong[profile.ReadAheadBytes / 512];
        _consumed = new bool[_keys.Length];
        Reset();
    }

    internal void Reset()
    {
        Array.Fill(_keys, ulong.MaxValue); Array.Clear(_consumed);
        _replacement = 0;
        _seekStart = _seekEnd = _seekFromCylinder = _seekToCylinder = 0; _head = 0;
        _readAhead = _keys.Length != 0; Ready = Failed = false;
        _foreground = false; _prefetchNext = _streamFirst = ulong.MaxValue; NextCycle = long.MaxValue;
    }

    internal void SetReadAhead(bool enabled, long cycle)
    {
        _readAhead = enabled && _keys.Length != 0;
        if (!_readAhead && !_foreground) NextCycle = long.MaxValue;
        else ResumePrefetch(cycle);
    }

    internal void Request(ulong sector, long cycle)
    {
        Ready = Failed = false; ReadySector = sector;
        if (sector >= (ulong)SectorCount) { Failed = true; Ready = true; return; }
        for (var i = 0; i < _keys.Length; i++)
        {
            if (_keys[i] != sector) continue;
            if (sector < _streamFirst || sector >= _prefetchNext) RedirectStream(sector);
            _cache.AsSpan(i * 512, 512).CopyTo(_sector); _consumed[i] = true;
            Ready = true; ResumePrefetch(cycle); return;
        }
        if (NextCycle != long.MaxValue && !_foreground && _scheduled == sector)
        { _foreground = true; return; }
        // A miss redirects the speculative stream. Old cache entries remain
        // readable, but cannot pin space needed by the new read-ahead stream.
        // Unread foreground data lives in its separate protected sector buffer.
        RedirectStream(sector);
        _scheduled = sector; _foreground = true;
        NextCycle = MediaReadyCycle(sector, cycle);
        if (NextCycle == cycle) Step(cycle);
    }

    internal void CopySector(Span<byte> destination)
    {
        if (!Ready || Failed || destination.Length != 512) throw new InvalidOperationException("Sector is not ready.");
        _sector.CopyTo(destination); Ready = false;
    }

    internal void CancelForeground(long cycle)
    {
        Ready = Failed = false;
        if (_foreground) { _foreground = false; NextCycle = long.MaxValue; }
        ResumePrefetch(cycle);
    }

    internal void Step(long cycle)
    {
        if (cycle != NextCycle) return;
        var requested = _foreground; var sector = _scheduled;
        NextCycle = long.MaxValue; _foreground = false;
        var slot = CacheSlot(requested);
        if (!requested && slot < 0) return;
        var target = requested ? _sector.AsSpan() : _cache.AsSpan(slot * 512, 512);
        try { _media.Read(checked((long)sector * 512), target); _observer?.MediaRead(sector, cycle); }
        catch (IOException) { if (requested) { Failed = Ready = true; } return; }
        if (slot >= 0)
        {
            if (requested) _sector.CopyTo(_cache, slot * 512);
            _keys[slot] = sector; _consumed[slot] = requested;
            _replacement = (slot + 1) % _keys.Length;
        }
        if (requested) { ReadySector = sector; Ready = true; }
        _prefetchNext = sector + 1;
        ResumePrefetch(cycle);
    }

    private int CacheSlot(bool foreground)
    {
        for (var i = 0; i < _keys.Length; i++)
        {
            var slot = (_replacement + i) % _keys.Length;
            if (_keys[slot] == ulong.MaxValue || _consumed[slot]) return slot;
        }
        // Foreground has its own protected sector buffer; speculative entries
        // may be displaced, but pending command data is never overwritten.
        return foreground && _keys.Length != 0 ? _replacement : -1;
    }

    private void ResumePrefetch(long cycle)
    {
        if (!_readAhead || _foreground || NextCycle != long.MaxValue || _prefetchNext >= (ulong)SectorCount || CacheSlot(false) < 0) return;
        // A resumed stream can already contain several successors. Skip
        // those hits instead of pausing forever at its first retained sector.
        for (var skipped = 0; skipped < _keys.Length; skipped++)
        {
            var found = false;
            for (var i = 0; i < _keys.Length; i++)
                if (_keys[i] == _prefetchNext) { found = true; break; }
            if (!found) break;
            if (++_prefetchNext >= (ulong)SectorCount) return;
        }
        _scheduled = _prefetchNext;
        NextCycle = MediaReadyCycle(_scheduled, cycle);
    }

    private void RedirectStream(ulong sector)
    {
        _streamFirst = sector; _prefetchNext = sector + 1;
        if (!_foreground) NextCycle = long.MaxValue;
        // Cached successors of the resumed stream are still unread. Protect
        // them while allowing the abandoned stream's entries to be replaced.
        for (var i = 0; i < _keys.Length; i++)
            _consumed[i] = !(_keys[i] > sector && _keys[i] - sector < (ulong)_keys.Length);
    }

    private long MediaReadyCycle(ulong sector, long cycle)
    {
        // The CF preset specifies no mechanical/media delay. Its configured
        // transfer limit belongs to the abstract CopperHDF link; IDE instead
        // pays for actual PIO bus accesses. Charging it here would count the
        // same abstract transfer twice, especially for one-sector commands.
        if (_profile.RevolutionsPerMinute == 0) return cycle;
        var track = sector / (uint)_profile.SectorsPerTrack;
        var cylinder = track / (uint)_profile.Heads;
        var oldCylinder = cycle >= _seekEnd ? _seekToCylinder : _seekFromCylinder +
            (long)((Int128)(_seekToCylinder-_seekFromCylinder) * Math.Max(0,cycle-_seekStart) / Math.Max(1,_seekEnd-_seekStart));
        var delta = Math.Abs((long)cylinder - oldCylinder);
        var head = track % (uint)_profile.Heads;
        var seek = delta != 0 ? Microseconds(checked(_profile.SeekBaseMicroseconds + delta * _profile.SeekPerCylinderMicroseconds)) :
            head != _head ? Microseconds(_profile.HeadSwitchMicroseconds) : 0;
        _seekFromCylinder=oldCylinder; _seekToCylinder=(long)cylinder; _seekStart=cycle; _seekEnd=checked(cycle+seek); _head=(uint)head;
        cycle=_seekEnd;
        var denominator = checked((long)_profile.RevolutionsPerMinute * _profile.SectorsPerTrack);
        var numerator = 60 * ClockHz;
        var slot = checked((long)((Int128)cycle * denominator / numerator));
        // A sector edge is quantized upward to a machine cycle. Arriving on
        // that same quantized edge must not miss the next sector by a turn.
        if (Ratio((Int128)slot * numerator, denominator) < cycle) slot++;
        var target = (sector % (uint)_profile.SectorsPerTrack + track % (uint)_profile.SectorsPerTrack *
            ((uint)_profile.TrackSkewSectors % (uint)_profile.SectorsPerTrack)) % (uint)_profile.SectorsPerTrack;
        slot += ((long)target - slot % _profile.SectorsPerTrack + _profile.SectorsPerTrack) % _profile.SectorsPerTrack;
        return Ratio((Int128)(slot + 1) * numerator, denominator);
    }
}
