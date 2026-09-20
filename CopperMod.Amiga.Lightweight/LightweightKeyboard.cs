using System.Runtime.CompilerServices;

namespace CopperMod.Amiga.Lightweight;

// Bounded digital keyboard protocol; no MCU instruction or matrix-scan loop.
internal struct LightweightKeyboard
{
    private const int PhaseCycles = 142; // ~20 us at PAL CPU clock.
    internal const int TimeoutCycles = 1_014_412; // ceil(143 ms), aligned to CCK.
    [InlineArray(10)] private struct KeyQueue { private byte _first; }
    private KeyQueue _queue;
    private int _head, _count, _bits;
    private byte _wireByte, _currentRaw, _retryRaw, _startupStage;
    private byte _phase; // 0 idle, 1 data, 2 clock low, 3 clock high, 4 handshake.
    private long _hostLowSince;
    private bool _hostHigh, _sync, _powerUp, _retryPending, _overflow;
    private ulong _heldLow, _heldHigh, _startupLow, _startupHigh;
    private ulong _physicalLow, _physicalHigh;
    internal bool CapsLockOn { get; private set; }
    internal bool ResetChord => (_physicalHigh & ((1UL << 35) | (1UL << 38) | (1UL << 39))) ==
        ((1UL << 35) | (1UL << 38) | (1UL << 39)); // CTRL, left/right Amiga.
    internal long NextCycle { get; private set; }
    internal bool WaitingForHandshake => _phase == 4;
    internal bool ClockHigh { get; private set; }
    internal bool DataHigh { get; private set; }

    internal void Reset(bool coldStart = false, long cycle = 0)
    {
        this = default;
        NextCycle = long.MaxValue;
        _hostLowSince = -1;
        _hostHigh = ClockHigh = DataHigh = true;
        if (coldStart)
        {
            _powerUp = true;
            StartSync(cycle);
        }
    }

    // Physical transitions suppress host repeat and implement the keyboard's
    // Caps Lock latch. Raw Enqueue remains available for deterministic scripts.
    internal void SetKeyState(byte key, bool down, long cycle)
    {
        ref var held = ref (key < 64 ? ref _physicalLow : ref _physicalHigh);
        var bit = 1UL << (key & 63);
        if (((held & bit) != 0) == down) return;
        if (down) held |= bit;
        else held &= ~bit;
        if (key == 0x62)
        {
            if (!down) return;
            CapsLockOn = !CapsLockOn;
            down = CapsLockOn;
        }
        if (!Enqueue((byte)(key | (down ? 0 : 0x80)), cycle)) _overflow = true;
    }

    internal bool Enqueue(byte raw, long cycle)
    {
        var key = raw & 0x7F;
        if (key < 0x78)
        {
            ref var held = ref (key < 64 ? ref _heldLow : ref _heldHigh);
            var bit = 1UL << (key & 63);
            if ((raw & 0x80) == 0) held |= bit;
            else held &= ~bit;
        }
        // Startup reports current held keys, not stale pre-synchronization events.
        if (_powerUp) return true;
        if (_count == 10) return false;
        _queue[(_head + _count) % 10] = raw;
        _count++;
        TryStart(cycle);
        return true;
    }

    private void StartByte(byte raw, long cycle)
    {
        _currentRaw = raw;
        _wireByte = (byte)~((raw << 1) | (raw >> 7));
        _bits = 0;
        _phase = 1;
        NextCycle = (cycle + 2) & ~1L;
    }

    private void StartSync(long cycle)
    {
        _sync = true;
        _bits = 0;
        _wireByte = 0; // Logical one: active-low KDAT.
        _phase = 1;
        NextCycle = (cycle + 2) & ~1L;
    }

    private void TryStart(long cycle)
    {
        if (_phase != 0 || !_hostHigh) return;
        if (_retryPending)
        {
            _retryPending = false;
            StartByte(_retryRaw, cycle);
            return;
        }
        if (_startupStage == 1)
        {
            _startupStage = 2;
            StartByte(0xFD, cycle);
            return;
        }
        if (_startupStage == 2)
        {
            if (_startupLow != 0 || _startupHigh != 0)
            {
                var low = _startupLow != 0;
                var key = System.Numerics.BitOperations.TrailingZeroCount(low ? _startupLow : _startupHigh);
                if (low) _startupLow &= _startupLow - 1;
                else _startupHigh &= _startupHigh - 1;
                StartByte((byte)(key + (low ? 0 : 64)), cycle);
                return;
            }
            _startupStage = 0;
            StartByte(0xFE, cycle);
            return;
        }
        if (_count != 0)
        {
            var raw = _queue[_head];
            _head = (_head + 1) % 10;
            _count--;
            StartByte(raw, cycle);
        }
        else if (_overflow)
        {
            _overflow = false;
            StartByte(0xFA, cycle);
        }
    }

    internal void HostDataChanged(bool high, long cycle)
    {
        if (high == _hostHigh) return;
        _hostHigh = high;
        if (!high) _hostLowSince = cycle;
        else
        {
            // >=1 us is accepted. Software should allow >=85 us on all models.
            if (_phase == 4 && _hostLowSince >= 0 && cycle - _hostLowSince >= 8)
            {
                _phase = 0;
                NextCycle = long.MaxValue;
                if (_sync)
                {
                    _sync = false;
                    if (_powerUp)
                    {
                        _powerUp = false;
                        _startupLow = _heldLow;
                        _startupHigh = _heldHigh;
                        _startupStage = 1;
                    }
                    else StartByte(0xF9, cycle);
                }
            }
            _hostLowSince = -1;
            TryStart(cycle);
        }
    }

    internal void Step(long cycle, LightweightCia cia, LightweightA500Machine machine)
    {
        if (_phase == 4)
        {
            if (!_sync)
            {
                // A lost F9 must not overwrite the original unacknowledged byte.
                if (!_retryPending) _retryRaw = _currentRaw;
                _retryPending = true;
            }
            StartSync(cycle);
            return;
        }
        switch (_phase)
        {
            case 1:
                DataHigh = (_wireByte & 0x80) != 0;
                _phase = 2;
                break;
            case 2:
                ClockHigh = false;
                _phase = 3;
                break;
            case 3:
                ClockHigh = true;
                _wireByte <<= 1;
                if (++_bits == (_sync ? 1 : 8))
                {
                    machine.SetKeyboardPins(DataHigh, ClockHigh, cycle);
                    DataHigh = true;
                    _phase = 4;
                    _hostLowSince = _hostHigh ? -1 : cycle;
                    NextCycle = cycle + TimeoutCycles;
                    machine.SetKeyboardPins(DataHigh, ClockHigh, cycle);
                    return;
                }
                _phase = 1;
                break;
        }
        machine.SetKeyboardPins(DataHigh, ClockHigh, cycle);
        NextCycle = cycle + PhaseCycles;
    }
}
