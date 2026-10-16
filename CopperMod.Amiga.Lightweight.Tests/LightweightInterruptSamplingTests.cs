using System.Buffers.Binary;
using Copper68k;
using CopperMod.Amiga.Lightweight;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

public sealed class LightweightInterruptSamplingTests
{
    [Theory]
    [InlineData(0x2000)]
    [InlineData(0x2700)]
    public void RepeatedInterruptsRetainTheSameInstructionSamplingWindow(ushort gapStatus)
    {
        using var machine = new LightweightA500Machine();
        long setup = 0;
        void Put(uint address, IEnumerable<ushort> words)
        {
            foreach (var word in words)
            {
                machine.WriteWord(address, word, ref setup, M68kBusAccessKind.CpuDataWrite);
                address += 2;
            }
        }
        // Call the same pulse routine twice, with a long inactive interval.
        // The second pulse must not inherit the first pulse's sampled IPL.
        ushort[] begin = [0x207C, 0, 0x0900, 0x4EB9, 0, 0x1100, 0x46FC, gapStatus];
        ushort[] end = [0x46FC, 0x2000, 0x4EB9, 0, 0x1100, 0x33FC, 0x7FFF, 0x00DF, 0xF09A, 0x60FE];
        Put(0x1000, begin.Concat(Enumerable.Repeat((ushort)0x4E71, 16)).Concat(end));
        Put(0x1100, [0x33FC, 0x8020, 0x00DF, 0xF09C, 0x4E71, 0x4E71, 0x4E75]);
        // Record the stacked return PC, acknowledge VERTB, then RTE.
        Put(0x1200, [0x202F, 2, 0x20C0, 0x33FC, 0x0020, 0x00DF, 0xF09C, 0x4E73]);
        machine.WriteLong(0x6C, 0x1200, ref setup, M68kBusAccessKind.CpuDataWrite);
        machine.Reset();
        setup = machine.Cpu.Cycles;
        machine.WriteWord(0xDFF09A, 0xC020, ref setup, M68kBusAccessKind.CpuDataWrite);
        machine.Cpu.StatusRegister = 0x2000;

        machine.ExecuteFrame();

        var first = BinaryPrimitives.ReadUInt32BigEndian(machine.ChipRam.Span.Slice(0x900, 4));
        var second = BinaryPrimitives.ReadUInt32BigEndian(machine.ChipRam.Span.Slice(0x904, 4));
        Assert.Equal(0x110Au, first); // one NOP after the software request's late sample
        Assert.Equal(first, second);
        Assert.Equal(0x908u, machine.Cpu.A[0]);
    }
}
