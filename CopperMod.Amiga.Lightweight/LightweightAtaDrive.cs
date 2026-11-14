using System.Buffers.Binary;
using CopperDisk;

namespace CopperMod.Amiga.Lightweight;

/// <summary>Read-only ATA-2 PIO master. Backing sectors use the canonical Amiga HDF byte order.</summary>
internal sealed class LightweightAtaDrive : IDisposable
{
    private readonly AmigaHardfile _media;
    private readonly LightweightStorageDrive _drive;
    private readonly ILightweightStorageObserver? _observer;
    private readonly byte[] _pio = new byte[16 * 512];
    private byte _error, _features, _count = 1, _sector = 1, _low, _high, _device = 0xA0;
    private byte _status = 0x50, _control, _command, _multiple = 1;
    private uint _nextLba;
    private int _remaining, _loadRemaining, _blockSectors, _pioLength, _pioAt;
    private bool _loading, _identify, _irq, _resetting;
    private long _phaseCycle = long.MaxValue;
    private int _heads, _sectors;
    internal bool InterruptAsserted => _irq && (_control & 2) == 0;
    internal long NextCycle => Math.Min(_phaseCycle, _drive.NextCycle);
    internal bool DataRegister(uint address) => (address & 0x1C) == 0 && (address & 0xF000) is 0 or 0x2000;

    internal LightweightAtaDrive(LightweightIdeDriveConfiguration configuration, ILightweightStorageObserver? observer)
    {
        if (!configuration.Media.ReadOnly || configuration.Media.Unit != 0)
            throw new ArgumentException("The initial Gayle ATA model requires a read-only master at unit zero.");
        _media = AmigaHardfile.Open(configuration.Media);
        try
        {
            if (_media.SectorCount is < 1 or > 0x0FFFFFFF) throw new ArgumentException("IDE media requires 512-byte sectors within LBA28.");
            _drive = new(_media, configuration.Timing, observer);
        }
        catch { _media.Dispose(); throw; }
        _observer = observer; _heads = configuration.Timing.Heads; _sectors = configuration.Timing.SectorsPerTrack;
    }

    internal void Reset()
    {
        _drive.Reset(); Signature(); _control = _command = 0; _multiple = 1;
        _heads = _drive.Profile.Heads; _sectors = _drive.Profile.SectorsPerTrack;
        _irq = _loading = _identify = _resetting = false;
        _phaseCycle = long.MaxValue; _pioLength = _pioAt = _remaining = 0;
    }

    private void Signature()
    { _error = 1; _count = _sector = 1; _low = _high = 0; _device = 0xA0; _status = 0x50; }

    internal byte ReadRegister(int register, bool alternate, bool strobe)
    {
        if ((_device & 0x10) != 0) return 0xFF;
        if (register == 7)
        {
            if (!alternate && strobe) _irq = false;
            return _status;
        }
        return register switch { 1 => _error, 2 => _count, 3 => _sector, 4 => _low, 5 => _high, 6 => _device, _ => (byte)0xFF };
    }

    internal ushort ReadData(long cycle, bool strobe)
    {
        if ((_device & 0x10) != 0 || (_status & 8) == 0 || _pioAt >= _pioLength) return 0xFFFF;
        var value = BinaryPrimitives.ReadUInt16BigEndian(_pio.AsSpan(_pioAt, 2));
        if (!strobe) return value;
        _observer?.PioRead(2, cycle); _pioAt += 2;
        if (_pioAt == _pioLength)
        {
            _pioAt = _pioLength = 0;
            if (_identify) { _identify = false; _status = 0x50; return value; }
            _remaining -= _blockSectors; _count = (byte)_remaining;
            SetPosition(_nextLba);
            if (_remaining == 0) { _status = 0x50; _observer?.Completed(true, cycle); }
            else { _status = 0x80; _phaseCycle = cycle + 1; }
        }
        return value;
    }

    internal void WriteRegister(int register, byte value, long cycle)
    {
        switch (register)
        {
            case 1: _features = value; break;
            case 2: _count = value; break;
            case 3: _sector = value; break;
            case 4: _low = value; break;
            case 5: _high = value; break;
            case 6: _device = value; break;
            case 7:
                if ((_device & 0x10) != 0) return;
                if ((_status & 0x80) != 0) return;
                _drive.CancelForeground(cycle); _loading = false; _pioAt = _pioLength = 0;
                _command = value; _error = 0; _irq = false; _status = 0x80;
                _remaining = 0; _identify = false;
                _phaseCycle = checked(cycle + Math.Max(1, _drive.CommandCycles));
                break;
        }
    }

    internal void WriteControl(byte value, long cycle)
    {
        var previous = _control; _control = value;
        if ((value & 4) != 0)
        {
            _drive.CancelForeground(cycle); _loading = false; _resetting = true;
            _status = 0x80; _irq = false; _phaseCycle = long.MaxValue; _pioAt = _pioLength = 0;
        }
        else if ((previous & 4) != 0) _phaseCycle = cycle + LightweightStorageDrive.Microseconds(5);
    }

    internal void Step(long cycle)
    {
        _drive.Step(cycle);
        if (_loading) Pump(cycle);
        if (_phaseCycle != cycle) return;
        _phaseCycle = long.MaxValue;
        if (_resetting) { Signature(); _resetting = false; return; }
        if (_remaining != 0) { BeginBlock(cycle); return; }
        switch (_command)
        {
            case 0xEC: Identify(); _identify = true; _status = 0x58; _irq = true; break;
            case 0x20: case 0x21: case 0xC4:
                if (!Position(out _nextLba)) { Error(0x10); break; }
                _remaining = _count == 0 ? 256 : _count;
                if ((ulong)_nextLba + (uint)_remaining > (ulong)_drive.SectorCount) { _remaining = 0; Error(0x10); break; }
                _observer?.ReadCommand(true, _nextLba, (uint)_remaining * 512, cycle);
                BeginBlock(cycle); break;
            case 0xC6:
                if (_count == 0 || _count > 16 || (_count & (_count - 1)) != 0) Error(4);
                else { _multiple = _count; Complete(); } break;
            case 0x91:
                if (_count == 0) Error(4);
                else { _heads = (_device & 15) + 1; _sectors = _count; Complete(); } break;
            case 0x90: Signature(); _irq = true; break;
            case 0xEF:
                if (_features is 0xAA or 0x55) { _drive.SetReadAhead(_features == 0xAA, cycle); Complete(); }
                else if (_features == 3 && (_count is 0 or 1 or 8)) Complete();
                else Error(4); break;
            case 0xE7: Complete(); break;
            case >= 0x10 and <= 0x1F: Complete(); break;
            case >= 0x70 and <= 0x7F: case 0x40: case 0x41:
                if (!Position(out var position) || position >= _drive.SectorCount) Error(0x10); else Complete(); break;
            default: Error(4); break;
        }
    }

    private void BeginBlock(long cycle)
    {
        _blockSectors = Math.Min(_remaining, _command == 0xC4 ? _multiple : 1);
        _loadRemaining = _blockSectors; _pioAt = _pioLength = 0; _loading = true;
        _drive.Request(_nextLba, cycle); Pump(cycle);
    }

    private void Pump(long cycle)
    {
        while (_loading && _drive.Ready)
        {
            if (_drive.Failed) { _loading = false; _remaining = 0; Error(0x40); return; }
            _drive.CopySector(_pio.AsSpan(_pioLength, 512)); _pioLength += 512; _nextLba++;
            if (--_loadRemaining == 0) { _loading = false; _status = 0x58; _irq = true; return; }
            _drive.Request(_nextLba, cycle);
        }
    }

    private void Error(byte error) { _error = error; _status = 0x51; _irq = true; }
    private void Complete() { _status = 0x50; _irq = true; }
    private bool Position(out uint lba)
    {
        if ((_device & 0x40) != 0) { lba = (uint)((_device & 15) << 24 | _high << 16 | _low << 8 | _sector); return true; }
        var cylinder = _high << 8 | _low; var head = _device & 15;
        lba = (uint)((cylinder * _heads + head) * _sectors + _sector - 1);
        return _sector != 0 && _sector <= _sectors && head < _heads;
    }
    private void SetPosition(uint lba)
    {
        if ((_device & 0x40) != 0)
        { _sector = (byte)lba; _low = (byte)(lba >> 8); _high = (byte)(lba >> 16); _device = (byte)((uint)(_device & 0xF0) | (lba >> 24 & 15)); }
        else
        {
            _sector = (byte)(lba % _sectors + 1); var track = lba / (uint)_sectors;
            _device = (byte)((uint)(_device & 0xF0) | track % (uint)_heads); var cylinder = track / (uint)_heads;
            _low = (byte)cylinder; _high = (byte)(cylinder >> 8);
        }
    }
    private void Identify()
    {
        _pio.AsSpan().Clear();
        // Gayle exposes the ATA byte lanes in memory order. IDENTIFY numeric
        // words are little-endian, unlike the Amiga filesystem's sector bytes.
        void Word(int index, uint value) => BinaryPrimitives.WriteUInt16LittleEndian(_pio.AsSpan(index * 2, 2), (ushort)value);
        void Text(int index, int words, string value)
        { for (var i = 0; i < words * 2; i++) _pio[index * 2 + (i ^ 1)] = i < value.Length ? (byte)value[i] : (byte)' '; }
        Word(0, 0x0040); Word(1, (uint)Math.Min(16383, _drive.SectorCount / (_heads * _sectors)));
        Word(3, (uint)_heads); Word(6, (uint)_sectors);
        Text(10, 10, "COPPER00000000000001"); Text(23, 4, "1.0"); Text(27, 20, "Copper synthetic read-only ATA");
        Word(47, 0x8010); Word(49, 0x0200); Word(51, 0); Word(53, 1);
        Word(54, (uint)Math.Min(16383, _drive.SectorCount / (_heads * _sectors)));
        Word(55, (uint)_heads); Word(56, (uint)_sectors);
        Word(57, (uint)_drive.SectorCount); Word(58, (uint)(_drive.SectorCount >> 16));
        Word(59, (uint)(0x100 | _multiple)); Word(60, (uint)_drive.SectorCount); Word(61, (uint)(_drive.SectorCount >> 16));
        Word(80, 2); Word(82, _drive.Profile.ReadAheadBytes == 0 ? 0u : 0x40u); Word(85, _drive.Profile.ReadAheadBytes == 0 ? 0u : 0x40u);
        _pioAt = 0; _pioLength = 512;
    }
    public void Dispose() => _media.Dispose();
}
