using Copper68k;
using CopperDisk;

namespace CopperMod.Amiga.Lightweight;

// Mount-time controller and bounded host gateway services. No clock of its own.
internal sealed class LightweightHdfBus : IDisposable
{
    private readonly LightweightA500Machine _machine;
    private readonly Dictionary<uint, Action<M68kCpuState>> _callbacks = new();
    private readonly Dictionary<uint, uint> _fixedGateways = new();
    private uint _nextToken;
    private readonly Dictionary<uint, (uint Offset, Func<uint> RomBase, int Size)> _relocatable = new();
    internal CopperHdfController CopperHdf { get; }
    internal LightweightHostFsController? HostFs { get; }

    internal LightweightHdfBus(LightweightA500Machine machine, IEnumerable<AmigaHardfileConfiguration> configurations, LightweightHdfBus? previous = null,
        IReadOnlyList<LightweightHostDirectoryConfiguration>? hostDirectories = null)
    {
        _machine = machine;
        CopperHdf = new(configurations, previous?.CopperHdf);
        try
        {
            if (hostDirectories is { Count: > 0 })
            {
                var names = CopperHdf.Hardfiles.Values.SelectMany(h => h.GetMountablePartitions()).Select(p => p.DeviceName).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var mount in hostDirectories)
                    if (!names.Add(mount.DeviceName)) throw new ArgumentException($"Duplicate storage device name {mount.DeviceName}.");
                HostFs = new(hostDirectories);
            }
        }
        catch { CopperHdf.Dispose(); throw; }
    }

    internal void Reset()
    {
        _callbacks.Clear();
        _fixedGateways.Clear();
        _nextToken = 0;
        _relocatable.Clear();
        if (CopperHdf.IsPresent) CopperHdf.InstallBootstrapTraps(this);
        HostFs?.Install(this);
    }

    internal byte ReadExpansionByte(uint address)
        => _machine.ReadExpansionByte(address);

    internal bool TryWriteExpansionByte(uint address, byte value)
        => _machine.TryWriteExpansionByte(address, value);

    internal uint RegisterRelocatableHostGateway(Action<M68kCpuState> callback)
    {
        var token = ++_nextToken;
        _callbacks.Add(token, callback);
        return token;
    }

    internal uint RegisterBootstrapGateway(Action<M68kCpuState> callback, uint offset, Func<uint> romBase, int size)
    {
        var token = RegisterRelocatableHostGateway(callback);
        _relocatable.Add(token, (offset, romBase, size));
        return token;
    }

    internal void RegisterHostGateway(uint address, Action<M68kCpuState> callback)
    {
        var token = RegisterRelocatableHostGateway(callback);
        _fixedGateways.Add(address, token);
        WriteWord(address, 0xFF00);
        WriteLong(address + 2, token);
    }

    internal bool HasHostGateway(uint address)
    {
        if ((address & 1) != 0 || ReadWord(address) != 0xFF00) return false;
        var token = ReadLong(address + 2);
        if (_fixedGateways.TryGetValue(address, out var expected)) return token == expected;
        // Only registered bootstrap entries are relocatable, within a complete
        // diagnostic area copied to RAM or the configured board's diagnostic ROM.
        if (!_relocatable.TryGetValue(token, out var entry) || address < entry.Offset) return false;
        var start = address - entry.Offset;
        if (!IsMappedMemoryRange(start, entry.Size) && start != entry.RomBase()) return false;
        return ReadWord(start) == 0x9000 && ReadWord(start + 2) == entry.Size;
    }

    internal bool TryInvokeHostGateway(uint address, uint token, M68kCpuState state)
    {
        if (!HasHostGateway(address) || ReadLong(address + 2) != token || !_callbacks.TryGetValue(token, out var callback)) return false;
        callback(state);
        return true;
    }

    internal bool IsMappedMemoryRange(uint address, int count) => _machine.IsHdfRamRange(address, count);
    internal uint CpuAddress(uint address) => _machine.NormalizeCpuAddress(address);
    internal bool IsReadableMemoryRange(uint address, int count) => IsMappedMemoryRange(address, count) || _machine.IsHdfRomRange(address, count);
    internal byte ReadByte(uint address) => _machine.PeekHdfByte(address);
    internal ushort ReadWord(uint address) => (ushort)((ReadByte(address) << 8) | ReadByte(address + 1));
    internal uint ReadLong(uint address) => ((uint)ReadWord(address) << 16) | ReadWord(address + 2);
    internal void WriteByte(uint address, byte value, long cycle = 0) => _machine.PokeHdfByte(address, value);
    internal void WriteWord(uint address, ushort value) { WriteByte(address, (byte)(value >> 8)); WriteByte(address + 1, (byte)value); }
    internal void WriteLong(uint address, uint value) { WriteWord(address, (ushort)(value >> 16)); WriteWord(address + 2, (ushort)value); }
    internal void ClearMemory(uint address, int count)
    { _machine.HdfRam(address, count).Clear(); _machine.NotifyJitRamWrite(address, count); }
    internal void CopyToMemory(uint address, ReadOnlySpan<byte> data)
    { data.CopyTo(_machine.HdfRam(address, data.Length)); _machine.NotifyJitRamWrite(address, data.Length); }
    internal void CopyFromMemory(uint address, Span<byte> data) => _machine.HdfRam(address, data.Length).CopyTo(data);
    public void Dispose() { try { HostFs?.Dispose(); } finally { CopperHdf.Dispose(); } }
}

public sealed partial class LightweightA500Machine
{
    private readonly LightweightHdfBus? _hdf;
    internal LightweightHdfBus? HardfileBus => _hdf;
    /// <summary>Construct a stopped restart candidate, retaining shared hardfile
    /// handles until either machine is disposed. The caller must quiesce this
    /// machine first and must not execute both machines concurrently.</summary>
    public LightweightA500Machine CreateRestartCandidate(LightweightA500Configuration configuration)
    {
        ThrowIfDisposed();
        return new(configuration, _enableConservativeCpuLoopBatch, _hdf, _rtc);
    }
    public bool HasHostGateway(uint address) => _hdf is { } hdf && hdf.HasHostGateway(address);
    internal uint NormalizeCpuAddress(uint address)
        => CpuModel is M68kCpuModel.M68000 or M68kCpuModel.M68010 or M68kCpuModel.M68EC020 ? address & 0xFFFFFF : address;
    public bool TryInvokeHostGateway(uint address, uint token, M68kCpuState state)
        => _hdf is { } hdf && hdf.TryInvokeHostGateway(address, token, state);

    internal bool IsHdfRamRange(uint address, int count)
        => count >= 0 && ((address < _chipRam.Length && (ulong)address + (uint)count <= (uint)_chipRam.Length) ||
            (address >= 0xC00000 && (ulong)address + (uint)count <= 0xC00000u + (uint)_slowRam.Length) ||
            _fastRam?.ContainsRange(address, count) == true);

    internal Span<byte> HdfRam(uint address, int count)
    {
        if (!IsHdfRamRange(address, count)) throw new ArgumentOutOfRangeException(nameof(address), "CopperHDF requires a contiguous guest RAM buffer.");
        if (address < _chipRam.Length) return _chipRam.AsSpan((int)address, count);
        if (_fastRam is { } ram && ram.ContainsRange(address, count))
            return ram.Memory.AsSpan((int)(address - ram.ConfiguredBase), count);
        return _slowRam.AsSpan((int)(address - 0xC00000), count);
    }

    internal byte PeekHdfByte(uint address)
    {
        if (IsHdfRamRange(address, 1)) return HdfRam(address, 1)[0];
        if (address >= RomBase && address - RomBase < _rom.Length) return _rom[address - RomBase];
        return _hdf?.ReadExpansionByte(address) ?? (byte)255;
    }
    internal bool IsHdfRomRange(uint address, int count) => count >= 0 && address >= RomBase &&
        address - RomBase <= _rom.Length && (uint)count <= _rom.Length - (address - RomBase);

    internal void PokeHdfByte(uint address, byte value)
    {
        if (IsHdfRamRange(address, 1)) { HdfRam(address, 1)[0] = value; NotifyJitRamWrite(address, 1); }
        else if (_hdf?.TryWriteExpansionByte(address, value) != true) throw new ArgumentOutOfRangeException(nameof(address));
    }
}
