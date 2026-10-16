using System.Buffers.Binary;
using Copper68k;
using CopperMod.Amiga.Lightweight;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

public sealed class LightweightCpuIntegrationTests
{
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void A1200EoriPostIncrementUpdatesChipRamAtOriginalAddress(bool batch)
    {
        using var machine = new LightweightA500Machine(new()
        {
            CpuModel = M68kCpuModel.M68EC020,
            AgnusModel = LightweightAgnusModel.Mos8374Alice,
            DeniseModel = LightweightDeniseModel.Lisa4203,
            ChipRamBytes = 2 * 1024 * 1024, SlowRamBytes = 0
        }, batch);
        // MOVEA.L #$2000,A0; EORI.B #$9E,(A0)+; EORI.B #$80,(A0)+; STOP.
        Write(machine, 0x1000, [0x207C, 0, 0x2000, 0x0A18, 0x009E, 0x0A18, 0x0080, 0x4E72, 0x2700]);
        Write(machine, 0x2000, [0x019E, 0xBEEF]);
        machine.Reset(); machine.ExecuteFrame();
        Assert.True(machine.Cpu.Stopped);
        Assert.Equal(0x2002u, machine.Cpu.A[0]);
        Assert.Equal(0x9F1E, BinaryPrimitives.ReadUInt16BigEndian(machine.ChipRam.Span[0x2000..]));
        Assert.Equal(0xBEEF, BinaryPrimitives.ReadUInt16BigEndian(machine.ChipRam.Span[0x2002..]));
        Assert.Equal(machine.Cycle, machine.Cpu.Cycles);
        Assert.Null(machine.UnsupportedActiveFeature);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void M68010RunsSystemInstructionsThroughMotherboardBus(bool batch)
    {
        using var machine = new LightweightA500Machine(new() { CpuModel = M68kCpuModel.M68010 }, batch);
        Write(machine, 0x1000, [0x701F, 0x44C0, 0x42C1, 0x4E7B, 0x1801, 0x4E7A, 0x2801, 0x4E72, 0x2700]);
        machine.Reset(); machine.ExecuteFrame();
        Assert.True(machine.Cpu.Stopped); Assert.Equal(31u, machine.Cpu.D[1]); Assert.Equal(31u, machine.Cpu.D[2]);
        Assert.Equal(31u, machine.Cpu.VectorBaseRegister); Assert.Equal(machine.Cycle, machine.Cpu.Cycles);
        Assert.Null(machine.UnsupportedActiveFeature);
    }

    [Theory]
    [InlineData(M68kCpuModel.M68040, true)] [InlineData(M68kCpuModel.M68040, false)]
    [InlineData(M68kCpuModel.M68060, true)] [InlineData(M68kCpuModel.M68060, false)]
    public void NativeFpuArithmeticExecutesThroughGuestInstructions(M68kCpuModel model, bool batch)
    {
        using var machine = new LightweightA500Machine(new() { CpuModel = model }, batch);
        // FMOVE.L #3,FP0; FMUL FP0,FP0; FMOVE.L FP0,D1; STOP.
        Write(machine, 0x1000, [0xF23C, 0x4000, 0, 3, 0xF200, 0x0023, 0xF201, 0x6000, 0x4E72, 0x2700]);
        machine.Reset(); machine.ExecuteFrame();
        Assert.True(machine.Cpu.Stopped); Assert.Equal(9u, machine.Cpu.D[1]);
        Assert.Equal(machine.Cycle, machine.Cpu.Cycles); Assert.Null(machine.UnsupportedActiveFeature);
    }

    [Theory]
    [InlineData(M68kCpuModel.M68040, true, false)] [InlineData(M68kCpuModel.M68040, false, false)]
    [InlineData(M68kCpuModel.M68060, true, false)] [InlineData(M68kCpuModel.M68060, false, false)]
    [InlineData(M68kCpuModel.M68040, true, true)]
    public void GuestCanEnablePagingAndReadWriteTranslatedRam(M68kCpuModel model, bool batch, bool jit)
    {
        using var machine = new LightweightA500Machine(new() { CpuModel = model,
            CpuExecutionMode = jit ? M68kExecutionMode.Jit : M68kExecutionMode.Interpreter }, batch);
        Write(machine, 0x1000, [0x203C, 0, 0x4000, 0x4E7B, 0x0807,
            0x203C, 0, 0xA000, 0x4E7B, 0x0004,
            0x203C, 0, 0x8000, 0x4E7B, 0x0003,
            0x2239, 0, 0x2000, 0x23C1, 0, 0x2004, 0x4E72, 0x2700]);
        Long(machine, 0x4000, 0x4202); Long(machine, 0x4200, 0x4402); Long(machine, 0x4408, 0xC02001);
        machine.PokeHdfByte(0xC02000, 0x12); machine.PokeHdfByte(0xC02001, 0x34);
        machine.PokeHdfByte(0xC02002, 0x56); machine.PokeHdfByte(0xC02003, 0x78);
        machine.Reset(); machine.ExecuteFrame();
        Assert.True(machine.Cpu.Stopped); Assert.False(machine.Cpu.Halted); Assert.Equal(0x12345678u, machine.Cpu.D[1]);
        Assert.Equal(0x12345678u, BinaryPrimitives.ReadUInt32BigEndian(machine.HdfRam(0xC02004, 4)));
        Assert.Equal(0xC02019u, BinaryPrimitives.ReadUInt32BigEndian(machine.ChipRam.Span[0x4408..]));
        Assert.Equal(machine.Cycle, machine.Cpu.Cycles); Assert.Null(machine.UnsupportedActiveFeature);
        if (jit) Assert.True(machine.JitStatistics!.Value.FallbackInstructions > 0);
    }

    [Fact]
    public void JitRunsCompiledCodeAndRecompilesAfterGuestCacheFlush()
    {
        using var machine = new LightweightA500Machine(new() { CpuModel = M68kCpuModel.M68040, CpuExecutionMode = M68kExecutionMode.Jit });
        Write(machine, 0x1000, [0x2E3C, 0, 0x8000, 0x4E7B, 0x7002, 0x4EF9, 0x00C0, 0x1000]);
        WriteSlow(machine, 0xC01000, [0x5280, 0x60FC]);
        WriteSlow(machine, 0xC01100, [0xF498, 0x4EF9, 0x00C0, 0x1000]);
        machine.Reset();
        for (var i = 0; i < 4; i++) machine.ExecuteFrame();
        var before = machine.JitStatistics!.Value;
        Assert.True(before.CompiledTraces > 0, $"{before}; PC={machine.Cpu.ProgramCounter:X8}; CACR={machine.Cpu.CacheControlRegister:X8}; unsupported={machine.UnsupportedActiveFeature}");
        Assert.True(before.CompiledInstructions > 0, before.ToString());
        Assert.True(machine.Cpu.D[0] > 0); Assert.Equal(machine.Cycle, machine.Cpu.Cycles);
        machine.PokeHdfByte(0xC01000, 0x54); // ADDQ.L #2,D0
        machine.Cpu.ProgramCounter = 0xC01100; machine.Cpu.D[0] = 0;
        for (var i = 0; i < 4; i++) machine.ExecuteFrame();
        var after = machine.JitStatistics!.Value;
        Assert.True(after.CompiledInstructions > before.CompiledInstructions);
        Assert.Equal(0u, machine.Cpu.D[0] & 1); Assert.True(machine.Cpu.D[0] > 0);
        Assert.Null(machine.UnsupportedActiveFeature); Assert.Equal(machine.Cycle, machine.Cpu.Cycles);
    }

    [Fact]
    public void SnapshotIsImmutableAndDmaInvalidatesChipAliases()
    {
        using var machine = new LightweightA500Machine(new() { CpuModel = M68kCpuModel.M68040, CpuExecutionMode = M68kExecutionMode.Jit });
        Write(machine, 0x1000, [0x7201, 0x60FC]);
        var bus = new LightweightAcceleratorBus(machine, M68kCpuModel.M68040);
        var before = machine.Cycle;
        Assert.True(bus.TryCaptureJitCodeSnapshot(0x81000, 4, out var snapshot));
        var generation = bus.GetJitCodePageGeneration(0x81000);
        machine.WriteChipWordDma(0x1000, 0x7207);
        Assert.NotEqual(generation, bus.GetJitCodePageGeneration(0x81000));
        Assert.Equal(new byte[] { 0x72, 1, 0x60, 0xFC }, snapshot.Bytes);
        Assert.Equal(0x7207, bus.ReadJitCodeWord(0x81000)); Assert.Equal(before, machine.Cycle);
        Assert.False(bus.IsJitCodeAddress(0xDFF000, 2, M68kBusAccessKind.CpuInstructionFetch));
        Assert.False(bus.IsJitCodeAddress(0x10FE, 4, M68kBusAccessKind.CpuInstructionFetch));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PendingInterruptExitsRunningLoopWithArchitecturalFrame(bool jit)
    {
        using var machine = new LightweightA500Machine(new() { CpuModel = M68kCpuModel.M68040,
            CpuExecutionMode = jit ? M68kExecutionMode.Jit : M68kExecutionMode.Interpreter });
        Write(machine, 0x1000, [0x203C, 0, 0x8000, 0x4E7B, 0x0002, 0x4EF9, 0x00C0, 0x1000]);
        WriteSlow(machine, 0xC01000, [0x5280, 0x60FC]);
        Write(machine, 0x1100, [0x4E72, 0x2700]); Long(machine, 0x70, 0x1100);
        machine.Reset(); for (var i = 0; i < 4; i++) machine.ExecuteFrame();
        if (jit) Assert.True(machine.JitStatistics!.Value.CompiledInstructions > 0);
        var count = machine.Cpu.D[0]; var stack = machine.Cpu.A[7]; machine.Cpu.StatusRegister = 0x2000;
        machine.WriteCustomRegisterFromCopper(0x09A, 0xC080, machine.Cycle);
        machine.WriteCustomRegisterFromCopper(0x09C, 0x8080, machine.Cycle);
        machine.ExecuteFrame();
        Assert.True(machine.Cpu.Stopped, $"PC={machine.Cpu.ProgramCounter:X8}; SR={machine.Cpu.StatusRegister:X4}; SP={machine.Cpu.A[7]:X8}; beforeD0={count}; D0={machine.Cpu.D[0]}; unsupported={machine.UnsupportedActiveFeature}; stats={machine.JitStatistics}");
        Assert.Equal(0x1104u, machine.Cpu.ProgramCounter);
        Assert.InRange(machine.Cpu.D[0], count, count + 1); Assert.Equal(stack - 8, machine.Cpu.A[7]);
        var frame = machine.ChipRam.Span.Slice((int)machine.Cpu.A[7], 8);
        Assert.Equal(0x2000, BinaryPrimitives.ReadUInt16BigEndian(frame));
        Assert.Contains(BinaryPrimitives.ReadUInt32BigEndian(frame[2..]), new uint[] { 0xC01000, 0xC01002 });
        Assert.Equal(0x70, BinaryPrimitives.ReadUInt16BigEndian(frame[6..]));
        Assert.Equal(machine.Cycle, machine.Cpu.Cycles);
    }

    [Fact]
    public void CpuBulkWritesAndResetInvalidateObservedCodeWithoutReadingDevices()
    {
        using var machine = new LightweightA500Machine(new() { CpuModel = M68kCpuModel.M68040, CpuExecutionMode = M68kExecutionMode.Jit });
        var bus = new LightweightAcceleratorBus(machine, M68kCpuModel.M68040);
        var cycle = machine.Cycle;
        var generation = bus.GetJitCodePageGeneration(0x1000);
        bus.WriteWord(0x1000, 0x7201, ref cycle, M68kBusAccessKind.CpuDataWrite);
        Assert.NotEqual(generation, bus.GetJitCodePageGeneration(0x1000));
        generation = bus.GetJitCodePageGeneration(0x1000);
        using var hardfile = new LightweightHdfBus(machine, []);
        hardfile.CopyToMemory(0x1000, new byte[] { 0x72, 7, 0x60, 0xFC });
        Assert.NotEqual(generation, bus.GetJitCodePageGeneration(0x1000));
        generation = bus.GetJitCodePageGeneration(0x1000); hardfile.ClearMemory(0x1080, 4);
        Assert.NotEqual(generation, bus.GetJitCodePageGeneration(0x1000));
        generation = bus.GetJitCodePageGeneration(0x1000); var mapping = machine.CpuMemoryMapGeneration;
        machine.Reset();
        Assert.NotEqual(generation, bus.GetJitCodePageGeneration(0x1000));
        Assert.NotEqual(mapping, machine.CpuMemoryMapGeneration); Assert.Equal(0x7207, bus.ReadJitCodeWord(0x1000));
    }

    private static void Write(LightweightA500Machine machine, uint address, ushort[] code)
    { for (var i = 0; i < code.Length; i++) machine.WriteChipWordDma(address + (uint)i * 2, code[i]); }
    private static void Long(LightweightA500Machine machine, uint address, uint value)
        => Write(machine, address, [(ushort)(value >> 16), (ushort)value]);
    private static void WriteSlow(LightweightA500Machine machine, uint address, ushort[] code)
    { for (var i = 0; i < code.Length; i++) { machine.PokeHdfByte(address + (uint)i * 2, (byte)(code[i] >> 8)); machine.PokeHdfByte(address + (uint)i * 2 + 1, (byte)code[i]); } }
}
