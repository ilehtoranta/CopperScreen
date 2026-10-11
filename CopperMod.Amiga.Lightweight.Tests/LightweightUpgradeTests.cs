using System.Buffers.Binary;
using Copper68k;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

public sealed class LightweightUpgradeTests
{
    private const M68kBusAccessKind Read = M68kBusAccessKind.CpuDataRead, Write = M68kBusAccessKind.CpuDataWrite;
    private static LightweightA500Configuration Configuration(M68kCpuModel cpu, bool aga, int fast = 16 * 1048576, bool jit = false)
        => new() { CpuModel = cpu, CpuExecutionMode = jit ? M68kExecutionMode.Jit : M68kExecutionMode.Interpreter,
            AgnusModel = aga ? LightweightAgnusModel.Mos8374Alice : LightweightAgnusModel.Mos8371,
            DeniseModel = aga ? LightweightDeniseModel.Lisa4203 : LightweightDeniseModel.Mos8362,
            ChipRamBytes = aga ? 2097152 : 524288, SlowRamBytes = aga ? 0 : 524288, FastRamBytes = fast };

    [Theory]
    [InlineData(M68kCpuModel.M68020, false)] [InlineData(M68kCpuModel.M68030, false)] [InlineData(M68kCpuModel.M68040, false)]
    [InlineData(M68kCpuModel.M68020, true)] [InlineData(M68kCpuModel.M68030, true)] [InlineData(M68kCpuModel.M68040, true)]
    public void Full32BitCpuConfiguresHighRamWithoutMotherboardAliases(M68kCpuModel cpu, bool aga)
    {
        using var m = new LightweightA500Machine(Configuration(cpu, aga));
        var bus = new LightweightAcceleratorBus(m, cpu); long cycle = 0;
        var originalChipWord = m.ReadChipWordDma(0);
        Assert.Equal(0xA0, bus.ReadByte(0xFF000000, ref cycle, Read));
        Assert.Equal(0xA0, bus.ReadByte(0xE80000, ref cycle, Read));
        Assert.Equal(0xCF, bus.ReadByte(0xFF000008, ref cycle, Read));
        bus.WriteLong(0x40000000, 0x12345678, ref cycle, Write);
        Assert.Equal(uint.MaxValue, bus.ReadLong(0x40000000, ref cycle, Read));
        bus.WriteWord(0xFF000044, 0x4000, ref cycle, Write);
        Assert.Equal(0x40000000u, m.FastRamBase);
        var end = 0x40000000u + (uint)m.FastRam.Length;
        bus.WriteLong(0x40000001, 0x12345678, ref cycle, Write);
        bus.WriteLong(end - 4, 0xABCDEF12, ref cycle, Write);
        Assert.Equal(0x12345678u, bus.ReadLong(0x40000001, ref cycle, Read));
        Assert.Equal(0xABCDEF12u, bus.ReadLong(end - 4, ref cycle, Read));
        Assert.Equal(0xEF12FFFFu, bus.ReadLong(end - 2, ref cycle, Read));
        Assert.Equal(originalChipWord, m.ReadChipWordDma(0));
        Assert.True(m.TryPeekInstructionWord(0x40000002, out var word)); Assert.Equal(0x3456, word);
        var before = cycle; bus.ReadLong(0x40FFFFFE, ref cycle, Read); Assert.Equal(cycle, m.Cycle);
        Assert.True(cycle > before);
        m.Reset(); Assert.Null(m.FastRamBase);
        bus.WriteByte(0xE80046, 0, ref cycle, Write); bus.WriteByte(0xE80044, 0x40, ref cycle, Write);
        bus.WriteByte(0xE8004A, 0, ref cycle, Write); bus.WriteByte(0xE80048, 0, ref cycle, Write);
        Assert.Equal(0x40000000u, m.FastRamBase);
        Assert.Equal(0xABCDEF12u, bus.ReadLong(end - 4, ref cycle, Read));
        m.ResetExternalDevices(m.Cycle); Assert.Null(m.FastRamBase);
    }

    [Theory]
    [InlineData(M68kCpuModel.M68000)] [InlineData(M68kCpuModel.M68010)] [InlineData(M68kCpuModel.M68EC020)]
    public void NarrowAddressCpuCannotFitHighRam(M68kCpuModel cpu)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new LightweightA500Machine(Configuration(cpu, false)));

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void HighRamInstructionsExecuteAndJitInvalidatesModifiedCode(bool aga)
    {
        using var m = new LightweightA500Machine(Configuration(M68kCpuModel.M68040, aga, jit: true));
        var bus = new LightweightAcceleratorBus(m, M68kCpuModel.M68040); long cycle = 0;
        bus.WriteWord(0xFF000044, 0x4000, ref cycle, Write);
        bus.WriteLong(0x40000100, 0x70015280, ref cycle, Write); // MOVEQ #1,D0; ADDQ #1,D0
        bus.WriteWord(0x40000104, 0x60FC, ref cycle, Write);
        m.Cpu.ProgramCounter = 0x40000100; m.Cpu.CacheControlRegister = 0x8000;
        m.ExecuteFrame(); Assert.True(m.Cpu.D[0] > 1); Assert.True(m.JitStatistics!.Value.CompiledInstructions > 0);
        var generation = m.GetJitCodePageGeneration(0x40000100);
        bus.WriteWord(0x40000102, 0x5480, ref cycle, Write); // ADDQ #2,D0
        Assert.NotEqual(generation, m.GetJitCodePageGeneration(0x40000100));
        Assert.Equal(0x5480, m.ReadJitCodeWord(0x40000102));
        m.ExecuteFrame(); Assert.Null(m.UnsupportedActiveFeature);
        Assert.Equal(m.Cpu.Cycles, m.Cycle);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void StorageRequestsBuffersAndGuestGatewaysRetainFull32BitAddresses(bool aga)
    {
        var path = Path.Combine(Path.GetTempPath(), $"copperscreen-upgrade-{Guid.NewGuid():N}.hdf");
        try
        {
            CopperDisk.AmigaHardfile.CreateBlank(path, 32768);
            using var m = new LightweightA500Machine(Configuration(M68kCpuModel.M68040, aga, jit: true) with { Hardfiles = [new(0, path)] });
            var cpuBus = new LightweightAcceleratorBus(m, M68kCpuModel.M68040); long cycle = 0;
            cpuBus.WriteWord(0xFF000044, 0x4000, ref cycle, Write);
            var bus = m.HardfileBus!;
            const uint unit = 0x40001000, request = 0x40001100, buffer = 0x40002000;
            var data = Enumerable.Range(0, 512).Select(i => (byte)(i * 13)).ToArray();
            bus.CopperHdf.Hardfiles[0].Write(512, data);
            bus.WriteLong(unit, 0); bus.WriteLong(request + 24, unit);
            bus.WriteWord(request + 28, 2); bus.WriteLong(request + 36, 512);
            bus.WriteLong(request + 40, buffer); bus.WriteLong(request + 44, 512);
            _ = m.ReadJitCodeWord(buffer);
            var generation = m.GetJitCodePageGeneration(buffer);
            Assert.True(bus.CopperHdf.TryExecuteIoRequest(bus, request));
            Assert.Equal(0, bus.ReadByte(request + 31)); Assert.Equal(512u, bus.ReadLong(request + 32));
            Assert.Equal(data, m.FastRam.Span.Slice((int)(buffer - m.FastRamBase!.Value), 512).ToArray());
            Assert.NotEqual(generation, m.GetJitCodePageGeneration(buffer));
            bus.WriteWord(request + 28, 3); bus.WriteLong(request + 44, 1024);
            Assert.True(bus.CopperHdf.TryExecuteIoRequest(bus, request));
            Assert.Equal(data, bus.CopperHdf.Hardfiles[0].ReadBytes(1024, 512));

            const uint gateway = 0x40004000, program = 0x40003000;
            bus.RegisterHostGateway(gateway, state => state.D[1] = 0x76543210);
            bus.WriteWord(gateway + 6, 0x4E75); // RTS after the registered callback
            bus.WriteWord(program, 0x4EB9); bus.WriteLong(program + 2, gateway);
            bus.WriteWord(program + 6, 0x4E72); bus.WriteWord(program + 8, 0x2700);
            m.Cpu.ProgramCounter = program;
            m.ExecuteFrame();
            Assert.True(m.Cpu.Stopped); Assert.Equal(0x76543210u, m.Cpu.D[1]);
            Assert.Equal(m.Cpu.Cycles, m.Cycle);
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData(16)] [InlineData(32)] [InlineData(64)] [InlineData(128)] [InlineData(256)] [InlineData(512)] [InlineData(1024)]
    public void ExtendedSizeTableCoversEveryImplementedBank(int mib)
    {
        var identity = new LightweightFastRam(mib * 1048576).Identity;
        Assert.Equal(AutoconfigBusKind.ZorroIII, identity.BusKind);
        Assert.Equal((byte)(0xA0 | System.Numerics.BitOperations.Log2((uint)mib) - 4), identity.Type);
        Assert.Equal(0x30, identity.Flags);
    }
}
