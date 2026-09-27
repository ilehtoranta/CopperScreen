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
    [InlineData(M68kCpuModel.M68010)]
    [InlineData(M68kCpuModel.M68030)]
    [InlineData(M68kCpuModel.M68040)]
    public void UnsupportedModelsAreRejected(M68kCpuModel model)
        => Assert.Throws<ArgumentException>(() => new LightweightA500Machine(new() { CpuModel = model }));

    [Theory]
    [InlineData(M68kCpuModel.M68020)]
    [InlineData(M68kCpuModel.M68EC020)]
    public void AcceleratorRunsWithCacheEnabledInMotherboardClockDomain(M68kCpuModel model)
    {
        using var machine = new LightweightA500Machine(new() { CpuModel = model });
        ushort[] code = [0x7001, 0x4E7B, 0x0002, 0x4E71, 0x60FC];
        for (var i = 0; i < code.Length; i++) machine.WriteChipWordDma(0x1000u + (uint)i * 2, code[i]);
        machine.Reset();
        for (var i = 0; i < 4; i++) machine.ExecuteFrame();
        Assert.Equal(model, machine.CpuModel);
        Assert.Equal(1u, machine.Cpu.CacheControlRegister);
        Assert.InRange(machine.Cpu.ProgramCounter, 0x1006u, 0x1008u);
        Assert.InRange(machine.Cycle, 4L * 454 * 313, 4L * 454 * 313 + 100);
        Assert.Equal(machine.Cycle, machine.Cpu.Cycles);
        Assert.InRange(machine.Cpu.NativeCycles, machine.Cpu.Cycles * 2, machine.Cpu.Cycles * 2 + 20);
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
    public void OnlyEc020AliasesAbove24Bits(M68kCpuModel model, uint expected)
    {
        using var machine = new LightweightA500Machine(new() { CpuModel = model });
        var bus = new LightweightAcceleratorBus(machine, model);
        long cycle = 0;
        bus.WriteLong(0x4000, 0x11223344, ref cycle, M68kBusAccessKind.CpuDataWrite);
        bus.WriteLong(0x01004000, 0xAABBCCDD, ref cycle, M68kBusAccessKind.CpuDataWrite);
        Assert.Equal(expected, bus.ReadLong(0x4000, ref cycle, M68kBusAccessKind.CpuDataRead));
        Assert.Equal(model == M68kCpuModel.M68020 ? uint.MaxValue : expected,
            bus.ReadLong(0x01004000, ref cycle, M68kBusAccessKind.CpuDataRead));
        var before = machine.Cycle;
        Assert.Equal(model == M68kCpuModel.M68020 ? 0xFFFF : 0xAABB, bus.ReadHostWord(0x01004000));
        Assert.Equal(0xFFFF, bus.ReadHostWord(0xDFF000));
        Assert.Equal(before, machine.Cycle); // Cache peeks do not strobe devices.
    }

    [Theory]
    [InlineData(M68kCpuModel.M68020, 0x22334400u)]
    [InlineData(M68kCpuModel.M68EC020, 0xABCDEF00u)]
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
    public void InterruptWakesStoppedAcceleratorWithFormatZeroFrame(M68kCpuModel model, uint vectorBase)
    {
        using var machine = new LightweightA500Machine(new() { CpuModel = model });
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
    public void MaskedInterruptDoesNotWakeStoppedAccelerator(M68kCpuModel model)
    {
        using var machine = new LightweightA500Machine(new() { CpuModel = model });
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
        Assert.Equal(machine.Cycle, machine.Cpu.Cycles);
        Assert.Equal(1, machine.CompletedFrames);
    }
}
