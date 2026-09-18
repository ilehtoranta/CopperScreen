using CopperDisk;

namespace CopperMod.Amiga.Lightweight;

/// <summary>One standard ADF, control pins and independent spindle.</summary>
internal sealed class LightweightFloppyDrive
{
    internal LightweightFloppyDrive(int index = 0) => _selectMask = 8 << index;
    private readonly int _selectMask;
    private const int BitRateDenominator = 2 * 5 * LightweightDiskSerial.TrackBits;
    private int _fraction;
    internal const int StandardAdfBytes = 80 * 2 * 11 * 512;
    // Chosen drive-model spin-up time, not a universal drive timing claim.
    internal const long MotorSpinUpCycles = LightweightPaulaAudio.PalCpuFrequency / 2;
    private byte[]? _image;
    private byte[][]? _tracks;
    private byte _controlPins = 0xFF;
    private readonly bool[] _dirtyTracks = new bool[160];
    private long _lastWriteCycle = long.MinValue;
    private int _writePosition, _writeTrack = -1;

    internal bool Mounted => _image is not null;
    internal bool WriteProtected { get; set; } = true;
    internal bool IsDirty { get; private set; }
    internal ReadOnlySpan<byte> Image => _image;
    internal ReadOnlySpan<byte> Track => _tracks is null ? default : _tracks[Cylinder * 2 + Head];
    internal int Cylinder { get; private set; }
    internal int Head => (_controlPins & 4) == 0 ? 1 : 0;
    internal bool Selected => (_controlPins & _selectMask) == 0;
    internal bool MotorOn { get; private set; }
    internal bool DiskChanged { get; private set; } = true;
    internal long ReadyCycle { get; private set; } = long.MaxValue;
    internal long NextBitCycle { get; private set; } = long.MaxValue;
    internal int BitPosition { get; private set; }

    internal void Mount(ReadOnlySpan<byte> image)
    {
        if (image.Length != StandardAdfBytes)
            throw new ArgumentException("Floppy drives support standard 880 KiB (80-cylinder DD) ADF images only.", nameof(image));
        // Media conversion belongs at the host boundary. A seek must never
        // allocate or invoke a lazy track decoder during emulated execution.
        var ownedImage = image.ToArray();
        var media = AmigaDiskLoader.FromAdfBytes(ownedImage);
        var tracks = new byte[160][];
        for (var track = 0; track < tracks.Length; track++)
            tracks[track] = AmigaDosTrackEncoder.EncodeTrack(media, track / 2, track & 1);
        _image = ownedImage;
        _tracks = tracks;
        Array.Clear(_dirtyTracks);
        IsDirty = false;
        _writeTrack = -1;
        DiskChanged = true;
    }

    internal void Eject()
    {
        _image = null;
        _tracks = null;
        IsDirty = false;
        _writeTrack = -1;
        DiskChanged = true;
    }

    internal void ResetControl()
    {
        _controlPins = 0xFF;
        MotorOn = false;
        ReadyCycle = long.MaxValue;
        NextBitCycle = long.MaxValue;
        BitPosition = _fraction = 0;
        _writeTrack = -1;
        // A reset does not remove media, move the head or clear disk change.
    }

    // Return false for a seek outside this deliberately bounded drive model.
    internal bool WriteControlPins(byte pins, long cycle)
    {
        var previous = _controlPins;
        _controlPins = pins;
        if (!Selected) return true;

        if ((previous & _selectMask) != 0)
        {
            // MOTOR is latched on selection, not on every PRB write.
            var motor = (pins & 0x80) == 0;
            if (motor != MotorOn)
            {
                MotorOn = motor;
                ReadyCycle = motor ? cycle + MotorSpinUpCycles : long.MaxValue;
            }
        }

        if ((previous & 1) != 0 && (pins & 1) == 0)
        {
            if (Mounted) DiskChanged = false;
            if ((pins & 2) != 0)
                Cylinder = Math.Max(0, Cylinder - 1);
            else if (Cylinder < 79)
                Cylinder++;
            else
                return false;
        }
        return true;
    }

    internal byte ReadInputPins(long cycle)
    {
        if (!Selected) return 0xFF;
        var pins = 0xFF;
        if (DiskChanged) pins &= ~0x04;
        if (Mounted && WriteProtected) pins &= ~0x08;
        if (Cylinder == 0) pins &= ~0x10;
        if (Mounted && MotorOn && cycle >= ReadyCycle) pins &= ~0x20;
        // External DD drives identify as DRT_AMIGA ($00000000) on /READY
        // with the motor off, including an empty connected drive.
        if (_selectMask != 8 && !MotorOn) pins &= ~0x20;
        return (byte)pins;
    }

    internal bool UpdateRotation(long cycle, bool mediaChanged)
    {
        if (!Mounted || !MotorOn)
        {
            NextBitCycle = long.MaxValue;
            return false;
        }
        if (NextBitCycle != long.MaxValue && !mediaChanged) return false;
        // Bounded model: a new spindle run starts at bit zero; no coast-down.
        BitPosition = 0;
        _fraction = BitRateDenominator - 1;
        NextBitCycle = (Math.Max(cycle, ReadyCycle) + 1) & ~1L;
        ScheduleFollowingBit();
        return true;
    }

    internal bool AdvanceRotation()
    {
        var index = ++BitPosition == LightweightDiskSerial.TrackBits;
        if (index) BitPosition = 0;
        ScheduleFollowingBit();
        return index;
    }

    private void ScheduleFollowingBit()
    {
        var ccks = LightweightPaulaAudio.PalCpuFrequency / BitRateDenominator;
        _fraction += LightweightPaulaAudio.PalCpuFrequency % BitRateDenominator;
        if (_fraction >= BitRateDenominator)
        {
            _fraction -= BitRateDenominator;
            ccks++;
        }
        NextBitCycle += 2 * ccks;
    }

    // Ideal-ADF splice: preserve consecutive emitted cells in the encoded
    // grid. Nominal 300 RPM and Paula's 7-CCK write clock differ by a few
    // cells/revolution; this model does not simulate analog splice drift.
    internal void WriteBit(int bit, long cycle, bool slow)
    {
        if (!Selected || WriteProtected || _tracks is null || !MotorOn || cycle < ReadyCycle) return;
        var track = Cylinder * 2 + Head;
        var cells = slow ? 2 : 1;
        if (_writeTrack != track || cycle - _lastWriteCycle != cells * 14)
            _writePosition = BitPosition;
        var bytes = _tracks[track];
        for (var cell = 0; cell < cells; cell++)
        {
            var mask = 1 << (7 - (_writePosition & 7));
            ref var value = ref bytes[_writePosition >> 3];
            value = (byte)((value & ~mask) | (cell == cells - 1 && bit != 0 ? mask : 0));
            if (++_writePosition == LightweightDiskSerial.TrackBits) _writePosition = 0;
        }
        _lastWriteCycle = cycle;
        _writeTrack = track;
        _dirtyTracks[track] = IsDirty = true;
    }

    internal byte[] ExportAdf()
    {
        if (_image is null || _tracks is null) throw new InvalidOperationException("No ADF is mounted.");
        var result = (byte[])_image.Clone();
        const int bytesPerTrack = 11 * 512;
        for (var track = 0; track < _tracks.Length; track++)
        {
            if (!_dirtyTracks[track]) continue;
            var decoded = AmigaDosTrackDecoder.DecodeTrackBestEffort(
                AmigaEncodedTrack.FromBytes(_tracks[track]), track / 2, track & 1,
                result.AsSpan(track * bytesPerTrack, bytesPerTrack));
            if (decoded != 11)
                throw new InvalidOperationException($"Track {track / 2}.{track & 1} contains {decoded}/11 valid AmigaDOS sectors; it cannot be saved as a standard ADF.");
        }
        return result;
    }

    internal void MarkSaved() => IsDirty = false;
}
