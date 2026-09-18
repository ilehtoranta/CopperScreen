using Copper68k;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

// Commodore HRM ch.7 CLXCON/CLXDAT; independent OCS probes:
// vAmigaTS 0489f55d22ef7560d924a304e30998aea6864264, Denise/Sprites/collision.
// These preserve the engine's current register/pixel phases, not a new silicon timing claim.
public sealed class LightweightCollisionTests
{
    [Theory]
    [InlineData(0, 2, 0x0200)] [InlineData(0, 4, 0x0400)]
    [InlineData(0, 6, 0x0800)] [InlineData(2, 4, 0x1000)]
    [InlineData(2, 6, 0x2000)] [InlineData(4, 6, 0x4000)]
    public void EverySpriteGroupPairLatchesItsOwnBit(int a, int b, int expected)
    {
        using var m = Create(false, false, 0, 0x0FFF);
        Sprite(m, a); Sprite(m, b);
        m.ExecuteFrame();
        Assert.Equal((ushort)(0x8000 | expected), Peek(m));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OddSpriteInclusionDoesNotDependOnAttachment(bool attached)
    {
        using var m = Create(false, false, 0, 0x0FFF);
        Sprite(m, 0, data: 0); Sprite(m, 1, attached: attached); Sprite(m, 2);
        m.ExecuteFrame();
        Assert.Equal(0x8000, Peek(m));
        W(m, 0x098, 0x1FFF);
        m.ExecuteFrame();
        Assert.Equal(0x8200, Peek(m));
    }

    [Theory]
    [InlineData(false, false, 1, 0x0041, 0x8023)]
    [InlineData(false, false, 0, 0x0041, 0x8020)]
    [InlineData(false, false, 1, 0x0082, 0x8000)]
    [InlineData(false, true, 1, 0x0082, 0x8002)]
    [InlineData(true, false, 1, 0x0082, 0x8000)]
    [InlineData(true, true, 1, 0x0082, 0x8002)]
    public void PlaneMatchingAndSinglePlayfieldQuirk(bool hires, bool dual, int code, int control, int expected)
    {
        using var m = Create(hires, dual, code, control);
        Sprite(m, 0);
        m.ExecuteFrame();
        Assert.Equal((ushort)expected, Peek(m));
    }

    [Fact]
    public void NoEnabledPlanesMeansAnUnconditionalMatch()
    {
        using var m = Create(false, false, 0, 0);
        Sprite(m, 0);
        m.ExecuteFrame();
        Assert.Equal(0x8023, Peek(m));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PriorityAndHamColorDoNotHideRawCollisions(bool ham)
    {
        using var m = Create(false, false, 1, 0x0041);
        if (ham) W(m, 0x100, 0x6A00);
        W(m, 0x104, 0); // Opaque playfield hides every sprite pair.
        Sprite(m, 0); Sprite(m, 2);
        m.ExecuteFrame();
        Assert.Equal(0x8267, Peek(m));
    }

    [Fact]
    public void HiddenLowerPrioritySpritesStillCollideWithEachOther()
    {
        using var m = Create(false, false, 0, 0x0FFF);
        for (var sprite = 0; sprite < 8; sprite += 2) Sprite(m, sprite);
        m.ExecuteFrame();
        Assert.Equal(0xFE00, Peek(m));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void DisplayWindowClipsCollisionChecksIncludingZeroMatches(bool straddlesWindow)
    {
        using var m = Create(false, false, 1, 0x0FC0, startX: 160);
        Sprite(m, 0, x: straddlesWindow ? 152 : 128, data: 0xFFFF);
        m.ExecuteFrame();
        Assert.Equal(straddlesWindow ? 0x8020 : 0x8000, Peek(m));
    }

    [Fact]
    public void HiresSecondSubpixelCanCauseACollision()
    {
        using var m = Create(true, true, 0, 0x0041);
        var cycle = m.Cycle;
        m.WriteWord(0x10000, 0x4000, ref cycle, M68kBusAccessKind.CpuDataWrite);
        Sprite(m, 0);
        m.ExecuteFrame();
        Assert.Equal(0x8023, Peek(m));
    }

    [Fact]
    public void DisablingBitplanesDoesNotGenerateAZeroBackgroundPlayfieldCollision()
    {
        using var m = Create(false, false, 1, 0x0FC0);
        m.ExecuteFrame();
        Assert.Equal(0x8000, Peek(m));
        W(m, 0x100, 0x0200);
        for (var frame = 0; frame < 8; frame++)
        {
            m.ExecuteFrame();
            Assert.Equal(0x8000, Peek(m));
        }
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public void MidlineDisableRetainsCollisionComparisonOnlyThroughCurrentWindow(bool dual, bool narrowWindow)
    {
        // Reduced sprcoll8/8d versus sprcollbrd layout: a solid nonmatching
        // plane is disabled before the wide DIW closes, but after the narrow
        // DIW closes. This isolates the transition model inferred from the
        // native OCS photos; the photos are not an exact pixel-phase oracle.
        using var m = Create(false, dual, 2, 0xF0C0);
        W(m, 0x100, (ushort)(dual ? 0x2600 : 0x2200));
        if (narrowWindow) W(m, 0x090, 0x2D80);
        m.AdvanceHardwareTo(44 * 454 + 430);
        Assert.Equal(0x8000, Peek(m));
        W(m, 0x100, 0x0200);
        m.AdvanceHardwareTo(45 * 454);
        Assert.Equal(narrowWindow ? 0x8000 : 0x8001, Peek(m));

        var cycle = m.Cycle;
        m.ReadWord(0xDFF00E, ref cycle, M68kBusAccessKind.CpuDataRead);
        m.AdvanceHardwareTo(2 * 313 * 454);
        Assert.Equal(0x8000, Peek(m));
    }

    [Fact]
    public void LatchesSurviveControlWritesFramesAndPeeksButCpuReadsClear()
    {
        using var m = Create(false, false, 0, 0);
        Sprite(m, 0); Sprite(m, 2);
        m.ExecuteFrame();
        var before = Peek(m);
        Assert.Equal(0x8267, before);
        Assert.Equal(before, Peek(m));
        W(m, 0x098, 0x0FFF);
        W(m, 0x00E, 0); // Read-only register; writes do not clear it.
        m.ExecuteFrame();
        Assert.Equal(before, Peek(m));
        var cycle = m.Cycle;
        Assert.Equal(before, m.ReadWord(0xDFF00E, ref cycle, M68kBusAccessKind.CpuDataRead));
        Assert.Equal(0x8000, Peek(m));
        m.ExecuteFrame();
        Assert.Equal(0x8200, Peek(m));
        cycle = m.Cycle;
        Assert.Equal(0x82, m.ReadByte(0xDFF00E, ref cycle, M68kBusAccessKind.CpuDataRead));
        Assert.Equal(0x8000, Peek(m));
        m.Reset();
        Assert.Equal(0x8000, Peek(m));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ActiveSpriteCollisionsResumeAfterReadClearAndRespectNewMatchControl(bool hires)
    {
        using var m = Create(hires, false, 1, 0x0041);
        Sprite(m, 0, data: 0xFFFF);
        m.ExecuteFrame();
        Assert.Equal(0x8023, Peek(m));
        var cycle = m.Cycle;
        m.ReadWord(0xDFF00E, ref cycle, M68kBusAccessKind.CpuDataRead);
        m.ExecuteFrame();
        Assert.Equal(0x8023, Peek(m));

        W(m, 0x098, 0x0082);
        cycle = m.Cycle;
        m.ReadWord(0xDFF00E, ref cycle, M68kBusAccessKind.CpuDataRead);
        m.ExecuteFrame();
        Assert.Equal(0x8000, Peek(m));
        W(m, 0x098, 0x0041);
        m.ExecuteFrame();
        Assert.Equal(0x8023, Peek(m));
    }

    private static ushort Peek(LightweightA500Machine m) => m.GetCustomRegister(0x00E);

    private static LightweightA500Machine Create(bool hires, bool dual, int code, int control, int startX = 129)
    {
        var m = new LightweightA500Machine(new() { FramebufferWidth = 908 });
        m.Cpu.Stopped = true;
        var cycle = m.Cycle;
        for (var plane = 0; plane < (hires ? 4 : 6); plane++)
        {
            var address = 0x10000u + (uint)(plane * 0x1000);
            for (var word = 0; word < 40; word++)
                m.WriteWord(address + (uint)(word * 2), (ushort)(((code >> plane) & 1) != 0 ? 0xFFFF : 0),
                    ref cycle, M68kBusAccessKind.CpuDataWrite);
            m.WriteLong(0xDFF0E0u + (uint)(plane * 4), address, ref cycle, M68kBusAccessKind.CpuDataWrite);
        }
        W(m, 0x08E, (ushort)(0x2C00 | startX)); W(m, 0x090, 0x2DC1);
        W(m, 0x092, (ushort)(hires ? 0x3C : 0x38)); W(m, 0x094, (ushort)(hires ? 0xD4 : 0xD0));
        W(m, 0x108, unchecked((ushort)(hires ? -80 : -40)));
        W(m, 0x10A, unchecked((ushort)(hires ? -80 : -40)));
        W(m, 0x100, (ushort)((hires ? 0xC200 : 0x6200) | (dual ? 0x400 : 0)));
        W(m, 0x098, (ushort)control);
        W(m, 0x096, 0x8300);
        m.Cpu.Cycles = m.Cycle;
        return m;
    }

    private static void Sprite(LightweightA500Machine m, int sprite, int x = 128, ushort data = 0x8000, bool attached = false)
    {
        var offset = (ushort)(0x140 + sprite * 8);
        W(m, offset, (ushort)(0x2C00 | (x >> 1)));
        W(m, (ushort)(offset + 2), (ushort)(0x2D00 | (attached ? 0x80 : 0) | (x & 1)));
        W(m, (ushort)(offset + 6), 0); W(m, (ushort)(offset + 4), data);
    }

    private static void W(LightweightA500Machine m, ushort register, ushort value)
    {
        var cycle = m.Cycle;
        m.WriteWord(0xDFF000u + register, value, ref cycle, M68kBusAccessKind.CpuDataWrite);
    }
}
