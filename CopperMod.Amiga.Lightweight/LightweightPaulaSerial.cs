namespace CopperMod.Amiga.Lightweight;

// Paula UART on the canonical CCK timeline. External pins are supplied by the
// execution owner; there are no background callbacks, queues or host I/O here.
internal sealed class LightweightPaulaSerial
{
    private ushort _period;
    private ushort _holding;
    private ushort _transmit;
    private ushort _receive;
    private ushort _received;
    private bool _holdingFull;
    private bool _transmitting;
    private bool _receiving;
    private bool _overrun;
    private bool _break;
    private bool _tx = true;
    private bool _rx = true;
    private int _receiveBit;
    private int _receiveBits;
    private long _nextTransmit = long.MaxValue;
    private long _nextReceive = long.MaxValue;

    internal long NextCycle => Math.Min(_nextTransmit, _nextReceive);
    internal bool TransmitHigh => _tx && !_break;
    private long BitCycles => 2L * ((_period & 0x7FFF) + 1);

    internal void Reset()
    {
        _period = _holding = _transmit = _receive = _received = 0;
        _holdingFull = _transmitting = _receiving = _overrun = _break = false;
        _tx = _rx = true;
        _receiveBit = _receiveBits = 0;
        _nextTransmit = _nextReceive = long.MaxValue;
    }

    internal void WritePeriod(ushort value) => _period = value;
    internal void WriteBreak(bool enabled) => _break = enabled;

    internal void WriteData(ushort value, long cycle)
    {
        _holding = value;
        _holdingFull = value != 0;
        if (!_transmitting)
            _nextTransmit = _holdingFull ? (cycle + 2) & ~1L : long.MaxValue;
    }

    internal ushort ReadData(ushort interruptRequests)
        => (ushort)(_received | (_overrun ? 0x8000 : 0) |
            ((interruptRequests & 0x0800) != 0 ? 0x4000 : 0) |
            (!_holdingFull ? 0x2000 : 0) | (!_transmitting ? 0x1000 : 0) |
            (_rx ? 0x0800 : 0));

    internal void AcknowledgeReceive(ushort interruptWrite)
    {
        if ((interruptWrite & 0x8800) == 0x0800) _overrun = false;
    }

    internal void SetReceivePin(bool high, long cycle)
    {
        var falling = _rx && !high;
        _rx = high;
        if (falling && !_receiving) BeginReceive(cycle);
    }

    private void BeginReceive(long cycle)
    {
        _receiving = true;
        _receive = 0;
        _receiveBit = -1;
        _receiveBits = (_period & 0x8000) != 0 ? 9 : 8;
        // Qualify/sample on the CCK grid, rounding a half-bit to the next CCK.
        _nextReceive = ((cycle + 1) & ~1L) + Math.Max(2, (BitCycles / 2 + 1) & ~1L);
    }

    internal void Step(long cycle, LightweightA500Machine machine)
    {
        if (cycle == _nextTransmit)
        {
            if (!_transmitting || _transmit == 0)
            {
                _transmitting = false;
                _tx = true;
                _nextTransmit = long.MaxValue;
                if (_holdingFull)
                {
                    _transmit = _holding;
                    _holdingFull = false;
                    _transmitting = true;
                    _tx = false; // Automatic start bit.
                    _nextTransmit = cycle + BitCycles;
                    machine.LatchSerialInterrupt(0x0001, cycle);
                }
            }
            else
            {
                _tx = (_transmit & 1) != 0;
                _transmit >>= 1;
                // The final set bit is a stop bit and occupies its whole period.
                _nextTransmit = cycle + BitCycles;
            }
        }
        if (cycle == _nextReceive)
        {
            if (_receiveBit == -1)
            {
                if (_rx)
                {
                    _receiving = false; // Rejected short start pulse.
                    _nextReceive = long.MaxValue;
                    return;
                }
                _receiveBit = 0;
            }
            else
            {
                if (_rx) _receive |= (ushort)(1 << _receiveBit);
                if (_receiveBit++ == _receiveBits)
                {
                    _overrun |= (machine.Intreq & 0x0800) != 0;
                    _received = _receive;
                    _receiving = false;
                    _nextReceive = long.MaxValue;
                    machine.LatchSerialInterrupt(0x0800, cycle);
                    if (!_rx) BeginReceive(cycle);
                    return;
                }
            }
            _nextReceive = cycle + BitCycles;
        }
    }
}
