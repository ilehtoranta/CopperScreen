using System.Buffers.Binary;
using Copper68k;

namespace CopperMod.Amiga.Lightweight;

// Fast RAM uses direct access. Backed ROM also uses direct reads with the
// clock multiplier. All other accesses use the timed motherboard bridge.
internal sealed class LightweightAccelerated040Bus(LightweightA500Machine machine)
    : LightweightAcceleratorBus(machine, M68kCpuModel.M68040), IM68kBus, IM68kJitFastMemoryBus
{
    byte IM68kBus.ReadByte(uint address, ref long cycle, M68kBusAccessKind kind)
    {
        if (TryReadJitZeroWaitMemory(address, M68kOperandSize.Byte, out var direct))
            return (byte)direct;
        Machine.RequireTimedCpuBusBoundary();
        var boardCycle = Machine.ToBoardCycle(cycle);
        var value = base.ReadByte(address, ref boardCycle, kind);
        cycle = Machine.ToCpuCycle(boardCycle);
        return value;
    }

    ushort IM68kBus.ReadWord(uint address, ref long cycle, M68kBusAccessKind kind)
    {
        if (TryReadJitZeroWaitMemory(address, M68kOperandSize.Word, out var direct))
            return (ushort)direct;
        Machine.RequireTimedCpuBusBoundary();
        var boardCycle = Machine.ToBoardCycle(cycle);
        var value = base.ReadWord(address, ref boardCycle, kind);
        cycle = Machine.ToCpuCycle(boardCycle);
        return value;
    }

    uint IM68kBus.ReadLong(uint address, ref long cycle, M68kBusAccessKind kind)
    {
        if (TryReadJitZeroWaitMemory(address, M68kOperandSize.Long, out var direct))
            return direct;
        Machine.RequireTimedCpuBusBoundary();
        var boardCycle = Machine.ToBoardCycle(cycle);
        var value = base.ReadLong(address, ref boardCycle, kind);
        cycle = Machine.ToCpuCycle(boardCycle);
        return value;
    }

    void IM68kBus.WriteByte(uint address, byte value, ref long cycle, M68kBusAccessKind kind)
    {
        if (TryWriteJitZeroWaitMemory(address, value, M68kOperandSize.Byte)) return;
        Machine.RequireTimedCpuBusBoundary();
        var boardCycle = Machine.ToBoardCycle(cycle);
        base.WriteByte(address, value, ref boardCycle, kind);
        cycle = Machine.ToCpuCycle(boardCycle);
    }

    void IM68kBus.WriteWord(uint address, ushort value, ref long cycle, M68kBusAccessKind kind)
    {
        if (TryWriteJitZeroWaitMemory(address, value, M68kOperandSize.Word)) return;
        Machine.RequireTimedCpuBusBoundary();
        var boardCycle = Machine.ToBoardCycle(cycle);
        base.WriteWord(address, value, ref boardCycle, kind);
        cycle = Machine.ToCpuCycle(boardCycle);
    }

    void IM68kBus.WriteLong(uint address, uint value, ref long cycle, M68kBusAccessKind kind)
    {
        if (TryWriteJitZeroWaitMemory(address, value, M68kOperandSize.Long)) return;
        Machine.RequireTimedCpuBusBoundary();
        var boardCycle = Machine.ToBoardCycle(cycle);
        base.WriteLong(address, value, ref boardCycle, kind);
        cycle = Machine.ToCpuCycle(boardCycle);
    }

    void IM68kBus.ResetExternalDevices(long cycle)
        => base.ResetExternalDevices(Machine.ToBoardCycle(cycle));

    public bool TryReadJitZeroWaitMemory(uint address, M68kOperandSize size, out uint value)
    {
        if (!TryGetJitZeroWaitReadMemory(address, (int)size, out var memory, out var offset, out _))
        {
            value = 0;
            return false;
        }

        value = size switch
        {
            M68kOperandSize.Byte => memory[offset],
            M68kOperandSize.Word => BinaryPrimitives.ReadUInt16BigEndian(memory.AsSpan(offset, 2)),
            M68kOperandSize.Long => BinaryPrimitives.ReadUInt32BigEndian(memory.AsSpan(offset, 4)),
            _ => throw new ArgumentOutOfRangeException(nameof(size))
        };
        return true;
    }

    public bool TryWriteJitZeroWaitMemory(uint address, uint value, M68kOperandSize size)
    {
        if (!Machine.TryGetAcceleratedRam(address, (int)size, out var memory, out var offset))
            return false;

        switch (size)
        {
            case M68kOperandSize.Byte: memory[offset] = (byte)value; break;
            case M68kOperandSize.Word: BinaryPrimitives.WriteUInt16BigEndian(memory.AsSpan(offset, 2), (ushort)value); break;
            case M68kOperandSize.Long: BinaryPrimitives.WriteUInt32BigEndian(memory.AsSpan(offset, 4), value); break;
            default: throw new ArgumentOutOfRangeException(nameof(size));
        }
        CompleteJitZeroWaitWrite(address, (int)size);
        return true;
    }

    public bool TryGetJitZeroWaitReadMemory(
        uint address, int bytes, out byte[] memory, out int offset, out M68kJitMemoryKind kind)
    {
        if (TryGetJitZeroWaitWriteMemory(address, bytes, out memory, out offset, out kind))
            return true;
        kind = M68kJitMemoryKind.Rom;
        return Machine.UsesAcceleratedCpuClock &&
            Machine.TryGetAcceleratedRom(address, bytes, out memory, out offset);
    }

    public bool TryGetJitZeroWaitWriteMemory(
        uint address, int bytes, out byte[] memory, out int offset, out M68kJitMemoryKind kind)
    {
        kind = M68kJitMemoryKind.FastRam;
        return Machine.TryGetAcceleratedRam(address, bytes, out memory, out offset);
    }

    public bool CanBindUnwatchedFastRam => true;

    public bool TryGetJitUnwatchedFastRam(uint address, int bytes, out byte[] memory, out int offset)
        => Machine.TryGetUnwatchedAcceleratedRam(address, bytes, out memory, out offset);

    public void CompleteJitZeroWaitWrite(uint address, int bytes)
        => Machine.NotifyJitRamWrite(address, bytes);
}
