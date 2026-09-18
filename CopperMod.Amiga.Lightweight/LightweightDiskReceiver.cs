namespace CopperMod.Amiga.Lightweight;

// Causal integer separator following Keller US4780844A, tables I/II.
// A pulse is a transition, not an already recovered bit. Empty windows emit
// zero, including through no-flux spans. Index/seek never reset this receiver.
// Patent transfer law is a bounded model; see STORAGE.md for silicon limits.
internal struct LightweightDiskReceiver
{
    private int _phase, _rate, _history, _direction;
    private uint _phaseCorrections;
    private int _phaseAdd, _frequencyCorrections, _frequencyDirection;
    private bool _pulse, _fast;
    private long _cycle;
    internal int Rate => _rate;
    internal int Phase => _phase;
    internal bool Active { get; private set; }
    internal long NextCycle { get; private set; }

    internal void Reset() { this = default; NextCycle = long.MaxValue; }
    internal void Enable(long cycle, bool fast)
    {
        if (Active) return;
        Active = true; _cycle = cycle; _phase = 0; _rate = 146; _fast = fast;
        _history = _direction = 0; _phaseCorrections = 0; _frequencyCorrections = 0; _pulse = false;
        Schedule();
    }
    internal void SetFast(bool fast)
    {
        if (_fast == fast) return;
        _phase = fast ? _phase >> 1 : _phase << 1;
        _fast = fast;
        Schedule();
    }

    // The caller schedules NextCycle as a canonical CCK deadline; no returned
    // bit can precede the hardware clock or depend on a later pulse.
    internal int Advance(long cycle)
    {
        var result = -1;
        while (_cycle < cycle)
        {
            _cycle++;
            if (_frequencyCorrections > 0)
            {
                _rate = Math.Clamp(_rate + _frequencyDirection, 134, 159);
                _frequencyCorrections--;
            }
            _phase += (_phaseCorrections & 1) != 0 ? _phaseAdd : _rate;
            _phaseCorrections >>= 1;
            var modulus = _fast ? 2048 : 4096;
            if (_phase >= modulus)
            {
                _phase -= modulus;
                result = _pulse ? 1 : 0;
                _pulse = false;
            }
        }
        Schedule();
        return result;
    }

    internal void Pulse()
    {
        _pulse = true;
        var segment = _phase >> (_fast ? 8 : 9);
        var direction = segment < 4 ? 1 : -1;
        var corrections = segment < 4 ? 4 - segment : segment - 3;
        _phaseAdd = direction > 0 ? 258 : 34;
        var mask = (1u << corrections) - 1;
        _phaseCorrections = _fast ? mask : mask | (mask << 4);
        var same = _direction == direction ? _history : 0;
        _frequencyCorrections = same == 0 ? 0 : Math.Max(0, corrections - (same == 1 ? 1 : 0));
        _frequencyDirection = direction;
        _direction = direction;
        _history = Math.Min(2, same + 1);
        Schedule();
    }

    private void Schedule()
    {
        if (!Active) { NextCycle = long.MaxValue; return; }
        var phase = _phase; var rate = _rate; var corrections = _phaseCorrections;
        var frequency = _frequencyCorrections;
        var steps = 0;
        var modulus = _fast ? 2048 : 4096;
        if (corrections == 0 && frequency == 0)
            steps = (modulus - phase + rate - 1) / rate;
        else
            while (phase < modulus)
            {
                if (frequency-- > 0) rate = Math.Clamp(rate + _frequencyDirection, 134, 159);
                phase += (corrections & 1) != 0 ? _phaseAdd : rate;
                corrections >>= 1;
                steps++;
            }
        NextCycle = (_cycle + steps + 1) & ~1L;
    }
}
