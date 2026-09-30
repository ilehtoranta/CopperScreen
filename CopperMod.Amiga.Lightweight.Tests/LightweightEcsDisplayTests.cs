using Copper68k;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

public sealed class LightweightEcsDisplayTests
{
    private static LightweightA500Machine Create(bool denise = true)
    {
        var m = new LightweightA500Machine(new() { AgnusModel = LightweightAgnusModel.Mos8375Pal2M,
            ChipRamBytes = 2 * 1024 * 1024, SlowRamBytes = 0,
            DeniseModel = denise ? LightweightDeniseModel.Mos8373 : LightweightDeniseModel.Mos8362,
            FramebufferWidth = denise ? 1816 : 908 });
        m.WriteChipWordDma(0x1000, 0x4E72); m.WriteChipWordDma(0x1002, 0x2700); m.Reset();
        return m;
    }
    private static void Write(LightweightA500Machine m, ushort r, ushort v)
    {
        m.WriteCustomRegisterFromCopper(r, v, m.Cycle);
        m.AdvanceHardwareTo(m.Cycle + 2);
    }
    // Commodore HRM appendix C and the retained Legacy register-bank tests.
    [Theory]
    [InlineData(0x1C0, 0x1FF)] [InlineData(0x1C2, 0x1FF)] [InlineData(0x1C4, 0x1FF)] [InlineData(0x1C6, 0x1FF)]
    [InlineData(0x1C8, 0x7FF)] [InlineData(0x1CA, 0x7FF)] [InlineData(0x1CC, 0x7FF)] [InlineData(0x1CE, 0x7FF)]
    [InlineData(0x1D0, 0x1FF)] [InlineData(0x1D2, 0x1FF)] [InlineData(0x1D4, 0x1FF)] [InlineData(0x1D6, 0x1FF)]
    [InlineData(0x1D8, 0x1FF)] [InlineData(0x1DC, 0x7FFF)] [InlineData(0x1DE, 0x1FF)] [InlineData(0x1E0, 0x7FF)] [InlineData(0x1E2, 0x1FF)]
    public void BeamRegistersApplyHardwareMasks(ushort r, ushort mask)
    { using var m = Create(); Write(m, r, 0xFFFF); Assert.Equal(mask, m.GetCustomRegister(r)); }

    [Fact]
    public void EcsDeniseIdAndControlsAreIndependentOfAgnus()
    {
        using var ecs = Create(); using var ocs = Create(false);
        long c = 0; Assert.Equal(0xFC, ecs.ReadWord(0xDFF07C, ref c, M68kBusAccessKind.CpuDataRead) & 255);
        Write(ecs, 0x106, 0xFFFF); Write(ocs, 0x106, 0xFFFF);
        Assert.Equal(0x37, ecs.GetCustomRegister(0x106)); Assert.Equal(0, ocs.GetCustomRegister(0x106));
    }
    [Fact]
    public void ExplicitWindowZeroAndUpperBitsRemainValidUntilBaseWindowWrite()
    {
        using var m = Create(); Write(m, 0x08E, 0x2C81); Write(m, 0x090, 0x2CC1);
        Assert.Equal(449, m.DmaDisplayWindow.HorizontalStop);
        Write(m, 0x1E4, 0); Assert.True(m.DiwHighValid); Assert.Equal(193, m.DmaDisplayWindow.HorizontalStop);
        Assert.Equal(4140, m.DmaDisplayWindow.VerticalStop);
        Write(m, 0x1E4, 0x2101); Assert.Equal(300, m.DmaDisplayWindow.VerticalStart); Assert.Equal(449, m.DmaDisplayWindow.HorizontalStop);
        Write(m, 0x08E, 0x2C81); Assert.False(m.DiwHighValid); Assert.Equal(44, m.DmaDisplayWindow.VerticalStart);
    }
    [Fact]
    public void TotalsRemainInertUntilVariableBeamThenResizeReusableOutput()
    {
        using var m = Create(); Write(m, 0x1C0, 113); Write(m, 0x1C8, 524);
        Assert.Equal(454, m.LineCycles); Assert.Equal(313, m.FieldLines);
        Write(m, 0x1DC, 0x1B80); Assert.Equal(228, m.LineCycles); Assert.Equal(525, m.FieldLines);
        m.ExecuteFrame(); m.ExecuteFrame(); Assert.Equal(912, m.FramebufferWidth); Assert.Equal(525, m.FramebufferHeight);
        Assert.Equal(912 * 525, m.Framebuffer.Length); Assert.Null(m.UnsupportedActiveFeature);
        Write(m, 0x1DC, 0x20); m.ExecuteFrame(); m.ExecuteFrame(); Assert.Equal(1816, m.FramebufferWidth); Assert.Equal(313, m.FramebufferHeight);
    }
    [Fact]
    public void ShorteningCurrentLinePastComparatorCannotStallTheBeam()
    {
        using var m = Create(); m.AdvanceHardwareTo(400);
        Write(m, 0x1C0, 99); Write(m, 0x1C8, 199); Write(m, 0x1DC, 0x80);
        m.AdvanceHardwareTo(m.Cycle + 202); Assert.InRange(m.BeamLine, 1, 3); Assert.InRange(m.BeamColorClock, 0, 99);
    }
    [Fact]
    public void HorizontalPositionWriteMovesOnlyTheBeamAndRetainsTheNextBoundary()
    {
        using var m = Create(); m.AdvanceHardwareTo(100);
        m.WriteCustomRegisterFromCopper(0x1D8, 100, m.Cycle);
        Assert.Equal(100, m.Cycle); Assert.Equal(100, m.BeamColorClock); Assert.Equal(0, m.BeamLine);
        m.AdvanceHardwareTo(352); Assert.Equal(0, m.BeamLine);
        m.AdvanceHardwareTo(354); Assert.Equal(1, m.BeamLine); Assert.Equal(0, m.BeamColorClock);
        m.WriteCustomRegisterFromCopper(0x1D8, 511, m.Cycle); Assert.Equal(226, m.BeamColorClock);
        m.AdvanceHardwareTo(356); Assert.Equal(2, m.BeamLine);
    }
    [Fact]
    public void NtscHasAlternatingLineLengthsAndCanDisableLongLines()
    {
        using var m = Create(); Write(m, 0x1DC, 0); Assert.Equal(263, m.FieldLines); Assert.Equal(454, m.LineCycles);
        m.AdvanceHardwareTo(454); Assert.Equal(456, m.LineCycles); m.AdvanceHardwareTo(910); Assert.Equal(454, m.LineCycles);
        Write(m, 0x1DC, 0x800); m.AdvanceHardwareTo(1364); Assert.Equal(454, m.LineCycles);
    }
    [Theory]
    [InlineData(0x20, 25, 26)]
    [InlineData(0, 20, 21)]
    public void SelectedStandardKeepsSpriteReloadBeforeItsFirstVisibleLine(ushort beam, int reload, int visible)
    {
        using var m = Create(); Write(m, 0x1DC, beam); Write(m, 0x180, 0xF00);
        m.WriteChipWordDma(0x2000, 0xFE00); m.WriteChipWordDma(0x2002, 0xFF00);
        Write(m, 0x120, 0); Write(m, 0x122, 0x2000); Write(m, 0x096, 0x8220);
        var lineStart = reload * 454L + (beam == 0 ? (reload / 2) * 2 : 0);
        m.AdvanceHardwareTo(lineStart + 0x17 * 2 - 1);
        Assert.Equal(0x2000u, m.GetLiveSpritePointer(0));
        m.AdvanceHardwareTo(lineStart + 0x1A * 2);
        Assert.Equal(0x2004u, m.GetLiveSpritePointer(0));
        Assert.Equal((ushort)0xFE00, m.GetSpriteDmaPos(0));
        m.ExecuteFrame(); m.ExecuteFrame();
        var width = m.FramebufferWidth;
        Assert.Equal(unchecked((int)0xFF000000), m.Framebuffer.Span[(visible - 1) * width + 600]);
        Assert.Equal(unchecked((int)0xFFFF0000), m.Framebuffer.Span[visible * width + 600]);
    }
    [Fact]
    public void NtscTodAlarmWaitsForTheActualLongLineBoundary()
    {
        using var m = Create(); Write(m, 0x1DC, 0);
        long cycle = m.Cycle;
        void Cia(int r, byte v) => m.WriteByte((uint)(0xBFD000 + r * 256), v, ref cycle, M68kBusAccessKind.CpuDataWrite);
        Cia(8, 0); Cia(15, 0x80); Cia(8, 2); Cia(13, 0x84);
        Assert.Equal(454, m.NextCiaInterruptCycle);
        m.AdvanceHardwareTo(454); Assert.Equal(1u, m.CiaBTod); Assert.Equal(910, m.NextCiaInterruptCycle);
        m.AdvanceHardwareTo(908); Assert.Equal(0, m.CiaBPendingInterrupts);
        m.AdvanceHardwareTo(910); Assert.Equal(2u, m.CiaBTod); Assert.Equal(4, m.CiaBPendingInterrupts);
    }
    [Fact]
    public void SuperHiresFetchesTwoPlanesAtEveryCckAndAppliesModuloOnce()
    {
        using var m = Create(); Write(m, 0x08E, 0x0181); Write(m, 0x090, 0x02C1);
        Write(m, 0x092, 0x38); Write(m, 0x094, 0x3E); Write(m, 0x0E0, 0); Write(m, 0x0E2, 0x2000);
        Write(m, 0x0E4, 0); Write(m, 0x0E6, 0x3000); Write(m, 0x108, 4); Write(m, 0x10A, 6);
        for (uint i = 0; i < 32; i += 2) { m.WriteChipWordDma(0x2000 + i, 0xAAAA); m.WriteChipWordDma(0x3000 + i, 0xFFFF); }
        Write(m, 0x100, 0x2040); Write(m, 0x096, 0x8300);
        var start = 454 + 0x38 * 2; m.AdvanceHardwareTo(start + 2); Assert.Equal(1, m.BitplaneLastPlane);
        m.AdvanceHardwareTo(start + 4); Assert.Equal(0, m.BitplaneLastPlane); m.AdvanceHardwareTo(2 * 454);
        Assert.Equal(0x2014u, m.GetLiveBitplanePointer(0)); Assert.Equal(0x3016u, m.GetLiveBitplanePointer(1));
    }
    [Fact]
    public void TimingAndPointerChangesCannotRetargetAnAcceptedSuperHiresWord()
    {
        using var m = Create(); Write(m, 0x1DC, 0x80);
        Write(m, 0x08E, 0x0181); Write(m, 0x090, 0x02C1); Write(m, 0x092, 0x38); Write(m, 0x094, 0x3E);
        Write(m, 0x0E6, 0x3000); Write(m, 0x100, 0x2040); Write(m, 0x096, 0x8300);
        m.WriteChipWordDma(0x3000, 0xCAFE); m.WriteChipWordDma(0x4000, 0x1234);
        var input = 454 + 0x38 * 2; m.AdvanceHardwareTo(input);
        Assert.Equal(input, m.BitplaneLastInputCycle);
        m.WriteCustomRegisterFromCopper(0x1C0, 40, input); // next CCK now ends this shortened line
        m.WriteCustomRegisterFromCopper(0x0E6, 0x4000, input);
        m.AdvanceHardwareTo(input + 2);
        Assert.Equal(1, m.BitplaneLastPlane); Assert.Equal(0x3000u, m.BitplaneLastAddress);
        Assert.Equal(0xCAFE, m.GetBitplaneDataLatch(1));
        Assert.Equal(2, m.BeamLine);
    }
    [Fact]
    public void StableEcsOutputDoesNotAllocateDuringFieldExecution()
    {
        using var m = Create(); Write(m, 0x100, 0x2040);
        m.Cpu.Cycles = m.Cycle;
        for (var i = 0; i < 300; i++) m.ExecuteFrame();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 20; i++) m.ExecuteFrame();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
    [Theory]
    [InlineData(0x1000)] [InlineData(0x9000)] [InlineData(0x1040)]
    public void EcsDisplayWindowStillClipsZeroMatchAndSpriteCollisions(ushort mode)
    {
        using var m = Create(); Write(m, 0x08E, 0x2CA0); Write(m, 0x090, 0xAD68);
        Write(m, 0x092, 0x38); Write(m, 0x094, 0xD0); Write(m, 0x0E0, 1);
        for (uint a = 0x10000; a < 0x20000; a += 2) m.WriteChipWordDma(a, 0xFFFF);
        Write(m, 0x100, mode); Write(m, 0x098, 0x0FC0); Write(m, 0x096, 0x8300);
        Write(m, 0x140, 0x2C40); Write(m, 0x142, 0x2D00); Write(m, 0x144, 0xFFFF);
        m.Cpu.Cycles = m.Cycle; m.ExecuteFrame();
        Assert.Equal(0x8000, m.GetCustomRegister(0x00E));
    }
    [Fact]
    public void SuperHiresPaletteMultiplexProducesIndependentAdjacentPixels()
    {
        using var m = Create(); Write(m, 0x08E, 0x2C81); Write(m, 0x090, 0x2CC1);
        Write(m, 0x100, 0x1040); Write(m, 0x182, 0x0C03); Write(m, 0x188, 0x030C);
        m.AdvanceHardwareTo(44 * 454 + 128); Write(m, 0x110, 0xAAAA); m.ExecuteFrame();
        var row = m.Framebuffer.Span.Slice(44 * 1816 + 129 * 4, 64).ToArray();
        Assert.Contains(unchecked((int)0xFFFF0000), row); Assert.Contains(unchecked((int)0xFF0000FF), row);
        Assert.Contains(Enumerable.Range(0, row.Length - 1), i => row[i] == unchecked((int)0xFFFF0000) && row[i + 1] == unchecked((int)0xFF0000FF));
    }
    [Theory]
    [InlineData(0, 0x20, 0xFFFF0000u)] [InlineData(1, 0x20, 0x00000000u)] [InlineData(1, 0x10, 0xFFFF0000u)]
    public void BorderControlsRequireEnableAndPreserveTransparency(ushort bplcon0, ushort bplcon3, uint expected)
    {
        using var m = Create(); Write(m, 0x08E, 0x2C81); Write(m, 0x090, 0x2CC1); Write(m, 0x180, 0xF00); Write(m, 0x100, bplcon0); Write(m, 0x106, bplcon3); m.ExecuteFrame();
        Assert.Equal(expected, (uint)m.Framebuffer.Span[30 * 1816 + 400]);
    }
    [Fact]
    public void DoffSuppressesMemoryOutputWhileKeepingDAddressAndBusyTiming()
    {
        using var m = Create(); m.WriteChipWordDma(0x2000, 0xCAFE); Write(m, 0x040, 0x01FF); Write(m, 0x042, 0x80);
        Write(m, 0x054, 0); Write(m, 0x056, 0x2000); Write(m, 0x096, 0x8240); Write(m, 0x058, 0x41);
        m.AdvanceHardwareTo(m.Cycle + 200); Assert.Equal(0xCAFE, m.ReadChipWordDma(0x2000));
        Assert.Equal(0x2002u, m.GetBlitterPointer(0x054)); Assert.False(m.BlitterBusy); Assert.Null(m.UnsupportedActiveFeature);
    }

    [Theory]
    [InlineData(0, 804)] [InlineData(0x10, 806)]
    public void SuperHiresSpritesHave70nsPixelsAndPositionIncrement(ushort fine, int start)
    {
        using var m = Create(); Write(m, 0x08E, 0x2C81); Write(m, 0x090, 0x2CC1);
        Write(m, 0x100, 0x0040); Write(m, 0x1AA, 0xFFF);
        Write(m, 0x140, 0x2C64); Write(m, 0x142, (ushort)(0x2D00 | fine));
        Write(m, 0x146, 0); Write(m, 0x144, 0xFFFF); m.ExecuteFrame();
        var row = m.Framebuffer.Span.Slice(44 * 1816, 1816).ToArray();
        var white = Enumerable.Range(0, row.Length).Where(i => row[i] == unchecked((int)0xFFFFFFFF)).ToArray();
        Assert.Equal(Enumerable.Range(start, 32), white);
    }

    [Theory]
    [InlineData(0x1000)] [InlineData(0xA000)] [InlineData(0x6000)] [InlineData(0x6800)] [InlineData(0x2400)]
    public void ExistingPlayfieldModesKeepTheirPixelsWhenEcsControlsAreInactive(ushort mode)
    {
        using var ecs = Create(); using var ocs = Create(false);
        foreach (var m in new[] { ecs, ocs })
        {
            Write(m, 0x08E, 0x2C81); Write(m, 0x090, 0x2CC1); Write(m, 0x100, mode);
            for (ushort i = 0; i < 32; i++) Write(m, (ushort)(0x180 + 2 * i), (ushort)(i * 0x41));
            m.AdvanceHardwareTo(44 * 454 + 128);
            for (ushort plane = 5; plane > 0; plane--) Write(m, (ushort)(0x110 + plane * 2), (ushort)(0xF0F0 >> plane));
            Write(m, 0x110, 0xCCCC); m.ExecuteFrame();
        }
        var left = ocs.Framebuffer.Span; var right = ecs.Framebuffer.Span;
        for (var x = 0; x < 908; x++)
        {
            Assert.Equal(left[44 * 908 + x], right[44 * 1816 + x * 2]);
            Assert.Equal(left[44 * 908 + x], right[44 * 1816 + x * 2 + 1]);
        }
    }

    [Fact]
    public void ProgrammableBlankAndSyncFollowWrappedComparators()
    {
        using var m = Create(); Write(m, 0x180, 0xF00);
        Write(m, 0x1C0, 113); Write(m, 0x1C8, 524);
        Write(m, 0x1C4, 105); Write(m, 0x1C6, 19); Write(m, 0x1CC, 510); Write(m, 0x1CE, 30);
        Write(m, 0x1DE, 5); Write(m, 0x1C2, 9); Write(m, 0x1E0, 5); Write(m, 0x1CA, 8);
        Write(m, 0x1DC, 0x5B80);
        m.AdvanceHardwareTo(5 * 228 + 12);
        Assert.True(m.ProgrammableHorizontalSyncActive); Assert.True(m.ProgrammableVerticalSyncActive);
        m.AdvanceHardwareTo(8 * 228 + 20);
        Assert.False(m.ProgrammableHorizontalSyncActive); Assert.False(m.ProgrammableVerticalSyncActive);
        m.ExecuteFrame(); m.ExecuteFrame(); var pixels = m.Framebuffer.Span;
        Assert.Equal(unchecked((int)0xFF000000), pixels[29 * 912 + 164]);
        Assert.Equal(unchecked((int)0xFFFF0000), pixels[30 * 912 + 164]);
        Assert.Equal(unchecked((int)0xFF000000), pixels[510 * 912 + 164]);
        Assert.Equal(unchecked((int)0xFF000000), pixels[40 * 912 + 8]);
    }

    [Theory]
    [InlineData(0x400, 0x8F00, 0x00FF0000u)]
    [InlineData(0x400, 0x0F00, 0xFFFF0000u)]
    [InlineData(0x800, 0x0F00, 0x00FF0000u)]
    [InlineData(0x1800, 0x0F00, 0xFFFF0000u)]
    public void DigitalGenlockUsesColorKeyOrSelectedBitplane(ushort control, ushort encoded, uint expected)
    {
        using var m = Create(); Write(m, 0x08E, 0x2C81); Write(m, 0x090, 0x2CC1);
        Write(m, 0x100, 0x1001); Write(m, 0x104, control); Write(m, 0x182, encoded);
        m.AdvanceHardwareTo(44 * 454 + 128); Write(m, 0x110, 0xFFFF); m.ExecuteFrame();
        Assert.Equal(expected, (uint)m.Framebuffer.Span[44 * 1816 + 129 * 4]);
    }
}
