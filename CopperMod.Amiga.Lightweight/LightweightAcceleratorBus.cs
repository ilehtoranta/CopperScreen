using Copper68k;

namespace CopperMod.Amiga.Lightweight;

// Experimental OCS accelerator/A1200 bridge. Copper68k converts native CPU
// clocks to the 7.09 MHz motherboard domain. All transfers below use that same
// clock and the fitted motherboard bus, including split unaligned operands.
// This is a bounded bridge policy, not a model of a particular accelerator card.
internal class LightweightAcceleratorBus(LightweightA500Machine machine, M68kCpuModel model)
    : IM68kBus, IM68kCodeReader, IM68kJitBus, IM68kStablePhysicalAddressMap, IM68kFixedPhysicalAddressMap
{
    private readonly uint _addressMask = model == M68kCpuModel.M68EC020 ? 0x00FF_FFFFu : uint.MaxValue;
    protected LightweightA500Machine Machine => machine;
    // The existing floating-bus policy is preserved for unmapped addresses.
    public bool IsCpuPhysicalAddressMapped(uint address, int bytes, M68kBusAccessKind kind) => true;
    public bool TryGetCpuPhysicalAddressMappedRange(M68kBusAccessKind kind, out uint startAddress, out uint endAddress)
    {
        startAddress = 0;
        endAddress = uint.MaxValue;
        return true;
    }
    public uint CpuPhysicalAddressMapGeneration => machine.CpuMemoryMapGeneration;
    public event Action<uint, int>? JitCodeRangeWritten
    { add => machine.JitCodeRangeWritten += value; remove => machine.JitCodeRangeWritten -= value; }
    public bool IsJitCodeAddress(uint address, int bytes, M68kBusAccessKind kind)
        => machine.IsJitCodeAddress(address, bytes);
    public bool IsJitReadOnlyCodeAddress(uint address, int bytes, M68kBusAccessKind kind)
        => machine.IsJitCodeAddress(address, bytes, readOnly: true);
    public ushort ReadJitCodeWord(uint address) => machine.ReadJitCodeWord(address);
    public uint GetJitCodePageGeneration(uint address) => machine.GetJitCodePageGeneration(address);
    public bool JitCodeRangeGenerationMatches(uint address, int bytes, uint start, uint end)
        => machine.JitCodeRangeGenerationMatches(address, bytes, start, end);
    public bool TryCaptureJitCodeSnapshot(uint root, int maxBytes, out M68kJitCodeSnapshot snapshot)
        => machine.TryCaptureJitCodeSnapshot(root, maxBytes, out snapshot);
    public bool CanCaptureJitCodeSnapshot(uint root, int maxBytes)
        => machine.CanCaptureJitCodeSnapshot(root, maxBytes);

    // Copper68k only captures cacheable RAM/ROM through this capability.
    // Peeking never performs a CIA/custom read strobe or advances hardware.
    public ushort ReadHostWord(uint address)
    {
        address &= _addressMask;
        return machine.TryPeekInstructionWord(address, out var value) ? value : (ushort)0xFFFF;
    }

    public byte ReadByte(uint address, ref long cycle, M68kBusAccessKind kind)
    {
        address &= _addressMask;
        BeginTransfer(ref cycle);
        var requested = cycle;
        var result = address <= 0x00FF_FFFF ? machine.ReadByte(address, ref cycle, kind) : machine.ReadExpansionByte(address);
        CompleteTransfer(requested, ref cycle);
        return result;
    }

    public ushort ReadWord(uint address, ref long cycle, M68kBusAccessKind kind)
    {
        address &= _addressMask;
        if ((address & 1) != 0)
        {
            if ((address & 3) == 1 && machine.HasAgaLongBus(address & ~3u))
                return (ushort)ReadWideLanes(address, 2, ref cycle);
            return (ushort)((ReadByte(address, ref cycle, kind) << 8) |
                ReadByte(unchecked(address + 1), ref cycle, kind));
        }
        BeginTransfer(ref cycle);
        var requested = cycle;
        var result = address <= 0x00FF_FFFF ? machine.ReadWord(address, ref cycle, kind)
            : (ushort)((machine.ReadExpansionByte(address) << 8) | machine.ReadExpansionByte(address + 1));
        CompleteTransfer(requested, ref cycle);
        return result;
    }

    public uint ReadLong(uint address, ref long cycle, M68kBusAccessKind kind)
    {
        address &= _addressMask;
        if ((address & 3) == 0 && machine.HasAgaLongBus(address))
            return ReadWideLanes(address, 4, ref cycle);
        if ((address & 1) == 0)
            return ((uint)ReadWord(address, ref cycle, kind) << 16) |
                ReadWord(unchecked(address + 2), ref cycle, kind);
        if ((address & 3) == 1)
            return (ReadThreeBytes(address, ref cycle, kind) << 8) |
                ReadByte(unchecked(address + 3), ref cycle, kind);
        return ((uint)ReadByte(address, ref cycle, kind) << 24) |
            ReadThreeBytes(unchecked(address + 1), ref cycle, kind);
    }

    private uint ReadThreeBytes(uint address, ref long cycle, M68kBusAccessKind kind)
    {
        address &= _addressMask;
        if (machine.HasAgaLongBus(address & ~3u))
            return ReadWideLanes(address, 3, ref cycle);
        if ((address & 1) == 0)
            return ((uint)ReadWord(address, ref cycle, kind) << 8) | ReadByte(address + 2, ref cycle, kind);
        return ((uint)ReadByte(address, ref cycle, kind) << 16) | ReadWord(address + 1, ref cycle, kind);
    }

    private uint ReadWideLanes(uint address, int size, ref long cycle)
    {
        BeginTransfer(ref cycle);
        var requested = cycle;
        var value = machine.ReadAgaBusLanes(address, size, ref cycle);
        CompleteTransfer(requested, ref cycle);
        return value;
    }

    public void WriteByte(uint address, byte value, ref long cycle, M68kBusAccessKind kind)
    {
        address &= _addressMask;
        BeginTransfer(ref cycle);
        var requested = cycle;
        if (address <= 0x00FF_FFFF) machine.WriteByte(address, value, ref cycle, kind);
        else machine.TryWriteExpansionByte(address, value);
        CompleteTransfer(requested, ref cycle);
    }

    public void WriteWord(uint address, ushort value, ref long cycle, M68kBusAccessKind kind)
    {
        address &= _addressMask;
        if ((address & 1) != 0)
        {
            if ((address & 3) == 1 && machine.HasAgaLongBus(address & ~3u))
            {
                WriteWideLanes(address, value, 2, ref cycle);
                return;
            }
            WriteByte(address, (byte)(value >> 8), ref cycle, kind);
            WriteByte(unchecked(address + 1), (byte)value, ref cycle, kind);
            return;
        }
        BeginTransfer(ref cycle);
        var requested = cycle;
        if (address <= 0x00FF_FFFF) machine.WriteWord(address, value, ref cycle, kind);
        else if (!machine.TryWriteExpansionWord(address, value))
        { machine.TryWriteExpansionByte(address, (byte)(value >> 8)); machine.TryWriteExpansionByte(address + 1, (byte)value); }
        CompleteTransfer(requested, ref cycle);
    }

    public void WriteLong(uint address, uint value, ref long cycle, M68kBusAccessKind kind)
    {
        if ((address & 3) == 0 && machine.HasAgaLongBus(address & _addressMask))
        {
            WriteWideLanes(address & _addressMask, value, 4, ref cycle);
            return;
        }
        if ((address & 1) == 0)
        {
            WriteWord(address, (ushort)(value >> 16), ref cycle, kind);
            WriteWord(unchecked(address + 2), (ushort)value, ref cycle, kind);
            return;
        }
        // Only one aligned longword contains three of the operand's bytes.
        // Decode its port width independently of the single byte across the boundary.
        if ((address & 3) == 1)
        {
            WriteThreeBytes(address, value >> 8, ref cycle, kind);
            WriteByte(unchecked(address + 3), (byte)value, ref cycle, kind);
        }
        else
        {
            WriteByte(address, (byte)(value >> 24), ref cycle, kind);
            WriteThreeBytes(unchecked(address + 1), value & 0xFFFFFF, ref cycle, kind);
        }
    }

    private void WriteThreeBytes(uint address, uint value, ref long cycle, M68kBusAccessKind kind)
    {
        address &= _addressMask;
        if (machine.HasAgaLongBus(address & ~3u))
        {
            WriteWideLanes(address, value, 3, ref cycle);
        }
        else if ((address & 1) == 0)
        {
            WriteWord(address, (ushort)(value >> 8), ref cycle, kind);
            WriteByte(unchecked(address + 2), (byte)value, ref cycle, kind);
        }
        else
        {
            WriteByte(address, (byte)(value >> 16), ref cycle, kind);
            WriteWord(unchecked(address + 1), (ushort)value, ref cycle, kind);
        }
    }

    private void WriteWideLanes(uint address, uint value, int size, ref long cycle)
    {
        BeginTransfer(ref cycle);
        var requested = cycle;
        machine.WriteAgaBusLanes(address, value, size, ref cycle);
        CompleteTransfer(requested, ref cycle);
    }

    public bool HasHostGateway(uint address)
        => machine.HasHostGateway(address & _addressMask);
    public bool TryInvokeHostGateway(uint address, uint token, M68kCpuState state)
        => machine.TryInvokeHostGateway(address & _addressMask, token, state);
    public void ResetExternalDevices(long cycle) => machine.ResetExternalDevices(cycle);

    private void BeginTransfer(ref long cycle)
        => cycle = Math.Max(cycle, machine.Cycle) + 2;

    private void CompleteTransfer(long requested, ref long cycle)
    {
        // Address phase above plus a minimum data phase. Contention and CIA
        // synchronization can extend this, but a zero-wait ROM must not erase it.
        cycle = Math.Max(cycle, requested + 2);
        machine.AdvanceHardwareTo(cycle);
    }
}
