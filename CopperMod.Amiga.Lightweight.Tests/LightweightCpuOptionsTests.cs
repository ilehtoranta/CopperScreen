using Copper68k;
using CopperMod.Amiga.Lightweight;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

public sealed class LightweightCpuOptionsTests
{
    [Fact]
    public void DefaultRemains68000()
    {
        using var machine = new LightweightA500Machine();
        Assert.Equal(M68kCpuModel.M68000, machine.CpuModel);
    }

    [Theory]
    [InlineData((M68kCpuModel)999)]
    public void UnsupportedModelsAreRejected(M68kCpuModel model)
        => Assert.Throws<ArgumentException>(() => new LightweightA500Machine(new() { CpuModel = model }));

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    public void M68040JitStopsAtANewFieldBoundaryAfterGuestShortensRaster(int multiplier)
    {
        using var machine = new LightweightA500Machine(new()
        {
            CpuModel = M68kCpuModel.M68040,
            CpuExecutionMode = M68kExecutionMode.Jit,
            CpuClockMultiplier = multiplier,
            AgnusModel = LightweightAgnusModel.Mos8375Pal2M,
            ChipRamBytes = 2 * 1024 * 1024,
            SlowRamBytes = 0
        });
        // Enable the shorter programmable raster from inside the CPU batch.
        ushort[] code = [0x33FC, 0x0080, 0x00DF, 0xF1DC, 0x5280, 0x60FC];
        for (var i = 0; i < code.Length; i++)
            machine.WriteChipWordDma(0x1000u + (uint)i * 2, code[i]);
        machine.Reset();
        machine.WriteCustomRegisterFromCopper(0x1C0, 99, machine.Cycle);
        machine.WriteCustomRegisterFromCopper(0x1C8, 19, machine.Cycle);
        Assert.Equal(313, machine.FieldLines);

        machine.ExecuteFrame();

        Assert.Equal(1, machine.CompletedFrames);
        Assert.Equal(20, machine.FieldLines);
        Assert.InRange(machine.Cycle, 4000L, 4032L);
        Assert.Equal(machine.Cpu.Cycles / multiplier, machine.Cycle);
        Assert.True(machine.Cpu.D[0] > 0);
    }

    [Fact]
    public void AcceleratedFastRamAndTimedChipRamShareTheBoardClock()
    {
        using var machine = new LightweightA500Machine(new()
        {
            CpuModel = M68kCpuModel.M68040,
            CpuExecutionMode = M68kExecutionMode.Jit,
            CpuClockMultiplier = 8,
            FastRamBytes = 512 * 1024
        });
        long boardCycle = machine.Cycle;
        machine.WriteByte(0xE8004A, 0, ref boardCycle, M68kBusAccessKind.CpuDataWrite);
        machine.WriteByte(0xE80048, 0x20, ref boardCycle, M68kBusAccessKind.CpuDataWrite);
        Assert.Equal(0x200000u, machine.FastRamBase);

        IM68kBus accelerated = new LightweightAccelerated040Bus(machine);
        var direct = (IM68kJitFastMemoryBus)accelerated;
        var previousBoardCycle = machine.Cycle;
        var cpuCycle = machine.ToCpuCycle(previousBoardCycle);
        accelerated.WriteLong(0x200000, 0x89ABCDEF, ref cpuCycle, M68kBusAccessKind.CpuDataWrite);
        Assert.Equal(0x89ABCDEFu, accelerated.ReadLong(0x200000, ref cpuCycle, M68kBusAccessKind.CpuDataRead));
        Assert.Equal(previousBoardCycle, machine.Cycle);
        Assert.Equal(machine.ToCpuCycle(previousBoardCycle), cpuCycle);
        Assert.False(direct.TryGetJitZeroWaitReadMemory(0x1000, 4, out _, out _, out _));
        Assert.False(direct.TryGetJitZeroWaitReadMemory(0x81000, 4, out _, out _, out _));
        Assert.False(direct.TryGetJitZeroWaitReadMemory(0xC01000, 4, out _, out _, out _));

        accelerated.WriteLong(0x1000, 0x12345678, ref cpuCycle, M68kBusAccessKind.CpuDataWrite);
        Assert.Equal(0x12345678u, accelerated.ReadLong(0x81000, ref cpuCycle, M68kBusAccessKind.CpuDataRead));
        Assert.True(machine.Cycle > previousBoardCycle);
        Assert.Equal(machine.ToCpuCycle(machine.Cycle), cpuCycle);
        previousBoardCycle = machine.Cycle;

        accelerated.WriteWord(0xDFF180, 0x0456, ref cpuCycle, M68kBusAccessKind.CpuDataWrite);
        Assert.True(machine.Cycle > previousBoardCycle);
        Assert.Equal(machine.ToCpuCycle(machine.Cycle), cpuCycle);
        var timed = new LightweightAcceleratorBus(machine, M68kCpuModel.M68040);
        boardCycle = machine.Cycle;
        Assert.Equal(0x0456, timed.ReadWord(0xDFF180, ref boardCycle, M68kBusAccessKind.CpuDataRead));
    }

    [Theory]
    [InlineData(M68kCpuModel.M68020)]
    [InlineData(M68kCpuModel.M68EC020)]
    [InlineData(M68kCpuModel.M68030)]
    [InlineData(M68kCpuModel.M68040)]
    [InlineData(M68kCpuModel.M68060)]
    public void AcceleratorRunsWithCacheEnabledInMotherboardClockDomain(M68kCpuModel model)
    {
        using var machine = new LightweightA500Machine(new() { CpuModel = model });
        var cacheEnable = model is M68kCpuModel.M68040 or M68kCpuModel.M68060 ? 0x8000u : 1u;
        var ratio = model == M68kCpuModel.M68060 ? 8 : model == M68kCpuModel.M68040 ? 4 : 2;
        ushort[] code = [0x203C, 0, (ushort)cacheEnable, 0x4E7B, 0x0002, 0x4E71, 0x60FC];
        for (var i = 0; i < code.Length; i++) machine.WriteChipWordDma(0x1000u + (uint)i * 2, code[i]);
        machine.Reset();
        for (var i = 0; i < 4; i++) machine.ExecuteFrame();
        Assert.Equal(model, machine.CpuModel);
        Assert.Equal(cacheEnable, machine.Cpu.CacheControlRegister);
        Assert.InRange(machine.Cpu.ProgramCounter, 0x100Au, 0x100Cu);
        Assert.InRange(machine.Cycle, 4L * 454 * 313, 4L * 454 * 313 + 100);
        Assert.Equal(machine.Cycle, machine.Cpu.Cycles);
        // Native completion rounds up to the next whole motherboard clock.
        Assert.InRange(machine.Cpu.NativeCycles, machine.Cpu.Cycles * ratio - (ratio - 1), machine.Cpu.Cycles * ratio + 20);
        Assert.Null(machine.UnsupportedActiveFeature);
        machine.Reset();
        Assert.Equal(model, machine.CpuModel);
        Assert.Equal(0x2700, machine.Cpu.StatusRegister);
        Assert.Equal(0u, machine.Cpu.CacheControlRegister);
    }

    [Theory]
    [InlineData(M68kCpuModel.M68020)]
    [InlineData(M68kCpuModel.M68EC020)]
    [InlineData(M68kCpuModel.M68020, false)]
    [InlineData(M68kCpuModel.M68EC020, false)]
    [InlineData(M68kCpuModel.M68030, true)]
    [InlineData(M68kCpuModel.M68030, false)]
    public void ChipInstructionWritesRequireGuestCacheInvalidation(M68kCpuModel model, bool batch = true)
    {
        using var machine = new LightweightA500Machine(new() { CpuModel = model }, batch);
        // Cache a chip-RAM function, modify its opcode, call again, then flush
        // and call once more. D2/D3/D4 expose executed values across the boundary.
        ushort[] code = [0x7001, 0x4E7B, 0x0002,
            0x4EB9, 0, 0x1180, 0x2401,
            0x33FC, 0x7207, 0, 0x1180, 0x4EB9, 0, 0x1180, 0x2601,
            0x7009, 0x4E7B, 0x0002, 0x4EB9, 0, 0x1180, 0x2801,
            0x4E72, 0x2700];
        for (var i = 0; i < code.Length; i++) machine.WriteChipWordDma(0x1000u + (uint)i * 2, code[i]);
        machine.WriteChipWordDma(0x1180, 0x7201);
        machine.WriteChipWordDma(0x1182, 0x4E75);
        machine.Reset();
        machine.ExecuteFrame();
        Assert.True(machine.Cpu.Stopped);
        Assert.Null(machine.UnsupportedActiveFeature);
        Assert.Equal(1u, machine.Cpu.D[2]);
        Assert.Equal(1u, machine.Cpu.D[3]);
        Assert.Equal(7u, machine.Cpu.D[4]);
    }

    [Theory]
    [InlineData(M68kCpuModel.M68020)]
    [InlineData(M68kCpuModel.M68EC020)]
    [InlineData(M68kCpuModel.M68030)]
    [InlineData(M68kCpuModel.M68040)]
    [InlineData(M68kCpuModel.M68060)]
    public void UnalignedLongTransfersPreserveSurroundingBytesAndAdvanceSingleClock(M68kCpuModel model)
    {
        using var machine = new LightweightA500Machine(new() { CpuModel = model });
        var bus = new LightweightAcceleratorBus(machine, model);
        long cycle = 0;
        bus.WriteLong(0xC01000, uint.MaxValue, ref cycle, M68kBusAccessKind.CpuDataWrite);
        bus.WriteLong(0xC01004, uint.MaxValue, ref cycle, M68kBusAccessKind.CpuDataWrite);
        var before = cycle;
        bus.WriteLong(0xC01001, 0x12345678, ref cycle, M68kBusAccessKind.CpuDataWrite);
        Assert.True(cycle - before >= 12); // Three physical 16/8-bit transfers.
        Assert.Equal(cycle, machine.Cycle);
        Assert.Equal(0x12345678u, bus.ReadLong(0xC01001, ref cycle, M68kBusAccessKind.CpuDataRead));
        Assert.Equal(0xFF123456u, bus.ReadLong(0xC01000, ref cycle, M68kBusAccessKind.CpuDataRead));
        Assert.Equal(0x78FFFFFFu, bus.ReadLong(0xC01004, ref cycle, M68kBusAccessKind.CpuDataRead));
        Assert.Equal(cycle, machine.Cycle);
    }

    [Theory]
    [InlineData(M68kCpuModel.M68020, 0x11223344u)]
    [InlineData(M68kCpuModel.M68EC020, 0xAABBCCDDu)]
    [InlineData(M68kCpuModel.M68030, 0x11223344u)]
    [InlineData(M68kCpuModel.M68040, 0x11223344u)]
    [InlineData(M68kCpuModel.M68060, 0x11223344u)]
    public void OnlyEc020AliasesAbove24Bits(M68kCpuModel model, uint expected)
    {
        using var machine = new LightweightA500Machine(new() { CpuModel = model });
        var bus = new LightweightAcceleratorBus(machine, model);
        long cycle = 0;
        bus.WriteLong(0x4000, 0x11223344, ref cycle, M68kBusAccessKind.CpuDataWrite);
        bus.WriteLong(0x01004000, 0xAABBCCDD, ref cycle, M68kBusAccessKind.CpuDataWrite);
        Assert.Equal(expected, bus.ReadLong(0x4000, ref cycle, M68kBusAccessKind.CpuDataRead));
        Assert.Equal(model != M68kCpuModel.M68EC020 ? uint.MaxValue : expected,
            bus.ReadLong(0x01004000, ref cycle, M68kBusAccessKind.CpuDataRead));
        var before = machine.Cycle;
        Assert.Equal(model != M68kCpuModel.M68EC020 ? 0xFFFF : 0xAABB, bus.ReadHostWord(0x01004000));
        Assert.Equal(0xFFFF, bus.ReadHostWord(0xDFF000));
        Assert.Equal(before, machine.Cycle); // Cache peeks do not strobe devices.
    }

    [Theory]
    [InlineData(M68kCpuModel.M68020, 0x22334400u)]
    [InlineData(M68kCpuModel.M68EC020, 0xABCDEF00u)]
    [InlineData(M68kCpuModel.M68030, 0x22334400u)]
    [InlineData(M68kCpuModel.M68040, 0x22334400u)]
    [InlineData(M68kCpuModel.M68060, 0x22334400u)]
    public void Crossing24BitBoundaryWrapsEachEc020TransferOnly(M68kCpuModel model, uint expected)
    {
        using var machine = new LightweightA500Machine(new() { CpuModel = model });
        machine.WriteChipWordDma(0, 0x2233);
        machine.WriteChipWordDma(2, 0x4400);
        var bus = new LightweightAcceleratorBus(machine, model);
        long cycle = 0;
        bus.WriteLong(0x00FFFFFF, 0x89ABCDEF, ref cycle, M68kBusAccessKind.CpuDataWrite);
        Assert.Equal(expected, bus.ReadLong(0, ref cycle, M68kBusAccessKind.CpuDataRead));
    }

    [Theory]
    [InlineData(M68kCpuModel.M68020, 0u)]
    [InlineData(M68kCpuModel.M68EC020, 0u)]
    [InlineData(M68kCpuModel.M68020, 0x2000u)]
    [InlineData(M68kCpuModel.M68EC020, 0x2000u)]
    [InlineData(M68kCpuModel.M68030, 0u)]
    [InlineData(M68kCpuModel.M68040, 0u)]
    [InlineData(M68kCpuModel.M68060, 0u)]
    [InlineData(M68kCpuModel.M68030, 0x2000u)]
    [InlineData(M68kCpuModel.M68040, 0x2000u)]
    [InlineData(M68kCpuModel.M68060, 0x2000u)]
    [InlineData(M68kCpuModel.M68040, 0u, M68kExecutionMode.Jit)]
    [InlineData(M68kCpuModel.M68040, 0x2000u, M68kExecutionMode.Jit)]
    [InlineData(M68kCpuModel.M68040, 0u, M68kExecutionMode.Jit, 8)]
    public void InterruptWakesStoppedAcceleratorWithFormatZeroFrame(
        M68kCpuModel model, uint vectorBase, M68kExecutionMode executionMode = M68kExecutionMode.Interpreter, int multiplier = 1)
    {
        using var machine = new LightweightA500Machine(new() { CpuModel = model, CpuExecutionMode = executionMode, CpuClockMultiplier = multiplier });
        machine.WriteChipWordDma(0x1000, 0x4E72);
        machine.WriteChipWordDma(0x1002, 0x2000);
        machine.WriteChipWordDma(vectorBase + 0x0070, 0);
        machine.WriteChipWordDma(vectorBase + 0x0072, 0x1100);
        machine.WriteChipWordDma(0x1100, 0x60FE);
        machine.Reset();
        machine.Cpu.VectorBaseRegister = vectorBase;
        var initialStack = machine.Cpu.A[7];
        // A pending audio interrupt is masked at reset, then accepted by STOP.
        machine.WriteCustomRegisterFromCopper(0x09A, 0xC080, 0);
        machine.WriteCustomRegisterFromCopper(0x09C, 0x8080, 0);
        machine.ExecuteFrame();
        Assert.False(machine.Cpu.Stopped);
        Assert.Equal(0x1100u, machine.Cpu.ProgramCounter);
        Assert.Equal(4, (machine.Cpu.StatusRegister >> 8) & 7);
        Assert.Equal(initialStack - 8, machine.Cpu.A[7]);
        var bus = new LightweightAcceleratorBus(machine, model);
        var cycle = machine.Cycle;
        Assert.Equal(0x2000, bus.ReadWord(machine.Cpu.A[7], ref cycle, M68kBusAccessKind.CpuDataRead));
        Assert.Equal(0x1004u, bus.ReadLong(machine.Cpu.A[7] + 2, ref cycle, M68kBusAccessKind.CpuDataRead));
        Assert.Equal(0x0070, bus.ReadWord(machine.Cpu.A[7] + 6, ref cycle, M68kBusAccessKind.CpuDataRead));
    }

    [Theory]
    [InlineData(M68kCpuModel.M68020)]
    [InlineData(M68kCpuModel.M68EC020)]
    [InlineData(M68kCpuModel.M68030)]
    [InlineData(M68kCpuModel.M68040)]
    [InlineData(M68kCpuModel.M68060)]
    [InlineData(M68kCpuModel.M68040, M68kExecutionMode.Jit)]
    [InlineData(M68kCpuModel.M68040, M68kExecutionMode.Jit, 8)]
    public void MaskedInterruptDoesNotWakeStoppedAccelerator(
        M68kCpuModel model, M68kExecutionMode executionMode = M68kExecutionMode.Interpreter, int multiplier = 1)
    {
        using var machine = new LightweightA500Machine(new() { CpuModel = model, CpuExecutionMode = executionMode, CpuClockMultiplier = multiplier });
        machine.WriteChipWordDma(0x1000, 0x4E72);
        machine.WriteChipWordDma(0x1002, 0x2600);
        machine.Reset();
        var stack = machine.Cpu.A[7];
        machine.WriteCustomRegisterFromCopper(0x09A, 0xC080, 0);
        machine.WriteCustomRegisterFromCopper(0x09C, 0x8080, 0);
        machine.ExecuteFrame();
        Assert.True(machine.Cpu.Stopped);
        Assert.Equal(0x1004u, machine.Cpu.ProgramCounter);
        Assert.Equal(stack, machine.Cpu.A[7]);
        Assert.Equal(0x2600, machine.Cpu.StatusRegister);
        Assert.Equal(machine.Cycle * multiplier, machine.Cpu.Cycles);
        Assert.Equal(1, machine.CompletedFrames);
    }
}
