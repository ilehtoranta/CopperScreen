using Copper68k;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

// Lisa specification 2.1 (SPRES, ESPRM/OSPRM, CLXCON2, FMODE), AA functional
// specification pp.4-5. Horizontal samples retain the engine's +1 lores phase;
// these checks do not establish the physical Alice/Lisa clock phase.
public sealed class LightweightAgaSpriteTests
{
    private static void Write(LightweightA500Machine m, ushort r, ushort v)
    { m.WriteCustomRegisterFromCopper(r, v, m.Cycle); m.AdvanceHardwareTo(m.Cycle + 4); }

    private static LightweightA500Machine Create(int code = 0, int resolution = 1)
    {
        var m = new LightweightA500Machine(new() { AgnusModel = LightweightAgnusModel.Mos8374Alice,
            DeniseModel = LightweightDeniseModel.Lisa4203, CpuModel = M68kCpuModel.M68EC020,
            ChipRamBytes = 2097152, SlowRamBytes = 0, FramebufferWidth = 1816 });
        m.WriteChipWordDma(0x1000, 0x4E72); m.WriteChipWordDma(0x1002, 0x2700); m.Reset();
        for (var p = 0; p < 8; p++)
        {
            var address = 0x20000u + (uint)p * 0x4000;
            for (var b = 0; b < 1280; b += 2)
                m.WriteChipWordDma(address + (uint)b, (ushort)((code & (1 << p)) != 0 ? 0xFFFF : 0));
            Write(m, (ushort)(0xE0 + p * 4), (ushort)(address >> 16));
            Write(m, (ushort)(0xE2 + p * 4), (ushort)address);
        }
        Write(m, 0x08E, 0x2C81); Write(m, 0x090, 0x2CC1);
        Write(m, 0x092, 0x38); Write(m, 0x094, 0xD0); Write(m, 0x104, 0x24);
        Write(m, 0x1FC, 3);
        // Unused channels must fetch a null stream, rather than the machine's
        // reset vectors (whose low SP word can otherwise set the pair ATT bit).
        for (var sprite = 0; sprite < 8; sprite++)
        { Write(m, (ushort)(0x120 + sprite*4), 6); Write(m, (ushort)(0x122 + sprite*4), 0); }
        Write(m, 0x100, (ushort)(0x0211 | (resolution == 2 ? 0x8000 : resolution == 4 ? 0x40 : 0)));
        return m;
    }

    private static void Palette(LightweightA500Machine m, int index, ushort rgb)
    {
        Write(m, 0x106, (ushort)((index >> 5) << 13 | 0xC00));
        Write(m, (ushort)(0x180 + (index & 31) * 2), rgb);
    }

    private static void Sprite(LightweightA500Machine m, int sprite, int mode, int fine = 0,
        bool attached = false, ulong dataA = 0xFFFF0000FFFF0000, ulong dataB = 0x0000FFFF0000FFFF)
    {
        var bytes = mode == 0 ? 2 : mode == 3 ? 8 : 4;
        var address = 0x50000u + (uint)sprite * 0x1000;
        var ctl = (ushort)(0x2D01 | ((fine & 2) << 3) | ((fine & 1) << 3) | (attached ? 0x80 : 0));
        m.WriteChipWordDma(address, 0x2C40); m.WriteChipWordDma(address + (uint)bytes, ctl);
        for (var b = 0; b < bytes; b += 2)
        {
            m.WriteChipWordDma(address + (uint)(bytes * 2 + b), (ushort)(dataA >> (48 - b * 8)));
            m.WriteChipWordDma(address + (uint)(bytes * 3 + b), (ushort)(dataB >> (48 - b * 8)));
        }
        Write(m, (ushort)(0x120 + sprite * 4), (ushort)(address >> 16));
        Write(m, (ushort)(0x122 + sprite * 4), (ushort)address);
    }

    public static IEnumerable<object[]> WidthResolutionPositionCases()
    {
        for (var mode = 0; mode < 4; mode++)
            for (var resolution = 1; resolution <= 4; resolution *= 2)
                for (var fine = 0; fine < 4; fine++) yield return [mode, resolution, fine];
    }

    [Theory, MemberData(nameof(WidthResolutionPositionCases))]
    public void DmaWidthResolutionAndFinePositionAreIndependentOfLoresPlayfield(int mode, int resolution, int fine)
    {
        using var m = Create(); Palette(m, 33, 0xF00); Palette(m, 34, 0x0F0);
        Write(m, 0x10C, 0x802B); // sprite addresses must ignore the playfield XOR
        Write(m, 0x106, (ushort)(0xC00 | (resolution == 1 ? 0x40 : resolution == 2 ? 0x80 : 0xC0)));
        Write(m, 0x1FC, (ushort)(mode << 2)); Sprite(m, 0, mode, fine);
        Write(m, 0x096, 0x8320); m.ExecuteFrame();
        var bits = mode == 0 ? 16 : mode == 3 ? 64 : 32;
        var step = 4 / resolution; var start = 520 + fine;
        for (var x = start - 1; x <= start + bits * step; x++)
        {
            var expected = x < start || x >= start + bits * step ? 0 :
                mode == 2 || (x - start) / step / 16 % 2 == 0 ? 0xFF0000 : 0x00FF00;
            Assert.Equal(unchecked((int)0xFF000000) | expected, m.Framebuffer.Span[44 * 1816 + x]);
        }
        Assert.Null(m.UnsupportedActiveFeature);
    }

    [Theory]
    [InlineData(0, false, 33)] [InlineData(1, false, 177)]
    [InlineData(2, false, 37)] [InlineData(3, false, 181)]
    [InlineData(4, false, 41)] [InlineData(5, false, 185)]
    [InlineData(6, false, 45)] [InlineData(7, false, 189)]
    [InlineData(1, true, 181)] [InlineData(3, true, 181)]
    [InlineData(5, true, 181)] [InlineData(7, true, 181)]
    public void EveryChannelUsesItsBankAndAttachedPairsAlwaysUseTheOddBank(int sprite, bool attached, int index)
    {
        using var m = Create(); Palette(m, index, 0xA5C);
        Write(m, 0x106, 0xC40); Write(m, 0x10C, 0x802B);
        if (attached) Sprite(m, sprite - 1, 0);
        Sprite(m, sprite, 0, attached: attached); Write(m, 0x096, 0x8320); m.ExecuteFrame();
        Assert.Equal(unchecked((int)0xFFAA55CC), m.Framebuffer.Span[44 * 1816 + 520]);
        Assert.Null(m.UnsupportedActiveFeature);
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(4)]
    public void SuperHiresSpritesCanChangeWithinASinglePlayfieldPixel(int resolution)
    {
        using var m = Create(resolution: resolution); Palette(m, 33, 0xF00); Palette(m, 34, 0x0F0);
        Write(m, 0x10C, 0x2B); Write(m, 0x106, 0xCC0);
        Sprite(m, 0, 0, dataA: 0xAAAA000000000000, dataB: 0x5555000000000000);
        Write(m, 0x096, 0x8320); m.ExecuteFrame();
        for (var x = 0; x < 16; x++)
            Assert.Equal(unchecked((int)0xFF000000) | (x % 2 == 0 ? 0xFF0000 : 0x00FF00),
                m.Framebuffer.Span[44 * 1816 + 520 + x]);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void AttachedPairUsesOddBankBeforeTheOddComparatorAndWithTransparentOddData(int fine)
    {
        using var m = Create(); Palette(m, 177, 0xA5C);
        Write(m, 0x106, 0xCC0); Write(m, 0x10C, 0x2B);
        Sprite(m, 6, 0); Sprite(m, 7, 0, fine, true, 0, 0);
        Write(m, 0x096, 0x8320); m.ExecuteFrame();
        for (var x = 520; x < 536; x++)
            Assert.Equal(unchecked((int)0xFFAA55CC), m.Framebuffer.Span[44 * 1816 + x]);
    }

    [Fact]
    public void CpuWordWritesRepeatInSpr32ModeAndPageResidueRemainsExplicitlyUnverified()
    {
        using var m = Create(); Palette(m, 17, 0xF00);
        Write(m, 0x106, 0xCC0); Write(m, 0x1FC, 7);
        Write(m, 0x140, 0x2C40); Write(m, 0x142, 0x2D01);
        Write(m, 0x146, 0); Write(m, 0x144, 0x8000); Write(m, 0x096, 0x8300); m.ExecuteFrame();
        Assert.Equal(unchecked((int)0xFFFF0000), m.Framebuffer.Span[44 * 1816 + 520]);
        Assert.Equal(unchecked((int)0xFFFF0000), m.Framebuffer.Span[44 * 1816 + 536]);
        Assert.Null(m.UnsupportedActiveFeature);
        Write(m, 0x1FC, 15); Write(m, 0x144, 0x8000);
        Assert.Contains("bus residue", m.UnsupportedActiveFeature);
    }

    [Theory]
    [InlineData(0x0C40, true, 0x112233)] [InlineData(0x0C42, true, 0xFF0000)]
    [InlineData(0x0C62, true, 0)] [InlineData(0x0C42, false, 0x112233)]
    [InlineData(0x0C62, false, 0x112233)]
    public void BorderSpritesRequireEcsEnableAndCannotBypassBorderBlanking(ushort control, bool enabled, int rgb)
    {
        using var m = Create(); Palette(m, 0, 0x123); Palette(m, 17, 0xF00);
        Write(m, 0x100, (ushort)(enabled ? 0x0211 : 0x0210));
        Write(m, 0x106, control); Write(m, 0x08E, 0x2C8C);
        Sprite(m, 0, 0); Write(m, 0x096, 0x8320); m.ExecuteFrame();
        Assert.Equal(rgb, m.Framebuffer.Span[44 * 1816 + 520] & 0xFFFFFF);
    }

    [Theory]
    [InlineData(0xC3, false, 0x23)] [InlineData(0xC1, false, 0)] [InlineData(0xC2, false, 0x20)]
    [InlineData(0xC1, true, 2)] [InlineData(0xC2, true, 0x20)] [InlineData(0, false, 0x23)]
    public void PlanesSevenAndEightGateRawCollisionMatches(ushort clxcon2, bool dual, int expected)
    {
        using var m = Create(0xC0); Write(m, 0x100, (ushort)(dual ? 0x0611 : 0x0211));
        Write(m, 0x098, 0); Write(m, 0x10E, clxcon2); Sprite(m, 0, 0);
        Write(m, 0x096, 0x8320); m.ExecuteFrame();
        Assert.Equal(expected, m.GetCustomRegister(0x00E) & 0x23);
    }

    [Fact]
    public void OriginalCollisionControlClearsTheExtensionAtTheSameLisaInputPhase()
    {
        using var m = Create(0xC0); Write(m, 0x10E, 0xC0);
        Assert.Equal(0xC0, m.GetCustomRegister(0x10E));
        Write(m, 0x098, 0); Assert.Equal(0, m.GetCustomRegister(0x10E));
        Write(m, 0x096, 0x8300); m.ExecuteFrame();
        Assert.Equal(1, m.GetCustomRegister(0x00E) & 1); Assert.Null(m.UnsupportedActiveFeature);
    }

    [Theory]
    [InlineData(1, 0u, 4u, 0x1234)] [InlineData(1, 2u, 4u, 0xABCD)]
    [InlineData(2, 0u, 4u, 0x1234)] [InlineData(3, 4u, 8u, 0x5678)]
    public void AcceptedWideControlRetainsModeAddressAndReadsRamAtOutput(int mode, uint offset, uint stride, ushort expected)
    {
        using var m = Create(); const uint address = 0x50000;
        Write(m, 0x1FC, (ushort)(mode << 2));
        Write(m, 0x120, 5); Write(m, 0x122, (ushort)offset); Write(m, 0x096, 0x8220);
        var input = 25 * 454 + 0x17 * 2; m.AdvanceHardwareTo(input);
        m.WriteCustomRegisterFromCopper(0x122, 0x1000, input);
        m.WriteCustomRegisterFromCopper(0x1FC, 0, input);
        m.WriteChipWordDma(address, 0x1234); m.WriteChipWordDma(address + 2, 0xABCD);
        m.WriteChipWordDma(address + 4, 0x5678); m.WriteChipWordDma(address + 6, 0xDCBA);
        m.WriteCustomRegisterFromCopper(0x096, 0x20, input);
        m.AdvanceHardwareTo(input + 2);
        Assert.Equal(address + offset, m.SpriteDmaLastAddress);
        Assert.Equal(address + stride, m.GetLiveSpritePointer(0)); Assert.Equal(expected, m.GetSpriteDmaPos(0));
        Assert.False(m.CanCopperOwnOutputSlot(input + 2));
    }

    [Fact]
    public void WideSpriteRenderingDoesNotAllocateAfterWarmup()
    {
        using var m = Create(); Palette(m, 17, 0xF00); Write(m, 0x1FC, 15); Write(m, 0x106, 0xCC0);
        Sprite(m, 0, 3); Write(m, 0x096, 0x8320);
        void Field()
        {
            Write(m, 0x120, 5); Write(m, 0x122, 0); m.ExecuteFrame();
        }
        Field(); Field();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 4; i++) Field();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(unchecked((int)0xFFFF0000), m.Framebuffer.Span[44 * 1816 + 520]);
        Assert.Equal(0, allocated);
    }
}
