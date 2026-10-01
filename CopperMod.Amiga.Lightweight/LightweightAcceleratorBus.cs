using Copper68k;

namespace CopperMod.Amiga.Lightweight;

// Experimental OCS accelerator/A1200 bridge. Copper68k converts native CPU
// clocks to the 7.09 MHz motherboard domain. All transfers below use that same
// clock and the fitted motherboard bus, including split unaligned operands.
// This is a bounded bridge policy, not a model of a particular accelerator card.
internal sealed class LightweightAcceleratorBus(LightweightA500Machine machine, M68kCpuModel model) : IM68kBus, IM68kCodeReader
{
    private readonly uint _addressMask = model == M68kCpuModel.M68EC020 ? 0x00FF_FFFFu : uint.MaxValue;

    // Copper68k only captures cacheable RAM/ROM through this capability.
    // Peeking never performs a CIA/custom read strobe or advances hardware.
    public ushort ReadHostWord(uint address)
    {
        address &= _addressMask;
        return address <= 0x00FF_FFFF && machine.TryPeekInstructionWord(address, out var value) ? value : (ushort)0xFFFF;
    }

    public byte ReadByte(uint address, ref long cycle, M68kBusAccessKind kind)
    {
        address &= _addressMask;
        BeginTransfer(ref cycle);
        var requested = cycle;
        var result = address <= 0x00FF_FFFF ? machine.ReadByte(address, ref cycle, kind) : (byte)0xFF;
        CompleteTransfer(requested, ref cycle);
        return result;
    }

    public ushort ReadWord(uint address, ref long cycle, M68kBusAccessKind kind)
    {
        if ((address & 1) != 0)
            return (ushort)((ReadByte(address, ref cycle, kind) << 8) |
                ReadByte(unchecked(address + 1), ref cycle, kind));
        address &= _addressMask;
        BeginTransfer(ref cycle);
        var requested = cycle;
        var result = address <= 0x00FF_FFFF ? machine.ReadWord(address, ref cycle, kind) : (ushort)0xFFFF;
        CompleteTransfer(requested, ref cycle);
        return result;
    }

    public uint ReadLong(uint address, ref long cycle, M68kBusAccessKind kind)
    {
        if ((address & 3) == 0 && machine.HasAgaLongBus(address & _addressMask))
        {
            address &= _addressMask; BeginTransfer(ref cycle); var requested = cycle;
            var value = machine.ReadAgaLong(address, ref cycle); CompleteTransfer(requested, ref cycle); return value;
        }
        if ((address & 1) == 0)
            return ((uint)ReadWord(address, ref cycle, kind) << 16) |
                ReadWord(unchecked(address + 2), ref cycle, kind);
        return ((uint)ReadByte(address, ref cycle, kind) << 24) |
            ((uint)ReadWord(unchecked(address + 1), ref cycle, kind) << 8) |
            ReadByte(unchecked(address + 3), ref cycle, kind);
    }

    public void WriteByte(uint address, byte value, ref long cycle, M68kBusAccessKind kind)
    {
        address &= _addressMask;
        BeginTransfer(ref cycle);
        var requested = cycle;
        if (address <= 0x00FF_FFFF) machine.WriteByte(address, value, ref cycle, kind);
        CompleteTransfer(requested, ref cycle);
    }

    public void WriteWord(uint address, ushort value, ref long cycle, M68kBusAccessKind kind)
    {
        if ((address & 1) != 0)
        {
            WriteByte(address, (byte)(value >> 8), ref cycle, kind);
            WriteByte(unchecked(address + 1), (byte)value, ref cycle, kind);
            return;
        }
        address &= _addressMask;
        BeginTransfer(ref cycle);
        var requested = cycle;
        if (address <= 0x00FF_FFFF) machine.WriteWord(address, value, ref cycle, kind);
        CompleteTransfer(requested, ref cycle);
    }

    public void WriteLong(uint address, uint value, ref long cycle, M68kBusAccessKind kind)
    {
        if ((address & 3) == 0 && machine.HasAgaLongBus(address & _addressMask))
        {
            address &= _addressMask; BeginTransfer(ref cycle); var requested = cycle;
            machine.WriteAgaLong(address, value, ref cycle); CompleteTransfer(requested, ref cycle); return;
        }
        if ((address & 1) == 0)
        {
            WriteWord(address, (ushort)(value >> 16), ref cycle, kind);
            WriteWord(unchecked(address + 2), (ushort)value, ref cycle, kind);
            return;
        }
        WriteByte(address, (byte)(value >> 24), ref cycle, kind);
        WriteWord(unchecked(address + 1), (ushort)(value >> 8), ref cycle, kind);
        WriteByte(unchecked(address + 3), (byte)value, ref cycle, kind);
    }

    public bool HasHostGateway(uint address)
        => (address & _addressMask) <= 0x00FF_FFFF && machine.HasHostGateway(address & _addressMask);
    public bool TryInvokeHostGateway(uint address, uint token, M68kCpuState state)
        => (address & _addressMask) <= 0x00FF_FFFF && machine.TryInvokeHostGateway(address & _addressMask, token, state);
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
