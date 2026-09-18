using Copper68k;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

// Commodore HRM ch.3 dual-playfield palette and ch.7 BPLCON2 priority tables.
// Pixel phase expectations retain the engine's existing Denise pipeline contract.
public sealed class LightweightDualPlayfieldTests
{
    [Theory]
    [InlineData(454, false, false)] [InlineData(454, false, true)]
    [InlineData(908, false, false)] [InlineData(908, false, true)]
    [InlineData(908, true, false)] [InlineData(908, true, true)]
    public void EveryColorCombinationRespectsPlaneGroupingTransparencyAndPriority(int width, bool hires, bool pf2First)
    {
        var colors = hires ? 4 : 8;
        var codes = new List<int>();
        var expected = new List<int>();
        for (var a = 0; a < colors; a++)
            for (var b = 0; b < colors; b++)
            {
                codes.Add(Interleave(a, b));
                expected.Add(pf2First ? b != 0 ? 8 + b : a : a != 0 ? a : b != 0 ? 8 + b : 0);
            }
        using var m = Create(width, hires, codes.ToArray(), control: pf2First ? 0x40 : 0);
        m.ExecuteFrame();
        for (var i = 0; i < expected.Count; i++) Pixel(m, hires, i, expected[i]);
        Assert.Null(m.UnsupportedActiveFeature);
    }

    [Theory]
    [InlineData(false, 1, 6)] [InlineData(false, 15, 3)]
    [InlineData(true, 2, 5)] [InlineData(true, 9, 14)]
    public void EachPlayfieldScrollsIndependently(bool hires, int pf1Scroll, int pf2Scroll)
    {
        using var m = Create(908, hires, [Interleave(1, 1)], scroll: pf1Scroll | (pf2Scroll << 4));
        m.ExecuteFrame();
        var a = hires ? 2 * (pf1Scroll & 7) : pf1Scroll;
        var b = hires ? 2 * (pf2Scroll & 7) : pf2Scroll;
        for (var i = 0; i < 32; i++) Pixel(m, hires, i, i == a ? 1 : i == b ? 9 : 0);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void AllSpriteGroupsObeyBothPlayfieldsIncludingTheHiddenOne(bool hires)
    {
        for (var group = 0; group < 4; group++)
            for (var p1 = 0; p1 <= 4; p1++)
                for (var p2 = 0; p2 <= 4; p2++)
                {
                    // PF2 in front, but opaque PF1 must still mask the sprite.
                    using var m = Create(908, hires, Enumerable.Repeat(3, 16).ToArray(), control: 0x40 | p1 | (p2 << 3));
                    Sprite(m, group * 2);
                    m.ExecuteFrame();
                    Pixel(m, hires, 0, group < p1 && group < p2 ? 17 + group * 4 : 9);
                }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void HiresSpriteAdvancesOnceAndEachSubpixelHasItsOwnMask(bool attached)
    {
        using var m = Create(908, true, [1, 2, 0, 0], control: 0x20); // PF1 above sprites, PF2 below.
        Sprite(m, 6, attached);
        m.ExecuteFrame();
        Pixel(m, true, 0, 1);
        Pixel(m, true, 1, attached ? 21 : 29);
        Pixel(m, true, 2, 0); // One lores sprite bit, not two.
    }

    [Theory]
    [InlineData(false, 0, 0x00, 17)] [InlineData(true, 0, 0x00, 17)]
    [InlineData(false, 1, 0x04, 17)] [InlineData(true, 1, 0x04, 17)]
    [InlineData(false, 2, 0x20, 17)] [InlineData(true, 2, 0x20, 17)]
    [InlineData(false, 3, 0x04, 1)] [InlineData(true, 3, 0x04, 1)]
    public void OnlyOpaquePlayfieldsMaskSprites(bool hires, int code, int control, int expected)
    {
        using var m = Create(908, hires, [code], control: control);
        Sprite(m, 0);
        m.ExecuteFrame();
        Pixel(m, hires, 0, expected);
    }

    [Fact]
    public void HiddenHighestSpriteDoesNotExposeALowerPrioritySprite()
    {
        using var m = Create(908, false, Enumerable.Repeat(3, 16).ToArray());
        Sprite(m, 0); Sprite(m, 2);
        m.ExecuteFrame();
        Pixel(m, false, 0, 1);
    }

    [Theory]
    [InlineData(5)] [InlineData(6)] [InlineData(7)]
    [InlineData(5 << 3)] [InlineData(6 << 3)] [InlineData(7 << 3)]
    public void UndocumentedPriorityCodesWrittenAfterModeEnableAreReported(int priority)
    {
        using var m = Create(908, false, [3]);
        m.ExecuteFrame();
        Assert.Null(m.UnsupportedActiveFeature);
        W(m, 0x104, (ushort)priority);
        m.ExecuteFrame();
        Assert.Contains("priority codes above four", m.UnsupportedActiveFeature);
    }

    [Fact]
    public void DualPlayfieldHamRemainsExplicitlyUnsupported()
    {
        using var m = Create(908, false, [3]);
        W(m, 0x100, 0x6E00);
        m.ExecuteFrame();
        Assert.Contains("dual-playfield HAM", m.UnsupportedActiveFeature);
    }

    [Theory]
    [InlineData(false, 132)] [InlineData(false, 133)]
    [InlineData(true, 132)] [InlineData(true, 133)]
    public void DisplayWindowClipsBothPlayfieldsAtEachPhysicalPixel(bool hires, int start)
    {
        using var m = Create(908, hires, Enumerable.Repeat(2, 32).ToArray(), startX: start);
        m.ExecuteFrame();
        var boundary = (start - 129) * (hires ? 2 : 1);
        Pixel(m, hires, boundary - 1, 0);
        Pixel(m, hires, boundary, 9);
    }

    [Fact]
    public void Bplcon2WriteChangesOnlyFuturePixelsAndModeSwitchRetainsShifters()
    {
        using var m = Create(454, false, Enumerable.Repeat(3, 32).ToArray());
        var writeCycle = 44L * 454 + 136;
        m.AdvanceHardwareTo(writeCycle);
        m.WriteCustomRegisterFromCopper(0x104, 0x40, writeCycle);
        m.AdvanceHardwareTo(writeCycle + 8);
        m.WriteCustomRegisterFromCopper(0x100, 0x6200, m.Cycle); // Single playfield: raw code 3.
        m.ExecuteFrame();
        Pixel(m, false, 6, 1); // x135 still sees the old effective control.
        Pixel(m, false, 7, 9); // x136 sees new PF2 priority.
        Pixel(m, false, 14, 9); // x143 still has dual composition.
        Pixel(m, false, 15, 3); // x144 sees single-playfield composition.
    }

    [Theory]
    [InlineData(454, false, false)] [InlineData(454, false, true)]
    [InlineData(908, false, false)] [InlineData(908, false, true)]
    [InlineData(908, true, false)] [InlineData(908, true, true)]
    public void HiddenPixelsStillAdvanceShiftersBeforeTheDisplayWindow(int width, bool hires, bool dual)
    {
        var codes = Enumerable.Range(0, 64).Select(i => Interleave((i + 1) % 4, (i * 3) % 4)).ToArray();
        using var m = Create(width, hires, codes, startX: 134);
        if (!dual) W(m, 0x100, (ushort)(hires ? 0xC200 : 0x6200));
        m.ExecuteFrame();
        var first = 5 * (hires ? 2 : 1);
        Pixel(m, hires, first - 1, 0);
        for (var i = first; i < first + 8; i++)
        {
            var pf1 = (i + 1) % 4;
            var pf2 = (i * 3) % 4;
            var dualColor = pf1 != 0 ? pf1 : pf2 != 0 ? 8 + pf2 : 0;
            Pixel(m, hires, i, dual ? dualColor : codes[i]);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void DualModeDoesNotChangeDmaFetchTimingPointersOrCpuContention(bool hires)
    {
        using var dual = Create(908, hires, [3]);
        using var single = Create(908, hires, [3]);
        W(single, 0x100, (ushort)(hires ? 0xC200 : 0x6200));
        var target = 44L * 454 + 150;
        dual.AdvanceHardwareTo(target); single.AdvanceHardwareTo(target);
        var a = target; var b = target;
        dual.ReadWord(0x1000, ref a, M68kBusAccessKind.CpuDataRead);
        single.ReadWord(0x1000, ref b, M68kBusAccessKind.CpuDataRead);
        Assert.Equal(a, b);
        for (var i = 0; i < (hires ? 4 : 6); i++)
            Assert.Equal(single.GetLiveBitplanePointer(i), dual.GetLiveBitplanePointer(i));
        Assert.Equal(single.BitplaneLastOutputCycle, dual.BitplaneLastOutputCycle);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void InterlacedDualOutputIsStableAndSteadyFramesDoNotAllocate(bool hires)
    {
        using var m = Create(908, hires, [3, 2, 1], interlace: true);
        for (var i = 0; i < 4; i++) m.ExecuteFrame();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 8; i++) m.ExecuteFrame();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Pixel(m, hires, 0, 1); Pixel(m, hires, 1, 9); Pixel(m, hires, 2, 1);
        Assert.Null(m.UnsupportedActiveFeature);
    }

    private static int Interleave(int a, int b)
    {
        var code = 0;
        for (var bit = 0; bit < 3; bit++)
            code |= (((a >> bit) & 1) << (bit * 2)) | (((b >> bit) & 1) << (bit * 2 + 1));
        return code;
    }

    private static LightweightA500Machine Create(int width, bool hires, int[] codes,
        int control = 0, int scroll = 0, int startX = 129, bool interlace = false)
    {
        var m = new LightweightA500Machine(new() { FramebufferWidth = width });
        m.Cpu.Stopped = true;
        var cycle = m.Cycle;
        for (var plane = 0; plane < (hires ? 4 : 6); plane++)
        {
            var address = 0x10000u + (uint)(plane * 0x1000);
            for (var word = 0; word * 16 < codes.Length; word++)
            {
                ushort data = 0;
                for (var bit = 0; bit < 16 && word * 16 + bit < codes.Length; bit++)
                    data |= (ushort)(((codes[word * 16 + bit] >> plane) & 1) << (15 - bit));
                m.WriteWord(address + (uint)(word * 2), data, ref cycle, M68kBusAccessKind.CpuDataWrite);
            }
            m.WriteLong(0xDFF0E0u + (uint)(plane * 4), address, ref cycle, M68kBusAccessKind.CpuDataWrite);
        }
        for (var i = 0; i < 32; i++) W(m, (ushort)(0x180 + i * 2), (ushort)(0x100 | i));
        W(m, 0x08E, (ushort)(0x2C00 | startX)); W(m, 0x090, 0x2DC1);
        W(m, 0x092, (ushort)(hires ? 0x3C : 0x38)); W(m, 0x094, (ushort)(hires ? 0xD4 : 0xD0));
        W(m, 0x108, unchecked((ushort)(hires ? -80 : -40)));
        W(m, 0x10A, unchecked((ushort)(hires ? -80 : -40)));
        W(m, 0x104, (ushort)control); W(m, 0x102, (ushort)scroll);
        W(m, 0x100, (ushort)((hires ? 0xC600 : 0x6600) | (interlace ? 4 : 0)));
        W(m, 0x096, 0x8300);
        m.Cpu.Cycles = m.Cycle;
        return m;
    }

    private static void Sprite(LightweightA500Machine m, int sprite, bool attached = false)
    {
        var offset = (ushort)(0x140 + sprite * 8);
        W(m, offset, 0x2C40); W(m, (ushort)(offset + 2), 0x2D00);
        W(m, (ushort)(offset + 6), 0); W(m, (ushort)(offset + 4), 0x8000);
        if (attached)
        {
            W(m, (ushort)(offset + 8), 0x2C40); W(m, (ushort)(offset + 10), 0x2D80);
            W(m, (ushort)(offset + 14), 0); W(m, (ushort)(offset + 12), 0x8000);
        }
    }
    private static void W(LightweightA500Machine m, ushort register, ushort value)
    {
        var cycle = m.Cycle;
        m.WriteWord(0xDFF000u + register, value, ref cycle, M68kBusAccessKind.CpuDataWrite);
    }
    private static void Pixel(LightweightA500Machine m, bool hires, int offset, int index)
    {
        var rgb = 0x100 | index;
        var expected = unchecked((int)0xFF000000) | ((rgb >> 8) & 15) * 0x110000 |
            ((rgb >> 4) & 15) * 0x1100 | (rgb & 15) * 17;
        var scale = m.FramebufferWidth / 454;
        var x = 129 * scale + offset * (hires ? 1 : scale);
        for (var i = 0; i < (hires ? 1 : scale); i++)
            Assert.Equal(expected, m.Framebuffer.Span[44 * m.FramebufferWidth + x + i]);
    }
}
