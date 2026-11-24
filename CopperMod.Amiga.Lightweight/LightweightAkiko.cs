namespace CopperMod.Amiga.Lightweight;

internal sealed class LightweightAkiko(int clockFrequency) : IDisposable
{
    internal const uint Base = 0x00B8_0000;
    private readonly LightweightCd32Eeprom _eeprom = new(clockFrequency);
    private readonly LightweightAkikoCd _cd = new(clockFrequency);
    private readonly uint[] _chunky = new uint[8];
    private readonly uint[] _planes = new uint[8];
    private int _writeIndex;
    private int _readIndex;
    private bool _needsConversion;

    internal LightweightCd32Eeprom Eeprom => _eeprom;
    internal LightweightAkikoCd Cd => _cd;
    internal long NextCycle => Math.Min(_eeprom.NextCycle, _cd.NextCycle);

    internal static bool ContainsAddress(uint address) => address - Base < 0x10000;

    internal void Reset(long cycle, bool cold)
    {
        _eeprom.Reset(cold);
        _cd.Reset(cycle, cold);
        Array.Clear(_chunky);
        Array.Clear(_planes);
        _writeIndex = _readIndex = 0;
        _needsConversion = false;
    }

    // Each data strobe reads one plane and advances the index once, regardless
    // of the number of byte lanes driven. Writes process the low lane first.
    internal uint Read(uint address, int size, long cycle, bool strobe = true)
    {
        if ((address & 0x8000) != 0) return 0;
        var offset = (int)(address & 0x3F);
        if (offset is >= 0x38 and < 0x3C)
        {
            if (_needsConversion)
            {
                ConvertChunkyPixels();
                _needsConversion = false;
                _readIndex = 0;
            }
            if (strobe) _writeIndex = 0;
        }

        uint result = 0;
        for (var lane = 0; lane < size; lane++)
            result = (result << 8) | ReadLane((offset + lane) & 0x3F, cycle, strobe);
        if (strobe && offset is >= 0x38 and < 0x3C)
            _readIndex = (_readIndex + 1) & 7;
        return result;
    }

    internal void Write(uint address, int size, uint value, long cycle)
    {
        if ((address & 0x8000) != 0) return;
        if ((address & 0x3F) == 0x30 && size == 4)
        {
            // Both EEPROM lanes belong to one CPU strobe. Releasing SDA
            // before lowering SCL would synthesize an I2C STOP.
            _eeprom.WritePinsAndDirection((byte)(value >> 24), (byte)(value >> 8), cycle);
            return;
        }
        for (var lane = size - 1; lane >= 0; lane--)
        {
            var offset = (int)((address + (uint)lane) & 0x3F);
            var data = (byte)value;
            value >>= 8;
            switch (offset)
            {
                case < 0x30:
                    _cd.Write(offset, data, cycle);
                    break;
                case 0x30:
                    _eeprom.WritePins(data, cycle);
                    break;
                case 0x32:
                    _eeprom.WriteDirection(data, cycle);
                    break;
                case >= 0x38 and < 0x3C:
                    var byteLane = offset - 0x38;
                    if (byteLane == 3) _chunky[_writeIndex] = 0;
                    _chunky[_writeIndex] |= (uint)data << (8 * (3 - byteLane));
                    if (byteLane == 0) _writeIndex = (_writeIndex + 1) & 7;
                    _needsConversion = true;
                    break;
            }
        }
    }

    private byte ReadLane(int offset, long cycle, bool strobe) => offset switch
    {
        0x00 => 0xC0,
        0x01 or 0x02 => 0xCA,
        0x03 => 0xFE,
        < 0x30 => _cd.Read(offset, cycle, strobe),
        0x30 => _eeprom.ReadPins(),
        0x32 => _eeprom.ReadDirection(),
        >= 0x38 and < 0x3C => (byte)(_planes[_readIndex] >> (8 * (0x3B - offset))),
        _ => 0
    };

    private void ConvertChunkyPixels()
    {
        for (var plane = 0; plane < _planes.Length; plane++)
        {
            uint output = 0;
            foreach (var fourPixels in _chunky)
            {
                for (var shift = 24 + plane; shift >= plane; shift -= 8)
                    output = (output << 1) | ((fourPixels >> shift) & 1);
            }
            _planes[plane] = output;
        }
    }

    internal void Step(long cycle, LightweightA500Machine machine)
    {
        _eeprom.CommitWrite(cycle);
        if (_cd.NextCycle == cycle) _cd.Step(cycle, machine);
    }

    public void Dispose() => _cd.Dispose();
}
