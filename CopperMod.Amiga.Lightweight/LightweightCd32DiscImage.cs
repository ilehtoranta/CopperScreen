using System.Buffers.Binary;
using Microsoft.Win32.SafeHandles;

namespace CopperMod.Amiga.Lightweight;

// One sector in flight. Only the execution owner consumes results and advances
// the guest. The worker has no machine access.
internal sealed class LightweightCd32DiscImage : IDisposable
{
    internal readonly record struct Track(int Number, int StartLba, int IndexZeroLba, bool Audio);
    internal const int RawSectorBytes = 2352;
    private const int UserSectorBytes = 2048;
    private readonly SafeFileHandle _handle;
    private readonly int _sectorBytes;
    private readonly byte[] _sector = new byte[RawSectorBytes];
    private readonly AutoResetEvent _request = new(false);
    private readonly ManualResetEventSlim _ready = new(true);
    private readonly Thread _worker;
    private volatile bool _closing;
    private int _requestedLba = -1;
    private bool _readOk;

    private readonly Track[] _tracks;

    private LightweightCd32DiscImage(SafeFileHandle handle, int sectorBytes, int sectorCount, Track[] tracks)
    {
        _handle = handle;
        _sectorBytes = sectorBytes;
        SectorCount = sectorCount;
        _tracks = tracks;
        _worker = new Thread(ReadSectors) { IsBackground = true, Name = "CopperScreen CD image" };
        _worker.Start();
    }

    internal int SectorCount { get; }
    internal ReadOnlySpan<Track> Tracks => _tracks;

    internal Track GetTrack(int lba)
    {
        var index = _tracks.Length - 1;
        while (index > 0 && _tracks[index].IndexZeroLba > lba) index--;
        return _tracks[index];
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
                ? RawSectorBytes : throw new ArgumentException("Use a Mode 1 ISO, raw BIN/IMG, or single-file BIN/CUE disc image.", nameof(path));
        return Open(path, sectorBytes, [new(1, 0, 0, false)]);
    }

    private static LightweightCd32DiscImage Open(string path, int sectorBytes, Track[] tracks)
    {
        var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.RandomAccess);
        try
        {
            var length = RandomAccess.GetLength(handle);
            if (length < sectorBytes || length % sectorBytes != 0 || length / sectorBytes > 449850 ||
                tracks[^1].StartLba >= length / sectorBytes)
                throw new InvalidDataException("The image size does not match the CD sector format.");
            if (sectorBytes == RawSectorBytes && !tracks[0].Audio)
            {
                Span<byte> header = stackalloc byte[16];
                ReadExactly(handle, header, 0);
                ValidateRawDataHeader(header);
            }
            return new(handle, sectorBytes, (int)(length / sectorBytes), tracks);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    private static LightweightCd32DiscImage OpenCue(string cuePath)
    {
        string? bin = null;
        var tracks = new List<Track>();
        var number = 0;
        var audio = false;
        int? indexZero = null;
        foreach (var line in File.ReadLines(cuePath))
        {
            var fields = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            switch (fields.FirstOrDefault()?.ToUpperInvariant())
            {
                case null or "REM" or "TITLE" or "PERFORMER" or "SONGWRITER" or "CATALOG" or "ISRC":
                    continue;
                case "FILE":
                    if (bin is not null || fields.Length < 3 || !fields[^1].Equals("BINARY", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("This CUE reader requires one BINARY file.");
                    var description = line.Trim().AsSpan();
                    var file = description[fields[0].Length..^fields[^1].Length].Trim();
                    if (file.Length >= 2 && file[0] == '"' && file[^1] == '"')
                        file = file[1..^1];
                    bin = Path.GetFullPath(file.ToString(), Path.GetDirectoryName(Path.GetFullPath(cuePath))!);
                    break;
                case "TRACK":
                    if (bin is null || number != tracks.Count || fields.Length != 3 ||
                        !int.TryParse(fields[1], out number) || number != tracks.Count + 1 || number > 99)
                        throw new InvalidDataException("CUE track numbers must be consecutive and each track needs INDEX 01.");
                    audio = fields[2].Equals("AUDIO", StringComparison.OrdinalIgnoreCase);
                    if (!audio && !fields[2].Equals("MODE1/2352", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Supported CUE tracks are MODE1/2352 and AUDIO.");
                    indexZero = null;
                    break;
                case "INDEX" when fields.Length == 3 && fields[1] == "00":
                    if (number != tracks.Count + 1 || indexZero.HasValue)
                        throw new InvalidDataException("Unexpected CUE INDEX 00.");
                    indexZero = ParseMsf(fields[2]);
                    break;
                case "INDEX" when fields.Length == 3 && fields[1] == "01":
                    var start = ParseMsf(fields[2]);
                    var first = indexZero ?? start;
                    if (number != tracks.Count + 1 || first > start ||
                        (tracks.Count == 0 ? start != 0 : first <= tracks[^1].StartLba))
                        throw new InvalidDataException("CUE INDEX 01 positions must start at zero and increase.");
                    tracks.Add(new(number, start, first, audio));
                    break;
                default:
                    throw new InvalidDataException($"This CUE reader does not support {fields[0]} {fields.Skip(1).FirstOrDefault()}.");
            }
        }
        if (bin is null || tracks.Count == 0 || number != tracks.Count)
            throw new InvalidDataException("This CUE sheet has no complete disc layout.");
        return Open(bin, RawSectorBytes, tracks.ToArray());
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
                if (_sectorBytes == RawSectorBytes)
                {
                    ReadExactly(_handle, _sector, (long)_requestedLba * _sectorBytes);
                    if (IsData(_requestedLba)) ValidateRawDataHeader(_sector);
                }
                else
                {
                    ReadExactly(_handle, _sector.AsSpan(16, UserSectorBytes), (long)_requestedLba * _sectorBytes);
                    EncodeMode1(_sector, _requestedLba);
                }
                _readOk = true;
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException) { _readOk = false; }
            finally { _ready.Set(); }
        }
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
        _handle.Dispose();
    }
}
