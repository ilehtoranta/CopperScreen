using Copper68k;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

public sealed class LightweightOcsIdentificationTests
{
    private const uint DeniseId = LightweightA500Machine.CustomBase + 0x07C;

    // OCS has no DENISEID register (Commodore HRM, Appendix C). The idle
    // pull-up / preceding-DMA distinction is the bounded bus model documented
    // in docs/engine/TOWER_ASSAULT_INVESTIGATION.md, not an ECS revision ID.
    [Theory]
    [InlineData(0x0000)]
    [InlineData(0x00F8)]
    [InlineData(0x00FC)]
    public void AbsentIdDoesNotReadBackRegisterStorage(ushort written)
    {
        using var machine = new LightweightA500Machine();
        long cycle = 100;
        machine.WriteWord(DeniseId, written, ref cycle, M68kBusAccessKind.CpuDataWrite);
        Assert.Equal(0xFFFF, machine.ReadWord(DeniseId, ref cycle, M68kBusAccessKind.CpuDataRead));
        Assert.Equal(0xFF, machine.ReadByte(DeniseId, ref cycle, M68kBusAccessKind.CpuDataRead));
        Assert.Equal(0xFF, machine.ReadByte(DeniseId + 1, ref cycle, M68kBusAccessKind.CpuDataRead));
    }

    [Theory]
    [InlineData(0, 0x1268)]
    [InlineData(1, 0x94FC)]
    [InlineData(2, 0xCA03)]
    [InlineData(3, 0xBEEF)]
    public void PrecedingAudioDmaWordReachesCpuWithoutChangingReadTiming(int channel, ushort data)
    {
        using var machine = AudioDma(channel, data);
        long cycle = 34 + channel * 4;
        Assert.Equal(data, machine.ReadWord(DeniseId, ref cycle, M68kBusAccessKind.CpuDataRead));
        Assert.Equal(36 + channel * 4, cycle);
        Assert.Equal(0xFFFF, machine.ReadWord(DeniseId, ref cycle, M68kBusAccessKind.CpuDataRead));
    }

    [Theory]
    [InlineData(0, 0x12)]
    [InlineData(1, 0x68)]
    public void ByteReadSelectsCorrectLaneOfPrecedingDmaWord(uint lane, byte expected)
    {
        using var machine = AudioDma(0, 0x1268);
        long cycle = 34;
        Assert.Equal(expected, machine.ReadByte(DeniseId + lane, ref cycle, M68kBusAccessKind.CpuDataRead));
    }

    [Fact]
    public void DiagnosticMemoryAccessDoesNotOverwriteDrivenDmaWord()
    {
        using var machine = AudioDma(0, 0x1268);
        machine.AdvanceHardwareTo(32);
        machine.WriteChipWordDma(0x1000, 0xCAFE);
        Assert.Equal(0xCAFE, machine.ReadChipWordDma(0x1000));
        long cycle = 34;
        Assert.Equal(0x1268, machine.ReadWord(DeniseId, ref cycle, M68kBusAccessKind.CpuDataRead));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResetDiscardsOldDmaSample(bool external)
    {
        using var machine = AudioDma(0, 0x1268);
        machine.AdvanceHardwareTo(32);
        if (external) machine.ResetExternalDevices(32);
        else machine.Reset();
        long cycle = 34;
        Assert.Equal(0xFFFF, machine.ReadWord(DeniseId, ref cycle, M68kBusAccessKind.CpuDataRead));
    }

    [Theory]
    [InlineData(28, 0xFFFF)]
    [InlineData(31, 0x1268)]
    public void OnlyCompletedAcceptedDmaDrivesBus(long disableCycle, ushort expected)
    {
        using var machine = AudioDma(0, 0x1268);
        machine.AdvanceHardwareTo(disableCycle);
        machine.WriteCustomRegisterFromCopper(0x096, 1, disableCycle);
        long cycle = 34;
        Assert.Equal(expected, machine.ReadWord(DeniseId, ref cycle, M68kBusAccessKind.CpuDataRead));
    }

    private static LightweightA500Machine AudioDma(int channel, ushort data)
    {
        var machine = new LightweightA500Machine();
        machine.WriteChipWordDma(0x1000, data);
        machine.WriteCustomRegisterFromCopper((ushort)(0x0A2 + channel * 16), 0x1000, 0);
        machine.WriteCustomRegisterFromCopper((ushort)(0x0A4 + channel * 16), 2, 0);
        machine.WriteCustomRegisterFromCopper((ushort)(0x0A6 + channel * 16), 200, 0);
        machine.WriteCustomRegisterFromCopper(0x096, (ushort)(0x8200 | 1 << channel), 0);
        return machine;
    }

    // Preserve the previously verified bus-latch model while removing its
    // duplicate timestamp. Exercise actual device transfers, not helper calls.
    [Theory]
    [InlineData("bitplane", 0xA136)]
    [InlineData("copper", 0x0180)]
    [InlineData("sprite", 0xFE00)]
    [InlineData("blitter-read", 0xA136)]
    [InlineData("blitter-write", 0xCAFE)]
    public void CompletedGraphicsDmaWordReachesCpu(string source, ushort expected)
    {
        using var machine = new LightweightA500Machine();
        // Start after refresh slots so a CPU read in the next free CCK is
        // not displaced across several subsequent Copper transfers.
        machine.AdvanceHardwareTo(32);
        void Write(ushort register, ushort value) =>
            machine.WriteCustomRegisterFromCopper(register, value, machine.Cycle);
        machine.WriteChipWordDma(0x2000, expected);
        long LastOutput() => source switch
        {
            "bitplane" => machine.BitplaneLastOutputCycle,
            "copper" => machine.CopperLastOutputCycle,
            "sprite" => machine.SpriteDmaLastOutputCycle,
            _ => machine.BlitterLastOutputCycle
        };
        switch (source)
        {
            case "bitplane":
                Write(0x0E2, 0x2000);
                Write(0x08E, 0x0100);
                Write(0x090, 0x0300);
                Write(0x092, 0x0038);
                Write(0x094, 0x0040);
                Write(0x100, 0x1000);
                Write(0x096, 0x8300);
                break;
            case "copper":
                machine.WriteChipWordDma(0x2002, 0x0ABC);
                Write(0x082, 0x2000);
                Write(0x096, 0x8280);
                Write(0x088, 0);
                break;
            case "sprite":
                machine.WriteChipWordDma(0x2002, 0xFF00);
                Write(0x122, 0x2000);
                Write(0x096, 0x8220);
                break;
            default:
                Write(0x040, source == "blitter-read" ? (ushort)0x08F0 : (ushort)0x01F0);
                Write(0x074, expected);
                Write(0x044, 0xFFFF);
                Write(0x046, 0xFFFF);
                Write(0x052, 0x2000);
                Write(0x056, 0x3000);
                Write(0x096, 0x8240);
                Write(0x058, 0x0041);
                break;
        }
        while (LastOutput() < 0 && machine.Cycle < 12000)
            machine.AdvanceHardwareTo(machine.Cycle + 2);
        Assert.True(LastOutput() >= 0, $"No {source} transfer");
        var outputCycle = LastOutput();
        // Changing backing RAM after DMA must not change the retained word.
        machine.WriteChipWordDma(0x2000, 0x5ACE);
        long cycle = outputCycle + 2;
        var observed = machine.ReadWord(DeniseId, ref cycle, M68kBusAccessKind.CpuDataRead);
        Assert.True(expected == observed,
            $"{source}: {observed:X4}, output={outputCycle}, completed={cycle}, copperPC={machine.CopperProgramCounter:X}");
        Assert.Equal(outputCycle + 4, cycle);
    }

    [Fact]
    public void BlitterInternalPhasesDoNotMakeStaleDmaWordVisible()
    {
        using var machine = AudioDma(0, 0x1268);
        machine.AdvanceHardwareTo(36);
        machine.WriteCustomRegisterFromCopper(0x096, 1, 36);
        // No enabled memory channels: internal blitter phases do not drive RAM.
        machine.WriteCustomRegisterFromCopper(0x040, 0x00FF, 36);
        machine.WriteCustomRegisterFromCopper(0x058, 0x0048, 36);
        for (long cycle = 40; cycle < 96;)
            Assert.Equal(0xFFFF, machine.ReadWord(DeniseId, ref cycle, M68kBusAccessKind.CpuDataRead));
        Assert.Equal(long.MinValue, machine.BlitterLastOutputCycle);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DiskDmaWriteDrivesAbsentIdIncludingLowHalfOfLongRead(bool longAccess)
    {
        using var machine = new LightweightA500Machine();
        machine.WriteCustomRegisterFromCopper(0x022, 0x1000, 0);
        machine.WriteCustomRegisterFromCopper(0x096, 0x8210, 0);
        machine.WriteCustomRegisterFromCopper(0x024, 0x8001, 0);
        machine.WriteCustomRegisterFromCopper(0x024, 0x8001, 0);
        for (var bit = 0; bit < 16; bit++) machine.ReceiveDiskBit(0xCAFE, false, 0);
        machine.WriteCustomRegisterFromCopper(0x096, 0x8210, 0);
        // Disk address14 / output16; CPU can accept18. For the long read,
        // the first half occupies14 and the second half follows the DMA write.
        long cycle = longAccess ? 14 : 16;
        if (longAccess)
            Assert.Equal(0xCAFEu, machine.ReadLong(DeniseId - 2, ref cycle, M68kBusAccessKind.CpuDataRead) & 0xFFFF);
        else
            Assert.Equal(0xCAFE, machine.ReadWord(DeniseId, ref cycle, M68kBusAccessKind.CpuDataRead));
        Assert.Equal(20, cycle);
        Assert.Equal(0xCAFE, machine.ReadChipWordDma(0x1000));
    }
}
