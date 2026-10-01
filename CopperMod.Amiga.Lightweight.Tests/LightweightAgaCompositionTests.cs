using Copper68k;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

// Commodore AA functional specification, p.4; Lisa specification, section 2.1.
// Sample locations retain the existing accepted-DMA/shifter phase contract.
public sealed class LightweightAgaCompositionTests
{
    private static void Write(LightweightA500Machine m, ushort r, ushort v)
    { m.WriteCustomRegisterFromCopper(r, v, m.Cycle); m.AdvanceHardwareTo(m.Cycle + 4); }

    private static LightweightA500Machine Create(int[] codes, ushort control = 0x0A10, int resolution = 1, int fetch = 3)
    {
        var m = new LightweightA500Machine(new() { AgnusModel = LightweightAgnusModel.Mos8374Alice,
            DeniseModel = LightweightDeniseModel.Lisa4203, CpuModel = M68kCpuModel.M68EC020,
            ChipRamBytes = 2097152, SlowRamBytes = 0, FramebufferWidth = 1816 });
        m.WriteChipWordDma(0x1000, 0x4E72); m.WriteChipWordDma(0x1002, 0x2700); m.Reset();
        for (var p = 0; p < 8; p++)
        {
            var address = 0x20000u + (uint)p * 0x4000;
            for (var i = 0; i < codes.Length; i += 16)
            {
                ushort word = 0;
                for (var b = 0; b < 16 && i + b < codes.Length; b++)
                    if ((codes[i + b] & (1 << p)) != 0) word |= (ushort)(0x8000 >> b);
                m.WriteChipWordDma(address + (uint)(i / 8), word);
            }
            Write(m, (ushort)(0xE0 + p * 4), (ushort)(address >> 16)); Write(m, (ushort)(0xE2 + p * 4), (ushort)address);
        }
        Write(m, 0x08E, 0x2C81); Write(m, 0x090, 0x2CC1);
        Write(m, 0x092, 0x38); Write(m, 0x094, (ushort)(fetch == 0 ? 0xD0 : fetch == 3 ? 0xB8 : 0xC8));
        Write(m, 0x1FC, (ushort)fetch);
        Write(m, 0x100, (ushort)(control | (resolution == 2 ? 0x8000 : resolution == 4 ? 0x40 : 0)));
        Write(m, 0x096, 0x8300);
        return m;
    }

    private static void Palette(LightweightA500Machine m, int index, int rgb)
    {
        var bank = (ushort)((index >> 5) << 13 | 0x0C00);
        var high = (ushort)((rgb >> 12 & 0xF00) | (rgb >> 8 & 0xF0) | (rgb >> 4 & 15));
        var low = (ushort)((rgb >> 8 & 0xF00) | (rgb >> 4 & 0xF0) | (rgb & 15));
        Write(m, 0x106, bank); Write(m, (ushort)(0x180 + (index & 31) * 2), high);
        Write(m, 0x106, (ushort)(bank | 0x200)); Write(m, (ushort)(0x180 + (index & 31) * 2), low);
        Write(m, 0x106, 0x0C00);
    }

    private static void Pixel(LightweightA500Machine m, int pixel, int rgb, int resolution = 1, int line = 44)
    {
        var start = line * 1816 + 516 + pixel * 4 / resolution;
        for (var i = 0; i < 4 / resolution; i++) Assert.Equal(unchecked((int)0xFF000000) | rgb, m.Framebuffer.Span[start + i]);
    }

    [Theory]
    [InlineData(1, 0)] [InlineData(1, 1)] [InlineData(1, 2)] [InlineData(1, 3)]
    [InlineData(2, 1)] [InlineData(2, 3)] [InlineData(4, 3)]
    public void Ham8UsesLowControlPlanesAndPreservesTwoComponentBits(int resolution, int fetch)
    {
        using var m = Create([0x55, 4, 0xA6, 0xCB, 0xFD, 0xFC], resolution: resolution, fetch: fetch);
        Palette(m, 0, 0x112233); Palette(m, 1, 0x142536); Palette(m, 63, 0xABCDEF);
        m.ExecuteFrame();
        int[] expected = [0x112257, 0x142536, 0xA42536, 0xA4C936, 0xA4C9FE, 0xABCDEF];
        for (var i = 0; i < expected.Length; i++) Pixel(m, i, expected[i], resolution);
        Assert.Null(m.UnsupportedActiveFeature);
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(4)]
    public void Ham6RetainsRgb24OnDirectSelectionAndExpandsModifiedNibbles(int resolution)
    {
        using var m = Create([0x1A, 1, 0x2B, 0x3C, 0x1D, 1], 0x6A00, resolution);
        Palette(m, 0, 0x112233); Palette(m, 1, 0x142536);
        m.ExecuteFrame();
        int[] expected = [0x1122AA, 0x142536, 0xBB2536, 0xBBCC36, 0xBBCCDD, 0x142536];
        for (var i = 0; i < expected.Length; i++) Pixel(m, i, expected[i], resolution);
        Assert.Null(m.UnsupportedActiveFeature);
    }

    [Fact]
    public void HamHoldResetsAtLeftWindowBoundaryAndEachNewLine()
    {
        using var m = Create([4, 0xA6, 0xCB, 0x55]);
        Palette(m, 0, 0x112233); Palette(m, 1, 0x142536);
        Write(m, 0x08E, 0x2C84);
        // The first visible code modifies blue; the three hidden codes must
        // not seed the hold. End this line with direct COLOR01, then start
        // the next visible line with a modification to distinguish hold reset.
        m.WriteChipWordDma(0x28000 + 38, 1); // plane three, last pixel = direct COLOR01
        for (var p = 0; p < 8; p++)
            m.WriteChipWordDma(0x20000u + (uint)p * 0x4000 + 40, (ushort)((0x55 & (1 << p)) != 0 ? 0x1000 : 0));
        m.ExecuteFrame();
        Pixel(m, 3, 0x112257);
        Pixel(m, 319, 0x142536);
        Pixel(m, 3, 0x112257, line: 45);
    }

    [Fact]
    public void HamSpriteOverlayDoesNotChangeTheHeldColor()
    {
        using var m = Create([4, 0xA6, 0xCB]);
        Palette(m, 1, 0x142536); Palette(m, 17, 0xFF0000);
        Write(m, 0x104, 0x20);
        Write(m, 0x140, 0x2C40); Write(m, 0x142, 0x2D01);
        Write(m, 0x146, 0); Write(m, 0x144, 0x8000);
        m.ExecuteFrame();
        Pixel(m, 0, 0x142536); Pixel(m, 1, 0xFF0000); Pixel(m, 2, 0xA4C936);
    }

    private static int Interleave(int a, int b)
    {
        var code = 0;
        for (var p = 0; p < 4; p++) code |= ((a >> p & 1) << (p * 2)) | ((b >> p & 1) << (p * 2 + 1));
        return code;
    }

    [Fact]
    public void NativeHamScreenSetupCanDisableAllPlanesWithoutRejectingTheMode()
    {
        using var m = Create([], 0x0A01);
        m.ExecuteFrame(); Assert.Null(m.UnsupportedActiveFeature);
    }

    [Theory]
    [InlineData(0x0A10, 0x0111, 0x80, 0x112283)] // XOR chooses blue; modification data stays raw.
    [InlineData(0x0A10, 0xFC11, 0, 0x142536)] // XOR transforms direct register 0 to register 63.
    [InlineData(0x6A00, 0x1311, 0, 0x112200)] // HAM6 XOR chooses blue, raw component remains zero.
    public void HamXorChangesControlAndDirectAddressButNotModifiedData(ushort control, ushort xor, int code, int rgb)
    {
        using var m = Create([code], control);
        Palette(m, 0, 0x112233); Palette(m, 63, 0x142536);
        Write(m, 0x10C, xor);
        m.ExecuteFrame(); Pixel(m, 0, rgb);
    }

    [Fact]
    public void SuperHiresMasksEachSubpixelUsingTheActualAttachedSpriteGroup()
    {
        using var m = Create([64, 128, 0, 0, 0, 0], 0x0610, 4);
        Palette(m, 8, 0xFF0000); Palette(m, 16, 0x0000FF); Palette(m, 21, 0x00FF00);
        Write(m, 0x106, 0x0C40); Write(m, 0x104, 0x23);
        for (var sprite = 6; sprite < 8; sprite++)
        {
            Write(m, (ushort)(0x140 + sprite*8), 0x2C40);
            Write(m, (ushort)(0x142 + sprite*8), (ushort)(sprite == 7 ? 0x2D80 : 0x2D00));
            Write(m, (ushort)(0x146 + sprite*8), 0);
            Write(m, (ushort)(0x144 + sprite*8), 0x8000);
        }
        m.ExecuteFrame();
        Pixel(m, 0, 0xFF0000, 4); Pixel(m, 1, 0x00FF00, 4);
        Pixel(m, 2, 0x00FF00, 4); Pixel(m, 3, 0x00FF00, 4); Pixel(m, 4, 0, 4);
    }

    [Theory]
    [InlineData(1)] [InlineData(3)]
    public void LowResolutionSpriteCollisionsObserveEverySuperHiresSubpixel(int matchingPixel)
    {
        var codes = new int[4]; codes[matchingPixel] = 1;
        using var m = Create(codes, 0x0610, 4);
        Write(m, 0x106, 0x0C40); Write(m, 0x098, 0x0041);
        Write(m, 0x140, 0x2C40); Write(m, 0x142, 0x2D00);
        Write(m, 0x146, 0); Write(m, 0x144, 0x8000);
        m.ExecuteFrame();
        Assert.Equal(2, m.GetCustomRegister(0x00E) & 2);
    }

    [Theory]
    [InlineData(1, false)] [InlineData(1, true)] [InlineData(2, false)]
    [InlineData(2, true)] [InlineData(4, false)] [InlineData(4, true)]
    public void DualPlayfieldsUseAllFourPlanesAndRawTransparency(int resolution, bool pf2First)
    {
        var codes = new List<int>(); var expected = new List<int>();
        for (var a = 0; a < 16; a++) for (var b = 0; b < 16; b++)
        {
            codes.Add(Interleave(a, b));
            var select2 = b != 0 && (pf2First || a == 0);
            var index = (select2 ? 32 + b : a) ^ 0x80;
            expected.Add(index << 16 | (index * 37 & 255) << 8 | (index * 73 & 255));
        }
        using var m = Create(codes.ToArray(), 0x0610, resolution);
        for (var i = 0; i < 256; i++) Palette(m, i, i << 16 | (i * 37 & 255) << 8 | (i * 73 & 255));
        Write(m, 0x106, 0x1400); Write(m, 0x10C, 0x8011); Write(m, 0x104, (ushort)(pf2First ? 0x40 : 0));
        m.ExecuteFrame();
        for (var i = 0; i < expected.Count; i++) Pixel(m, i, expected[i], resolution);
        Assert.Null(m.UnsupportedActiveFeature);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    public void EveryPf2OffsetAddsToTheSelectedColor(int offset)
    {
        using var m = Create([Interleave(0, 15)], 0x0610);
        var index = 15 + (offset == 0 ? 0 : 1 << offset);
        Palette(m, index, 0x142536);
        Write(m, 0x106, (ushort)(offset << 10));
        m.ExecuteFrame(); Pixel(m, 0, 0x142536);
    }

    [Theory]
    [InlineData(0x0021, 1, 2)] [InlineData(0x8880, 32, 40)]
    public void DualPlayfieldsScrollIndependentlyAcrossWideFetches(ushort scroll, int first, int second)
    {
        using var m = Create([3], 0x0610);
        Palette(m, 1, 0xFF0000); Palette(m, 9, 0x0000FF);
        Write(m, 0x102, scroll);
        m.ExecuteFrame();
        Pixel(m, first, 0xFF0000); Pixel(m, second, 0x0000FF);
        Pixel(m, first - 1, 0); Pixel(m, second + 1, 0);
    }

    [Theory]
    [InlineData(0x21, 0x142536)] [InlineData(0x01, 0x0A121B)]
    public void EhbTestsTheColorAddressAfterXor(int code, int rgb)
    {
        using var m = Create([code], 0x6200);
        Palette(m, 1, 0x142536); Write(m, 0x10C, 0x2011);
        m.ExecuteFrame(); Pixel(m, 0, rgb);
    }

    [Theory]
    [InlineData(1)] [InlineData(2)]
    public void ObscuredOpaqueFieldStillMasksSpriteAndTransparentPixelsDoNot(int resolution)
    {
        using var m = Create([3, 2, 0, 0], 0x0610, resolution);
        Palette(m, 1, 0xFF0000); Palette(m, 9, 0x0000FF); Palette(m, 17, 0x00FF00);
        Write(m, 0x104, 0x60); // PF2 first and below sprites; PF1 above sprites.
        Write(m, 0x140, 0x2C40); Write(m, 0x142, 0x2D00);
        Write(m, 0x146, 0); Write(m, 0x144, 0xC000);
        m.ExecuteFrame();
        if (resolution == 1) { Pixel(m, 1, 0x00FF00); Pixel(m, 2, 0); }
        else { Pixel(m, 2, 0x00FF00, 2); Pixel(m, 3, 0x00FF00, 2); Pixel(m, 4, 0, 2); }
        Pixel(m, 0, 0x0000FF, resolution);
    }

    [Theory]
    [InlineData(0x0A10)] [InlineData(0x6A00)] [InlineData(0x0610)]
    public void SteadySpecialModeRenderingDoesNotAllocate(ushort control)
    {
        using var m = Create([255, 0, 255, 0], control);
        m.ExecuteFrame(); m.ExecuteFrame();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 4; i++) m.ExecuteFrame();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
