using CopperDisk;

namespace CopperMod.Amiga.Lightweight;

/// <summary>Owned floppy tracks, control pins and independent spindle.</summary>
internal sealed class LightweightFloppyDrive
{
    internal LightweightFloppyDrive(int index = 0) => _selectMask = 8 << index;
    private readonly int _selectMask;
    private const int RevolutionCcks = LightweightPaulaAudio.PalCpuFrequency / 10;
    private int _bitRateDenominator = 10 * LightweightDiskSerial.TrackBits;
    private int _bitWholeCcks = LightweightPaulaAudio.PalCpuFrequency / (10 * LightweightDiskSerial.TrackBits);
    private int _bitRemainder = LightweightPaulaAudio.PalCpuFrequency % (10 * LightweightDiskSerial.TrackBits);
    private int _fraction;
    internal const int StandardAdfBytes = 80 * 2 * 11 * 512;
    // Chosen drive-model spin-up time, not a universal drive timing claim.
    internal const long MotorSpinUpCycles = LightweightPaulaAudio.PalCpuFrequency / 2;
    private byte[]? _image;
    private byte[][]? _tracks;
    private int[]? _trackLengths;
    private byte[]?[]? _unstableMasks;
    private byte[]? _currentTrack, _currentUnstableMask;
    private int[]?[]? _cellDeadlines;
    private int[]? _currentDeadlines;
    private int _trackBits = LightweightDiskSerial.TrackBits;
    private uint _mediaSeed, _revolutionSeed;
    private long _revolutionStartCycle;
    private uint _revolution;
    private bool _writeProtected = true;
    private byte _controlPins = 0xFF;
    private readonly bool[] _dirtyTracks = new bool[160];
    private long _lastWriteCycle = long.MinValue;
    private int _writePosition, _writeTrack = -1;

    internal bool Mounted => _tracks is not null;
    internal LightweightFloppyFormat Format { get; private set; }
    internal bool CanWrite => Format == LightweightFloppyFormat.Adf;
    internal bool WriteProtected
    {
        get => Format == LightweightFloppyFormat.Ipf || _writeProtected;
        set
        {
            if (!value && Format == LightweightFloppyFormat.Ipf)
                throw new InvalidOperationException("IPF media is permanently read-only.");
            _writeProtected = value;
        }
    }
    internal bool IsDirty { get; private set; }
    internal ReadOnlySpan<byte> Image => _image;
    internal ReadOnlySpan<byte> Track => _currentTrack;
    internal int TrackBitLength => _trackBits;
    internal uint Revolution => _revolution;
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
        Format = LightweightFloppyFormat.Adf;
        _trackLengths = null;
        _unstableMasks = null;
        _cellDeadlines = null;
        SelectTrack(0, preservePhase: false);
        Array.Clear(_dirtyTracks);
        IsDirty = false;
        _writeTrack = -1;
        DiskChanged = true;
    }

    internal void MountIpf(ReadOnlySpan<byte> image)
    {
        MountIpf(LightweightIpfImage.Prepare(image));
    }

    internal void MountIpf(LightweightIpfImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        var tracks = image.Tracks;
        var lengths = image.Lengths;
        var masks = image.WeakMasks;
        var seed = image.Seed;
        _mediaSeed = seed ^ (uint)_selectMask;
        _image = null;
        _tracks = tracks;
        _trackLengths = lengths;
        _unstableMasks = masks;
        _cellDeadlines = image.CellDeadlines;
        Format = LightweightFloppyFormat.Ipf;
        SelectTrack(0, preservePhase: false);
        Array.Clear(_dirtyTracks);
        IsDirty = false;
        _writeTrack = -1;
        DiskChanged = true;
    }

    internal void Eject()
    {
        _image = null;
        _tracks = null;
        _trackLengths = null;
        _unstableMasks = null;
        _cellDeadlines = null;
        _currentDeadlines = null;
        _currentTrack = _currentUnstableMask = null;
        Format = LightweightFloppyFormat.None;
        IsDirty = false;
        _writeTrack = -1;
        DiskChanged = true;
    }

    internal void ResetControl()
    {
        _controlPins = 0xFF;
        SelectTrack(0, preservePhase: false);
        MotorOn = false;
        ReadyCycle = long.MaxValue;
        NextBitCycle = long.MaxValue;
        BitPosition = _fraction = 0;
        _revolution = 0;
        _revolutionSeed = _mediaSeed;
        _writeTrack = -1;
        // A reset does not remove media, move the head or clear disk change.
    }

    // Return false for a seek outside this deliberately bounded drive model.
    internal bool WriteControlPins(byte pins, long cycle)
    {
        var previous = _controlPins;
        var previousTrack = Cylinder * 2 + Head;
        _controlPins = pins;
        if (!Selected)
        {
            if (previousTrack != Cylinder * 2 + Head) SelectTrack(cycle, preservePhase: true);
            return true;
        }

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
            else if (Cylinder < (Format == LightweightFloppyFormat.Ipf ? 83 : 79))
                Cylinder++;
            else if (Format != LightweightFloppyFormat.Ipf)
                return false; // IPF drive hits its mechanical stop at cylinder 83.
        }
        if (previousTrack != Cylinder * 2 + Head) SelectTrack(cycle, preservePhase: true);
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
        _fraction = _bitRateDenominator - 1;
        NextBitCycle = (Math.Max(cycle, ReadyCycle) + 1) & ~1L;
        _revolutionStartCycle = NextBitCycle;
        _revolution = 0;
        _revolutionSeed = _mediaSeed;
        ScheduleFollowingBit();
        return true;
    }

    internal bool AdvanceRotation()
    {
        var index = ++BitPosition == _trackBits;
        if (index)
        {
            BitPosition = 0;
            _revolutionStartCycle = NextBitCycle;
            _revolutionSeed = Mix(_mediaSeed ^ ++_revolution);
        }
        ScheduleFollowingBit();
        return index;
    }

    private void ScheduleFollowingBit()
    {
        if (_currentDeadlines is { } times)
        {
            NextBitCycle = _revolutionStartCycle + 2L * times[BitPosition + 1];
            return;
        }
        var ccks = _bitWholeCcks;
        _fraction += _bitRemainder;
        if (_fraction >= _bitRateDenominator)
        {
            _fraction -= _bitRateDenominator;
            ccks++;
        }
        NextBitCycle += 2 * ccks;
    }

    private void SelectTrack(long cycle, bool preservePhase)
    {
        if (_tracks is null) return;
        var track = Cylinder * 2 + Head;
        var present = track < _tracks.Length;
        _currentTrack = present ? _tracks[track] : null;
        _currentUnstableMask = present ? _unstableMasks?[track] : null;
        var previousDeadlines = _currentDeadlines;
        _currentDeadlines = present ? _cellDeadlines?[track] : null;
        var bits = present ? _trackLengths?[track] ?? LightweightDiskSerial.TrackBits : LightweightDiskSerial.TrackBits;
        if (_trackBits == bits && ReferenceEquals(previousDeadlines, _currentDeadlines)) return;
        _trackBits = bits;
        _bitRateDenominator = 10 * bits;
        _bitWholeCcks = LightweightPaulaAudio.PalCpuFrequency / _bitRateDenominator;
        _bitRemainder = LightweightPaulaAudio.PalCpuFrequency % _bitRateDenominator;
        if (preservePhase && NextBitCycle != long.MaxValue)
        {
            var elapsed = Math.Clamp((cycle - _revolutionStartCycle) / 2, 0, RevolutionCcks - 1);
            if (_currentDeadlines is { } times)
            {
                var found = Array.BinarySearch(times, (int)elapsed);
                BitPosition = found >= 0 ? found : ~found - 1;
                ScheduleFollowingBit();
                return;
            }
            BitPosition = (int)(elapsed * bits / RevolutionCcks);
            var ordinal = (long)BitPosition + 1;
            NextBitCycle = _revolutionStartCycle + 2 * ((ordinal * RevolutionCcks + bits - 1) / bits);
            _fraction = (int)((_bitRateDenominator - 1L + ordinal * _bitRemainder) % _bitRateDenominator);
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal int ReadBit()
    {
        var position = BitPosition;
        var mask = 0x80 >> (position & 7);
        if (_currentUnstableMask is { } unstable && (unstable[position >> 3] & mask) != 0)
            return (int)(Mix(_revolutionSeed ^ (uint)(Cylinder * 2 + Head) * 0x9E3779B9u ^ (uint)position) & 1);
        return _currentTrack is { } data && (data[position >> 3] & mask) != 0 ? 1 : 0;
    }

    private static uint Mix(uint value)
    {
        value ^= value >> 16;
        value = unchecked(value * 0x7FEB352Du);
        value ^= value >> 15;
        value = unchecked(value * 0x846CA68Bu);
        return value ^ (value >> 16);
    }

    // Ideal-ADF splice: preserve consecutive emitted cells in the encoded
    // grid. Nominal 300 RPM and Paula's 7-CCK write clock differ by a few
    // cells/revolution; this model does not simulate analog splice drift.
    internal void WriteBit(int bit, long cycle, bool slow)
    {
        if (!Selected || WriteProtected || _tracks is null || !MotorOn || cycle < ReadyCycle) return;
        var track = Cylinder * 2 + Head;
        if (track >= _tracks.Length) return;
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
        if (Format == LightweightFloppyFormat.Ipf) throw new InvalidOperationException("Preserved IPF tracks cannot be exported as standard ADF.");
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
