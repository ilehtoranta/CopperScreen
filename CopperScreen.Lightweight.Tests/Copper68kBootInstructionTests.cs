using System.Buffers.Binary;
using Copper68k;
using CopperScreen;

namespace CopperScreen.Tests;

// Exercise the pinned public CPU package independently of the boot firmware.
public sealed class Copper68kBootInstructionTests
{
    [Theory]
    [InlineData("move.l $3000,$2000", 0xffffffffu, 0x80000001u, 0x18)]
    [InlineData("tst.l $2000", 0x80000000u, 0x80000000u, 0x18)]
    [InlineData("tst.l $2000", 0u, 0u, 0x14)]
    [InlineData("tst.w $2000", 0x80001234u, 0x80001234u, 0x18)]
    [InlineData("clr.w $2000", 0xffffffffu, 0x0000ffffu, 0x14)]
    [InlineData("addi.w #1,$2000", 0x7fffabcdu, 0x8000abcdu, 0x0a)]
    [InlineData("addi.b #1,$2000", 0xffabcdefu, 0x00abcdefu, 0x15)]
    [InlineData("subi.l #1,$2000", 0u, 0xffffffffu, 0x19)]
    [InlineData("subi.w #1,$2000", 0x0000abcdu, 0xffffabcdu, 0x19)]
    [InlineData("subi.b #1,$2000", 0x00abcdefu, 0xffabcdefu, 0x19)]
    public void A1200ExecutesAbsoluteMemoryFormsWithCorrectWidthsAndFlags(
        string instruction, uint initial, uint expected, int flags)
    {
        var bus = new MemoryBus();
        var code = new CopperScreenBootAssembler(0x1000).Assemble(instruction);
        code.CopyTo(bus.Memory, 0x1000);
        BinaryPrimitives.WriteUInt32BigEndian(bus.Memory.AsSpan(0x2000), initial);
        BinaryPrimitives.WriteUInt32BigEndian(bus.Memory.AsSpan(0x3000), 0x80000001);
        BinaryPrimitives.WriteUInt16BigEndian(bus.Memory.AsSpan(0x2004), 0xa55a);
        using var cpu = M68kCoreFactory.Default.CreateA1200Ec020(bus);
        cpu.Reset(0x1000, 0x8000);
        cpu.State.StatusRegister = 0x201f;
        cpu.ExecuteInstruction();
        Assert.Equal(expected, BinaryPrimitives.ReadUInt32BigEndian(bus.Memory.AsSpan(0x2000)));
        Assert.Equal(0x80000001u, BinaryPrimitives.ReadUInt32BigEndian(bus.Memory.AsSpan(0x3000)));
        Assert.Equal(0xa55a, BinaryPrimitives.ReadUInt16BigEndian(bus.Memory.AsSpan(0x2004)));
        Assert.Equal(flags, cpu.State.StatusRegister & 31);
        Assert.Equal(0x1000u + (uint)code.Length, cpu.State.ProgramCounter);
    }

    private sealed class MemoryBus : IM68kBus
    {
        internal byte[] Memory { get; } = new byte[65536];
        public byte ReadByte(uint address, ref long cycle, M68kBusAccessKind kind) => Memory[(int)address];
        public ushort ReadWord(uint address, ref long cycle, M68kBusAccessKind kind)
            => BinaryPrimitives.ReadUInt16BigEndian(Memory.AsSpan((int)address));
        public uint ReadLong(uint address, ref long cycle, M68kBusAccessKind kind)
            => BinaryPrimitives.ReadUInt32BigEndian(Memory.AsSpan((int)address));
        public void WriteByte(uint address, byte value, ref long cycle, M68kBusAccessKind kind) => Memory[(int)address] = value;
        public void WriteWord(uint address, ushort value, ref long cycle, M68kBusAccessKind kind)
            => BinaryPrimitives.WriteUInt16BigEndian(Memory.AsSpan((int)address), value);
        public void WriteLong(uint address, uint value, ref long cycle, M68kBusAccessKind kind)
            => BinaryPrimitives.WriteUInt32BigEndian(Memory.AsSpan((int)address), value);
        public void ResetExternalDevices(long cycle) { }
    }
}
