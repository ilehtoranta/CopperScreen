using System.Buffers.Binary;
using Microsoft.Win32.SafeHandles;
using NLayer;

namespace CopperMod.Amiga.Lightweight;

// One sector in flight. Only the execution owner consumes results and advances
// the guest. The worker has no machine access.
internal sealed class LightweightCd32DiscImage : IDisposable
{
    internal readonly record struct Track(int Number, int StartLba, int IndexZeroLba, bool Audio);
    private readonly record struct CueTrack(int Start, int First, int Pregap, int SectorBytes, bool Audio);
    private readonly record struct TrackSource(SafeFileHandle? Handle, long Offset, int FirstLba, int SectorBytes,
        MpegFile? Mp3 = null);

    internal const int RawSectorBytes = 2352;
    private const int UserSectorBytes = 2048;
    private const int MaxSectors = 449850;
    private const int SectorsPerSecond = 75;
    private const int AudioFramesPerSector = RawSectorBytes / (2 * sizeof(short));
    private const int MaxMp3FramesPerSector = 48000 / SectorsPerSecond;
    private readonly IDisposable[] _files;
    private readonly TrackSource[] _sources;
    private readonly byte[] _sector = new byte[RawSectorBytes];
    private readonly AutoResetEvent _request = new(false);
    private readonly ManualResetEventSlim _ready = new(true);
    private readonly Thread _worker;
    private volatile bool _closing;
    private int _requestedLba = -1;
    private bool _readOk;

    private readonly Track[] _tracks;

    private LightweightCd32DiscImage(IDisposable[] files, TrackSource[] sources, int sectorCount, Track[] tracks)
    {
        _files = files;
        _sources = sources;
        SectorCount = sectorCount;
        _tracks = tracks;
        _worker = new Thread(ReadSectors) { IsBackground = true, Name = "CopperScreen CD image" };
        _worker.Start();
    }

    internal int SectorCount { get; }
    internal ReadOnlySpan<Track> Tracks => _tracks;

    internal Track GetTrack(int lba) => _tracks[GetTrackIndex(lba)];

    private int GetTrackIndex(int lba)
    {
        var index = _tracks.Length - 1;
        while (index > 0 && _tracks[index].IndexZeroLba > lba) index--;
        return index;
    }

    internal bool IsAudio(int lba) => (uint)lba < (uint)SectorCount && GetTrack(lba).Audio;
    internal bool IsData(int lba) => (uint)lba < (uint)SectorCount && !GetTrack(lba).Audio;

    internal static LightweightCd32DiscImage Open(string path)
    {
        var extension = Path.GetExtension(path);
        if (extension.Equals(".cue", StringComparison.OrdinalIgnoreCase))
            return OpenCue(path);
        var sectorBytes = extension.Equals(".iso", StringComparison.OrdinalIgnoreCase) ? UserSectorBytes :
            extension.Equals(".bin", StringComparison.OrdinalIgnoreCase) || extension.Equals(".img", StringComparison.OrdinalIgnoreCase)
                ? RawSectorBytes : throw new ArgumentException("Use a Mode 1 ISO, raw BIN/IMG, or CUE disc image.", nameof(path));
        var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.RandomAccess);
        try
        {
            var sectors = GetSectorCount(RandomAccess.GetLength(handle), sectorBytes);
            if (sectorBytes == RawSectorBytes) ValidateRawDataHeader(handle, 0);
            return new([handle], [new(handle, 0, 0, sectorBytes)], sectors, [new(1, 0, 0, false)]);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    private static LightweightCd32DiscImage OpenCue(string cuePath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(cuePath))!;
        var files = new List<IDisposable>();
        var sources = new List<TrackSource>();
        var tracks = new List<Track>();
        var fileTracks = new List<CueTrack>();
        string? path = null;
        var wave = false;
        var mp3 = false;
        var sectorCount = 0;
        var number = 0;
        var sectorBytes = 0;
        var audio = false;
        int? indexZero = null;
        int? pregap = null;
        try
        {
            foreach (var line in File.ReadLines(cuePath))
            {
                var fields = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                var complete = number == tracks.Count + fileTracks.Count;
                switch (fields.FirstOrDefault()?.ToUpperInvariant())
                {
                    case null or "REM" or "TITLE" or "PERFORMER" or "SONGWRITER" or "CATALOG" or "ISRC":
                        continue;
                    case "FILE":
                        if (path is not null) FinishFile();
                        wave = fields[^1].Equals("WAVE", StringComparison.OrdinalIgnoreCase);
                        mp3 = fields[^1].Equals("MP3", StringComparison.OrdinalIgnoreCase);
                        if (fields.Length < 3 || (!wave && !mp3 && !fields[^1].Equals("BINARY", StringComparison.OrdinalIgnoreCase)))
                            throw new InvalidDataException("Supported CUE file types are BINARY, WAVE and MP3.");
                        var description = line.Trim().AsSpan();
                        var file = description[fields[0].Length..^fields[^1].Length].Trim();
                        if (file.Length >= 2 && file[0] == '"' && file[^1] == '"')
                            file = file[1..^1];
                        path = Path.GetFullPath(file.ToString(), directory);
                        break;
                    case "TRACK":
                        if (path is null || !complete || fields.Length != 3 ||
                            !int.TryParse(fields[1], out var next) || next != number + 1 || next > 99)
                            throw new InvalidDataException("CUE track numbers must be consecutive and each track needs INDEX 01.");
                        number = next;
                        var mode = fields[2].ToUpperInvariant();
                        audio = mode == "AUDIO";
                        sectorBytes = mode switch
                        {
                            "AUDIO" or "MODE1/2352" => RawSectorBytes,
                            "MODE1/2048" => UserSectorBytes,
                            _ => throw new InvalidDataException("Supported CUE tracks are MODE1/2048, MODE1/2352 and AUDIO.")
                        };
                        if (((wave || mp3) && !audio) || (fileTracks.Count != 0 && sectorBytes != fileTracks[0].SectorBytes))
                            throw new InvalidDataException("Tracks sharing a CUE file must have the same sector size. WAVE and MP3 files hold only audio.");
                        indexZero = pregap = null;
                        break;
                    case "PREGAP" when !complete && fields.Length == 2 && !pregap.HasValue:
                        pregap = ParseMsf(fields[1]);
                        break;
                    case "INDEX" when !complete && fields.Length == 3 && fields[1] == "00" && !indexZero.HasValue:
                        indexZero = ParseMsf(fields[2]);
                        break;
                    case "INDEX" when !complete && fields.Length == 3 && fields[1] == "01":
                        var start = ParseMsf(fields[2]);
                        var first = indexZero ?? start;
                        if (first > start || (fileTracks.Count == 0 ? first != 0 : first <= fileTracks[^1].Start) ||
                            (number == 1 && (start != 0 || pregap.GetValueOrDefault() != 0)))
                            throw new InvalidDataException("CUE indexes must start at zero for each file and increase within it.");
                        fileTracks.Add(new(start, first, pregap.GetValueOrDefault(), sectorBytes, audio));
                        break;
                    default:
                        throw new InvalidDataException($"Unsupported or misplaced CUE entry: {line.Trim()}.");
                }
            }
            FinishFile();
            return new(files.ToArray(), sources.ToArray(), sectorCount, tracks.ToArray());
        }
        catch
        {
            foreach (var file in files) file.Dispose();
            throw;
        }

        void FinishFile()
        {
            if (path is null || fileTracks.Count == 0 || number != tracks.Count + fileTracks.Count)
                throw new InvalidDataException("Each CUE file needs a complete track layout.");

            path = ResolveCueCompanion(path);
            SafeFileHandle? handle = null;
            MpegFile? decoder = null;
            long offset = 0;
            var bytes = fileTracks[0].SectorBytes;
            int sectors;
            if (mp3)
            {
                var input = File.OpenRead(path);
                files.Add(input);
                decoder = new MpegFile(input);
                if (decoder.SampleRate is not (44100 or 48000) || decoder.Channels is < 1 or > 2)
                    throw new InvalidDataException("CD audio MP3 files require 44.1 or 48 kHz mono or stereo audio.");
                var framesPerSector = decoder.SampleRate / SectorsPerSecond;
                var frames = decoder.Length / (sizeof(float) * decoder.Channels);
                if (frames <= 0 || frames > (long)MaxSectors * framesPerSector)
                    throw new InvalidDataException("The decoded MP3 length is outside the supported CD size.");
                sectors = (int)((frames + framesPerSector - 1) / framesPerSector);
            }
            else
            {
                handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.RandomAccess);
                files.Add(handle);
                var length = RandomAccess.GetLength(handle);
                if (wave)
                {
                    (offset, length) = ReadWaveData(handle, length);
                    // WinUAE counts complete CD sectors in PCM tracks when building the TOC.
                    length -= length % RawSectorBytes;
                }
                sectors = GetSectorCount(length, bytes);
            }
            if (fileTracks[^1].Start >= sectors)
                throw new InvalidDataException("A CUE track starts past the end of its file.");

            foreach (var track in fileTracks)
            {
                var firstLba = sectorCount + track.First;
                sectorCount += track.Pregap;
                var fileOffset = offset + (long)track.First * bytes;
                if (!track.Audio && bytes == RawSectorBytes) ValidateRawDataHeader(handle!, fileOffset);
                sources.Add(new(handle, fileOffset, firstLba + track.Pregap, bytes, decoder));
                tracks.Add(new(tracks.Count + 1, sectorCount + track.Start, firstLba, track.Audio));
            }
            sectorCount += sectors;
            if (sectorCount > MaxSectors) throw new InvalidDataException("The CUE disc exceeds the supported CD size.");
            fileTracks.Clear();
        }
    }

    private static string ResolveCueCompanion(string path)
    {
        if (File.Exists(path)) return path;
        var directory = Path.GetDirectoryName(path)!;
        if (!Directory.Exists(directory)) return path;
        var name = Path.GetFileName(path);
        string? matched = null;
        foreach (var file in Directory.EnumerateFiles(directory))
        {
            if (!Path.GetFileName(file).Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
            if (matched is not null)
                throw new InvalidDataException($"CUE companion name is ambiguous: {name}.");
            matched = file;
        }
        return matched ?? path;
    }

    private static int GetSectorCount(long length, int sectorBytes)
    {
        if (length < sectorBytes || length % sectorBytes != 0 || length / sectorBytes > MaxSectors)
            throw new InvalidDataException("The image size does not match the CD sector format.");
        return (int)(length / sectorBytes);
    }

    private static (long Offset, long Length) ReadWaveData(SafeFileHandle handle, long length)
    {
        Span<byte> header = stackalloc byte[16];
        ReadExactly(handle, header[..12], 0);
        var end = 8L + BinaryPrimitives.ReadUInt32LittleEndian(header[4..]);
        if (!header[..4].SequenceEqual("RIFF"u8) || !header[8..12].SequenceEqual("WAVE"u8) || end > length)
            throw new InvalidDataException("Invalid RIFF WAVE file.");

        var pcm = false;
        for (long offset = 12; offset <= end - 8;)
        {
            ReadExactly(handle, header[..8], offset);
            var size = BinaryPrimitives.ReadUInt32LittleEndian(header[4..]);
            offset += 8;
            if (size + (size & 1L) > end - offset)
                throw new InvalidDataException("A WAVE chunk extends beyond the RIFF data.");
            if (header[..4].SequenceEqual("fmt "u8) && size >= 16)
            {
                ReadExactly(handle, header, offset);
                pcm = BinaryPrimitives.ReadUInt16LittleEndian(header) == 1 &&
                      BinaryPrimitives.ReadUInt16LittleEndian(header[2..]) == 2 &&
                      BinaryPrimitives.ReadUInt32LittleEndian(header[4..]) == 44100 &&
                      BinaryPrimitives.ReadUInt16LittleEndian(header[12..]) == 4 &&
                      BinaryPrimitives.ReadUInt16LittleEndian(header[14..]) == 16;
            }
            else if (header[..4].SequenceEqual("data"u8) && pcm)
            {
                if (size % 4 != 0)
                    throw new InvalidDataException("CD audio WAVE data ends inside a stereo sample frame.");
                return (offset, size);
            }
            offset += size + (size & 1L);
        }
        throw new InvalidDataException("CD audio WAVE files require 44.1 kHz stereo 16-bit PCM.");
    }

    private static int ParseMsf(string text)
    {
        var fields = text.Split(':');
        if (fields.Length != 3 || !uint.TryParse(fields[0], out var minutes) || minutes > 99 ||
            !uint.TryParse(fields[1], out var seconds) || seconds > 59 ||
            !uint.TryParse(fields[2], out var frames) || frames > 74)
            throw new InvalidDataException("Invalid MSF field in CUE sheet.");
        return (int)((minutes * 60 + seconds) * 75 + frames);
    }

    internal bool Request(int lba)
    {
        if ((uint)lba >= (uint)SectorCount) return false;
        if (_requestedLba == lba) return true;
        _ready.Wait();
        _requestedLba = lba;
        _ready.Reset();
        _request.Set();
        return true;
    }

    internal bool CopyRequestedSector(Span<byte> destination)
    {
        _ready.Wait();
        if (!_readOk) return false;
        _sector.CopyTo(destination);
        return true;
    }

    private void ReadSectors()
    {
        while (true)
        {
            _request.WaitOne();
            if (_closing) break;
            try
            {
                var track = GetTrackIndex(_requestedLba);
                var source = _sources[track];
                var relativeLba = _requestedLba - source.FirstLba;
                var offset = source.Offset + (long)relativeLba * source.SectorBytes;
                if (relativeLba < 0)
                {
                    // PREGAP sectors are generated by the drive and have no file bytes.
                    _sector.AsSpan().Clear();
                    if (!_tracks[track].Audio) EncodeMode1(_sector, _requestedLba);
                }
                else if (source.Mp3 is { } decoder)
                {
                    ReadMp3Sector(decoder, offset / RawSectorBytes);
                }
                else if (source.SectorBytes == RawSectorBytes)
                {
                    ReadExactly(source.Handle!, _sector, offset);
                    if (!_tracks[track].Audio) ValidateRawDataHeader(_sector);
                }
                else
                {
                    ReadExactly(source.Handle!, _sector.AsSpan(16, UserSectorBytes), offset);
                    EncodeMode1(_sector, _requestedLba);
                }
                _readOk = true;
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException) { _readOk = false; }
            finally { _ready.Set(); }
        }
    }

    private void ReadMp3Sector(MpegFile decoder, long sector)
    {
        var channels = decoder.Channels;
        var sourceFrames = decoder.SampleRate / SectorsPerSecond;
        var sampleCount = sourceFrames * channels;
        var position = sector * sampleCount * sizeof(float);
        Span<float> samples = stackalloc float[MaxMp3FramesPerSector * 2];
        if (decoder.Position != position)
        {
            // NLayer seeks to an MPEG frame boundary. Consume the remaining
            // samples to reach this CD sector before filling its PCM buffer.
            try { decoder.Position = position; }
            catch (ArgumentOutOfRangeException ex)
            {
                throw new InvalidDataException("The MP3 ended before the requested CD sector.", ex);
            }
            while (decoder.Position < position)
            {
                var skip = (int)Math.Min((position - decoder.Position) / sizeof(float), samples.Length);
                if (decoder.ReadSamples(samples[..skip]) == 0)
                    throw new EndOfStreamException("The MP3 ended before the requested CD sector.");
            }
        }
        var decodedFrames = decoder.ReadSamples(samples[..sampleCount]) / channels;
        var frames = (decodedFrames * AudioFramesPerSector + sourceFrames - 1) / sourceFrames;
        for (var frame = 0; frame < frames; frame++)
        {
            // The supported input rates have a whole number of samples per CD sector.
            var sourceFrame = Math.DivRem(frame * sourceFrames, AudioFramesPerSector, out var fraction);
            var first = sourceFrame * channels;
            var next = Math.Min(sourceFrame + 1, decodedFrames - 1) * channels;
            var weight = (float)fraction / AudioFramesPerSector;
            var left = samples[first] + (samples[next] - samples[first]) * weight;
            var right = samples[first + channels - 1] +
                        (samples[next + channels - 1] - samples[first + channels - 1]) * weight;
            BinaryPrimitives.WriteInt16LittleEndian(_sector.AsSpan(frame * 4), ToPcm16(left));
            BinaryPrimitives.WriteInt16LittleEndian(_sector.AsSpan(frame * 4 + 2), ToPcm16(right));
        }
        _sector.AsSpan(frames * 4).Clear();
    }

    private static short ToPcm16(float sample)
        => (short)Math.Clamp(sample * 32768f, short.MinValue, short.MaxValue);

    private static void ValidateRawDataHeader(SafeFileHandle handle, long offset)
    {
        Span<byte> header = stackalloc byte[16];
        ReadExactly(handle, header, offset);
        ValidateRawDataHeader(header);
    }

    private static void ValidateRawDataHeader(ReadOnlySpan<byte> header)
    {
        if (header[0] != 0 || header[1..11].ContainsAnyExcept((byte)0xFF) || header[11] != 0 || header[15] != 1)
            throw new InvalidDataException("The raw data sector is not CD-ROM Mode 1.");
    }

    private static void ReadExactly(SafeFileHandle handle, Span<byte> buffer, long offset)
    {
        while (!buffer.IsEmpty)
        {
            var read = RandomAccess.Read(handle, buffer, offset);
            if (read == 0) throw new EndOfStreamException("The CD image ended inside a sector.");
            buffer = buffer[read..];
            offset += read;
        }
    }

    private static void EncodeMode1(Span<byte> sector, int lba)
    {
        sector[0] = sector[11] = 0;
        sector[1..11].Fill(0xFF);
        WriteMsf(sector[12..15], lba + 150);
        sector[15] = 1;
        uint edc = 0;
        foreach (var data in sector[..2064])
        {
            edc ^= data;
            for (var bit = 0; bit < 8; bit++)
                edc = (edc >> 1) ^ ((edc & 1) == 0 ? 0 : 0xD8018001);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(sector[2064..], edc);
        sector[2068..2076].Clear();
        WriteParity(sector[12..2076], sector[2076..2248], 86, 24, 2, 86);
        WriteParity(sector[12..2248], sector[2248..], 52, 43, 86, 88);
    }

    // ECMA-130 Annex A. P codewords traverse columns. Q codewords traverse
    // diagonals and include the completed P parity bytes.
    private static void WriteParity(ReadOnlySpan<byte> data, Span<byte> parity,
        int words, int symbols, int firstStride, int step)
    {
        for (var word = 0; word < words; word++)
        {
            var source = word / 2 * firstStride + (word & 1);
            byte weighted = 0;
            byte total = 0;
            for (var symbol = 0; symbol < symbols; symbol++)
            {
                var value = data[source];
                total ^= value;
                weighted = MultiplyByTwo((byte)(weighted ^ value));
                source += step;
                if (source >= data.Length) source -= data.Length;
            }
            // In GF(256), the inverse of (1 + alpha) is 0xF4.
            var p = Multiply((byte)(MultiplyByTwo(weighted) ^ total), 0xF4);
            parity[word] = p;
            parity[words + word] = (byte)(p ^ total);
        }
    }

    private static byte MultiplyByTwo(byte value) => (byte)((value << 1) ^ ((value & 0x80) != 0 ? 0x11D : 0));

    private static byte Multiply(byte value, int multiplier)
    {
        byte result = 0;
        while (multiplier != 0)
        {
            if ((multiplier & 1) != 0) result ^= value;
            value = MultiplyByTwo(value);
            multiplier >>= 1;
        }
        return result;
    }

    internal static byte ToBcd(int value) => (byte)((value / 10 << 4) | (value % 10));
    internal static void WriteMsf(Span<byte> destination, int sectors)
    {
        destination[0] = ToBcd(sectors / (60 * 75));
        destination[1] = ToBcd(sectors / 75 % 60);
        destination[2] = ToBcd(sectors % 75);
    }

    public void Dispose()
    {
        _closing = true;
        _request.Set();
        _worker.Join();
        _request.Dispose();
        _ready.Dispose();
        foreach (var file in _files) file.Dispose();
    }
}
