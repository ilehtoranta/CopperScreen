using System.Buffers.Binary;
using Copper68k;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

public sealed class CopperStartBootstrapTests
{
    private static void Long(LightweightA500Machine machine, uint address, uint value)
    {
        machine.WriteChipWordDma(address, (ushort)(value >> 16));
        machine.WriteChipWordDma(address + 2, (ushort)value);
    }

    private static LightweightA500Machine Request()
    {
        var machine = new LightweightA500Machine();
        machine.LoadKickstart(new byte[524288]);
        machine.WriteChipWordDma(0x780, 0xFF00); Long(machine, 0x782, 0x43534731);
        machine.WriteChipWordDma(0x786, 0x4E75);
        Long(machine, 0x760, 0x43534231); Long(machine, 0x764, 1);
        Long(machine, 0x768, 0x12345678); Long(machine, 0x76C, 1);
        Long(machine, 0x7FFF0, 0xF80080);
        var state = machine.Cpu;
        state.ProgramCounter = 0x786; state.LastInstructionProgramCounter = 0x780;
        state.StatusRegister = 0x2700; state.A[0] = 0x760; state.A[7] = 0x7FFF0;
        state.D[0] = 0x43534231; state.D[1] = 1;
        return machine;
    }

    [Fact]
    public void ReplyChangesOnlyTheBoundedReceiptAndD0WithoutStorage()
    {
        using var machine = Request();
        var before = machine.ChipRam.ToArray();
        Assert.True(machine.HasHostGateway(0x780));
        Assert.False(machine.HasHostGateway(0x782));
        Assert.False(machine.TryInvokeHostGateway(0x780, 0x43534730, machine.Cpu));
        Assert.True(machine.TryInvokeHostGateway(0x780, 0x43534731, machine.Cpu));
        var after = machine.ChipRam.Span;
        Assert.True(before.AsSpan(0, 0x770).SequenceEqual(after[..0x770]));
        Assert.True(before.AsSpan(0x780).SequenceEqual(after[0x780..]));
        Assert.Equal(0x43504D31u, machine.Cpu.D[0]);
        Assert.Equal(1u, machine.Cpu.D[1]); Assert.Equal(0x760u, machine.Cpu.A[0]);
        Assert.Equal(0x7FFF0u, machine.Cpu.A[7]); Assert.Equal(0x786u, machine.Cpu.ProgramCounter);
        Assert.Equal((ushort)0x2700, machine.Cpu.StatusRegister);
        Assert.Equal(new uint[] { 0x43504D31, 1, 0x12345678 ^ 0x43504D31 ^ 1, 1 },
            Enumerable.Range(0, 4).Select(i => BinaryPrimitives.ReadUInt32BigEndian(machine.ChipRam.Span.Slice(0x770 + i * 4, 4))));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)]
    public void InvalidRequestsReturnFailureWithoutWritingRam(int invalid)
    {
        using var machine = Request();
        var state = machine.Cpu;
        switch (invalid)
        {
            case 0: state.StatusRegister = 0; break;
            case 1: state.D[1] = 2; break;
            case 2: state.A[0] = uint.MaxValue; break;
            case 3: state.A[7] = 0x7FFFF; break;
            case 4: Long(machine, 0x7FFF0, 0x1000); break;
            case 5: Long(machine, 0x764, 2); break;
            case 6: state.LastInstructionProgramCounter = 0x782; break;
            case 7: state.A[7] = uint.MaxValue - 1; break;
            case 8: Long(machine, 0x76C, 2); break;
        }
        var before = machine.ChipRam.ToArray();
        Assert.True(machine.TryInvokeHostGateway(0x780, 0x43534731, state));
        Assert.Equal(0u, state.D[0]); Assert.Equal(before, machine.ChipRam.ToArray());
    }

    [Theory]
    [InlineData(M68kCpuModel.M68000, false, true)]
    [InlineData(M68kCpuModel.M68EC020, false, true)]
    [InlineData(M68kCpuModel.M68040, false, true)]
    [InlineData(M68kCpuModel.M68040, true, true)]
    [InlineData(M68kCpuModel.M68000, false, false)]
    [InlineData(M68kCpuModel.M68EC020, false, false)]
    [InlineData(M68kCpuModel.M68040, false, false)]
    [InlineData(M68kCpuModel.M68040, true, false)]
    public void GuestJsrReadsTheReplyAndReturnsToRomAcrossReset(M68kCpuModel model, bool jit, bool recognized)
    {
        using var machine = new LightweightA500Machine(new()
        {
            CpuModel = model, CpuExecutionMode = jit ? M68kExecutionMode.Jit : M68kExecutionMode.Interpreter,
            AgnusModel = model == M68kCpuModel.M68EC020 ? LightweightAgnusModel.Mos8374Alice : LightweightAgnusModel.Mos8371,
            DeniseModel = model == M68kCpuModel.M68EC020 ? LightweightDeniseModel.Lisa4203 : LightweightDeniseModel.Mos8362,
            ChipRamBytes = model == M68kCpuModel.M68EC020 ? 2097152 : 524288, SlowRamBytes = 0
        });
        var rom = new byte[524288];
        BinaryPrimitives.WriteUInt32BigEndian(rom, 0x7FFF0);
        BinaryPrimitives.WriteUInt32BigEndian(rom.AsSpan(4), 0xF80008);
        ushort[] code = [0x13FC, 2, 0x00BF, 0xE001, // Disable overlay.
            0x4EB9, 0, 0x780, 0x2439, 0, 0x770, 0x2639, 0, 0x778, 0x4E72, 0x2700];
        for (var i = 0; i < code.Length; i++) BinaryPrimitives.WriteUInt16BigEndian(rom.AsSpan(8 + i * 2), code[i]);
        machine.LoadKickstart(rom);
        for (uint attempt = 0; attempt < 2; attempt++)
        {
            machine.Reset();
            machine.WriteChipWordDma(0x780, 0xFF00); Long(machine, 0x782, recognized ? 0x43534731u : 0x43534730u);
            machine.WriteChipWordDma(0x786, 0x4E75);
            Long(machine, 0x2C, 0x790);
            ushort[] fallback = [0x7000, 0x2F7C, 0, 0x786, 2, 0x4E73];
            for (var i = 0; i < fallback.Length; i++) machine.WriteChipWordDma(0x790 + (uint)i * 2, fallback[i]);
            Long(machine, 0x760, 0x43534231); Long(machine, 0x764, 1);
            Long(machine, 0x768, 0x12345678 + attempt); Long(machine, 0x76C, 1);
            machine.Cpu.D[0] = 0x43534231; machine.Cpu.D[1] = 1; machine.Cpu.A[0] = 0x760;
            machine.ExecuteFrame();
            Assert.True(machine.Cpu.Stopped); Assert.False(machine.Cpu.Halted);
            Assert.Equal(recognized ? 0x43504D31u : 0u, machine.Cpu.D[0]);
            Assert.Equal(recognized ? 0x43504D31u : 0u, machine.Cpu.D[2]);
            Assert.Equal(recognized ? (0x12345678 + attempt) ^ 0x43504D31 ^ 1 : 0u, machine.Cpu.D[3]);
            Assert.Equal(0x7FFF0u, machine.Cpu.A[7]); Assert.Equal(machine.Cycle, machine.Cpu.Cycles);
            Assert.Null(machine.UnsupportedActiveFeature);
        }
    }
}
