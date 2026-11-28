using System.Buffers.Binary;
using System.Security.Cryptography;
using Copper68k;

namespace CopperMod.Amiga.Lightweight;

public sealed partial class LightweightA500Machine
{
    private const int Cd32RomBankSize = 512 * 1024;
    private const string Cd32KickstartSha1 = "3525BE8887F79B5929E017B42380A79EDFEE542D";
    private const string Cd32ExtensionSha1 = "5BEF3D628CE59CC02A66E6E4AE0DA48F60E78F7F";
    private const string Cd32PhysicalDumpSha1 = "816CE6C5077875850C7D43452230A9BA3A2902DB";

    private readonly LightweightAkiko? _akiko;
    private readonly LightweightCd32Pads? _cd32Pads;
    private int _cd32TodLines;

    public LightweightCd32Region? Cd32Region => _configuration.Cd32Region;
    public int MotherboardClockHz { get; }

    private static void ValidateCd32Configuration(LightweightA500Configuration configuration)
    {
        if (configuration.Cd32Region is not { } region) return;
        if (!Enum.IsDefined(region) || configuration.AgnusModel != LightweightAgnusModel.Mos8374Alice ||
            configuration.DeniseModel != LightweightDeniseModel.Lisa4203 || configuration.CpuModel != M68kCpuModel.M68EC020 ||
            configuration.ChipRamBytes != 2 * 1024 * 1024 || configuration.SlowRamBytes != 0 ||
            configuration.FloppyDriveCount != 0 || configuration.RtcEnabled || configuration.IdeMaster is not null ||
            configuration.Hardfiles.Count != 0 || configuration.HostDirectories.Count != 0)
            throw new ArgumentException("CD32 requires Alice/Lisa, 68EC020 and 2 MiB Chip RAM. Only optional Zorro II Fast RAM is supported.", nameof(configuration));
    }

    /// <summary>Load a stock CD32 ROM pair, a physical console dump, or a CD32-capable CopperStart Full ROM pair.</summary>
    public void LoadCd32Rom(ReadOnlySpan<byte> kickstart, ReadOnlySpan<byte> extension = default)
    {
        RequireAkiko();
        if (extension.IsEmpty && kickstart.Length == 2 * Cd32RomBankSize)
        {
            var first = kickstart[..Cd32RomBankSize];
            var second = kickstart[Cd32RomBankSize..];
            if (IsCopperStartCd32Pair(second, first))
            {
                LoadCd32RomBanks(second, first);
                return;
            }
            if (IsCopperStartCd32Pair(first, second))
            {
                LoadCd32RomBanks(first, second);
                return;
            }
            if (MatchesRom(kickstart, Cd32PhysicalDumpSha1))
            {
                // This physical dump stores alternating word lanes in separate
                // halves, each in little-endian word order.
                var decoded = new byte[kickstart.Length];
                for (var word = 0; word < Cd32RomBankSize; word += 2)
                {
                    var output = decoded.AsSpan(word * 2, 4);
                    BinaryPrimitives.WriteUInt16BigEndian(output, BinaryPrimitives.ReadUInt16LittleEndian(kickstart[word..]));
                    BinaryPrimitives.WriteUInt16BigEndian(output[2..], BinaryPrimitives.ReadUInt16LittleEndian(kickstart[(word + Cd32RomBankSize)..]));
                }
                LoadCd32Rom(decoded.AsSpan(0, Cd32RomBankSize), decoded.AsSpan(Cd32RomBankSize));
                return;
            }
            extension = second;
            kickstart = first;
            if (MatchesRom(kickstart, Cd32ExtensionSha1))
            {
                var firstBank = kickstart;
                kickstart = extension;
                extension = firstBank;
            }
        }
        if (!IsCopperStartCd32Pair(kickstart, extension) &&
            (kickstart.Length != Cd32RomBankSize || extension.Length != Cd32RomBankSize ||
             !MatchesRom(kickstart, Cd32KickstartSha1) || !MatchesRom(extension, Cd32ExtensionSha1)))
            throw new ArgumentException("Choose the stock CD32 Kickstart and extended ROMs, or a CopperStart Full ROM pair.");
        LoadCd32RomBanks(kickstart, extension);
    }

    private static bool IsCopperStartCd32Pair(ReadOnlySpan<byte> reset, ReadOnlySpan<byte> extension)
    {
        if (reset.Length != Cd32RomBankSize || extension.Length != Cd32RomBankSize ||
            !extension.StartsWith("CSEE"u8) ||
            BinaryPrimitives.ReadUInt32BigEndian(extension[12..]) != 0x00E0_0010 ||
            !extension.Slice(16, 4).SequenceEqual("CD32"u8))
            return false;

        var stack = BinaryPrimitives.ReadUInt32BigEndian(reset);
        var entry = BinaryPrimitives.ReadUInt32BigEndian(reset[4..]);
        if ((stack & 1) != 0 || stack is < 4 or > 0x0020_0000 ||
            (entry & 1) != 0 || entry is < 0x00F8_0008 or >= 0x0100_0000)
            return false;

        return IsExtensionEntry(extension[4..]) && IsExtensionEntry(extension[8..]) &&
            IsExtensionEntry(extension[20..]) && IsExtensionEntry(extension[24..]) && IsExtensionEntry(extension[28..]);

        static bool IsExtensionEntry(ReadOnlySpan<byte> pointer)
        {
            var address = BinaryPrimitives.ReadUInt32BigEndian(pointer);
            return (address & 1) == 0 && address is >= 0x00E0_0020 and < 0x00E7_FFE4;
        }
    }

    internal void LoadCd32RomBanks(ReadOnlySpan<byte> kickstart, ReadOnlySpan<byte> extension)
    {
        RequireAkiko();
        if (kickstart.Length != Cd32RomBankSize || extension.Length != Cd32RomBankSize)
            throw new ArgumentException("Both CD32 ROM banks must contain 512 KiB.");
        kickstart.CopyTo(_rom);
        extension.CopyTo(_extensionRom);
        _romImageOffset = 0;
        _romImageLength = _extensionRomImageLength = Cd32RomBankSize;
        _romLoaded = _extensionRomLoaded = true;
        _overlayUsesExtension = BinaryPrimitives.ReadUInt16BigEndian(extension) == 0x1114;
        Reset();
    }

    private static bool MatchesRom(ReadOnlySpan<byte> image, string sha1)
        => Convert.ToHexString(SHA1.HashData(image)) == sha1;

    public ReadOnlyMemory<byte> Cd32Nvram => RequireAkiko().Eeprom.Contents;
    public bool IsCd32NvramDirty => RequireAkiko().Eeprom.Dirty;
    public void LoadCd32Nvram(ReadOnlySpan<byte> data)
    {
        RequireAkiko().Eeprom.Load(data);
        RefreshStorageSchedule(Cycle);
    }
    public void MarkCd32NvramSaved() => RequireAkiko().Eeprom.MarkSaved();

    /// <summary>List selectable CD32 disc entries in a ZIP without opening their contents.</summary>
    public static IReadOnlyList<string> ListCd32ArchiveDiscs(string path) => LightweightCd32DiscImage.ListZipDiscNames(path);

    /// <summary>Mount a read-only Mode 1 or CUE disc, either standalone or in a ZIP. A failed open preserves the current disc.</summary>
    public void MountCd32Image(string path)
    {
        var drive = RequireAkiko().Cd;
        var image = LightweightCd32DiscImage.Open(path);
        drive.Mount(image, Cycle);
        RefreshStorageSchedule(Cycle);
    }

    public void EjectCd32Disc()
    {
        RequireAkiko().Cd.Mount(null, Cycle);
        RefreshStorageSchedule(Cycle);
    }

    public bool IsCd32DiscMounted => _akiko?.Cd.IsMounted == true;

    public void SetCd32TurboReads(bool enabled) => RequireAkiko().Cd.TurboReads = enabled;

    private void PulseCd32Tod(long cycle)
    {
        if (--_cd32TodLines != 0) return;
        HandleCiaInterrupt(ciaA: true, _ciaA.PulseTod(cycle));
        _cd32TodLines = _akiko!.Cd.TodLinesPerPulse;
    }

    /// <summary>Attach a pad to a port, supply its buttons, or detach with null. Directions use SubmitInput.</summary>
    public void SetCd32PadButtons(int port, LightweightCd32Buttons? buttons)
    {
        RequireAkiko();
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)port, 1u);
        if (((byte)(buttons ?? 0) & 0x80) != 0)
            throw new ArgumentOutOfRangeException(nameof(buttons));
        var potgo = _registers.Read(0x034);
        _pots.Advance(Cycle, ReadControllerPotPins(potgo));
        _cd32Pads!.SetButtons(port, buttons, potgo);
    }

    private LightweightAkiko RequireAkiko()
    {
        ThrowIfDisposed();
        return _akiko ?? throw new InvalidOperationException("This motherboard has no CD32 hardware.");
    }

    private bool IsAkikoAddress(uint address) => LightweightAkiko.ContainsAddress(address) && _akiko is not null;

    private ushort ReadControllerPotPins(ushort potgo)
    {
        var pins = _controllers.ReadPotgor(potgo, _cd32Pads?.IsConnected(0) == true);
        return _cd32Pads?.PotPins(pins) ?? pins;
    }

    private void WriteAkiko(uint address, int size, uint value, long cycle)
    {
        _akiko!.Write(address, size, value, cycle);
        RefreshStorageSchedule(cycle);
    }

    private uint ReadAkiko(uint address, int size, long cycle)
    {
        var value = _akiko!.Read(address, size, cycle);
        if ((address & 0x3F) == 0x28) RefreshStorageSchedule(cycle);
        return value;
    }

    internal bool CanAkikoOwnOutput(long cycle)
        => !IsHigherPriorityOutputOwned(cycle) && _blitter.LastBusOutputCycle != cycle &&
            (!_akiko!.Cd.SectorTransferInProgress || !_blitter.HasCpuRequestAt(cycle));

    internal bool AkikoOwnsOutput(long cycle) => _akiko?.Cd.LastOutputCycle == cycle;

    internal byte ReadAkikoDmaByte(uint address)
    {
        address &= 0x00FF_FFFF;
        if (address >= _chipRam.Length)
            return _fastRam?.ReadBoardByte(address) ?? 0xFF;
        var word = ReadChipWordBus(address);
        return (byte)((address & 1) == 0 ? word >> 8 : word);
    }

    internal void WriteAkikoDmaByte(uint address, byte value)
    {
        address &= 0x00FF_FFFF;
        if (address >= _chipRam.Length)
        {
            if (_fastRam?.TryWriteBoardByte(address, value) == true) NotifyJitRamWrite(address, 1);
            return;
        }
        var word = ReadChipWordDma(address);
        WriteChipWordBus(address, (ushort)((address & 1) == 0
            ? (word & 0x00FF) | (value << 8)
            : (word & 0xFF00) | value));
    }

    internal void WriteAkikoDmaWord(uint address, ushort value)
    {
        address &= 0x00FF_FFFF;
        if (address < _chipRam.Length) WriteChipWordBus(address, value);
        else if (_fastRam is { } ram && ram.ContainsRange(address, 2))
        {
            BinaryPrimitives.WriteUInt16BigEndian(ram.Memory.AsSpan((int)(address - ram.ConfiguredBase), 2), value);
            NotifyJitRamWrite(address, 2);
        }
    }

    private void CompleteAudioFrame(long cycle)
    {
        _paula.CompleteFrame(cycle);
        _akiko?.Cd.MixAudio(_paula.CompletedAudioSpan, cycle);
    }
}
