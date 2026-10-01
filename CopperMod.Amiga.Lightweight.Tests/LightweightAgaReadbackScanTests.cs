// Copyright (C) 2026 Ilkka Lehtoranta. SPDX-License-Identifier: MIT
using Copper68k;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

public sealed class LightweightAgaReadbackScanTests
{
    private static LightweightA500Machine Create()
    {
        var m = new LightweightA500Machine(new() { AgnusModel = LightweightAgnusModel.Mos8374Alice,
            DeniseModel = LightweightDeniseModel.Lisa4203, CpuModel = M68kCpuModel.M68EC020,
            ChipRamBytes = 2097152, SlowRamBytes = 0, FramebufferWidth = 1816 });
        m.WriteChipWordDma(0x1000, 0x4E72); m.WriteChipWordDma(0x1002, 0x2700); m.Reset();
        return m;
    }
    private static void Write(LightweightA500Machine m, ushort r, ushort v)
    { m.WriteCustomRegisterFromCopper(r, v, m.Cycle); m.AdvanceHardwareTo(m.Cycle + 4); }
    private static ushort Read(LightweightA500Machine m, ushort r)
    { long c = m.Cycle; return m.ReadWord(0xDFF000u + r, ref c, M68kBusAccessKind.CpuDataRead); }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    public void ReadbackSelectsBothHalvesAndTransparencyInEveryPaletteBank(int bank)
    {
        using var m = Create();
        for (var b = 0; b < 8; b++)
        {
            Write(m, 0x106, (ushort)(0xC00 | b << 13));
            Write(m, 0x180, (ushort)(0xF000 | (b+1) * 0x111));
            Write(m, 0x106, (ushort)(0xE00 | b << 13));
            Write(m, 0x180, (ushort)(0xF000 | (15-b) * 0x111));
        }
        Write(m, 0x104, 0x124); Write(m, 0x106, (ushort)(0xC00 | bank << 13));
        Assert.Equal(0x8000 | (bank+1) * 0x111, Read(m, 0x180));
        Write(m, 0x106, (ushort)(0xE00 | bank << 13));
        Assert.Equal((15-bank) * 0x111, Read(m, 0x180));
        Assert.Null(m.UnsupportedActiveFeature);
    }

    [Fact]
    public void RdramSuppressesColourWritesAndHighWritesReplaceBothHalves()
    {
        using var m = Create(); Write(m, 0x180, 0x8ABC); Write(m, 0x106, 0xE00); Write(m, 0x180, 0x123);
        Write(m, 0x104, 0x100); Write(m, 0x180, 0x789);
        Assert.Equal(0x123, Read(m, 0x180));
        Write(m, 0x106, 0xC00); Write(m, 0x180, 0x456); Assert.Equal(0x8ABC, Read(m, 0x180));
        Write(m, 0x104, 0); Assert.Equal(0xFFFF, Read(m, 0x180));
        Write(m, 0x180, 0x456); Write(m, 0x104, 0x100);
        Assert.Equal(0x456, Read(m, 0x180)); Write(m, 0x106, 0xE00); Assert.Equal(0x456, Read(m, 0x180));
    }

    [Fact]
    public void ByteAndLongReadsSelectTheSamePaletteAsWordReads()
    {
        using var m = Create(); Write(m, 0x180, 0x8ABC); Write(m, 0x182, 0x123); Write(m, 0x104, 0x100);
        long c = m.Cycle;
        Assert.Equal(0x8A, m.ReadByte(0xDFF180, ref c, M68kBusAccessKind.CpuDataRead));
        Assert.Equal(0xBC, m.ReadByte(0xDFF181, ref c, M68kBusAccessKind.CpuDataRead));
        Assert.Equal(0x8ABC0123u, m.ReadLong(0xDFF180, ref c, M68kBusAccessKind.CpuDataRead));
    }

    [Theory]
    [InlineData(0, 44)] [InlineData(1, 44)] [InlineData(2, 44)] [InlineData(3, 44)]
    [InlineData(0, 45)] [InlineData(1, 45)] [InlineData(2, 45)] [InlineData(3, 45)]
    public void ScanDoublingSelectsOneModuloForAllEightPlanesByDiwStartParity(int mode, int firstLine)
    {
        using var m = Create();
        for (var p = 0; p < 8; p++) { Write(m, (ushort)(0xE0+p*4), (ushort)(2+p)); Write(m, (ushort)(0xE2+p*4), 0); }
        Write(m, 0x08E, (ushort)(firstLine << 8 | 0x81)); Write(m, 0x090, 0x2CC1);
        Write(m, 0x092, 0x38); Write(m, 0x094, (ushort)(mode == 0 ? 0xD0 : mode == 3 ? 0xB8 : 0xC8));
        Write(m, 0x108, unchecked((ushort)-40)); Write(m, 0x10A, 8);
        Write(m, 0x1FC, (ushort)(0x4000 | mode)); Write(m, 0x100, 0x210); Write(m, 0x096, 0x8300);
        for (var row = 0; row < 4; row++)
        {
            m.AdvanceHardwareTo((firstLine+row+1L)*454);
            for (var p = 0; p < 8; p++) Assert.Equal((uint)((2+p)*0x10000 + ((row+1)/2)*48), m.GetLiveBitplanePointer(p));
        }
        Assert.Null(m.UnsupportedActiveFeature);
    }

    [Theory]
    [InlineData(false, 10u)] [InlineData(true, 0u)]
    public void TerminalAcceptedFetchRetainsItsScanModeAcrossAnFmodeWrite(bool scan, uint advance)
    {
        using var m = Create(); Write(m, 0xE0, 2); Write(m, 0xE2, 0); Write(m, 0xE4, 3); Write(m, 0xE6, 0);
        Write(m, 0x08E, 0x2C81); Write(m, 0x090, 0x2CC1); Write(m, 0x092, 0x38); Write(m, 0x094, 0x40);
        Write(m, 0x108, 0xFFFC); Write(m, 0x10A, 6); Write(m, 0x1FC, (ushort)(scan ? 0x4000 : 0));
        Write(m, 0x100, 0x2200); Write(m, 0x096, 0x8300);
        var input = 44*454L+0x43*2; m.AdvanceHardwareTo(input);
        Assert.Equal(input+2, m.BitplanePendingOutputCycle);
        m.WriteCustomRegisterFromCopper(0x1FC, (ushort)(scan ? 0 : 0x4000), input);
        m.AdvanceHardwareTo(input+2);
        Assert.Equal(0x30000u+advance, m.GetLiveBitplanePointer(1));
    }

    [Fact]
    public void WrappedTerminalModuloUsesTheLiveVerticalBeamParity()
    {
        using var m = Create();
        for (var p = 0; p < 6; p++) { Write(m, (ushort)(0xE0+p*4), (ushort)(2+p)); Write(m, (ushort)(0xE2+p*4), 0); }
        Write(m, 0x08E, 0x2C81); Write(m, 0x090, 0x2CC1); Write(m, 0x092, 0xD4); Write(m, 0x094, 0xFC);
        Write(m, 0x108, 6); Write(m, 0x10A, 10); Write(m, 0x1FC, 0x4000);
        Write(m, 0x100, 0x6200); Write(m, 0x096, 0x8300); m.AdvanceHardwareTo(45*454L+2);
        Assert.Equal(0x2000Eu, m.GetLiveBitplanePointer(0));
        Assert.Equal(0x3000Au, m.GetLiveBitplanePointer(1));
        // The canonical refresh owner steals plane 5's wrapped slot at h=0.
        Assert.Equal(0x60002u, m.GetLiveBitplanePointer(4));
    }

    [Fact]
    public void EnablingSpriteScanDoublingCannotCancelAnAcceptedDataAddress()
    {
        using var m = Create();
        m.WriteChipWordDma(0x50000, 0x2CC0); m.WriteChipWordDma(0x50002, 0x3001);
        m.WriteChipWordDma(0x50004, 0x1111); m.WriteChipWordDma(0x50006, 0x2222);
        m.WriteChipWordDma(0x50008, 0x3333); m.WriteChipWordDma(0x5000A, 0x4444);
        Write(m, 0x120, 5); Write(m, 0x122, 0); Write(m, 0x096, 0x8220);
        var input = 45*454L+0x17*2; m.AdvanceHardwareTo(input);
        m.WriteCustomRegisterFromCopper(0x1FC, 0x8000, input); m.AdvanceHardwareTo(input+10);
        Assert.Equal(0x3333, m.GetSpriteDmaDataA(0)); Assert.Equal(0x2222, m.GetSpriteDmaDataB(0));
        Assert.Equal(0x5000Au, m.GetLiveSpritePointer(0)); Assert.Null(m.UnsupportedActiveFeature);
    }

    [Fact]
    public void SpriteSh10RemainsAHorizontalBitWhenGlobalScanDoublingIsOff()
    {
        using var m = Create(); Write(m, 0x106, 0xC42); Write(m, 0x100, 1);
        Write(m, 0x182, 0xF00); Write(m, 0x10C, 0);
        m.WriteChipWordDma(0x50000, 0x2CC0); m.WriteChipWordDma(0x50002, 0x2E01);
        m.WriteChipWordDma(0x50004, 0xFFFF); m.WriteChipWordDma(0x50006, 0);
        m.WriteChipWordDma(0x50008, 0xFFFF); m.WriteChipWordDma(0x5000A, 0);
        Write(m, 0x120, 5); Write(m, 0x122, 0); Write(m, 0x096, 0x8220); m.ExecuteFrame();
        for (var y = 44; y < 46; y++)
        {
            Assert.Equal(unchecked((int)0xFF000000), m.Framebuffer.Span[y*1816+520]);
            for (var x = 1544; x < 1608; x++) Assert.Equal(unchecked((int)0xFFFF0000), m.Framebuffer.Span[y*1816+x]);
        }
    }

    [Fact]
    public void ActiveScanDoublingAndPaletteReadsDoNotAllocateAfterWarmup()
    {
        using var m = Create();
        for (var p = 0; p < 8; p++)
        {
            Write(m, (ushort)(0xE0+p*4), (ushort)(2+p)); Write(m, (ushort)(0xE2+p*4), 0);
        }
        for (var s = 0; s < 8; s++) { Write(m, (ushort)(0x120+s*4), 6); Write(m, (ushort)(0x122+s*4), 0); }
        m.WriteChipWordDma(0x50000, 0x2CC0); m.WriteChipWordDma(0x50008, 0x2E01);
        for (uint b = 0; b < 8; b += 2) m.WriteChipWordDma(0x50010+b, 0xFFFF);
        Write(m, 0x08E, 0x2C81); Write(m, 0x090, 0x2CC1); Write(m, 0x092, 0x38); Write(m, 0x094, 0xB8);
        Write(m, 0x108, 0xFFD8); Write(m, 0x10A, 0); Write(m, 0x1FC, 0xC00F);
        Write(m, 0x106, 0xC40); Write(m, 0x182, 0xF00); Write(m, 0x10C, 0); Write(m, 0x100, 0x211);
        Write(m, 0x104, 0x124); Write(m, 0x096, 0x8320);
        ushort Field()
        {
            Write(m, 0x120, 5); Write(m, 0x122, 0); m.ExecuteFrame();
            return Read(m, 0x182);
        }
        Field(); Field(); ushort proof = 0; var before = GC.GetAllocatedBytesForCurrentThread();
        for (var f = 0; f < 4; f++) proof = Field();
        var allocated = GC.GetAllocatedBytesForCurrentThread()-before;
        Assert.Equal(0xF00, proof);
        Assert.Equal(unchecked((int)0xFFFF0000), m.Framebuffer.Span[45*1816+520]);
        Assert.Equal(0, allocated);
    }

    public static IEnumerable<object[]> SpriteCases()
    {
        for (var mode = 0; mode < 4; mode++) for (var start = 44; start <= 45; start++)
            foreach (var enabled in new[] { false, true }) yield return [mode, start, enabled];
    }

    [Theory, MemberData(nameof(SpriteCases))]
    public void ScanDoubledSpritesReuseDataAndSkipOnlyFlaggedDataDma(int mode, int start, bool flag)
    {
        using var m = Create(); var bytes = mode == 0 ? 2 : mode == 3 ? 8 : 4;
        // Null streams prevent reset-vector data from arming the other channels.
        for (var s = 0; s < 8; s++) { Write(m, (ushort)(0x120+s*4), 6); Write(m, (ushort)(0x122+s*4), 0); }
        Write(m, 0x106, 0xC42); Write(m, 0x100, 1); Write(m, 0x182, 0xF00); Write(m, 0x184, 0x0F0);
        Write(m, 0x10C, 0); Write(m, 0x1FC, (ushort)(0x8000 | mode << 2));
        m.WriteChipWordDma(0x50000, (ushort)(start << 8 | 0x40 | (flag ? 0x80 : 0)));
        m.WriteChipWordDma(0x50000u+(uint)bytes, (ushort)((start+4) << 8 | 1));
        for (var row = 0; row < (flag ? 2 : 4); row++) for (var b = 0; b < bytes; b += 2)
        {
            m.WriteChipWordDma(0x50000u+(uint)(bytes*(2+row*2)+b), (ushort)(row % 2 == 0 ? 0xFFFF : 0));
            m.WriteChipWordDma(0x50000u+(uint)(bytes*(3+row*2)+b), (ushort)(row % 2 == 1 ? 0xFFFF : 0));
        }
        Write(m, 0x120, 5); Write(m, 0x122, 0); Write(m, 0x096, 0x8220);
        for (var row = 0; row < 4; row++)
        {
            m.AdvanceHardwareTo((start+row)*454L+200);
            Assert.Equal(0x50000u+(uint)(bytes*(2+(flag ? row/2+1 : row+1)*2)), m.GetLiveSpritePointer(0));
        }
        m.AdvanceHardwareTo((start+4)*454L+200);
        Assert.Equal(0, m.GetSpriteDmaPos(0)); Assert.Equal(0, m.GetSpriteDmaCtl(0));
        m.ExecuteFrame(); var bits = mode == 0 ? 16 : mode == 3 ? 64 : 32;
        for (var row = 0; row < 4; row++) for (var x = 520; x < 520+bits*4; x++)
            Assert.Equal(unchecked((int)((flag ? row/2 : row) % 2 == 0 ? 0xFFFF0000 : 0xFF00FF00)), m.Framebuffer.Span[(start+row)*1816+x]);
        Assert.Null(m.UnsupportedActiveFeature);
    }
}
