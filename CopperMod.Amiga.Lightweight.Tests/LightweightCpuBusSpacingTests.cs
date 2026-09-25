using Copper68k;
using CopperMod.Amiga.Lightweight;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

public sealed class LightweightCpuBusSpacingTests
{
    [Theory]
    [InlineData(0x1000u)]
    [InlineData(0xC01000u)]
    public void CpuMemoryTransfersRetainFourClockBusCycles(uint codeAddress)
    {
        using var machine = new LightweightA500Machine();
        long cycle = 0;
        // MOVE.W 32(A0),D0; CLR.B 34(A0); BRA back. Chip and slow RAM
        // share the Agnus bus; no display DMA is required to expose spacing.
        ushort[] code = [0x3028, 0x0020, 0x4228, 0x0022, 0x60F6];
        for (var i = 0; i < code.Length; i++)
            machine.WriteWord(codeAddress + (uint)i * 2, code[i], ref cycle, M68kBusAccessKind.CpuDataWrite);
        machine.Reset();
        var bus = new ObservedBus(machine);
        using var cpu = M68kCoreFactory.Default.Create(M68kCpuModel.M68000, bus);
        cpu.Reset(codeAddress, 0x70000);
        cpu.State.A[0] = 0xC02000;
        for (var i = 0; i < 30; i++) cpu.ExecuteInstruction();

        Assert.True(bus.Transfers.Count > 60);
        // MC68000 user manual section 5: S0..S7 occupy four CPU clocks.
        // A one-CCK Agnus grant is not the complete CPU bus cycle.
        for (var i = 1; i < bus.Transfers.Count; i++)
            Assert.True(bus.Transfers[i] - bus.Transfers[i - 1] >= 4,
                $"CPU strobes at {bus.Transfers[i - 1]} and {bus.Transfers[i]} are less than four clocks apart.");
    }

    private sealed class ObservedBus(LightweightA500Machine machine) : IM68kBus, IM68000BusCycleTiming
    {
        private readonly IM68000BusCycleTiming? timing = (machine as object) as IM68000BusCycleTiming;
        internal List<long> Transfers { get; } = [];
        public int M68000BusCycleStartDelay => timing?.M68000BusCycleStartDelay ?? 0;
        public bool RequiresExactM68000PipelineFallback => timing?.RequiresExactM68000PipelineFallback ?? false;
        public M68000BusAccessTiming GetM68000BusAccessTiming(uint a, M68kOperandSize s,
            M68kBusAccessKind k, bool write, long requested, long completed)
            => timing?.GetM68000BusAccessTiming(a, s, k, write, requested, completed)
                ?? new(completed, completed);
        public byte ReadByte(uint a, ref long c, M68kBusAccessKind k)
        { var value = machine.ReadByte(a, ref c, k); Transfers.Add(c - 2); return value; }
        public ushort ReadWord(uint a, ref long c, M68kBusAccessKind k)
        { var value = machine.ReadWord(a, ref c, k); Transfers.Add(c - 2); return value; }
        public uint ReadLong(uint a, ref long c, M68kBusAccessKind k)
            => machine.ReadLong(a, ref c, k);
        public void WriteByte(uint a, byte v, ref long c, M68kBusAccessKind k)
        { machine.WriteByte(a, v, ref c, k); Transfers.Add(c - 2); }
        public void WriteWord(uint a, ushort v, ref long c, M68kBusAccessKind k)
        { machine.WriteWord(a, v, ref c, k); Transfers.Add(c - 2); }
        public void WriteLong(uint a, uint v, ref long c, M68kBusAccessKind k)
            => machine.WriteLong(a, v, ref c, k);
        public void ResetExternalDevices(long c) => machine.ResetExternalDevices(c);
    }
}
