using Copper68k;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

public sealed class LightweightCopperLineWrapTests
{
    [Theory]
    [InlineData(0, 0xFFD9, true)]
    [InlineData(4, 0xFFD9, true)]
    [InlineData(5, 0xFFD9, false)]
    [InlineData(6, 0xFFD5, false)]
    public void LateWaitCannotMatchAnUnwrappedHorizontalCounter(int planes, ushort firstWait, bool reachesMove)
    {
        // HRM chapter 2: HP ranges from $00 to $E2, then wraps. With five/six
        // lores planes the second WAIT's read is delayed by terminal DMA;
        // its first comparison is after HP has wrapped. It must stay asleep
        // through the low-eight-bit vertical wrap instead of seeing HP=$E4.
        using var machine = new LightweightA500Machine();
        const uint list = 0x2000;
        ushort[] words = [firstWait, 0xFFFE, 0xFFDF, 0xFFFE, 0x0180, 0x0678, 0xFFFF, 0xFFFE];
        for (var i = 0; i < words.Length; i++) machine.WriteChipWordDma(list + (uint)i * 2, words[i]);
        long cycle = machine.Cycle;
        foreach (var (offset, value) in new (ushort, ushort)[]
        {
            (0x08E, 0x2C81), (0x090, 0x2CC1), (0x092, 0x0028), (0x094, 0x00D8),
            (0x100, (ushort)(planes << 12)), (0x080, 0), (0x082, (ushort)list), (0x096, 0x8380)
        })
            machine.WriteWord(LightweightA500Machine.CustomBase + offset, value,
                ref cycle, M68kBusAccessKind.CpuDataWrite);

        machine.AdvanceHardwareTo(300L * LightweightClock.CpuCyclesPerLine);

        Assert.Equal(reachesMove ? (ushort)0x0678 : (ushort)0, machine.GetCustomRegister(0x180));
        if (!reachesMove)
        {
            Assert.True(machine.CopperWaiting);
            Assert.Equal(list + 8, machine.CopperProgramCounter);
        }
    }
}
