namespace CopperMod.Amiga.Lightweight;

// Ideal charge-time input, in horizontal scan periods. External RC tolerances
// and sub-line comparator phases are outside this digital interface model.
internal sealed class LightweightPotentiometers
{
    private readonly int[] _thresholds = new int[4];
    private readonly int[] _counts = new int[4];
    private byte _connected;
    private bool _started;
    private long _lastLine, _releaseLine;
    private ushort _pins = 0x5500;

    internal void Reset()
    {
        Array.Clear(_counts);
        _started = false;
        _lastLine = _releaseLine = 0;
        _pins = 0x5500;
    }

    internal void SetPosition(int port, byte x, byte y, long cycle, ushort pins)
    {
        Advance(cycle, pins);
        _connected |= (byte)(3 << (port * 2));
        _thresholds[port * 2] = x;
        _thresholds[port * 2 + 1] = y;
    }

    internal void WriteControl(ushort value, long cycle, ushort previousPins)
    {
        Advance(cycle, previousPins);
        if ((value & 1) == 0) return;
        Array.Clear(_counts);
        _started = true;
        _lastLine = _releaseLine = cycle / LightweightClock.CpuCyclesPerLine + 8;
        _pins = 0;
    }

    internal ushort ReadCounters(int port, long cycle, ushort pins)
    {
        Advance(cycle, pins);
        return (ushort)((_counts[port * 2] & 255) | ((_counts[port * 2 + 1] & 255) << 8));
    }

    internal ushort ReadPins(long cycle, ushort digitalPins, ushort control)
    {
        Advance(cycle, digitalPins);
        if (!_started) return digitalPins;
        var outputs = (control >> 1) & 0x5500;
        return (ushort)(digitalPins & ((_pins & ~outputs) | (control & outputs)));
    }

    internal void Advance(long cycle, ushort pins)
    {
        if (!_started) return;
        var line = cycle / LightweightClock.CpuCyclesPerLine;
        if (line < _releaseLine) return;
        var elapsed = (int)Math.Min(int.MaxValue, Math.Max(0, line - _lastLine));
        for (var channel = 0; channel < 4; channel++)
        {
            var mask = 1 << (8 + channel * 2);
            if ((_pins & mask) != 0) continue;
            if ((pins & mask) == 0)
                _counts[channel] = (_counts[channel] + elapsed) & 255;
            else if ((_connected & (1 << channel)) != 0)
            {
                _counts[channel] = Math.Min(_thresholds[channel], _counts[channel] + elapsed);
                if (_counts[channel] == _thresholds[channel]) _pins |= (ushort)mask;
            }
            else
                _pins |= (ushort)mask;
        }
        _lastLine = line;
    }
}
