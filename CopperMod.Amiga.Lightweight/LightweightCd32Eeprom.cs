namespace CopperMod.Amiga.Lightweight;

// AT24C08. SDA is open drain. Page writes start on STOP and wrap within the
// addressed 16-byte page. Sequential reads wrap across the whole device.
internal sealed class LightweightCd32Eeprom(int clockFrequency)
{
    internal const int Capacity = 1024;
    private const int Scl = 0x80;
    private const int Sda = 0x40;
    private const int PageSize = 16;
    private readonly byte[] _contents = new byte[Capacity];
    private readonly byte[] _page = new byte[PageSize];
    private int _pendingBytes;
    private int _address;
    private long _readyCycle;
    private byte _direction;
    private byte _latch;
    private bool _scl;
    private bool _hostSda;
    private bool _pullSdaLow;
    private int _bits;
    private byte _received;
    private bool _ack;
    private Phase _phase;
    private Phase _afterAck;

    private enum Phase { Idle, Control, Address, Write, AckSetup, Ack, Read, ReadAckSetup, ReadAck, ReadContinue }

    internal ReadOnlyMemory<byte> Contents => _contents;
    internal long NextCycle => _pendingBytes != 0 && _readyCycle != 0 ? _readyCycle : long.MaxValue;
    internal bool Dirty { get; private set; }

    internal void Reset(bool cold)
    {
        // A motherboard reset releases the pins. An already-started write
        // powers through a guest RESET instruction.
        if (cold)
        {
            if (_readyCycle != 0) CommitWrite(_readyCycle);
            _pendingBytes = 0;
            _address = 0;
        }
        else if (_readyCycle == 0)
            _pendingBytes = 0;
        _direction = 0;
        _latch = Scl | Sda;
        _scl = _hostSda = true;
        _pullSdaLow = false;
        _phase = Phase.Idle;
        _bits = 0;
    }

    internal void Load(ReadOnlySpan<byte> data)
    {
        if (data.Length != Capacity)
            throw new ArgumentException("CD32 NVRAM must contain exactly 1 KiB.", nameof(data));
        _pendingBytes = 0;
        _readyCycle = 0;
        data.CopyTo(_contents);
        Reset(cold: true);
        Dirty = false;
    }

    internal void MarkSaved() => Dirty = false;

    internal byte ReadPins() => (byte)((_scl ? Scl : 0) | (_hostSda && !_pullSdaLow ? Sda : 0));
    internal byte ReadDirection() => _direction;

    internal void WritePins(byte value, long cycle)
    {
        _latch = value;
        UpdateLines(cycle);
    }

    internal void WriteDirection(byte value, long cycle)
    {
        WritePinsAndDirection(_latch, value, cycle);
    }

    internal void WritePinsAndDirection(byte pins, byte direction, long cycle)
    {
        _direction = (byte)(direction & (Scl | Sda));
        _latch = pins;
        UpdateLines(cycle);
    }

    internal void CommitWrite(long cycle)
    {
        if (_readyCycle == 0 || cycle < _readyCycle) return;
        var pageAddress = _address & ~(PageSize - 1);
        for (var i = 0; i < PageSize; i++)
        {
            if ((_pendingBytes & (1 << i)) == 0) continue;
            Dirty |= _contents[pageAddress + i] != _page[i];
            _contents[pageAddress + i] = _page[i];
        }
        _pendingBytes = 0;
        _readyCycle = 0;
    }

    private void UpdateLines(long cycle)
    {
        CommitWrite(cycle);
        var released = _latch | ~_direction;
        var scl = (released & Scl) != 0;
        var sda = (released & Sda) != 0;
        if (_scl && scl && sda != _hostSda && !_pullSdaLow)
        {
            if (sda)
            {
                if (_pendingBytes != 0 && _readyCycle == 0)
                    _readyCycle = cycle + clockFrequency / 200; // 5 ms write cycle
                _phase = Phase.Idle;
            }
            else
            {
                if (_readyCycle == 0) _pendingBytes = 0;
                BeginReceive(Phase.Control);
            }
        }
        else if (scl && !_scl)
            Rise(sda, cycle);
        else if (!scl && _scl)
            Fall();
        _scl = scl;
        _hostSda = sda;
    }

    private void Rise(bool sda, long cycle)
    {
        switch (_phase)
        {
            case Phase.Control:
            case Phase.Address:
            case Phase.Write:
                _received = (byte)((_received << 1) | (sda ? 1 : 0));
                if (++_bits == 8) FinishReceive(cycle);
                break;
            case Phase.Read:
                if (++_bits == 8)
                {
                    _address = (_address + 1) & (Capacity - 1);
                    _phase = Phase.ReadAckSetup;
                }
                break;
            case Phase.ReadAck:
                _phase = sda ? Phase.Idle : Phase.ReadContinue;
                break;
        }
    }

    private void Fall()
    {
        switch (_phase)
        {
            case Phase.AckSetup:
                _pullSdaLow = _ack;
                _phase = Phase.Ack;
                break;
            case Phase.Ack:
                BeginReceive(_afterAck);
                if (_phase == Phase.Read) DriveReadBit();
                break;
            case Phase.Read:
                DriveReadBit();
                break;
            case Phase.ReadAckSetup:
                _pullSdaLow = false;
                _phase = Phase.ReadAck;
                break;
            case Phase.ReadContinue:
                BeginReceive(Phase.Read);
                DriveReadBit();
                break;
        }
    }

    private void FinishReceive(long cycle)
    {
        _ack = true;
        switch (_phase)
        {
            case Phase.Control:
                _ack = (_received & 0xF8) == 0xA0 && cycle >= _readyCycle;
                if (_ack)
                    _address = ((_received & 6) << 7) | (_address & 0xFF);
                _afterAck = !_ack ? Phase.Idle : (_received & 1) != 0 ? Phase.Read : Phase.Address;
                break;
            case Phase.Address:
                _address = (_address & 0x300) | _received;
                _afterAck = Phase.Write;
                break;
            case Phase.Write:
                var pageOffset = _address & (PageSize - 1);
                _page[pageOffset] = _received;
                _pendingBytes |= 1 << pageOffset;
                _address = (_address & ~(PageSize - 1)) | ((pageOffset + 1) & (PageSize - 1));
                _afterAck = Phase.Write;
                break;
        }
        _phase = Phase.AckSetup;
    }

    private void BeginReceive(Phase phase)
    {
        _phase = phase;
        _bits = 0;
        _received = 0;
        _pullSdaLow = false;
    }

    private void DriveReadBit() => _pullSdaLow = (_contents[_address] & (0x80 >> _bits)) == 0;
}
