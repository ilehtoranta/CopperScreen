using Copper68k;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

public sealed class LightweightAgnusMemoryTests
{
    private static LightweightA500Machine Machine(LightweightAgnusModel model, int chipKiB, int slowKiB = 0)
        => new(new() { AgnusModel = model, ChipRamBytes = chipKiB * 1024, SlowRamBytes = slowKiB * 1024 });

    private static void Register(LightweightA500Machine m, ushort offset, ushort value)
        => m.WriteCustomRegisterFromCopper(offset, value, m.Cycle);

    private static void Pointer(LightweightA500Machine m, ushort offset, uint address)
    {
        Register(m, offset, (ushort)(address >> 16));
        Register(m, (ushort)(offset + 2), (ushort)address);
    }

    [Theory]
    [InlineData(LightweightAgnusModel.Mos8371, 1024, 0)]
    [InlineData(LightweightAgnusModel.Mos8371, 2048, 0)]
    [InlineData(LightweightAgnusModel.Mos8372A, 2048, 0)]
    [InlineData(LightweightAgnusModel.Mos8372A, 1024, 512)]
    [InlineData(LightweightAgnusModel.Mos8375Pal2M, 512, 0)]
    [InlineData(LightweightAgnusModel.Mos8375Pal2M, 2048, 512)]
    [InlineData(LightweightAgnusModel.Mos8375Pal2M, 0, 0)]
    [InlineData((LightweightAgnusModel)99, 512, 512)]
    public void IncompatibleChipAndSlowBanksAreRejected(LightweightAgnusModel model, int chip, int slow)
        => Assert.Throws<ArgumentException>(() => Machine(model, chip, slow));

    // Commodore HRM appendix C: VPOSR identification is $00 for PAL OCS,
    // $20 for PAL ECS. LOF and the beam-position bits are separate fields.
    [Theory]
    [InlineData(LightweightAgnusModel.Mos8371, 512, 0)]
    [InlineData(LightweightAgnusModel.Mos8372A, 1024, 0x2000)]
    [InlineData(LightweightAgnusModel.Mos8375Pal2M, 2048, 0x2000)]
    public void NativeChipIdentificationSurvivesBeamAndReset(LightweightAgnusModel model, int chip, int id)
    {
        using var m = Machine(model, chip);
        long cycle = 0;
        Assert.Equal(id, m.ReadWord(0xDFF004, ref cycle, M68kBusAccessKind.CpuDataRead) & 0x7F00);
        m.AdvanceHardwareTo(300 * 454L);
        cycle = m.Cycle;
        Assert.Equal(id, m.ReadWord(0xDFF004, ref cycle, M68kBusAccessKind.CpuDataRead) & 0x7F00);
        m.Reset();
        cycle = m.Cycle;
        Assert.Equal(id, m.ReadWord(0xDFF004, ref cycle, M68kBusAccessKind.CpuDataRead) & 0x7F00);
    }

    [Theory]
    [InlineData(LightweightAgnusModel.Mos8372A, 1024, 0x080002u)]
    [InlineData(LightweightAgnusModel.Mos8375Pal2M, 2048, 0x180002u)]
    public void CpuAndDmaShareDistinctUpperChipBanks(LightweightAgnusModel model, int chip, uint high)
    {
        using var m = Machine(model, chip);
        long cycle = 454;
        m.WriteWord(2, 0x1111, ref cycle, M68kBusAccessKind.CpuDataWrite);
        m.WriteWord(high, 0xCAFE, ref cycle, M68kBusAccessKind.CpuDataWrite);
        Assert.Equal(0xCAFE, m.ReadChipWordDma(high));
        Assert.Equal(0x1111, m.ReadChipWordDma(2));
        m.WriteChipWordDma(high, 0xABCD);
        Assert.Equal(0xABCD, m.ReadWord(high, ref cycle, M68kBusAccessKind.CpuInstructionFetch));
        Assert.Equal(0x1111, m.ReadWord(2, ref cycle, M68kBusAccessKind.CpuDataRead));
    }

    [Fact]
    public void Unfitted8375SecondBankDoesNotAliasItsFittedFirstBank()
    {
        using var m = Machine(LightweightAgnusModel.Mos8375Pal2M, 1024);
        m.WriteChipWordDma(0x2000, 0x1234);
        m.WriteChipWordDma(0x102000, 0xBEEF);
        long cycle = 0;
        m.WriteWord(0x102000, 0xCAFE, ref cycle, M68kBusAccessKind.CpuDataWrite);
        Assert.Equal(0xFFFF, m.ReadChipWordDma(0x102000));
        Assert.Equal(0xFFFF, m.ReadWord(0x102000, ref cycle, M68kBusAccessKind.CpuDataRead));
        Assert.Equal(0x1234, m.ReadChipWordDma(0x2000));
    }

    [Fact]
    public void EcsA19DmaSelectsTheSlowBankInTheUnmodified512KiBLayout()
    {
        using var m = Machine(LightweightAgnusModel.Mos8372A, 512, 512);
        long cycle = 0;
        m.WriteWord(0xC02000, 0xCAFE, ref cycle, M68kBusAccessKind.CpuDataWrite);
        Assert.Equal(0xCAFE, m.ReadChipWordDma(0x082000));
        m.WriteChipWordDma(0x182000, 0xABCD); // A20 is not decoded by the 1 MiB Agnus.
        Assert.Equal(0xABCD, m.ReadWord(0xC02000, ref cycle, M68kBusAccessKind.CpuDataRead));
        m.WriteWord(0x082000, 0x1111, ref cycle, M68kBusAccessKind.CpuDataWrite);
        Assert.Equal(0x1111, m.ReadChipWordDma(0x2000)); // CPU JP2 still routes A23.
        Assert.Equal(0xABCD, m.ReadChipWordDma(0x082000));
    }

    // Appendix C extends ECS pointer registers from three to five high bits.
    // The 8372A physical decoder ignores A20; the pointer still retains it.
    [Theory]
    [InlineData(LightweightAgnusModel.Mos8371, 512, 0x07FFFEu)]
    [InlineData(LightweightAgnusModel.Mos8372A, 1024, 0x1FFFFEu)]
    [InlineData(LightweightAgnusModel.Mos8375Pal2M, 2048, 0x1FFFFEu)]
    public void EveryDmaPointerUsesTheSelectedRegisterWidth(LightweightAgnusModel model, int chip, uint expected)
    {
        using var m = Machine(model, chip);
        foreach (var offset in new ushort[] { 0x020, 0x048, 0x080, 0x0E0, 0x120 }) Pointer(m, offset, uint.MaxValue);
        Assert.Equal(expected, m.GetDiskPointer());
        Assert.Equal(expected, m.GetBlitterPointer(0x048));
        Assert.Equal(expected, m.GetCopperListPointer(false));
        Assert.Equal(expected, m.GetBitplanePointer(0));
        Assert.Equal(expected, m.GetSpritePointer(0));
        m.SetDiskPointerFromDma(expected + 2);
        m.SetBlitterPointerFromDma(0x048, expected + 2);
        m.SetBitplanePointerFromDma(0, expected + 2);
        m.SetSpritePointerFromDma(0, expected + 2);
        Assert.Equal(0u, m.GetDiskPointer());
        Assert.Equal(0u, m.GetBlitterPointer(0x048));
        Assert.Equal(0u, m.GetBitplanePointer(0));
        Assert.Equal(0u, m.GetSpritePointer(0));
    }

    [Fact]
    public void CopperFetchesItsListAboveOneMiB()
    {
        using var m = Machine(LightweightAgnusModel.Mos8375Pal2M, 2048);
        const uint list = 0x180000;
        m.WriteChipWordDma(list, 0x0180); m.WriteChipWordDma(list + 2, 0x0F00);
        m.WriteChipWordDma(list + 4, 0xFFFF); m.WriteChipWordDma(list + 6, 0xFFFE);
        Pointer(m, 0x080, list);
        Register(m, 0x088, 0); Register(m, 0x096, 0x8280);
        m.AdvanceHardwareTo(160);
        Assert.Equal(0x0F00, m.GetCustomRegister(0x180));
        Assert.Equal(list + 8, m.CopperProgramCounter);
    }

    [Theory]
    [InlineData(65, 2)]
    [InlineData(1, 1025)]
    [InlineData(2048, 1)]
    [InlineData(1, 32768)]
    public void ExtendedBlitCopiesBeyondTheOldWidthAndHeightLimits(int width, int height)
    {
        using var m = Machine(LightweightAgnusModel.Mos8375Pal2M, 2048);
        const uint source = 0x180000, destination = 0x190000;
        for (uint i = 0; i < width * height; i++) m.WriteChipWordDma(source + i * 2, (ushort)(i + 1));
        Register(m, 0x040, 0x09F0); Register(m, 0x044, 0xFFFF); Register(m, 0x046, 0xFFFF);
        Pointer(m, 0x050, source); Pointer(m, 0x054, destination);
        Register(m, 0x096, 0x8240);
        Register(m, 0x05C, (ushort)height);
        Assert.False(m.BlitterBusy); // Vertical size alone is not the start strobe.
        Register(m, 0x05E, (ushort)width);
        Assert.True(m.BlitterBusy);
        m.AdvanceHardwareTo(width * height * 16L + 1000);
        Assert.False(m.BlitterBusy);
        for (uint i = 0; i < width * height; i++) Assert.Equal((ushort)(i + 1), m.ReadChipWordDma(destination + i * 2));
        Assert.Equal(0, m.ReadChipWordDma(destination + (uint)(width * height * 2)));
        Assert.Equal(destination + (uint)(width * height * 2), m.GetBlitterPointer(0x054));
    }

    [Fact]
    public void EcsMintermWritePreservesTheUpperControlBitsAndOcsIgnoresTheStartRegister()
    {
        using var m = Machine(LightweightAgnusModel.Mos8372A, 1024);
        Register(m, 0x040, 0x19AA); Register(m, 0x05A, 0xFFF0);
        Assert.Equal(0x19F0, m.GetCustomRegister(0x040));
        using var ocs = new LightweightA500Machine();
        Register(ocs, 0x040, 0x19AA); Register(ocs, 0x05A, 0xFFF0);
        Register(ocs, 0x05C, 1); Register(ocs, 0x05E, 1);
        Assert.Equal(0x19AA, ocs.GetCustomRegister(0x040));
        Assert.False(ocs.BlitterBusy);
    }

    [Fact]
    public void DiskDmaWritesAnUpperChipWordAtItsExistingOutputPhase()
    {
        using var m = Machine(LightweightAgnusModel.Mos8375Pal2M, 2048);
        m.WriteChipWordDma(0, 0x9876);
        Pointer(m, 0x020, 0x180000);
        Register(m, 0x096, 0x8210);
        Register(m, 0x024, 0x8001); Register(m, 0x024, 0x8001);
        for (var i = 0; i < 16; i++) m.ReceiveDiskBit(0xABCD, false, m.Cycle);
        m.AdvanceHardwareTo(14);
        Assert.Equal(0, m.ReadChipWordDma(0x180000));
        m.AdvanceHardwareTo(16);
        Assert.Equal(0xABCD, m.ReadChipWordDma(0x180000));
        Assert.Equal(0x180002u, m.GetDiskPointer());
        Assert.Equal(0x9876, m.ReadChipWordDma(0));
    }

    [Fact]
    public void AudioDmaUsesTheUpperAddressBits()
    {
        using var m = Machine(LightweightAgnusModel.Mos8375Pal2M, 2048);
        m.WriteChipWordDma(0x180000, 0x7F7F);
        Pointer(m, 0x0A0, 0x180000);
        Register(m, 0x0A4, 1); Register(m, 0x0A6, 128); Register(m, 0x0A8, 64);
        Register(m, 0x096, 0x8201);
        m.ExecuteFrame();
        Assert.Contains(m.AudioSamples.ToArray(), sample => sample > 0);
    }

    [Fact]
    public void SpriteControlDmaFetchesAboveOneMiB()
    {
        using var m = Machine(LightweightAgnusModel.Mos8375Pal2M, 2048);
        m.WriteChipWordDma(0x180000, 0x3200); m.WriteChipWordDma(0x180002, 0x4600);
        Pointer(m, 0x120, 0x180000); Register(m, 0x096, 0x8220);
        m.AdvanceHardwareTo(25 * 454L + 0x22 * 2);
        Assert.Equal(0x3200, m.GetSpriteDmaPos(0));
        Assert.Equal(0x4600, m.GetSpriteDmaCtl(0));
        Assert.Equal(0x180004u, m.GetLiveSpritePointer(0));
    }

    [Fact]
    public void BitplaneDmaFetchesAndIncrementsAboveOneMiB()
    {
        using var m = Machine(LightweightAgnusModel.Mos8375Pal2M, 2048);
        m.WriteChipWordDma(0x180000, 0xCAFE); m.WriteChipWordDma(0x180002, 0xCAFE);
        Pointer(m, 0x0E0, 0x180000);
        Register(m, 0x08E, 0x0100); Register(m, 0x090, 0x0300);
        Register(m, 0x092, 0x0038); Register(m, 0x094, 0x0040);
        Register(m, 0x100, 0x1000); Register(m, 0x096, 0x8300);
        m.AdvanceHardwareTo(2 * 454L);
        Assert.Equal(0xCAFE, m.GetBitplaneDataLatch(0));
        Assert.Equal(0x180004u, m.GetLiveBitplanePointer(0));
    }

    [Theory]
    [InlineData(LightweightAgnusModel.Mos8371, 512, 0)]
    [InlineData(LightweightAgnusModel.Mos8372A, 1024, 0x55)]
    public void EcsCopperDangerBitAllowsRegistersBelowTheOcsLimit(LightweightAgnusModel model, int chip, int expected)
    {
        using var m = Machine(model, chip);
        m.WriteChipWordDma(0x2000, 0x0032); m.WriteChipWordDma(0x2002, 0x0055);
        m.WriteChipWordDma(0x2004, 0xFFFF); m.WriteChipWordDma(0x2006, 0xFFFE);
        Register(m, 0x02E, 2); Pointer(m, 0x080, 0x2000);
        Register(m, 0x088, 0); Register(m, 0x096, 0x8280);
        m.AdvanceHardwareTo(160);
        Assert.Equal(expected, m.GetCustomRegister(0x032));
    }

    [Theory]
    [InlineData(0x1DC, 0)]
    [InlineData(0x1E4, 0)]
    [InlineData(0x1E4, 0x2100)]
    public void EcsDisplayRegistersNoLongerReportUnsupportedModes(ushort register, ushort value)
    {
        using var m = Machine(LightweightAgnusModel.Mos8375Pal2M, 2048);
        Register(m, 0x1DC, 0x0020);
        Assert.Null(m.UnsupportedActiveFeature);
        Register(m, register, value);
        Assert.Null(m.UnsupportedActiveFeature);
    }
}
