using Copper68k;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

public sealed class LightweightBlitterTests
{
    private const uint SourceA = 0x10000;
    private const uint SourceB = 0x12000;
    private const uint SourceC = 0x14000;
    private const uint DestinationD = 0x16000;
    private const long QuietStartCycle =
        LightweightClock.CpuCyclesPerLine + (80 * LightweightClock.CpuCyclesPerColorClock);

    [Theory]
    [InlineData(0x09F0)]
    [InlineData(0x0FF0)]
    public void AreaSourcesKeepOneWordAheadOfAnOverlappingDestination(ushort control)
    {
        using var machine = CreateAgaMachine();
        machine.WriteChipWordDma(SourceA, 0x1111);
        machine.WriteChipWordDma(SourceA + 2, 0x2222);
        machine.WriteChipWordDma(SourceA + 4, 0x3333);
        machine.WriteChipWordDma(SourceA + 6, 0x4444);
        ConfigureArea(machine, control);
        WritePointer(machine, LightweightRegisters.Bltdpth, SourceA + 2);
        EnableBlitter(machine, nasty: false);
        StartBlit(machine, 0x0043);
        AdvanceUntilBlitterIdle(machine);

        Assert.Equal(0x1111, machine.ReadChipWordDma(SourceA + 2));
        Assert.Equal(0x2222, machine.ReadChipWordDma(SourceA + 4));
        Assert.Equal(0x3333, machine.ReadChipWordDma(SourceA + 6));
    }

    private static LightweightA500Machine CreateAgaMachine() => new(new()
    {
        AgnusModel = LightweightAgnusModel.Mos8374Alice,
        DeniseModel = LightweightDeniseModel.Lisa4203,
        CpuModel = M68kCpuModel.M68EC020,
        ChipRamBytes = 2 * 1024 * 1024,
        SlowRamBytes = 0
    });

    [Theory]
    [InlineData(LightweightRegisters.Bltamod, LightweightRegisters.Bltapth, SourceA)]
    [InlineData(LightweightRegisters.Bltbmod, LightweightRegisters.Bltbpth, SourceB)]
    [InlineData(LightweightRegisters.Bltcmod, LightweightRegisters.Bltcpth, SourceC)]
    [InlineData(LightweightRegisters.Bltdmod, LightweightRegisters.Bltdpth, DestinationD)]
    public void ActiveAreaModuloWriteAffectsTheNextRowBoundary(
        ushort moduloRegister, ushort pointerRegister, uint address)
    {
        using var machine = CreateAgaMachine();
        ConfigureArea(machine, 0x0FF0);
        WriteRegister(machine, moduloRegister, 2);
        EnableBlitter(machine, nasty: false);
        var start = StartBlit(machine, 0x0104);
        machine.AdvanceHardwareTo(start + 20);
        Assert.True(machine.BlitterActive);
        Assert.InRange(machine.GetBlitterPointer(pointerRegister), address, address + 6);

        machine.WriteCustomRegisterFromCopper(moduloRegister, 0x0010, machine.Cycle);
        AdvanceUntilBlitterIdle(machine);

        Assert.Equal(address + 4 * (8 + 16), machine.GetBlitterPointer(pointerRegister));
    }

    [Theory]
    [InlineData(LightweightRegisters.Bltcon0)]
    [InlineData(LightweightRegisters.Bltcon0l)]
    public void LiveAreaMintermUsesTheNewValueForTheNextCalculation(ushort controlRegister)
    {
        using var machine = CreateAgaMachine();
        machine.WriteChipWordDma(SourceA, 0xA00B);
        machine.WriteChipWordDma(SourceA + 2, 0x135C);
        machine.WriteChipWordDma(SourceA + 4, 0xB460);
        ConfigureArea(machine, 0x49FF);
        EnableBlitter(machine, nasty: false);
        var start = StartBlit(machine, 0x0043);
        for (var cycle = start + 2;
             cycle <= start + 32 && machine.GetBlitterPointer(LightweightRegisters.Bltdpth) == DestinationD;
             cycle += LightweightClock.CpuCyclesPerColorClock)
        {
            machine.AdvanceHardwareTo(cycle);
        }
        Assert.Equal(DestinationD + 2, machine.GetBlitterPointer(LightweightRegisters.Bltdpth));
        Assert.Equal(0xFFFF, machine.ReadChipWordDma(DestinationD));

        machine.WriteCustomRegisterFromCopper(controlRegister, 0x49F0, machine.Cycle);
        AdvanceUntilBlitterIdle(machine);

        Assert.Equal(0xFFFF, machine.ReadChipWordDma(DestinationD));
        Assert.Equal(0xFFFF, machine.ReadChipWordDma(DestinationD + 2));
        Assert.Equal(0xCB46, machine.ReadChipWordDma(DestinationD + 4));
    }

    [Theory]
    [InlineData(LightweightRegisters.Bltcon0, 0x09F0, 0x49F0, SourceA)]
    [InlineData(LightweightRegisters.Bltcon1, 0x05CC, 0x4000, SourceB)]
    public void LiveAreaShiftWriteAppliesAfterTheAlreadyCalculatedWord(
        ushort register, ushort initialControl0, ushort newControl, uint source)
    {
        using var machine = CreateAgaMachine();
        machine.WriteChipWordDma(source, 0xA00B);
        machine.WriteChipWordDma(source + 2, 0x135C);
        machine.WriteChipWordDma(source + 4, 0xB460);
        ConfigureArea(machine, initialControl0);
        EnableBlitter(machine, nasty: false);
        var start = StartBlit(machine, 0x0043);
        for (var cycle = start + 2;
             cycle <= start + 32 && machine.GetBlitterPointer(LightweightRegisters.Bltdpth) == DestinationD;
             cycle += LightweightClock.CpuCyclesPerColorClock)
        {
            machine.AdvanceHardwareTo(cycle);
        }
        Assert.Equal(DestinationD + 2, machine.GetBlitterPointer(LightweightRegisters.Bltdpth));

        machine.WriteCustomRegisterFromCopper(register, newControl, machine.Cycle);
        AdvanceUntilBlitterIdle(machine);

        Assert.Equal(0xA00B, machine.ReadChipWordDma(DestinationD));
        Assert.Equal(0x135C, machine.ReadChipWordDma(DestinationD + 2));
        Assert.Equal(0xCB46, machine.ReadChipWordDma(DestinationD + 4));
    }

    [Theory]
    [InlineData(false, 0xBEEF, 0xBEEF, 0xBEEF, 0xA00B, 2)]
    [InlineData(true, 0xA00B, 0x135C, 0xB460, 0xB460, 6)]
    public void LiveAreaAEnableChangesUnacceptedReadsWithDestinationEnabled(
        bool sourceEnabled, ushort firstWord, ushort secondWord, ushort thirdWord,
        ushort fourthWord, uint sourceAdvance)
    {
        using var machine = CreateAgaMachine();
        machine.WriteChipWordDma(SourceA, 0xA00B);
        machine.WriteChipWordDma(SourceA + 2, 0x135C);
        machine.WriteChipWordDma(SourceA + 4, 0xB460);
        ConfigureArea(machine, sourceEnabled ? (ushort)0x09F0 : (ushort)0x01F0);
        WriteRegister(machine, LightweightRegisters.Bltadat, 0xBEEF);
        EnableBlitter(machine, nasty: false);
        var start = StartBlit(machine, 0x0044);
        for (var cycle = start + 2;
             cycle <= start + 32 && machine.GetBlitterPointer(LightweightRegisters.Bltdpth) == DestinationD;
             cycle += LightweightClock.CpuCyclesPerColorClock)
        {
            machine.AdvanceHardwareTo(cycle);
        }
        Assert.Equal(DestinationD + 2, machine.GetBlitterPointer(LightweightRegisters.Bltdpth));

        machine.WriteCustomRegisterFromCopper(LightweightRegisters.Bltcon0,
            sourceEnabled ? (ushort)0x01F0 : (ushort)0x09F0, machine.Cycle);
        AdvanceUntilBlitterIdle(machine);

        Assert.Equal(SourceA + sourceAdvance, machine.GetBlitterPointer(LightweightRegisters.Bltapth));
        Assert.Equal(firstWord, machine.ReadChipWordDma(DestinationD));
        Assert.Equal(secondWord, machine.ReadChipWordDma(DestinationD + 2));
        Assert.Equal(thirdWord, machine.ReadChipWordDma(DestinationD + 4));
        Assert.Equal(fourthWord, machine.ReadChipWordDma(DestinationD + 6));
    }

    [Fact]
    public void InsertingAreaBWithAnAPulseCanReadCWithUseCClear()
    {
        using var machine = CreateAgaMachine();
        ConfigureArea(machine, 0x09AA);
        EnableBlitter(machine, nasty: false);
        var start = StartBlit(machine, 0x0048);

        machine.AdvanceHardwareTo(start + 8);
        machine.WriteCustomRegisterFromCopper(LightweightRegisters.Bltcon0, 0x0DAA, machine.Cycle);
        machine.AdvanceHardwareTo(start + 14);
        Assert.Equal(SourceB + 2, machine.GetBlitterPointer(LightweightRegisters.Bltbpth));
        Assert.Equal(SourceC, machine.GetBlitterPointer(LightweightRegisters.Bltcpth));

        machine.AdvanceHardwareTo(start + 18);
        Assert.Equal(SourceC + 2, machine.GetBlitterPointer(LightweightRegisters.Bltcpth));
        Assert.Equal(start + 18, machine.BlitterLastOutputCycle);
    }

    [Fact]
    public void RemovingAreaBWithABPulseKeepsBusyAssertedUntilAnotherStart()
    {
        using var machine = CreateAgaMachine();
        ConfigureArea(machine, 0x0DF0);
        EnableBlitter(machine, nasty: false);
        var start = StartBlit(machine, 0x0048);

        machine.AdvanceHardwareTo(start + 6);
        machine.WriteCustomRegisterFromCopper(LightweightRegisters.Bltcon0, 0x09F0, machine.Cycle);
        machine.AdvanceHardwareTo(start + 88);
        Assert.True(machine.BlitterStatusBusy);
        Assert.Equal(SourceA + 2, machine.GetBlitterPointer(LightweightRegisters.Bltapth));
        Assert.Equal(SourceB, machine.GetBlitterPointer(LightweightRegisters.Bltbpth));
        Assert.Equal(DestinationD, machine.GetBlitterPointer(LightweightRegisters.Bltdpth));

        StartBlit(machine, 0x0041);
        machine.AdvanceHardwareTo(machine.Cycle + 48);
        Assert.False(machine.BlitterStatusBusy);
        Assert.Equal(DestinationD + 2, machine.GetBlitterPointer(LightweightRegisters.Bltdpth));
    }

    [Fact]
    public void RemovingAreaDRetiresItsAcceptedWriteAndFinishesTheRemainingSources()
    {
        using var machine = CreateAgaMachine();
        machine.WriteChipWordDma(SourceA, 0x1234);
        machine.WriteChipWordDma(DestinationD + 2, 0xFEDC);
        ConfigureArea(machine, 0x09F0);
        EnableBlitter(machine, nasty: false);
        var start = StartBlit(machine, 0x0044);

        machine.AdvanceHardwareTo(start + 12);
        Assert.Equal(DestinationD, machine.GetBlitterPointer(LightweightRegisters.Bltdpth));
        machine.WriteCustomRegisterFromCopper(LightweightRegisters.Bltcon0, 0x08F0, machine.Cycle);
        machine.AdvanceHardwareTo(start + 50);

        Assert.False(machine.BlitterStatusBusy);
        Assert.Equal(SourceA + 8, machine.GetBlitterPointer(LightweightRegisters.Bltapth));
        Assert.Equal(DestinationD + 2, machine.GetBlitterPointer(LightweightRegisters.Bltdpth));
        Assert.Equal(0x1234, machine.ReadChipWordDma(DestinationD));
        Assert.Equal(0xFEDC, machine.ReadChipWordDma(DestinationD + 2));
    }

    [Theory]
    [InlineData(LightweightRegisters.Bltadat, 0x01F0, 0, 0x1234, 0xABCD)]
    [InlineData(LightweightRegisters.Bltbdat, 0x01CC, 0x5000, 0x0091, 0x055E)]
    [InlineData(LightweightRegisters.Bltcdat, 0x01AA, 0, 0x1234, 0xABCD)]
    public void LiveAreaDataWriteFeedsLaterWordsWithDmaDisabledForThatSource(
        ushort register, ushort control0, ushort control1, ushort oldWord, ushort newWord)
    {
        using var machine = CreateAgaMachine();
        ConfigureArea(machine, control0, control1);
        WriteRegister(machine, register, 0x1234);
        EnableBlitter(machine, nasty: false);
        var start = StartBlit(machine, 0x0044);
        for (var cycle = start + 2;
             cycle <= start + 32 && machine.GetBlitterPointer(LightweightRegisters.Bltdpth) == DestinationD;
             cycle += LightweightClock.CpuCyclesPerColorClock)
        {
            machine.AdvanceHardwareTo(cycle);
        }
        Assert.Equal(DestinationD + 2, machine.GetBlitterPointer(LightweightRegisters.Bltdpth));
        Assert.Equal(oldWord, machine.ReadChipWordDma(DestinationD));

        machine.WriteCustomRegisterFromCopper(register, 0xABCD, machine.Cycle);
        AdvanceUntilBlitterIdle(machine);

        Assert.Equal(oldWord, machine.ReadChipWordDma(DestinationD));
        Assert.Equal(newWord, machine.ReadChipWordDma(DestinationD + 6));
    }

    [Theory]
    [InlineData(LightweightRegisters.Bltcon0, 0x09F0, 0x0000)]
    [InlineData(LightweightRegisters.Bltcon0l, 0x09F0, 0xFFFF)]
    [InlineData(LightweightRegisters.Bltcon1, 0x0012, 0x0000)]
    [InlineData(LightweightRegisters.Bltcon1, 0x0016, 0xFFFF)]
    public void LiveAreaControlWriteAppliesFillCarryAfterTheAlreadyCalculatedWord(
        ushort controlRegister, ushort controlValue, ushort lastWord)
    {
        using var machine = CreateAgaMachine();
        machine.WriteChipWordDma(SourceA + 4, 1);
        ConfigureArea(machine, 0x09F0, 0x0012);
        WritePointer(machine, LightweightRegisters.Bltapth, SourceA + 4);
        WritePointer(machine, LightweightRegisters.Bltdpth, DestinationD + 4);
        EnableBlitter(machine, nasty: false);
        var start = StartBlit(machine, 0x0043);
        machine.AdvanceHardwareTo(start + 18);
        Assert.Equal(DestinationD + 2, machine.GetBlitterPointer(LightweightRegisters.Bltdpth));

        machine.WriteCustomRegisterFromCopper(controlRegister, controlValue, machine.Cycle);
        AdvanceUntilBlitterIdle(machine);

        Assert.Equal(0xFFFF, machine.ReadChipWordDma(DestinationD + 4));
        Assert.Equal(0xFFFF, machine.ReadChipWordDma(DestinationD + 2));
        Assert.Equal(lastWord, machine.ReadChipWordDma(DestinationD));
    }

    [Fact]
    public void AliceCompletionProtectsFinalDestinationFromCpuStealAcrossBitplaneDma()
    {
        using var machine = CreateAgaMachine();
        const long lineStart = 100L * LightweightClock.CpuCyclesPerLine;
        machine.WriteChipWordDma(SourceA, 0xA5A5);
        machine.WriteChipWordDma(DestinationD, 0x1234);
        ConfigureArea(machine, 0x0FF0);
        for (var plane = 0; plane < 7; plane++)
        {
            WritePointer(machine, (ushort)(LightweightRegisters.BplPointerFirst + plane * 4),
                (uint)(0x8000 + 0x800 * plane));
        }
        WriteRegister(machine, LightweightRegisters.Diwstrt, 0x2C81);
        WriteRegister(machine, LightweightRegisters.Diwstop, 0x2CC1);
        WriteRegister(machine, LightweightRegisters.Ddfstrt, 0x0038);
        WriteRegister(machine, LightweightRegisters.Ddfstop, 0x00D0);
        WriteRegister(machine, LightweightRegisters.Bplcon0, 0x7401);
        WriteRegister(machine, LightweightRegisters.DmaconWrite, 0x8340);

        machine.AdvanceHardwareTo(lineStart + 0x31 * 2);
        machine.WriteCustomRegisterFromCopper(LightweightRegisters.Bltsize, 0x0041, machine.Cycle);
        machine.AdvanceHardwareTo(lineStart + 0x39 * 2);

        Assert.False(machine.BlitterStatusBusy);
        Assert.True(machine.BlitterActive);
        Assert.Equal(0x0040, machine.Intreq & 0x0040);
        Assert.Equal(0x1234, machine.ReadChipWordDma(DestinationD));
        Assert.Equal(long.MaxValue, machine.BlitterPendingOutputCycle);

        var cpuCycle = lineStart + 0x3A * 2;
        Assert.Equal(0xA5A5, machine.ReadWord(DestinationD, ref cpuCycle, M68kBusAccessKind.CpuDataRead));
        Assert.Equal(lineStart + 0x41 * 2, machine.BlitterLastTerminationCycle);
        Assert.Equal(lineStart + 0x4A * 2, cpuCycle);
    }

    [Theory]
    [InlineData(LightweightRegisters.Bltapth, SourceA)]
    [InlineData(LightweightRegisters.Bltbpth, SourceB)]
    [InlineData(LightweightRegisters.Bltcpth, SourceC)]
    public void ActiveAreaSourcePointerWriteFeedsSubsequentReads(ushort pointerRegister, uint source)
    {
        using var machine = new LightweightA500Machine();
        const uint replacement = 0x24000;
        for (var word = 0; word < 64; word++)
        {
            WriteWord(machine, source + (uint)(word * 2), 0x1111);
            WriteWord(machine, replacement + (uint)(word * 2), 0xCAFE);
        }
        ConfigureArea(machine, 0x0FFE); // A OR B OR C; other sources contain zero.
        EnableBlitter(machine, nasty: false);
        var start = StartBlit(machine, 0x0040); // One row of 64 words.
        machine.AdvanceHardwareTo(start + 40);
        WriteRegister(machine, LightweightRegisters.DmaconWrite, 0x0040);
        machine.AdvanceHardwareTo(machine.Cycle + 16); // Drain accepted output.
        Assert.True(machine.BlitterBusy);
        Assert.Equal((ushort)0x1111, ReadWord(machine, DestinationD));

        WritePointer(machine, pointerRegister, replacement);
        WriteRegister(machine, LightweightRegisters.DmaconWrite, 0x8040);
        AdvanceUntilBlitterIdle(machine);

        Assert.Equal((ushort)0x1111, ReadWord(machine, DestinationD));
        Assert.Equal((ushort)0xCAFE, ReadWord(machine, DestinationD + 126));
        Assert.InRange(machine.GetBlitterPointer(pointerRegister), replacement + 2, replacement + 128);
    }

    [Theory]
    [InlineData(LightweightRegisters.Bltapth, SourceA, 6)]
    [InlineData(LightweightRegisters.Bltbpth, SourceB, 8)]
    [InlineData(LightweightRegisters.Bltcpth, SourceC, 10)]
    [InlineData(LightweightRegisters.Bltdpth, DestinationD, 20)]
    public void AreaDmaPointerWritebackUsesTheAcceptedAddress(
        ushort pointerRegister, uint address, int acceptanceOffset)
    {
        using var machine = CreateAgaMachine();
        ConfigureArea(machine, 0x0FFF);
        EnableBlitter(machine, nasty: true);
        var start = StartBlit(machine, 0x0043);
        machine.AdvanceHardwareTo(start + acceptanceOffset);
        var outputCycle = machine.Cycle + LightweightClock.CpuCyclesPerColorClock;
        Assert.Equal(outputCycle, machine.BlitterPendingOutputCycle);
        Assert.Equal(address, machine.GetBlitterPointer(pointerRegister));

        var replacement = address + 0x0100;
        machine.WriteCustomRegisterFromCopper((ushort)(pointerRegister + 2), (ushort)replacement, machine.Cycle);
        Assert.Equal(replacement, machine.GetBlitterPointer(pointerRegister));

        machine.AdvanceHardwareTo(outputCycle);
        Assert.Equal(outputCycle, machine.BlitterLastOutputCycle);
        Assert.Equal(address + 2, machine.GetBlitterPointer(pointerRegister));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ActiveAreaDestinationHalfWriteRedirectsRemainingWords(bool descending, bool highHalf)
    {
        using var machine = new LightweightA500Machine();
        for (var word = -64; word < 128; word++)
        {
            WriteWord(machine, (uint)(DestinationD + word * 2), 0xBEEF);
            WriteWord(machine, (uint)(0x26000 + word * 2), 0xBEEF);
            WriteWord(machine, (uint)(0x17000 + word * 2), 0xBEEF);
        }
        ConfigureArea(machine, 0x0100, descending ? (ushort)2 : (ushort)0);
        EnableBlitter(machine, nasty: false);
        var start = StartBlit(machine, 0x0040);
        machine.AdvanceHardwareTo(start + 40);
        WriteRegister(machine, LightweightRegisters.DmaconWrite, 0x0040);
        machine.AdvanceHardwareTo(machine.Cycle + 16);
        Assert.True(machine.BlitterBusy);
        var previous = machine.GetBlitterPointer(LightweightRegisters.Bltdpth);
        var direction = descending ? -1 : 1;
        var completed = ((int)previous - (int)DestinationD) / (2 * direction);
        Assert.InRange(completed, 1, 63);

        WriteRegister(machine, (ushort)(LightweightRegisters.Bltdpth + (highHalf ? 0 : 2)),
            highHalf ? (ushort)2 : (ushort)0x7001); // Low bit is not an address bit.
        var replacement = machine.GetBlitterPointer(LightweightRegisters.Bltdpth);
        WriteRegister(machine, LightweightRegisters.DmaconWrite, 0x8040);
        AdvanceUntilBlitterIdle(machine);

        Assert.Equal((ushort)0xBEEF, ReadWord(machine, previous));
        for (var word = 0; word < 64 - completed; word++)
            Assert.Equal((ushort)0, ReadWord(machine, (uint)(replacement + word * 2 * direction)));
        Assert.Equal((uint)(replacement + (64 - completed) * 2 * direction),
            machine.GetBlitterPointer(LightweightRegisters.Bltdpth));
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, -1)]
    [InlineData(true, -1)]
    public void FinalAreaRowIncludesModuloForEveryEnabledPointer(bool descending, int moduloSign)
    {
        // HRM BLTxPT: completed pointers include the last increment and modulo.
        using var machine = new LightweightA500Machine();
        ConfigureArea(machine, 0x0FFE, descending ? (ushort)2 : (ushort)0);
        WriteRegister(machine, LightweightRegisters.Bltamod, unchecked((ushort)(2 * moduloSign)));
        WriteRegister(machine, LightweightRegisters.Bltbmod, unchecked((ushort)(4 * moduloSign)));
        WriteRegister(machine, LightweightRegisters.Bltcmod, unchecked((ushort)(6 * moduloSign)));
        WriteRegister(machine, LightweightRegisters.Bltdmod, unchecked((ushort)(8 * moduloSign)));
        WriteWord(machine, SourceA, 0x1111);
        WriteWord(machine, SourceB, 0x2222);
        WriteWord(machine, SourceC, 0x4444);
        EnableBlitter(machine, nasty: true);
        _ = StartBlit(machine, 0x0041);
        AdvanceUntilBlitterIdle(machine);

        var direction = descending ? -1 : 1;
        Assert.Equal((ushort)0x7777, ReadWord(machine, DestinationD));
        Assert.Equal((uint)(SourceA + direction * (2 + 2 * moduloSign)), machine.GetBlitterPointer(LightweightRegisters.Bltapth));
        Assert.Equal((uint)(SourceB + direction * (2 + 4 * moduloSign)), machine.GetBlitterPointer(LightweightRegisters.Bltbpth));
        Assert.Equal((uint)(SourceC + direction * (2 + 6 * moduloSign)), machine.GetBlitterPointer(LightweightRegisters.Bltcpth));
        Assert.Equal((uint)(DestinationD + direction * (2 + 8 * moduloSign)), machine.GetBlitterPointer(LightweightRegisters.Bltdpth));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AreaRestartContinuesStridedSourceWithoutReloadingPointers(bool descending)
    {
        // A 1,800-row pass must be split at the OCS 1,024-row size limit.
        // Sentinel words distinguish a missing final modulo from valid data.
        using var machine = new LightweightA500Machine();
        for (var i = 0; i < 1800; i++)
        {
            WriteWord(machine, SourceA + (uint)(i * 4), (ushort)(0x8000 + i));
            WriteWord(machine, SourceA + (uint)(i * 4 + 2), 0xDEAD);
        }
        ConfigureArea(machine, 0x09F0, descending ? (ushort)2 : (ushort)0);
        WriteRegister(machine, LightweightRegisters.Bltamod, 2);
        if (descending)
        {
            WritePointer(machine, LightweightRegisters.Bltapth, SourceA + 1799 * 4);
            WritePointer(machine, LightweightRegisters.Bltdpth, DestinationD + 1799 * 2);
        }
        EnableBlitter(machine, nasty: true);
        _ = StartBlit(machine, 1); // Height zero encodes 1,024 rows.
        AdvanceUntilBlitterIdle(machine);
        _ = StartBlit(machine, (776 << 6) | 1);
        AdvanceUntilBlitterIdle(machine);

        for (var i = 0; i < 1800; i++)
            Assert.Equal((ushort)(0x8000 + i), ReadWord(machine, DestinationD + (uint)(i * 2)));
    }

    [Theory]
    [InlineData(false, 8)]
    [InlineData(true, 12)]
    public void AreaReadAndWriteCommitAtRetainedFollowingOutputPhases(bool aga, int completionOffset)
    {
        using var machine = aga ? CreateAgaMachine() : new LightweightA500Machine();
        WriteWord(machine, SourceA, 0xCAFE);
        WriteWord(machine, DestinationD, 0x1234);
        ConfigureArea(machine, bltcon0: 0x09F0);
        EnableBlitter(machine, nasty: true);
        var start = StartBlit(machine, 0x0041);

        machine.AdvanceHardwareTo(start + 6);
        Assert.Equal(start + 8, machine.BlitterPendingOutputCycle);
        Assert.Equal(SourceA, machine.GetBlitterPointer(LightweightRegisters.Bltapth));
        Assert.Equal((ushort)0x1234, ReadWord(machine, DestinationD));

        machine.AdvanceHardwareTo(start + 8);
        Assert.Equal(SourceA + 2, machine.GetBlitterPointer(LightweightRegisters.Bltapth));
        Assert.Equal(start + 10, machine.BlitterPendingOutputCycle);
        Assert.Equal((ushort)0x1234, ReadWord(machine, DestinationD));

        machine.AdvanceHardwareTo(start + 12);
        Assert.False(machine.BlitterStatusBusy);
        Assert.True(machine.BlitterActive);
        Assert.Equal(start + completionOffset, machine.BlitterLastCompletionCycle);
        Assert.NotEqual(0, machine.Intreq & 0x0040);
        Assert.Equal((ushort)0x1234, ReadWord(machine, DestinationD));
        Assert.Equal(start + 14, machine.BlitterPendingOutputCycle);
        machine.AdvanceHardwareTo(start + 14);
        Assert.Equal((ushort)0xCAFE, ReadWord(machine, DestinationD));
        Assert.Equal(DestinationD + 2, machine.GetBlitterPointer(LightweightRegisters.Bltdpth));
        Assert.False(machine.BlitterZero);
        Assert.Equal(start + 14, machine.BlitterLastTerminationCycle);
    }

    [Fact]
    public void AllEnabledSourcesCompleteInAThenBThenCThenDOrder()
    {
        using var machine = new LightweightA500Machine();
        WriteWord(machine, SourceA, 0xFF00);
        WriteWord(machine, SourceB, 0x0F0F);
        WriteWord(machine, SourceC, 0x3333);
        ConfigureArea(machine, bltcon0: 0x0FCA);
        EnableBlitter(machine, nasty: true);
        var start = StartBlit(machine, 0x0041);

        machine.AdvanceHardwareTo(start + 8);
        Assert.Equal(SourceA + 2, machine.GetBlitterPointer(LightweightRegisters.Bltapth));
        Assert.Equal(SourceB, machine.GetBlitterPointer(LightweightRegisters.Bltbpth));
        machine.AdvanceHardwareTo(start + 10);
        Assert.Equal(SourceB + 2, machine.GetBlitterPointer(LightweightRegisters.Bltbpth));
        Assert.Equal(SourceC, machine.GetBlitterPointer(LightweightRegisters.Bltcpth));
        machine.AdvanceHardwareTo(start + 12);
        Assert.Equal(SourceC + 2, machine.GetBlitterPointer(LightweightRegisters.Bltcpth));
        Assert.Equal((ushort)0, ReadWord(machine, DestinationD));
        machine.AdvanceHardwareTo(start + 16);
        Assert.Equal((ushort)0, ReadWord(machine, DestinationD));
        machine.AdvanceHardwareTo(start + 18);

        Assert.Equal(
            ApplyMinterm(0xCA, 0xFF00, 0x0F0F, 0x3333),
            ReadWord(machine, DestinationD));
        Assert.Equal(start + 18, machine.BlitterLastOutputCycle);
    }

    [Fact]
    public void AreaMintermTruthTableAndEveryChannelCombinationUseLiveOrLatchedInputs()
    {
        const ushort memoryA = 0xFF00;
        const ushort memoryB = 0x0F0F;
        const ushort memoryC = 0x3333;
        const ushort latchedA = 0x00FF;
        const ushort latchedB = 0xF0F0;
        const ushort latchedC = 0xCCCC;
        const ushort sentinel = 0x5A5A;

        for (var minterm = 0; minterm < 256; minterm++)
        {
            using var machine = new LightweightA500Machine();
            WriteWord(machine, SourceA, memoryA);
            WriteWord(machine, SourceB, memoryB);
            WriteWord(machine, SourceC, memoryC);
            ConfigureArea(machine, (ushort)(0x0F00 | minterm));
            EnableBlitter(machine, nasty: true);
            _ = StartBlit(machine, 0x0041);
            AdvanceUntilBlitterIdle(machine);
            Assert.Equal(
                ApplyMinterm((byte)minterm, memoryA, memoryB, memoryC),
                ReadWord(machine, DestinationD));
        }

        for (var channels = 0; channels < 16; channels++)
        {
            using var machine = new LightweightA500Machine();
            WriteWord(machine, SourceA, memoryA);
            WriteWord(machine, SourceB, memoryB);
            WriteWord(machine, SourceC, memoryC);
            WriteWord(machine, DestinationD, sentinel);
            ConfigureArea(machine, (ushort)((channels << 8) | 0x00CA));
            WriteRegister(machine, LightweightRegisters.Bltadat, latchedA);
            WriteRegister(machine, LightweightRegisters.Bltbdat, latchedB);
            WriteRegister(machine, LightweightRegisters.Bltcdat, latchedC);
            EnableBlitter(machine, nasty: true);
            _ = StartBlit(machine, 0x0041);
            AdvanceUntilBlitterIdle(machine);

            var a = (channels & 8) != 0 ? memoryA : latchedA;
            var b = (channels & 4) != 0 ? memoryB : latchedB;
            var c = (channels & 2) != 0 ? memoryC : latchedC;
            var expected = (channels & 1) != 0
                ? ApplyMinterm(0xCA, a, b, c)
                : sentinel;
            Assert.Equal(expected, ReadWord(machine, DestinationD));
        }
    }

    [Fact]
    public void MasksShiftsPointersAndRowModulosFollowAreaWordBoundaries()
    {
        using var machine = new LightweightA500Machine();
        var sourceWords = new ushort[]
        {
            0xFFFF, 0xFFFF, 0xFFFF,
            0x1111, 0x2222,
            0x1234, 0x5678, 0x9ABC
        };
        for (var word = 0; word < sourceWords.Length; word++)
        {
            WriteWord(machine, SourceA + (uint)(word * 2), sourceWords[word]);
        }
        ConfigureArea(machine, bltcon0: 0x49F0);
        WriteRegister(machine, LightweightRegisters.Bltafwm, 0x0F0F);
        WriteRegister(machine, LightweightRegisters.Bltalwm, 0xF0F0);
        WriteRegister(machine, LightweightRegisters.Bltamod, 4);
        WriteRegister(machine, LightweightRegisters.Bltdmod, 4);
        EnableBlitter(machine, nasty: true);
        _ = StartBlit(machine, (ushort)((2 << 6) | 3));
        AdvanceUntilBlitterIdle(machine);

        ushort previous = 0;
        var expected = new ushort[6];
        var sourceIndexes = new[] { 0, 1, 2, 5, 6, 7 };
        for (var word = 0; word < expected.Length; word++)
        {
            var x = word % 3;
            var mask = x == 0 ? (ushort)0x0F0F : x == 2 ? (ushort)0xF0F0 : (ushort)0xFFFF;
            var current = (ushort)(sourceWords[sourceIndexes[word]] & mask);
            expected[word] = ShiftSource(current, ref previous, 4);
        }

        Assert.Equal(expected[0], ReadWord(machine, DestinationD));
        Assert.Equal(expected[1], ReadWord(machine, DestinationD + 2));
        Assert.Equal(expected[2], ReadWord(machine, DestinationD + 4));
        Assert.Equal(expected[3], ReadWord(machine, DestinationD + 10));
        Assert.Equal(expected[4], ReadWord(machine, DestinationD + 12));
        Assert.Equal(expected[5], ReadWord(machine, DestinationD + 14));
        Assert.Equal(SourceA + 20, machine.GetBlitterPointer(LightweightRegisters.Bltapth));
        Assert.Equal(DestinationD + 20, machine.GetBlitterPointer(LightweightRegisters.Bltdpth));
    }

    [Fact]
    public void StartupControlRetriesWhenItsFollowingOutputWouldBeRefreshOwned()
    {
        using var machine = new LightweightA500Machine();
        WriteWord(machine, SourceA, 0xCAFE);
        ConfigureArea(machine, bltcon0: 0x09F0);
        EnableBlitter(machine, nasty: true);
        var lineStart = 2L * LightweightClock.CpuCyclesPerLine;
        var start = WriteRegisterAt(
            machine,
            LightweightRegisters.Bltsize,
            0x0041,
            lineStart + (223 * LightweightClock.CpuCyclesPerColorClock));

        machine.AdvanceHardwareTo(start + 8);

        Assert.Equal(start + 10, machine.BlitterPendingOutputCycle);
        Assert.Equal(start + 8, machine.BlitterLastAcceptedInputCycle);
        machine.AdvanceHardwareTo(start + 12);
        Assert.Equal(start + 14, machine.BlitterPendingOutputCycle);
        machine.AdvanceHardwareTo(start + 14);
        Assert.Equal((ushort)0, ReadWord(machine, DestinationD));
        AdvanceUntilBlitterIdle(machine);
        Assert.Equal((ushort)0xCAFE, ReadWord(machine, DestinationD));
    }

    [Theory]
    [InlineData(false, 0x55)]
    [InlineData(true, 0x53)]
    public void AreaStartupRespectsTheCpuGrantAfterCopperAndBitplaneContention(
        bool priority, int firstBlitterClock)
    {
        using var machine = new LightweightA500Machine();
        const uint list = 0x2000;
        const int line = 3;
        const long lineStart = line * LightweightClock.CpuCyclesPerLine;
        const int cck = LightweightClock.CpuCyclesPerColorClock;
        WriteWord(machine, list, 0x0347);
        WriteWord(machine, list + 2, 0xFFFE);
        WriteWord(machine, list + 4, LightweightRegisters.Bltsize);
        WriteWord(machine, list + 6, 0x0041);
        WriteWord(machine, list + 8, 0xFFFF);
        WriteWord(machine, list + 10, 0xFFFE);
        WriteWord(machine, SourceA, 0xCAFE);
        ConfigureArea(machine, bltcon0: 0x09F0);
        WritePointer(machine, LightweightRegisters.Cop1lch, list);
        WriteRegister(machine, LightweightRegisters.Copcon, 0x8002);
        WriteRegister(machine, LightweightRegisters.Diwstrt, 0x0181);
        WriteRegister(machine, LightweightRegisters.Diwstop, 0x05C1);
        WriteRegister(machine, LightweightRegisters.Ddfstrt, 0x0038);
        WriteRegister(machine, LightweightRegisters.Ddfstop, 0x00D0);
        WriteRegister(machine, LightweightRegisters.Bplcon0, 0x4200);
        WriteRegister(machine, LightweightRegisters.DmaconWrite, (ushort)(0x83C0 | (priority ? 0x0400 : 0)));
        WriteRegister(machine, LightweightRegisters.Copjmp1, 0);

        var cpuCycle = lineStart + 0x48 * cck;
        Assert.Equal((ushort)0xCAFE, machine.ReadWord(SourceA, ref cpuCycle, M68kBusAccessKind.CpuDataRead));
        Assert.Equal(lineStart + 0x50 * cck, cpuCycle);

        var firstOutput = lineStart + firstBlitterClock * cck;
        machine.AdvanceHardwareTo(firstOutput - cck);
        Assert.True(machine.BlitterLastOutputCycle < lineStart);
        machine.AdvanceHardwareTo(firstOutput);
        Assert.Equal(firstOutput, machine.BlitterLastOutputCycle);
        AdvanceUntilBlitterIdle(machine);
        Assert.Equal((ushort)0xCAFE, ReadWord(machine, DestinationD));
    }

    [Theory]
    [InlineData(0x02AA, 10, 4)]
    [InlineData(0x06AA, 12, 6)]
    [InlineData(0x0AAA, 10, 4)]
    [InlineData(0x0EAA, 12, 6)]
    public void AreaBlitsWithCAndWithoutDRepeatAfterTheCRead(
        ushort control, int firstReadOffset, int readSpacing)
    {
        using var machine = new LightweightA500Machine();
        WriteWord(machine, SourceC, 0xCAFE);
        WriteWord(machine, DestinationD, 0x4321);
        ConfigureArea(machine, bltcon0: control);
        EnableBlitter(machine, nasty: true);
        var start = StartBlit(machine, 0x0043);

        Assert.True(machine.BlitterZero);
        for (var word = 0; word < 3; word++)
        {
            var readCycle = start + firstReadOffset + word * readSpacing;
            if (word == 2)
            {
                var inputCycle = readCycle - LightweightClock.CpuCyclesPerColorClock;
                machine.AdvanceHardwareTo(inputCycle);
                Assert.False(machine.BlitterStatusBusy);
                Assert.True(machine.BlitterActive);
                Assert.Equal(inputCycle, machine.BlitterLastCompletionCycle);
            }
            machine.AdvanceHardwareTo(readCycle);
            Assert.Equal(readCycle, machine.BlitterLastOutputCycle);
            Assert.Equal(SourceC + (uint)((word + 1) * 2), machine.GetBlitterPointer(LightweightRegisters.Bltcpth));
            Assert.False(machine.BlitterZero);
        }

        AdvanceUntilBlitterIdle(machine);
        Assert.Equal((ushort)0x4321, ReadWord(machine, DestinationD));
    }

    [Fact]
    public void AreaBOnlyBlitRetainsTheStartupClocksBeforeItsFirstRead()
    {
        using var machine = new LightweightA500Machine();
        WriteWord(machine, SourceB, 0xCAFE);
        WriteWord(machine, DestinationD, 0x4321);
        ConfigureArea(machine, bltcon0: 0x04CC);
        EnableBlitter(machine, nasty: true);
        var start = StartBlit(machine, 0x0043);

        machine.AdvanceHardwareTo(start + 8);
        Assert.Equal(SourceB, machine.GetBlitterPointer(LightweightRegisters.Bltbpth));
        Assert.True(machine.BlitterZero);

        for (var word = 0; word < 3; word++)
        {
            var readCycle = start + 10 + word * 6;
            machine.AdvanceHardwareTo(readCycle);
            Assert.Equal(readCycle, machine.BlitterLastOutputCycle);
            Assert.Equal(SourceB + (uint)((word + 1) * 2), machine.GetBlitterPointer(LightweightRegisters.Bltbpth));
            Assert.False(machine.BlitterZero);
        }

        AdvanceUntilBlitterIdle(machine);
        Assert.Equal(DestinationD, machine.GetBlitterPointer(LightweightRegisters.Bltdpth));
        Assert.Equal((ushort)0x4321, ReadWord(machine, DestinationD));
    }

    [Fact]
    public void BitplaneOutputKeepsPriorityAndDelaysOnlyIncomingBlitterPhase()
    {
        using var machine = new LightweightA500Machine();
        const uint plane = 0x3000;
        WriteWord(machine, plane, 0x8000);
        WriteRegister(machine, LightweightRegisters.BplPointerFirst, 0);
        WriteRegister(machine, (ushort)(LightweightRegisters.BplPointerFirst + 2), (ushort)plane);
        WriteRegister(machine, LightweightRegisters.Diwstrt, 0x0100);
        WriteRegister(machine, LightweightRegisters.Diwstop, 0x0300);
        WriteRegister(machine, LightweightRegisters.Ddfstrt, 0x0038);
        WriteRegister(machine, LightweightRegisters.Ddfstop, 0x0040);
        WriteRegister(machine, LightweightRegisters.Bplcon0, 0x1000);
        WriteWord(machine, SourceA, 0xCAFE);
        ConfigureArea(machine, bltcon0: 0x09F0);
        WriteRegister(machine, LightweightRegisters.DmaconWrite, 0x8740);
        var lineStart = LightweightClock.CpuCyclesPerLine;
        var start = WriteRegisterAt(
            machine,
            LightweightRegisters.Bltsize,
            0x0041,
            lineStart + (0x3C * LightweightClock.CpuCyclesPerColorClock));

        var bitplaneOutput = lineStart +
            (0x40 * LightweightClock.CpuCyclesPerColorClock);
        machine.AdvanceHardwareTo(bitplaneOutput);

        Assert.Equal(bitplaneOutput, machine.BitplaneLastOutputCycle);
        Assert.NotEqual(bitplaneOutput, machine.BlitterLastOutputCycle);
        Assert.Equal(bitplaneOutput + 2, machine.BlitterPendingOutputCycle);
        Assert.Equal(bitplaneOutput, machine.BlitterLastAcceptedInputCycle);
        machine.AdvanceHardwareTo(start + 14);
        Assert.Equal((ushort)0, ReadWord(machine, DestinationD));
        AdvanceUntilBlitterIdle(machine);
        Assert.Equal((ushort)0xCAFE, ReadWord(machine, DestinationD));
    }

    [Theory]
    [InlineData(false, 20)]
    [InlineData(true, 24)]
    public void DestinationOnlyStartupPublishesBusyBeforeFinalRetainedWriteDrains(bool aga, int completionOffset)
    {
        using var machine = aga ? CreateAgaMachine() : new LightweightA500Machine();
        for (var word = 0; word < 4; word++)
        {
            WriteWord(machine, DestinationD + (uint)(word * 2), 0xA5A5);
        }
        ConfigureArea(machine, bltcon0: 0x0100);
        EnableBlitter(machine, nasty: true);
        var start = StartBlit(machine, 0x0044);

        machine.AdvanceHardwareTo(start + 22);
        Assert.Equal(aga, machine.BlitterStatusBusy);
        Assert.Equal((ushort)0, ReadWord(machine, DestinationD + 4));
        Assert.Equal((ushort)0xA5A5, ReadWord(machine, DestinationD + 6));

        machine.AdvanceHardwareTo(start + 24);
        Assert.False(machine.BlitterStatusBusy);
        Assert.True(machine.BlitterActive);
        Assert.Equal(start + completionOffset, machine.BlitterLastCompletionCycle);
        Assert.Equal((ushort)0xA5A5, ReadWord(machine, DestinationD + 6));

        machine.AdvanceHardwareTo(start + 26);
        Assert.False(machine.BlitterActive);
        Assert.Equal((ushort)0, ReadWord(machine, DestinationD + 6));
        Assert.Equal(start + 26, machine.BlitterLastTerminationCycle);
    }

    [Fact]
    public void EnabledChannelsPauseWithDmaOffAndResumeFromSameStartupPhase()
    {
        using var machine = new LightweightA500Machine();
        WriteWord(machine, SourceA, 0x55AA);
        ConfigureArea(machine, bltcon0: 0x09F0);
        var start = StartBlit(machine, 0x0041);

        machine.AdvanceHardwareTo(start + 100);
        Assert.True(machine.BlitterStatusBusy);
        Assert.Equal(long.MaxValue, machine.BlitterNextCycle);
        Assert.Equal((ushort)0, ReadWord(machine, DestinationD));

        var enableCycle = start + 110;
        EnableBlitter(machine, nasty: true, requestedCycle: enableCycle);
        var enableOutput = enableCycle;
        machine.AdvanceHardwareTo(enableOutput + 14);

        Assert.Equal((ushort)0x55AA, ReadWord(machine, DestinationD));
        Assert.False(machine.BlitterActive);
    }

    [Fact]
    public void NiceModeYieldsFourthEligibleDmaSlotToPendingCpu()
    {
        using var machine = CreateThreeWordAllChannelBlit(nasty: false, out var start);
        var cpuCycle = start + 16;

        _ = machine.ReadWord(SourceA, ref cpuCycle, M68kBusAccessKind.CpuDataRead);

        Assert.Equal(start + 24, cpuCycle);
        Assert.Equal(start + 24, machine.BlitterLastOutputCycle);
        Assert.True(machine.BlitterActive);
    }

    [Fact]
    public void NastyModeKeepsEveryEligibleDmaSlotAcrossPendingCpu()
    {
        using var machine = CreateThreeWordAllChannelBlit(nasty: true, out var start);
        var cpuCycle = start + 16;

        _ = machine.ReadWord(SourceA, ref cpuCycle, M68kBusAccessKind.CpuDataRead);

        Assert.Equal(start + 34, cpuCycle);
        Assert.Equal(start + 34, machine.BlitterLastOutputCycle);
        Assert.Equal(start + 34, machine.BlitterLastTerminationCycle);
        Assert.False(machine.BlitterActive);
    }

    [Fact]
    public void BltpriChangesTwoCcksAfterDmaconOutput()
    {
        using var machine = new LightweightA500Machine();
        var setCycle = WriteRegisterAt(
            machine,
            LightweightRegisters.DmaconWrite,
            0x8400,
            QuietStartCycle);

        machine.AdvanceHardwareTo(setCycle + 3);
        Assert.False(machine.BlitterEffectiveNasty);
        machine.AdvanceHardwareTo(setCycle + 4);
        Assert.True(machine.BlitterEffectiveNasty);

        var clearCycle = WriteRegisterAt(
            machine,
            LightweightRegisters.DmaconWrite,
            0x0400,
            setCycle + 20);
        machine.AdvanceHardwareTo(clearCycle + 3);
        Assert.True(machine.BlitterEffectiveNasty);
        machine.AdvanceHardwareTo(clearCycle + 4);
        Assert.False(machine.BlitterEffectiveNasty);
    }

    [Fact]
    public void CopperBfdWaitObservesFinalPhysicalBlitterDrain()
    {
        using var machine = new LightweightA500Machine();
        const uint list = 0x2000;
        WriteWord(machine, list + 0, 0x0001);
        WriteWord(machine, list + 2, 0x7FFE);
        WriteWord(machine, list + 4, 0x0180);
        WriteWord(machine, list + 6, 0x0F00);
        WriteWord(machine, list + 8, 0xFFFF);
        WriteWord(machine, list + 10, 0xFFFE);
        WritePointer(machine, LightweightRegisters.Cop1lch, list);
        for (var word = 0; word < 4; word++)
        {
            WriteWord(machine, DestinationD + (uint)(word * 2), 0xA5A5);
        }
        ConfigureArea(machine, bltcon0: 0x0100);
        WriteRegister(machine, LightweightRegisters.DmaconWrite, 0x8240);
        var start = StartBlit(machine, 0x0044);
        WriteRegister(machine, LightweightRegisters.DmaconWrite, 0x8080);
        WriteRegister(machine, LightweightRegisters.Copjmp1, 0);

        for (var cycle = start + 2;
             cycle <= start + 80 && machine.BlitterStatusBusy;
             cycle += LightweightClock.CpuCyclesPerColorClock)
        {
            machine.AdvanceHardwareTo(cycle);
        }
        Assert.False(machine.BlitterStatusBusy);
        Assert.True(machine.BlitterActive);
        Assert.Equal((ushort)0, machine.GetCustomRegister(0x180));

        machine.AdvanceHardwareTo(machine.BlitterLastCompletionCycle + 40);
        Assert.False(machine.BlitterActive);
        Assert.Equal((ushort)0x0F00, machine.GetCustomRegister(0x180));
        Assert.True(machine.CopperLastMoveCycle > machine.BlitterLastTerminationCycle);
    }

    [Fact]
    public void DescendingShiftPointersAndEvenModuloMoveBackward()
    {
        for (var shift = 0; shift < 16; shift++)
        {
            using var machine = new LightweightA500Machine();
            WriteWord(machine, SourceA + 14, 0x5678);
            WriteWord(machine, SourceA + 12, 0x1234);
            WriteWord(machine, SourceA + 6, 0xABCD);
            WriteWord(machine, SourceA + 4, 0x0F0F);
            ConfigureArea(
                machine,
                bltcon0: (ushort)(0x0900 | (shift << 12) | 0x00F0),
                bltcon1: 0x0002);
            WritePointer(machine, LightweightRegisters.Bltapth, SourceA + 14);
            WritePointer(machine, LightweightRegisters.Bltdpth, DestinationD + 14);
            WriteRegister(machine, LightweightRegisters.Bltamod, 5);
            WriteRegister(machine, LightweightRegisters.Bltdmod, 5);
            EnableBlitter(machine, nasty: true);
            _ = StartBlit(machine, (ushort)((2 << 6) | 2));
            AdvanceUntilBlitterIdle(machine);

            ushort previous = 0;
            var expected0 = ShiftSource(0x5678, ref previous, shift, descending: true);
            var expected1 = ShiftSource(0x1234, ref previous, shift, descending: true);
            var expected2 = ShiftSource(0xABCD, ref previous, shift, descending: true);
            var expected3 = ShiftSource(0x0F0F, ref previous, shift, descending: true);
            Assert.Equal(expected0, ReadWord(machine, DestinationD + 14));
            Assert.Equal(expected1, ReadWord(machine, DestinationD + 12));
            Assert.Equal(expected2, ReadWord(machine, DestinationD + 6));
            Assert.Equal(expected3, ReadWord(machine, DestinationD + 4));
            Assert.Equal(SourceA - 2, machine.GetBlitterPointer(LightweightRegisters.Bltapth));
            Assert.Equal(DestinationD - 2, machine.GetBlitterPointer(LightweightRegisters.Bltdpth));
        }
    }

    [Theory]
    [InlineData(0x000A, 0xFFF8)]
    [InlineData(0x000E, 0x000F)]
    [InlineData(0x0012, 0xFFF8)]
    [InlineData(0x0016, 0x0007)]
    public void DescendingFillUsesInclusiveExclusiveCarryAndIdleCPhase(
        ushort bltcon1,
        ushort expected)
    {
        using var machine = new LightweightA500Machine();
        WriteWord(machine, SourceA, 0x0008);
        ConfigureArea(machine, bltcon0: 0x09F0, bltcon1: bltcon1);
        EnableBlitter(machine, nasty: true);
        var start = StartBlit(machine, 0x0041);
        AdvanceUntilBlitterIdle(machine);

        Assert.Equal(expected, ReadWord(machine, DestinationD));
        Assert.Equal(start + 16, machine.BlitterLastTerminationCycle);
    }

    [Fact]
    public void FillCarryRestartsAtEachAreaRow()
    {
        using var machine = new LightweightA500Machine();
        WriteWord(machine, SourceA + 2, 0x0001);
        WriteWord(machine, SourceA, 0x0001);
        ConfigureArea(machine, bltcon0: 0x09F0, bltcon1: 0x0012);
        WritePointer(machine, LightweightRegisters.Bltapth, SourceA + 2);
        WritePointer(machine, LightweightRegisters.Bltdpth, DestinationD + 2);
        EnableBlitter(machine, nasty: true);
        _ = StartBlit(machine, 0x0081);
        AdvanceUntilBlitterIdle(machine);

        Assert.Equal(0xFFFF, ReadWord(machine, DestinationD + 2));
        Assert.Equal(0xFFFF, ReadWord(machine, DestinationD));
    }

    [Theory]
    [InlineData(false, 22)]
    [InlineData(true, 26)]
    public void FillWithoutCDrainsThePreviousWordBeforeTheIdleSlot(bool alice, int interruptOffset)
    {
        using var machine = alice ? CreateAgaMachine() : new LightweightA500Machine();
        machine.WriteChipWordDma(SourceA + 4, 0x5000);
        machine.WriteChipWordDma(DestinationD + 4, 0xA5A5);
        ConfigureArea(machine, 0x09F0, 0x0012);
        WritePointer(machine, LightweightRegisters.Bltapth, SourceA + 4);
        WritePointer(machine, LightweightRegisters.Bltdpth, DestinationD + 4);
        EnableBlitter(machine, nasty: true);
        var start = StartBlit(machine, 0x0043);

        machine.AdvanceHardwareTo(start + 14);
        Assert.Equal(0xA5A5, machine.ReadChipWordDma(DestinationD + 4));
        machine.AdvanceHardwareTo(start + 16);
        Assert.Equal(0x3000, machine.ReadChipWordDma(DestinationD + 4));

        AdvanceUntilBlitterIdle(machine);
        Assert.Equal(start + interruptOffset, machine.BlitterLastCompletionCycle);
        Assert.Equal(start + 28, machine.BlitterLastTerminationCycle);
    }

    [Fact]
    public void OcsLineReadsCFiveColorClocksAfterStartAndWritesDTwoLater()
    {
        using var machine = new LightweightA500Machine();
        WriteWord(machine, DestinationD, 0x1234);
        ConfigureLine(machine, DestinationD, rowStride: 0x20, bltcon1: 0x0001);
        EnableBlitter(machine, nasty: true);
        var start = StartBlit(machine, 0x0042);

        machine.AdvanceHardwareTo(start + 6);
        Assert.Equal(start + 8, machine.BlitterPendingOutputCycle);
        Assert.Equal(long.MinValue, machine.BlitterLastOutputCycle);

        machine.AdvanceHardwareTo(start + 10);
        var cData = machine.GetCustomRegister(LightweightRegisters.Bltcdat);
        Assert.True(
            cData == 0x1234,
            $"BLTCDAT=0x{cData:X4}, cycle={machine.Cycle}, " +
            $"next={machine.BlitterNextCycle}, pending={machine.BlitterPendingOutputCycle}, " +
            $"lastInput={machine.BlitterLastAcceptedInputCycle}, lastOutput={machine.BlitterLastOutputCycle}, " +
            $"BLTCPT=0x{machine.GetBlitterPointer(LightweightRegisters.Bltcpth):X}");
        Assert.Equal(start + 10, machine.BlitterLastOutputCycle);
        Assert.Equal(start + 12, machine.BlitterPendingOutputCycle);
        Assert.Equal((ushort)0x1234, ReadWord(machine, DestinationD));

        machine.AdvanceHardwareTo(start + 12);
        Assert.True(machine.BlitterStatusBusy);
        Assert.Equal(start + 14, machine.BlitterPendingOutputCycle);
        Assert.Equal((ushort)0x1234, ReadWord(machine, DestinationD));

        machine.AdvanceHardwareTo(start + 14);
        Assert.False(machine.BlitterStatusBusy);
        Assert.False(machine.BlitterActive);
        Assert.Equal(start + 14, machine.BlitterLastCompletionCycle);
        Assert.Equal(start + 14, machine.BlitterLastTerminationCycle);
        Assert.Equal(start + 14, machine.BlitterLastOutputCycle);
        Assert.Equal((ushort)0x9234, ReadWord(machine, DestinationD));
        Assert.NotEqual(0, machine.Intreq & 0x0040);
    }

    [Fact]
    public void LineOctantsAndSignSelectTheHrmMajorAndMinorSteps()
    {
        const int rowStride = 0x20;
        for (var octant = 0; octant < 8; octant++)
        {
            foreach (var sign in new[] { false, true })
            {
                using var machine = new LightweightA500Machine();
                var baseAddress = DestinationD + 0x1000;
                var octantBits = (ushort)(octant << 2);
                var bltcon1 = (ushort)(0x0001 | octantBits |
                    (sign ? 0x0040 : 0));
                ConfigureLine(
                    machine,
                    baseAddress,
                    rowStride,
                    bltcon1,
                    initialAccumulator: sign ? (short)-2 : (short)0);
                EnableBlitter(machine, nasty: true);
                _ = StartBlit(machine, 0x0082);
                AdvanceUntilBlitterIdle(machine);

                var expected = GetLineDelta(octantBits, sign);
                Assert.True(IsLinePixelSet(machine, baseAddress, rowStride, 0, 0));
                Assert.True(IsLinePixelSet(
                    machine,
                    baseAddress,
                    rowStride,
                    expected.X,
                    expected.Y));
            }
        }
    }

    [Fact]
    public void LineTextureUsesTheConfiguredStartPhase()
    {
        for (var shift = 0; shift < 16; shift++)
        {
            using var machine = new LightweightA500Machine();
            var baseAddress = DestinationD + 0x1800;
            ConfigureLine(
                machine,
                baseAddress,
                rowStride: 0x20,
                bltcon1: (ushort)(0x0001 | (shift << 12)),
                texture: 0x8000);
            EnableBlitter(machine, nasty: true);
            _ = StartBlit(machine, 0x0042);
            AdvanceUntilBlitterIdle(machine);

            Assert.Equal(
                shift == 0,
                IsLinePixelSet(machine, baseAddress, 0x20, 0, 0));
        }
    }

    [Fact]
    public void OcsLineRequiresCButIgnoresDEnableAndLastWordMask()
    {
        var baseAddress = DestinationD + 0x2000;
        using (var withoutC = new LightweightA500Machine())
        {
            ConfigureLine(
                withoutC,
                baseAddress,
                rowStride: 0x20,
                bltcon1: 0x0001,
                channelMask: 0x0900);
            EnableBlitter(withoutC, nasty: true);
            _ = StartBlit(withoutC, 0x0042);
            AdvanceUntilBlitterIdle(withoutC);
            Assert.False(IsLinePixelSet(withoutC, baseAddress, 0x20, 0, 0));
        }

        using var withoutD = new LightweightA500Machine();
        ConfigureLine(
            withoutD,
            baseAddress,
            rowStride: 0x20,
            bltcon1: 0x0001,
            channelMask: 0x0A00);
        WriteRegister(withoutD, LightweightRegisters.Bltalwm, 0x0000);
        EnableBlitter(withoutD, nasty: true);
        _ = StartBlit(withoutD, 0x0042);
        AdvanceUntilBlitterIdle(withoutD);
        Assert.True(IsLinePixelSet(withoutD, baseAddress, 0x20, 0, 0));
    }

    [Fact]
    public void OcsSingleDotLineStillReadsCWhenItSkipsTheDestinationWrite()
    {
        using var machine = new LightweightA500Machine();
        var baseAddress = DestinationD + 0x2400;
        WriteWord(machine, baseAddress, 0x0100);
        WriteWord(machine, baseAddress + 2, 0x2468);
        ConfigureLine(machine, baseAddress, rowStride: 40, bltcon1: 0x0053,
            minterm: 0xFA, initialAccumulator: -2);
        WriteRegister(machine, LightweightRegisters.Bltcon0, 0xFBFA);
        EnableBlitter(machine, nasty: true);

        var start = StartBlit(machine, 0x0082);
        machine.AdvanceHardwareTo(start + 18);

        Assert.Equal(start + 18, machine.BlitterLastOutputCycle);
        Assert.Equal((ushort)0x2468,
            machine.GetCustomRegister(LightweightRegisters.Bltcdat));
        AdvanceUntilBlitterIdle(machine);
        Assert.Equal(start + 18, machine.BlitterLastOutputCycle);
        Assert.Equal((ushort)0x0101, ReadWord(machine, baseAddress));
        Assert.Equal((ushort)0x2468, ReadWord(machine, baseAddress + 2));
    }

    [Fact]
    public void ThreeDisplayPlanesDelayInternalLinePhases()
    {
        using var machine = new LightweightA500Machine();
        const uint list = 0x10000;
        WriteRegister(machine, LightweightRegisters.Bltafwm, 0xFFFF);
        WriteRegister(machine, LightweightRegisters.Bltalwm, 0xFFFF);
        WriteRegister(machine, LightweightRegisters.Diwstrt, 0x2881);
        WriteRegister(machine, LightweightRegisters.Diwstop, 0x28C1);
        WriteRegister(machine, LightweightRegisters.Ddfstrt, 0x0038);
        WriteRegister(machine, LightweightRegisters.Ddfstop, 0x00D0);
        WriteRegister(machine, LightweightRegisters.Bplcon0, 0x3200);
        for (var plane = 0; plane < 3; plane++)
        {
            WritePointer(machine, (ushort)(LightweightRegisters.BplPointerFirst + 4 * plane),
                0x30000u + 0x4000u * (uint)plane);
        }

        ushort[] copperWords =
        [
            0x7801, 0xFFFE,
            LightweightRegisters.Bltcon0, 0x4B4A,
            LightweightRegisters.Bltcon1, 0x005B,
            LightweightRegisters.Bltcmod, 40,
            LightweightRegisters.Bltbmod, 4,
            LightweightRegisters.Bltamod, 0xFFF6,
            LightweightRegisters.Bltdmod, 40,
            LightweightRegisters.Bltadat, 0x8000,
            LightweightRegisters.Bltbdat, 0xFFFF,
            LightweightRegisters.Bltapth, 0,
            LightweightRegisters.Bltapth + 2, 0xFFFC,
            LightweightRegisters.Bltcpth, 1,
            LightweightRegisters.Bltcpth + 2, 0xE000,
            LightweightRegisters.Bltdpth, 1,
            LightweightRegisters.Bltdpth + 2, 0xE000,
            0x7A3F, 0xFFFE,
            LightweightRegisters.Bltsize, 0x1202,
            0xFFFF, 0xFFFE
        ];
        for (var i = 0; i < copperWords.Length; i++)
        {
            WriteWord(machine, list + 2u * (uint)i, copperWords[i]);
        }
        WriteRegister(machine, LightweightRegisters.Copcon, 0x8002);
        WritePointer(machine, LightweightRegisters.Cop1lch, list);
        WriteRegister(machine, LightweightRegisters.Copjmp1, 0);
        WriteRegister(machine, LightweightRegisters.DmaconWrite, 0x83C0);

        var start = 122L * LightweightClock.CpuCyclesPerLine +
            65 * LightweightClock.CpuCyclesPerColorClock;
        machine.AdvanceHardwareTo(start + 20);
        Assert.Equal(start, machine.CopperLastMoveCycle);
        Assert.Equal(start + 20, machine.BlitterLastOutputCycle);

        machine.AdvanceHardwareTo(start + 832);
        Assert.Equal(start + 832, machine.BlitterLastOutputCycle);
    }

    [Fact]
    public void OcsLineRetainsFinalErrorSignForTheNextBlit()
    {
        using var machine = new LightweightA500Machine();
        const int stride = 40;
        var first = DestinationD + 0x2600;
        var second = first + 0x100;
        ConfigureLine(machine, first, rowStride: stride, bltcon1: 0x005B,
            minterm: 0x4A, initialAccumulator: -3, aModulo: -10, bModulo: 4);
        WriteRegister(machine, LightweightRegisters.Bltcon0, 0x4B4A);
        EnableBlitter(machine, nasty: true);
        _ = StartBlit(machine, 0x0202);
        AdvanceUntilBlitterIdle(machine);

        WriteRegister(machine, LightweightRegisters.Bltcon0, 0x4B4A);
        WriteRegister(machine, LightweightRegisters.Bltapth + 2, 0xFFFD);
        WritePointer(machine, LightweightRegisters.Bltcpth, second);
        WritePointer(machine, LightweightRegisters.Bltdpth, second);
        _ = StartBlit(machine, 0x0202);
        AdvanceUntilBlitterIdle(machine);

        Assert.Equal((ushort)0x0800, ReadWord(machine, first));
        Assert.Equal((ushort)0x0200, ReadWord(machine, first - stride));
        Assert.Equal((ushort)0x0020, ReadWord(machine, first - 2 * stride));
        Assert.Equal((ushort)0x0800, ReadWord(machine, second));
        Assert.Equal((ushort)0x0400, ReadWord(machine, second - stride));
        Assert.Equal((ushort)0x0020, ReadWord(machine, second - 2 * stride));
    }

    [Fact]
    public void OcsLineUsesDPointerOnlyForFirstPixelAndCModuloForRows()
    {
        const int rowStride = 0x20;
        var baseAddress = DestinationD + 0x2800;
        var firstPixelAddress = DestinationD + 0x3000;
        using var machine = new LightweightA500Machine();
        ConfigureLine(
            machine,
            baseAddress,
            rowStride,
            bltcon1: 0x0041,
            initialAccumulator: -2,
            aModulo: -4,
            dModulo: 0x40,
            destinationD: firstPixelAddress);
        EnableBlitter(machine, nasty: true);
        _ = StartBlit(machine, 0x0082);
        AdvanceUntilBlitterIdle(machine);

        Assert.Equal((ushort)0x8000, ReadWord(machine, firstPixelAddress));
        Assert.Equal((ushort)0x8000, ReadWord(machine, baseAddress + rowStride));
        Assert.Equal(
            machine.GetBlitterPointer(LightweightRegisters.Bltcpth),
            machine.GetBlitterPointer(LightweightRegisters.Bltdpth));
        Assert.Equal(baseAddress + rowStride,
            machine.GetBlitterPointer(LightweightRegisters.Bltcpth));
    }

    [Fact]
    public void OcsLineLeavesAErrorPointerAloneWhenAChannelIsDisabled()
    {
        using var machine = new LightweightA500Machine();
        var baseAddress = DestinationD + 0x3800;
        ConfigureLine(
            machine,
            baseAddress,
            rowStride: 0x20,
            bltcon1: 0x0001,
            initialAccumulator: 0x1234,
            channelMask: 0x0300);
        EnableBlitter(machine, nasty: true);
        _ = StartBlit(machine, 0x0042);
        AdvanceUntilBlitterIdle(machine);

        Assert.Equal((uint)0x1234,
            machine.GetBlitterPointer(LightweightRegisters.Bltapth));
        Assert.True(IsLinePixelSet(machine, baseAddress, 0x20, 0, 0));
    }

    [Fact]
    public void LineBChannelReloadsEachPatternWordTwiceAndAdvancesByModulo()
    {
        const int rowStride = 0x20;
        using var machine = new LightweightA500Machine();
        var baseAddress = DestinationD + 0x4000;
        WriteWord(machine, SourceB, 0x8000);
        WriteWord(machine, SourceB + 2, 0x4000);
        ConfigureLine(
            machine,
            baseAddress,
            rowStride,
            bltcon1: 0x0001,
            bModulo: 3,
            channelMask: 0x0F00);
        EnableBlitter(machine, nasty: true);
        var start = StartBlit(machine, 0x0082);

        machine.AdvanceHardwareTo(start + 12);
        Assert.Equal((ushort)0x8000,
            machine.GetCustomRegister(LightweightRegisters.Bltbdat));
        Assert.Equal(SourceB + 2,
            machine.GetBlitterPointer(LightweightRegisters.Bltbpth));
        AdvanceUntilBlitterIdle(machine);

        Assert.True(IsLinePixelSet(machine, baseAddress, rowStride, 0, 0));
        Assert.True(IsLinePixelSet(machine, baseAddress, rowStride, 1, 1));
        Assert.Equal((ushort)0x4000,
            machine.GetCustomRegister(LightweightRegisters.Bltbdat));
        Assert.Equal(SourceB + 4,
            machine.GetBlitterPointer(LightweightRegisters.Bltbpth));
        Assert.Equal(start + 24, machine.BlitterLastCompletionCycle);
    }

    [Fact]
    public void NoncanonicalLineWidthIsReportedInsteadOfSilentlyAccepted()
    {
        using var machine = new LightweightA500Machine();
        ConfigureLine(
            machine,
            DestinationD + 0x4400,
            rowStride: 0x20,
            bltcon1: 0x0001);
        EnableBlitter(machine, nasty: true);
        _ = StartBlit(machine, 0x0041);

        Assert.Equal(
            "OCS line-mode BLTSIZE widths other than two are not modeled",
            machine.UnsupportedActiveFeature);
    }

    [Fact]
    public void SingleDotLineSkipsRepeatedPixelsOnTheSameRasterRow()
    {
        using var machine = new LightweightA500Machine();
        var baseAddress = DestinationD + 0x4800;
        ConfigureLine(
            machine,
            baseAddress,
            rowStride: 0x20,
            bltcon1: 0x0053,
            initialAccumulator: -2);
        EnableBlitter(machine, nasty: true);
        var start = StartBlit(machine, 0x00C2);
        AdvanceUntilBlitterIdle(machine);

        Assert.Equal((ushort)0x8000, ReadWord(machine, baseAddress));
        Assert.Equal(start + 30, machine.BlitterLastCompletionCycle);
    }

    [Fact]
    public void DeferredLineRestartReceivesItsOwnStartupClocks()
    {
        using var machine = new LightweightA500Machine();
        ConfigureLine(
            machine,
            DestinationD + 0x5000,
            rowStride: 0x20,
            bltcon1: 0x0001);
        EnableBlitter(machine, nasty: true);
        var firstStart = StartBlit(machine, 0x0042);
        WriteRegister(machine, LightweightRegisters.Bltsize, 0x0042);
        AdvanceUntilBlitterIdle(machine);

        Assert.Equal(firstStart + 28, machine.BlitterLastCompletionCycle);
        Assert.Equal(firstStart + 28, machine.BlitterLastTerminationCycle);
    }

    [Fact]
    public void LineBusyWaitsForFinalWritePastMandatoryRefresh()
    {
        using var machine = new LightweightA500Machine();
        var destination = DestinationD + 0x5800;
        ConfigureLine(
            machine,
            destination,
            rowStride: 0x20,
            bltcon1: 0x0001);
        EnableBlitter(machine, nasty: true);
        var lineStart = 3L * LightweightClock.CpuCyclesPerLine;
        var start = WriteRegisterAt(
            machine,
            LightweightRegisters.Bltsize,
            0x0042,
            lineStart + (220 * LightweightClock.CpuCyclesPerColorClock));

        machine.AdvanceHardwareTo(start + 14);
        Assert.True(machine.BlitterStatusBusy);
        Assert.Equal((ushort)0, ReadWord(machine, destination));
        Assert.Equal(start + 16, machine.BlitterPendingOutputCycle);

        machine.AdvanceHardwareTo(start + 16);
        Assert.False(machine.BlitterStatusBusy);
        Assert.Equal((ushort)0x8000, ReadWord(machine, destination));
        Assert.Equal(start + 16, machine.BlitterLastCompletionCycle);
    }

    [Fact]
    public void LineStartupWaitsForMandatoryRefresh()
    {
        using var machine = new LightweightA500Machine();
        var destination = DestinationD + 0x6000;
        ConfigureLine(
            machine,
            destination,
            rowStride: 0x20,
            bltcon1: 0x0001);
        EnableBlitter(machine, nasty: true);
        var lineStart = 3L * LightweightClock.CpuCyclesPerLine;
        var start = WriteRegisterAt(
            machine,
            LightweightRegisters.Bltsize,
            0x0042,
            lineStart + (223 * LightweightClock.CpuCyclesPerColorClock));
        AdvanceUntilBlitterIdle(machine);

        Assert.Equal(start + 22, machine.BlitterLastCompletionCycle);
        Assert.Equal((ushort)0x8000, ReadWord(machine, destination));
    }

    [Fact]
    public void LineIdleOutputLeavesFollowingSlotAvailableToSprites()
    {
        using var machine = new LightweightA500Machine();
        ConfigureLine(machine, DestinationD, rowStride: 0x20, bltcon1: 0x0001);
        EnableBlitter(machine, nasty: true);
        var start = StartBlit(machine, 0x0042);

        // The first retained line phase is internal; the following C read
        // owns the bus. A completed C read must not reserve the next idle slot.
        machine.AdvanceHardwareTo(start + 6);
        Assert.Equal(start + 8, machine.BlitterPendingOutputCycle);
        Assert.True(machine.CanSpriteOwnOutputSlot(start + 8));

        machine.AdvanceHardwareTo(start + 8);
        Assert.Equal(start + 10, machine.BlitterPendingOutputCycle);
        Assert.False(machine.CanSpriteOwnOutputSlot(start + 10));

        machine.AdvanceHardwareTo(start + 10);
        Assert.Equal(start + 10, machine.BlitterLastOutputCycle);
        Assert.True(machine.IsCpuOutputOwned(start + 10));
        Assert.Equal(start + 12, machine.BlitterPendingOutputCycle);
        Assert.True(machine.CanSpriteOwnOutputSlot(start + 12));
    }

    private static LightweightA500Machine CreateThreeWordAllChannelBlit(
        bool nasty,
        out long start)
    {
        var machine = new LightweightA500Machine();
        WriteWord(machine, SourceA, 0xFF00);
        WriteWord(machine, SourceB, 0x0F0F);
        WriteWord(machine, SourceC, 0x3333);
        ConfigureArea(machine, bltcon0: 0x0FCA);
        EnableBlitter(machine, nasty);
        start = StartBlit(machine, 0x0043);
        return machine;
    }

    private static void ConfigureArea(
        LightweightA500Machine machine,
        ushort bltcon0,
        ushort bltcon1 = 0)
    {
        WriteRegister(machine, LightweightRegisters.Bltcon0, bltcon0);
        WriteRegister(machine, LightweightRegisters.Bltcon1, bltcon1);
        WriteRegister(machine, LightweightRegisters.Bltafwm, 0xFFFF);
        WriteRegister(machine, LightweightRegisters.Bltalwm, 0xFFFF);
        WritePointer(machine, LightweightRegisters.Bltapth, SourceA);
        WritePointer(machine, LightweightRegisters.Bltbpth, SourceB);
        WritePointer(machine, LightweightRegisters.Bltcpth, SourceC);
        WritePointer(machine, LightweightRegisters.Bltdpth, DestinationD);
        WriteRegister(machine, LightweightRegisters.Bltamod, 0);
        WriteRegister(machine, LightweightRegisters.Bltbmod, 0);
        WriteRegister(machine, LightweightRegisters.Bltcmod, 0);
        WriteRegister(machine, LightweightRegisters.Bltdmod, 0);
    }

    private static void ConfigureLine(
        LightweightA500Machine machine,
        uint baseAddress,
        int rowStride,
        ushort bltcon1,
        ushort texture = 0xFFFF,
        byte minterm = 0xCA,
        short initialAccumulator = 0,
        short aModulo = 0,
        short bModulo = 0,
        short? dModulo = null,
        ushort channelMask = 0x0B00,
        uint sourceB = SourceB,
        uint? destinationD = null)
    {
        WriteRegister(machine, LightweightRegisters.Bltcon0,
            (ushort)(channelMask | minterm));
        WriteRegister(machine, LightweightRegisters.Bltcon1, bltcon1);
        WriteRegister(machine, LightweightRegisters.Bltafwm, 0xFFFF);
        WriteRegister(machine, LightweightRegisters.Bltalwm, 0xFFFF);
        WritePointer(machine, LightweightRegisters.Bltapth,
            unchecked((uint)(ushort)initialAccumulator));
        WritePointer(machine, LightweightRegisters.Bltbpth, sourceB);
        WritePointer(machine, LightweightRegisters.Bltcpth, baseAddress);
        WritePointer(machine, LightweightRegisters.Bltdpth,
            destinationD ?? baseAddress);
        WriteRegister(machine, LightweightRegisters.Bltamod,
            unchecked((ushort)aModulo));
        WriteRegister(machine, LightweightRegisters.Bltbmod,
            unchecked((ushort)bModulo));
        WriteRegister(machine, LightweightRegisters.Bltcmod,
            unchecked((ushort)rowStride));
        WriteRegister(machine, LightweightRegisters.Bltdmod,
            unchecked((ushort)(dModulo ?? (short)rowStride)));
        WriteRegister(machine, LightweightRegisters.Bltbdat, texture);
        WriteRegister(machine, LightweightRegisters.Bltadat, 0x8000);
    }

    private static bool IsLinePixelSet(
        LightweightA500Machine machine,
        uint baseAddress,
        int rowStride,
        int x,
        int y)
    {
        var wordX = x >= 0 ? x / 16 : -((15 - x) / 16);
        var bit = x & 15;
        var address = unchecked((uint)((int)baseAddress +
            (y * rowStride) + (wordX * 2)));
        return (ReadWord(machine, address) & (0x8000 >> bit)) != 0;
    }

    private static (int X, int Y) GetLineDelta(ushort octantBits, bool sign)
    {
        var sud = (octantBits & 0x0010) != 0;
        var sul = (octantBits & 0x0008) != 0;
        var aul = (octantBits & 0x0004) != 0;
        var x = 0;
        var y = 0;
        if (!sign)
        {
            if (sud) y += sul ? -1 : 1;
            else x += sul ? -1 : 1;
        }
        if (sud) x += aul ? -1 : 1;
        else y += aul ? -1 : 1;
        return (x, y);
    }

    private static void EnableBlitter(
        LightweightA500Machine machine,
        bool nasty,
        long? requestedCycle = null)
    {
        var value = (ushort)(0x8240 | (nasty ? 0x0400 : 0));
        if (requestedCycle.HasValue)
        {
            _ = WriteRegisterAt(
                machine,
                LightweightRegisters.DmaconWrite,
                value,
                requestedCycle.Value);
        }
        else
        {
            WriteRegister(machine, LightweightRegisters.DmaconWrite, value);
        }
    }

    private static long StartBlit(LightweightA500Machine machine, ushort size)
        => WriteRegisterAt(
            machine,
            LightweightRegisters.Bltsize,
            size,
            Math.Max(QuietStartCycle, machine.Cycle));

    private static long WriteRegisterAt(
        LightweightA500Machine machine,
        ushort offset,
        ushort value,
        long requestedCycle)
    {
        var cycle = requestedCycle;
        machine.WriteWord(
            LightweightA500Machine.CustomBase + offset,
            value,
            ref cycle,
            M68kBusAccessKind.CpuDataWrite);
        return cycle - LightweightClock.CpuCyclesPerColorClock;
    }

    private static void WriteRegister(
        LightweightA500Machine machine,
        ushort offset,
        ushort value)
    {
        var cycle = machine.Cycle;
        machine.WriteWord(
            LightweightA500Machine.CustomBase + offset,
            value,
            ref cycle,
            M68kBusAccessKind.CpuDataWrite);
    }

    private static void WritePointer(
        LightweightA500Machine machine,
        ushort highOffset,
        uint pointer)
    {
        WriteRegister(machine, highOffset, (ushort)(pointer >> 16));
        WriteRegister(machine, (ushort)(highOffset + 2), (ushort)pointer);
    }

    private static void WriteWord(
        LightweightA500Machine machine,
        uint address,
        ushort value)
    {
        var cycle = machine.Cycle;
        machine.WriteWord(
            address,
            value,
            ref cycle,
            M68kBusAccessKind.CpuDataWrite);
    }

    private static ushort ReadWord(
        LightweightA500Machine machine,
        uint address)
        => (ushort)((machine.ChipRam.Span[(int)address] << 8) |
            machine.ChipRam.Span[(int)address + 1]);

    private static void AdvanceUntilBlitterIdle(LightweightA500Machine machine)
    {
        while (machine.BlitterActive)
        {
            Assert.NotEqual(long.MaxValue, machine.BlitterNextCycle);
            machine.AdvanceHardwareTo(machine.BlitterNextCycle);
        }
    }

    private static ushort ShiftSource(
        ushort current,
        ref ushort previous,
        int shift,
        bool descending = false)
    {
        uint combined = descending
            ? ((uint)current << 16) | previous
            : ((uint)previous << 16) | current;
        var value = shift == 0
            ? current
            : descending
                ? (ushort)(combined >> (16 - (shift & 15)))
                : (ushort)(combined >> (shift & 15));
        previous = current;
        return value;
    }

    private static ushort ApplyMinterm(
        byte minterm,
        ushort sourceA,
        ushort sourceB,
        ushort sourceC)
    {
        ushort output = 0;
        for (var bit = 0; bit < 16; bit++)
        {
            var index = (((sourceA >> bit) & 1) << 2) |
                (((sourceB >> bit) & 1) << 1) |
                ((sourceC >> bit) & 1);
            if ((minterm & (1 << index)) != 0)
            {
                output |= (ushort)(1 << bit);
            }
        }
        return output;
    }
}
