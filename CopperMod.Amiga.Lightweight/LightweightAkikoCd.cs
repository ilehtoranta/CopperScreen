using System.Buffers.Binary;
using System.Numerics;

namespace CopperMod.Amiga.Lightweight;

// A bounded Chinon command/data path for Mode 1 and CD audio tracks.
// Transfers share the machine's CCKs. The provisional timing and arbitration
// model requires native and hardware qualification.
internal sealed class LightweightAkikoCd(int clockFrequency) : IDisposable
{
    private const uint Subcode = 0x8000_0000;
    private const uint DriveTx = 0x4000_0000;
    private const uint DriveRx = 0x2000_0000;
    private const uint RxDone = 0x1000_0000;
    private const uint TxDone = 0x0800_0000;
    private const uint BlockDone = 0x0400_0000;
    private const uint DataError = 0x0200_0000;
    private const uint TxDma = 0x4000_0000;
    private const uint RxDma = 0x2000_0000;
    private const uint BlockDma = 0x0800_0000;
    private const uint SectorRead = 0x0400_0000;
    private const int LineCycles = LightweightClock.CpuCyclesPerLine;
    private const long Never = long.MaxValue;
    private const int RawBytes = LightweightCd32DiscImage.RawSectorBytes;

    private readonly byte[] _command = new byte[13];
    private readonly byte[] _response = new byte[21];
    private readonly byte[] _sector = new byte[RawBytes];
    private readonly byte[] _subcode = new byte[100];
    private readonly LightweightCd32Audio _audio = new(clockFrequency);
    private LightweightCd32DiscImage? _image;
    private uint _requests, _mask, _control, _dataBase, _ringsBase;
    private ushort _blocks;
    private byte _tx, _txEnd, _rx, _rxEnd, _subcodePosition, _lastResponseByte;
    private int _received, _responseLength, _responseIndex;
    private bool _led, _mediaChange;
    private int _tocPacket;
    private int _lba, _lastLba, _sectorNumber, _sectorRate, _sectorRemainder;
    private int _dataSlot, _dataOffset, _subcodeOffset;
    private uint _slotDataBase, _slotSubcodeBase;
    private bool _sendSubcode;
    private bool _subcodePending;
    private long _commandCycle = Never, _noticeCycle = Never, _sectorCycle = Never, _transferCycle = Never;
    private long _txAllowed, _rxAllowed;
    private long _audioCycle = Never, _pauseCycle, _pausedDeadline;
    private int _audioLba, _audioEnd, _audioRemainder;
    private bool _audioStartNotice;
    private byte? _audioEndNotice;
    private bool _playing, _paused, _audioHasSector;

    internal bool IsMounted => _image is not null;
    internal bool TurboReads { get; set; }
    internal int TodLinesPerPulse => (_control & 0x0080_0000) != 0 ? 262 : 313;
    internal bool InterruptAsserted => (_mask & _requests) != 0;
    internal long LastOutputCycle { get; private set; } = long.MinValue;
    internal long NextCycle => Math.Min(_audioCycle,
        Math.Min(Math.Min(_commandCycle, _noticeCycle), Math.Min(_sectorCycle, _transferCycle)));

    internal void Mount(LightweightCd32DiscImage? image, long cycle)
    {
        StopAudio(cycle);
        ClearAudioNotices();
        _image?.Dispose();
        _image = image;
        _mediaChange = true;
        StopRead();
        _responseLength = _responseIndex = 0;
        _received = 0;
        _commandCycle = Never;
        _blocks = 0;
        _requests &= ~(Subcode | BlockDone | DataError | DriveRx);
        _noticeCycle = cycle + LineCycles;
        UpdateTransfer(cycle);
    }

    internal void Reset(long cycle, bool cold)
    {
        _requests = _mask = _control = _dataBase = _ringsBase = 0;
        _tx = _txEnd = _rx = _rxEnd = _lastResponseByte = 0;
        _subcodePosition = 0xC2;
        _received = _responseLength = _responseIndex = _blocks = 0;
        _lastLba = 0;
        _txAllowed = _rxAllowed = 0;
        _led = false;
        _commandCycle = _transferCycle = Never;
        _mediaChange = IsMounted;
        _noticeCycle = _mediaChange ? cycle + LineCycles : Never;
        LastOutputCycle = long.MinValue;
        StopRead();
        _audio.Reset(cycle, cold);
        _audioCycle = Never;
        _playing = _paused = _audioHasSector = false;
        ClearAudioNotices();
    }

    internal void MixAudio(Span<short> samples, long cycle) => _audio.Mix(samples, cycle);

    internal byte Read(int offset, long cycle, bool strobe)
    {
        if (offset == 0x28) return ReadPio(cycle, strobe);
        return offset switch
        {
            >= 0x04 and <= 0x07 => Lane(_requests, offset),
            >= 0x08 and <= 0x0F => Lane(_mask, offset),
            0x10 or 0x14 or 0x18 or 0x1C => _subcodePosition,
            0x11 or 0x15 or 0x19 or 0x1D => _tx,
            0x12 or 0x16 or 0x1A or 0x1E => _rx,
            0x20 => (byte)(_blocks >> 8),
            0x21 => (byte)_blocks,
            >= 0x24 and <= 0x27 => Lane(_control, offset),
            _ => 0
        };
    }

    internal void Write(int offset, byte data, long cycle)
    {
        switch (offset)
        {
            case 0x08:
                _mask = (uint)data << 24;
                break;
            case >= 0x10 and <= 0x13:
                _dataBase = WithLane(_dataBase, offset, data) & 0x00FF_F000;
                break;
            case >= 0x14 and <= 0x17:
                _ringsBase = WithLane(_ringsBase, offset, data) & 0x00FF_FC00;
                break;
            case 0x18:
                _requests &= ~Subcode;
                break;
            case 0x1D:
                _txEnd = data;
                _txAllowed = cycle + 3 * LineCycles;
                _requests &= ~TxDone;
                break;
            case 0x1F:
                _rxEnd = data;
                _rxAllowed = cycle + 3 * LineCycles;
                _requests &= ~RxDone;
                break;
            case 0x20:
            case 0x21:
                if ((_control & BlockDma) != 0) _blocks |= (ushort)(data << (offset == 0x20 ? 8 : 0));
                _requests &= ~BlockDone;
                break;
            case >= 0x24 and <= 0x27:
                var previous = _control;
                _control = WithLane(_control, offset, data) & 0xFF80_0000;
                if ((_control & SectorRead) != 0 && (previous & SectorRead) == 0)
                {
                    _sectorNumber = 0;
                    _requests &= ~DataError;
                }
                if ((_control & BlockDma) == 0) _blocks = 0;
                break;
            case 0x28 when (_control & TxDma) == 0:
                _requests &= ~DriveTx;
                if (CanReceiveCommand) ReceiveCommandByte(data, cycle);
                break;
        }
        UpdateTransfer(cycle);
    }

    internal void Step(long cycle, LightweightA500Machine machine)
    {
        if (_commandCycle == cycle) ExecuteCommand(cycle);
        if (_noticeCycle == cycle)
        {
            _noticeCycle = Never;
            if (_responseLength == 0)
            {
                if (_mediaChange)
                {
                    _mediaChange = false;
                    ReturnStatus(0x0A, IsMounted ? (byte)1 : (byte)0);
                }
                else if (_audioStartNotice)
                {
                    _audioStartNotice = false;
                    ReturnStatus(4, 0x0B);
                }
                else if (_audioEndNotice is { } status)
                {
                    _audioEndNotice = null;
                    ReturnStatus(4, status);
                }
                else if (_tocPacket >= 0) ReturnTocEntry(cycle);
            }
        }
        if (_sectorCycle == cycle) AcceptSector(cycle);
        if (_audioCycle == cycle) AdvanceAudio(cycle);
        if (_transferCycle == cycle && machine.CanAkikoOwnOutput(cycle))
        {
            Transfer(cycle, machine);
            LastOutputCycle = cycle;
        }
        UpdateTransfer(cycle);
    }

    private void UpdateTransfer(long cycle)
    {
        var nextSlot = LightweightBusArbiter.AlignToSlot(cycle + 1);
        _transferCycle = Never;
        if ((_control & (TxDma | SectorRead)) == TxDma && _tx != _txEnd && CanReceiveCommand)
            _transferCycle = Math.Max(nextSlot, LightweightBusArbiter.AlignToSlot(_txAllowed));
        if ((_control & RxDma) != 0 && _rx != _rxEnd && _responseLength != 0)
            _transferCycle = Math.Min(_transferCycle, Math.Max(nextSlot, LightweightBusArbiter.AlignToSlot(_rxAllowed)));
        if (_dataSlot >= 0 || _subcodePending) _transferCycle = nextSlot;
    }

    private bool CanReceiveCommand => _commandCycle == Never && _responseLength == 0;

    private void Transfer(long cycle, LightweightA500Machine machine)
    {
        if ((_control & RxDma) != 0 && cycle >= _rxAllowed && _responseLength != 0 && _rx != _rxEnd)
        {
            machine.WriteAkikoDmaByte(_ringsBase + _rx, TakeResponse(cycle));
            _rx = unchecked((byte)(_rx + 1));
            if (_rx == _rxEnd) _requests |= RxDone;
        }
        else if ((_control & (TxDma | SectorRead)) == TxDma && cycle >= _txAllowed &&
            CanReceiveCommand && _tx != _txEnd)
        {
            ReceiveCommandByte(machine.ReadAkikoDmaByte((_ringsBase | 0x200) + _tx), cycle);
            _tx = unchecked((byte)(_tx + 1));
            if (_tx == _txEnd) _requests |= TxDone;
        }
        else if (_dataSlot >= 0 || _subcodePending) TransferSectorWord(machine);
    }

    private void ReceiveCommandByte(byte value, long cycle)
    {
        _command[_received++] = value;
        var length = (_command[0] & 15) switch { 4 => 13, 5 => 3, <= 7 => 2, _ => 1 };
        if (_received == length) _commandCycle = cycle + LineCycles;
        else _requests |= DriveTx;
    }

    private void ExecuteCommand(long cycle)
    {
        _commandCycle = Never;
        _response.AsSpan().Clear();
        var tag = _command[0];
        var command = tag & 15;
        byte sum = 0;
        for (var i = 0; i < _received; i++) sum = unchecked((byte)(sum + _command[i]));
        _received = 0;
        if (command > 7 || sum != 0xFF)
        {
            ReturnStatus((byte)((tag & 0xF0) | 5), command > 7 ? (byte)0x81 : (byte)0x89);
            return;
        }

        switch (command)
        {
            case 0:
                _response[0] = tag;
                FinishResponse(1);
                break;
            case 1:
                StopRead();
                StopAudio(cycle);
                ClearAudioNotices();
                ReturnStatus(tag, IsMounted ? (byte)0 : (byte)0xF9);
                break;
            case 2:
                _noticeCycle = Never;
                _tocPacket = -1;
                PauseAudio(cycle);
                ReturnStatus(tag, DriveStatus());
                break;
            case 3:
                ResumeAudio(cycle);
                ReturnStatus(tag, DriveStatus());
                break;
            case 4:
                ReadOrSeek(tag, cycle);
                break;
            case 5:
                _led = (_command[1] & 1) != 0;
                if ((_command[1] & 0x80) != 0) ReturnStatus(tag, _led ? (byte)1 : (byte)0);
                else _requests |= DriveTx;
                break;
            case 6:
                _response[0] = tag;
                if (_image is not null && _playing && _audioHasSector)
                    WritePositionQ(_response.AsSpan(3), _image.GetTrack(_audioLba - 1), _audioLba - 1);
                FinishResponse(15);
                break;
            case 7:
                _response[0] = tag;
                _response[1] = 1;
                "CHINON  O-658-2 24"u8.CopyTo(_response.AsSpan(2));
                FinishResponse(20);
                break;
        }
    }

    private void ReadOrSeek(byte tag, long cycle)
    {
        if (_image is null) { ReturnStatus(tag, 1); return; }
        var lba = ParseCommandLba(1);
        StopAudio(cycle);
        ClearAudioNotices();
        if ((_command[7] & 0x80) != 0)
        {
            StopRead();
            if (!_image.IsData(lba)) { ReturnStatus(tag, 0xA1); return; }
            var distance = Math.Abs(lba - _lastLba);
            var seekSteps = distance < 100 ? 1 : Math.Min(100, 10 + distance / 1000);
            _lba = lba;
            _sectorRate = (_command[8] & 0x40) == 0 ? 75 : 150;
            _sectorRemainder = 0;
            _sectorNumber = 0;
            _sectorCycle = cycle + (TurboReads ? LineCycles : (long)clockFrequency * seekSteps / _sectorRate);
            ReturnStatus(tag, 2);
        }
        else if (lba < 0)
        {
            StopRead();
            _tocPacket = 0;
            _noticeCycle = cycle + clockFrequency / 75;
            ReturnStatus(tag, 0);
        }
        else if (_image.IsAudio(lba))
        {
            StopRead();
            _playing = true;
            _audioLba = lba;
            var endLba = ParseCommandLba(4);
            _audioEnd = endLba > lba ? Math.Min(endLba, _image.SectorCount) : _image.SectorCount;
            _audioRemainder = 0;
            _audioCycle = cycle + LineCycles;
            _image.Request(lba);
            ReturnStatus(tag, 0x42);
        }
        else ReturnStatus(tag, 0xA9);
    }

    private int ParseCommandLba(int offset)
        => FromBcd(_command[offset]) * 4500 + FromBcd(_command[offset + 1]) * 75 + FromBcd(_command[offset + 2]) - 150;

    private void ReturnTocEntry(long cycle)
    {
        Array.Clear(_response);
        _response[0] = 6;
        _response[1] = 0x0A;
        var point = _tocPacket / 3;
        var tracks = _image!.Tracks;
        var track = point switch { 0 => tracks[0], 1 or 2 => tracks[^1], _ => tracks[point - 3] };
        _response[3] = track.Audio ? (byte)0x01 : (byte)0x41;
        _response[5] = point < 3 ? (byte)(0xA0 + point) : Bcd(track.Number);
        _response[6] = 0x99;
        _response[7] = 0x24;
        _response[8] = Bcd(_tocPacket);
        if (point < 2) _response[10] = Bcd(track.Number);
        else LightweightCd32DiscImage.WriteMsf(_response.AsSpan(10), (point == 2 ? _image.SectorCount : track.StartLba) + 150);
        FinishResponse(15);
        if (++_tocPacket >= (3 + tracks.Length) * 3) _tocPacket = -1;
        else _noticeCycle = cycle + clockFrequency / 75;
    }

    private byte ReadPio(long cycle, bool strobe)
    {
        if ((_control & RxDma) != 0 || _responseLength == 0) return _lastResponseByte;
        if (!strobe) return _response[_responseIndex];
        var value = TakeResponse(cycle);
        UpdateTransfer(cycle);
        return value;
    }

    private byte TakeResponse(long cycle)
    {
        _lastResponseByte = _response[_responseIndex++];
        if (_responseIndex == _responseLength)
        {
            _responseLength = _responseIndex = 0;
            _requests = (_requests & ~DriveRx) | DriveTx;
            if (_noticeCycle == Never && (_mediaChange || _audioStartNotice || _audioEndNotice.HasValue || _tocPacket >= 0))
                _noticeCycle = cycle + LineCycles;
        }
        return _lastResponseByte;
    }

    private void ReturnStatus(byte tag, byte status)
    {
        _response[0] = tag;
        _response[1] = status;
        FinishResponse(2);
    }

    private void FinishResponse(int length)
    {
        byte checksum = 0xFF;
        for (var i = 0; i < length; i++) checksum = unchecked((byte)(checksum - _response[i]));
        _response[length] = checksum;
        _responseLength = length + 1;
        _responseIndex = 0;
        _requests |= DriveRx;
    }

    private void AcceptSector(long cycle)
    {
        if (_image is null) { _sectorCycle = Never; return; }
        var ticks = clockFrequency + _sectorRemainder;
        _sectorCycle = cycle + (TurboReads ? LineCycles : ticks / _sectorRate);
        _sectorRemainder = ticks % _sectorRate;
        if (_blocks == 0 || _dataSlot >= 0 || (_control & (BlockDma | SectorRead)) != (BlockDma | SectorRead)) return;
        if (!_image.IsData(_lba) || !_image.Request(_lba) || !_image.CopyRequestedSector(_sector))
        {
            _requests |= DataError;
            _sectorCycle = Never;
            return;
        }
        _dataSlot = BitOperations.Log2(_blocks);
        _slotDataBase = _dataBase + (uint)(_dataSlot * 4096);
        _slotSubcodeBase = _ringsBase | 0x100;
        _dataOffset = 0;
        _subcodeOffset = 0;
        _sendSubcode = (_control & Subcode) != 0;
        _sector.AsSpan(0, 3).Clear();
        _sector[3] = (byte)(_sectorNumber++ & 31);
        if (_sendSubcode) PrepareSubcode(_image.GetTrack(_lba), _lba);
        _lastLba = _lba++;
        _image.Request(_lba);
    }

    private void TransferSectorWord(LightweightA500Machine machine)
    {
        if (_dataOffset < RawBytes)
        {
            machine.WriteAkikoDmaWord(_slotDataBase + (uint)_dataOffset,
                BinaryPrimitives.ReadUInt16BigEndian(_sector.AsSpan(_dataOffset)));
            _dataOffset += 2;
        }
        else if (_dataOffset < RawBytes + 146)
        {
            machine.WriteAkikoDmaWord(_slotDataBase + (uint)(0xC00 + _dataOffset - RawBytes), 0);
            _dataOffset += 2;
        }
        else
        {
            var buffer = _subcodePosition >= 128 ? 0u : 128u;
            machine.WriteAkikoDmaWord(_slotSubcodeBase + buffer + (uint)_subcodeOffset,
                BinaryPrimitives.ReadUInt16BigEndian(_subcode.AsSpan(_subcodeOffset)));
            _subcodeOffset += 2;
            if (_subcodeOffset == _subcode.Length)
            {
                _subcodePosition = (byte)(buffer + 100);
                _requests |= Subcode;
                _subcodePending = false;
            }
        }
        if (_dataSlot >= 0 && _dataOffset == RawBytes + 146 && (!_sendSubcode || _subcodeOffset == _subcode.Length))
        {
            _blocks &= (ushort)~(1 << _dataSlot);
            _requests |= BlockDone;
            _dataSlot = -1;
        }
    }

    private void PrepareSubcode(LightweightCd32DiscImage.Track track, int lba)
    {
        Span<byte> q = stackalloc byte[12];
        WritePositionQ(q, track, lba);
        ushort crc = 0;
        for (var i = 0; i < 10; i++)
        {
            crc ^= (ushort)(q[i] << 8);
            for (var bit = 0; bit < 8; bit++)
                crc = (ushort)((crc << 1) ^ ((crc & 0x8000) == 0 ? 0 : 0x1021));
        }
        BinaryPrimitives.WriteUInt16BigEndian(q[10..], (ushort)~crc);
        for (var i = 0; i < 96; i++)
            _subcode[i] = (q[i / 8] & (0x80 >> (i % 8))) != 0 ? (byte)0x40 : (byte)0;
        BinaryPrimitives.WriteUInt32BigEndian(_subcode.AsSpan(96), 0xFFFF0000);
    }

    private static void WritePositionQ(Span<byte> q, LightweightCd32DiscImage.Track track, int lba)
    {
        q[0] = track.Audio ? (byte)0x01 : (byte)0x41;
        q[1] = Bcd(track.Number);
        q[2] = lba >= track.StartLba ? (byte)1 : (byte)0;
        LightweightCd32DiscImage.WriteMsf(q[3..], Math.Abs(lba - track.StartLba));
        q[6] = 0;
        LightweightCd32DiscImage.WriteMsf(q[7..], lba + 150);
    }

    private void AdvanceAudio(long cycle)
    {
        _audio.RenderTo(cycle);
        var image = _image!;
        var ended = _audioLba >= _audioEnd;
        if (ended || !image.IsAudio(_audioLba) || !image.Request(_audioLba) || !image.CopyRequestedSector(_audio.Sector))
        {
            StopAudio(cycle);
            ScheduleAudioNotice(ended ? (byte)1 : (byte)0x81, cycle);
            return;
        }
        if (!_audioHasSector) ScheduleAudioNotice(0x0B, cycle);
        _audioHasSector = true;
        _audio.BeginSector(cycle);
        if ((_control & Subcode) != 0 && !_subcodePending)
        {
            _slotSubcodeBase = _ringsBase | 0x100;
            _subcodeOffset = 0;
            _dataOffset = RawBytes + 146;
            PrepareSubcode(image.GetTrack(_audioLba), _audioLba);
            _subcodePending = true;
        }
        if (++_audioLba < _audioEnd) image.Request(_audioLba);
        var ticks = clockFrequency + _audioRemainder;
        _audioCycle = cycle + ticks / 75;
        _audioRemainder = ticks % 75;
    }

    private void ScheduleAudioNotice(byte status, long cycle)
    {
        if (status == 0x0B) _audioStartNotice = true;
        else _audioEndNotice = status;
        if (_responseLength == 0) _noticeCycle = cycle + LineCycles;
    }

    private void ClearAudioNotices()
    {
        _audioStartNotice = false;
        _audioEndNotice = null;
    }

    private byte DriveStatus() => IsMounted ? (_playing ? (byte)9 : (byte)1) : (byte)0xF9;

    private void StopAudio(long cycle)
    {
        _audio.Silence(cycle);
        _audioCycle = Never;
        _playing = _paused = _audioHasSector = false;
    }

    private void PauseAudio(long cycle)
    {
        if (!_playing || _paused) return;
        _paused = true;
        _audio.Silence(cycle);
        _pauseCycle = cycle;
        _pausedDeadline = _audioCycle;
        _audioCycle = Never;
    }

    private void ResumeAudio(long cycle)
    {
        if (!_paused) return;
        var elapsed = cycle - _pauseCycle;
        if (_audioHasSector)
        {
            _audio.RenderTo(cycle);
            _audio.Resume(elapsed);
        }
        _audioCycle = _pausedDeadline + elapsed;
        _paused = false;
    }

    private void StopRead()
    {
        _sectorCycle = Never;
        _dataSlot = -1;
        _tocPacket = -1;
        _subcodePending = false;
    }

    public void Dispose() => _image?.Dispose();
    private static int FromBcd(byte value) => (value >> 4) * 10 + (value & 15);
    private static byte Bcd(int value) => LightweightCd32DiscImage.ToBcd(value);
    private static byte Lane(uint value, int offset) => (byte)(value >> ((3 - (offset & 3)) * 8));
    private static uint WithLane(uint value, int offset, byte data)
    {
        var shift = (3 - (offset & 3)) * 8;
        return (value & ~(0xFFu << shift)) | ((uint)data << shift);
    }
}
