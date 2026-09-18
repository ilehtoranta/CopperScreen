using Copper68k;
using CopperDisk;

namespace CopperMod.Amiga.Lightweight;

// Mount-time controller and bounded host gateway services. No clock of its own.
internal sealed class LightweightHdfBus : IDisposable
{
    private readonly LightweightA500Machine _machine;
    private readonly AutoconfigChain _chain;
    private readonly Dictionary<uint, Action<M68kCpuState>> _callbacks = new();
    private readonly Dictionary<uint, uint> _fixedGateways = new();
    private uint _nextToken;
    internal CopperHdfController CopperHdf { get; }

    internal LightweightHdfBus(LightweightA500Machine machine, IEnumerable<AmigaHardfileConfiguration> configurations, LightweightHdfBus? previous = null)
    {
        _machine = machine;
        CopperHdf = new(configurations, previous?.CopperHdf);
        _chain = new AutoconfigChain([CopperHdf]);
    }

    internal void Reset()
    {
        _chain.ResetConfiguration();
        _callbacks.Clear();
        _fixedGateways.Clear();
        _nextToken = 0;
        CopperHdf.InstallBootstrapTraps(this);
    }

    internal byte ReadExpansionByte(uint address)
        => _chain.TryReadByte(address, out var value) || _chain.TryReadConfiguredByte(address, out value) ? value : (byte)255;

    internal bool TryWriteExpansionByte(uint address, byte value)
        => _chain.TryWriteByte(address, value) || _chain.TryWriteConfiguredByte(address, value);

    internal uint RegisterRelocatableHostGateway(Action<M68kCpuState> callback)
    {
        var token = ++_nextToken;
        _callbacks.Add(token, callback);
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
        // Only the three bootstrap entries are relocatable, within a complete
        // diagnostic area copied to RAM or the configured board's diagnostic ROM.
        var relative = token switch { 1 => 0x20u, 2 => 0x30u, 3 => 0x140u, _ => uint.MaxValue };
        if (relative == uint.MaxValue || address < relative) return false;
        var start = address - relative;
        if (!IsMappedMemoryRange(start, CopperHdfController.DiagAreaCopySize) &&
            start != CopperHdf.ConfiguredBase + CopperHdfController.DiagAreaOffset) return false;
        return ReadWord(start) == 0x9000 && ReadWord(start + 2) == CopperHdfController.DiagAreaCopySize;
    }

    internal bool TryInvokeHostGateway(uint address, uint token, M68kCpuState state)
    {
        if (!HasHostGateway(address) || ReadLong(address + 2) != token || !_callbacks.TryGetValue(token, out var callback)) return false;
        callback(state);
        return true;
    }

    internal bool IsMappedMemoryRange(uint address, int count) => _machine.IsHdfRamRange(address, count);
    internal byte ReadByte(uint address) => _machine.PeekHdfByte(address);
    internal ushort ReadWord(uint address) => (ushort)((ReadByte(address) << 8) | ReadByte(address + 1));
    internal uint ReadLong(uint address) => ((uint)ReadWord(address) << 16) | ReadWord(address + 2);
    internal void WriteByte(uint address, byte value, long cycle = 0) => _machine.PokeHdfByte(address, value);
    internal void WriteWord(uint address, ushort value) { WriteByte(address, (byte)(value >> 8)); WriteByte(address + 1, (byte)value); }
    internal void WriteLong(uint address, uint value) { WriteWord(address, (ushort)(value >> 16)); WriteWord(address + 2, (ushort)value); }
    internal void ClearMemory(uint address, int count) => _machine.HdfRam(address, count).Clear();
    internal void CopyToMemory(uint address, ReadOnlySpan<byte> data) => data.CopyTo(_machine.HdfRam(address, data.Length));
    internal void CopyFromMemory(uint address, Span<byte> data) => _machine.HdfRam(address, data.Length).CopyTo(data);
    public void Dispose() => CopperHdf.Dispose();
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
        return new(configuration, _enableConservativeCpuLoopBatch, _hdf);
    }
    public bool HasHostGateway(uint address) => _hdf is { } hdf && hdf.HasHostGateway(address);
    public bool TryInvokeHostGateway(uint address, uint token, M68kCpuState state)
        => _hdf is { } hdf && hdf.TryInvokeHostGateway(address, token, state);

    internal bool IsHdfRamRange(uint address, int count)
        => count >= 0 && ((address < _chipRam.Length && (ulong)address + (uint)count <= (uint)_chipRam.Length) ||
            (address >= 0xC00000 && (ulong)address + (uint)count <= 0xC00000u + (uint)_slowRam.Length));

    internal Span<byte> HdfRam(uint address, int count)
    {
        if (!IsHdfRamRange(address, count)) throw new ArgumentOutOfRangeException(nameof(address), "CopperHDF requires a contiguous guest RAM buffer.");
        return address < _chipRam.Length ? _chipRam.AsSpan((int)address, count) : _slowRam.AsSpan((int)(address - 0xC00000), count);
    }

    internal byte PeekHdfByte(uint address)
    {
        if (IsHdfRamRange(address, 1)) return HdfRam(address, 1)[0];
        return _hdf?.ReadExpansionByte(address) ?? (byte)255;
    }

    internal void PokeHdfByte(uint address, byte value)
    {
        if (IsHdfRamRange(address, 1)) HdfRam(address, 1)[0] = value;
        else if (_hdf?.TryWriteExpansionByte(address, value) != true) throw new ArgumentOutOfRangeException(nameof(address));
    }
}
