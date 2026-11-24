using System.Buffers.Binary;
using System.Security.Cryptography;
using Copper68k;

namespace CopperMod.Amiga.Lightweight;

public sealed partial class LightweightA500Machine
{
    private const int Cd32RomBankSize = 512 * 1024;
    private const uint Cd32KickstartAlias = 0x00A8_0000;
    private const uint Cd32ExtensionAlias = 0x00B0_0000;
    private const string Cd32KickstartSha1 = "3525BE8887F79B5929E017B42380A79EDFEE542D";
    private const string Cd32ExtensionSha1 = "5BEF3D628CE59CC02A66E6E4AE0DA48F60E78F7F";
    private const string Cd32PhysicalDumpSha1 = "816CE6C5077875850C7D43452230A9BA3A2902DB";

    private readonly LightweightAkiko? _akiko;
    private readonly LightweightCd32Pads? _cd32Pads;
    private readonly uint _overlaySize;
    private bool _cd32OverlayUsesExtension;
    private int _cd32TodLines;

    public LightweightCd32Region? Cd32Region => _configuration.Cd32Region;
    public int MotherboardClockHz { get; }

    private static void ValidateCd32Configuration(LightweightA500Configuration configuration)
    {
        if (configuration.Cd32Region is not { } region) return;
        if (!Enum.IsDefined(region) || configuration.AgnusModel != LightweightAgnusModel.Mos8374Alice ||
            configuration.DeniseModel != LightweightDeniseModel.Lisa4203 || configuration.CpuModel != M68kCpuModel.M68EC020 ||
            configuration.ChipRamBytes != 2 * 1024 * 1024 || configuration.SlowRamBytes != 0 || configuration.FastRamBytes != 0 ||
            configuration.FloppyDriveCount != 0 || configuration.RtcEnabled || configuration.IdeMaster is not null ||
            configuration.Hardfiles.Count != 0 || configuration.HostDirectories.Count != 0)
            throw new ArgumentException("Stock CD32 requires Alice/Lisa, 68EC020, 2 MiB Chip RAM and no expansion hardware.", nameof(configuration));
    }

    /// <summary>Load a verified Kickstart 40.60 and CD32 extension, or one combined console dump.</summary>
    public void LoadCd32Rom(ReadOnlySpan<byte> kickstart, ReadOnlySpan<byte> extension = default)
    {
        RequireAkiko();
        if (extension.IsEmpty && kickstart.Length == 2 * Cd32RomBankSize)
        {
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
            extension = kickstart[Cd32RomBankSize..];
            kickstart = kickstart[..Cd32RomBankSize];
            if (MatchesRom(kickstart, Cd32ExtensionSha1))
            {
                var firstBank = kickstart;
                kickstart = extension;
                extension = firstBank;
            }
        }
        if (kickstart.Length != Cd32RomBankSize || extension.Length != Cd32RomBankSize ||
            !MatchesRom(kickstart, Cd32KickstartSha1) || !MatchesRom(extension, Cd32ExtensionSha1))
            throw new ArgumentException("Expected the verified CD32 Kickstart 40.60 and extended ROM.");
        LoadCd32RomBanks(kickstart, extension);
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
        _cd32OverlayUsesExtension = BinaryPrimitives.ReadUInt16BigEndian(extension) == 0x1114;
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

    /// <summary>Mount a read-only Mode 1 image or single-file BIN/CUE disc. A failed open preserves the current disc.</summary>
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

    private bool IsCd32RomAlias(uint address)
        => address is >= Cd32KickstartAlias and < 0x00B7_FFFF && address != Cd32ExtensionAlias - 1 &&
            _akiko is not null && _romLoaded;

    private ushort ReadCd32RomAlias(uint address)
        => address < Cd32ExtensionAlias
            ? ReadRomWord((int)(address - Cd32KickstartAlias))
            : BinaryPrimitives.ReadUInt16BigEndian(_extensionRom.AsSpan((int)(address - Cd32ExtensionAlias), 2));

    private ushort ReadOverlayWord(uint address)
    {
        var offset = (int)(address & (Cd32RomBankSize - 1));
        return _cd32OverlayUsesExtension
            ? BinaryPrimitives.ReadUInt16BigEndian(_extensionRom.AsSpan(offset, 2))
            : ReadRomWord(_romImageOffset + offset);
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
        => !IsHigherPriorityOutputOwned(cycle) && _blitter.LastBusOutputCycle != cycle;

    internal bool AkikoOwnsOutput(long cycle) => _akiko?.Cd.LastOutputCycle == cycle;

    internal byte ReadAkikoDmaByte(uint address)
    {
        var word = ReadChipWordBus(address);
        return (byte)((address & 1) == 0 ? word >> 8 : word);
    }

    internal void WriteAkikoDmaByte(uint address, byte value)
    {
        var word = ReadChipWordDma(address);
        WriteChipWordBus(address, (ushort)((address & 1) == 0
            ? (word & 0x00FF) | (value << 8)
            : (word & 0xFF00) | value));
    }

    internal void WriteAkikoDmaWord(uint address, ushort value) => WriteChipWordBus(address, value);

    private void CompleteAudioFrame(long cycle)
    {
        _paula.CompleteFrame(cycle);
        _akiko?.Cd.MixAudio(_paula.CompletedAudioSpan, cycle);
    }
}
