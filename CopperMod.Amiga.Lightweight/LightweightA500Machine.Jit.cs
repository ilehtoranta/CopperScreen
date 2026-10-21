using Copper68k;

namespace CopperMod.Amiga.Lightweight;

public sealed partial class LightweightA500Machine
{
    private readonly uint[]? _jitPageGenerations;
    private readonly bool[]? _jitObservedPages;
    private uint _jitWriteGeneration, _cpuMemoryMapGeneration;
    internal event Action<uint, int>? JitCodeRangeWritten;
    internal uint CpuMemoryMapGeneration => _cpuMemoryMapGeneration;
    /// <summary>Selected CPU execution mode.</summary>
    public M68kExecutionMode CpuExecutionMode => _configuration.CpuExecutionMode;
    /// <summary>JIT counters, or null for interpreter execution. Read on the execution owner thread.</summary>
    public M68kJitStatistics? JitStatistics => (_cpu as IM68kJitDiagnostics)?.GetJitStatistics();

    // Compilation is deliberately confined to one 256-byte page. This keeps
    // both endpoint guards sufficient and snapshots bounded and immutable.
    internal bool IsJitCodeAddress(uint address, int bytes, bool readOnly = false)
    {
        if (bytes <= 0 || (address & 1) != 0 || (address & 255) + (uint)bytes > 256)
            return false;
        for (var offset = 0; offset < bytes; offset += 2)
            if (!TryGetJitMemoryKey(address + (uint)offset, out _, out var rom) ||
                readOnly && !rom || HasHostGateway(address + (uint)offset)) return false;
        return true;
    }

    private bool TryGetJitMemoryKey(uint address, out uint key, out bool readOnly)
    {
        key = address; readOnly = false;
        if (address > 0xFFFFFF)
        {
            if (_fastRam?.ContainsRange(address, 1) != true) return false;
            key = 0x1000000 + address - _fastRam.ConfiguredBase;
            return true;
        }
        if (_overlayEnabled && address < 0x400)
        { key = RomBase + (uint)_romImageOffset + address; readOnly = true; return true; }
        if (address < ChipRamCpuDecodeSize)
        { key = address & _chipRamCpuAddressMask; return key < _chipRam.Length; }
        if (address >= 0xC00000 && address < 0xC00000 + _slowRam.Length || _fastRam?.ContainsRange(address, 1) == true)
            return true;
        if (_extensionRomLoaded && address >= ExtensionRomBase &&
            address < ExtensionRomBase + _extensionRomImageLength)
        { readOnly = true; return true; }
        if (address >= RomBase && address < RomBase + _rom.Length)
        { readOnly = true; return true; }
        return false;
    }

    internal uint GetJitCodePageGeneration(uint address)
    {
        if (_jitPageGenerations is null || !TryGetJitMemoryKey(address, out var key, out _)) return uint.MaxValue;
        _jitObservedPages![key >> 8] = true;
        return _jitPageGenerations[key >> 8];
    }

    internal ushort ReadJitCodeWord(uint address)
        => IsJitCodeAddress(address, 2) && TryPeekInstructionWord(address, out var value)
            ? value : throw new InvalidOperationException("JIT code access must refer to mapped RAM/ROM, without a host gateway.");

    internal bool JitCodeRangeGenerationMatches(uint address, int bytes, uint start, uint end)
        => IsJitCodeAddress(address, bytes) && GetJitCodePageGeneration(address) == start &&
            GetJitCodePageGeneration(address + (uint)bytes - 1) == end;

    internal bool TryCaptureJitCodeSnapshot(uint root, int maxBytes, out M68kJitCodeSnapshot snapshot)
    {
        snapshot = default;
        if (_jitPageGenerations is null || maxBytes < 2) return false;
        var count = Math.Min(maxBytes, 256 - (int)(root & 255)) & ~1;
        if (!IsJitCodeAddress(root, count)) return false;
        var bytes = new byte[count];
        for (var offset = 0; offset < count; offset += 2)
        { var word = ReadJitCodeWord(root + (uint)offset); bytes[offset] = (byte)(word >> 8); bytes[offset + 1] = (byte)word; }
        snapshot = new(root, bytes, new M68kCodeGenerationStamp([root & ~255u], [GetJitCodePageGeneration(root)]), []);
        return true;
    }

    internal void NotifyJitRamWrite(uint address, int bytes)
    {
        if (_jitPageGenerations is null || bytes <= 0) return;
        // RAM aliases share a generation. Publish each Chip alias as well so
        // suspended compiled callers cannot retain an alias of modified code.
        for (var offset = 0; offset < bytes;)
        {
            var current = address + (uint)offset;
            var canonical = current < ChipRamCpuDecodeSize ? current & _chipRamCpuAddressMask : current;
            if (canonical > 0xFFFFFF)
            {
                if (_fastRam?.ContainsRange(current, 1) != true) break;
                canonical = 0x1000000 + current - _fastRam.ConfiguredBase;
            }
            var page = canonical >> 8;
            _jitPageGenerations[page] = ++_jitWriteGeneration;
            var count = Math.Min(bytes - offset, 256 - (int)(canonical & 255));
            if (_jitObservedPages![page])
            {
                if (current < ChipRamCpuDecodeSize)
                    for (var alias = canonical; alias < ChipRamCpuDecodeSize; alias += _chipRamCpuAddressMask + 1)
                        JitCodeRangeWritten?.Invoke(alias, count);
                else JitCodeRangeWritten?.Invoke(current, count);
            }
            offset += count;
        }
    }

    private void InvalidateCpuMemoryMap()
    {
        _cpuMemoryMapGeneration++;
        if (_jitPageGenerations is null) return;
        Array.Fill(_jitPageGenerations, ++_jitWriteGeneration);
        Array.Clear(_jitObservedPages!);
        JitCodeRangeWritten?.Invoke(0, 0x1000000);
        if (_fastRam is { IsConfigured: true } ram && ram.Identity.BusKind == AutoconfigBusKind.ZorroIII)
            JitCodeRangeWritten?.Invoke(ram.ConfiguredBase, ram.Memory.Length);
    }
}
