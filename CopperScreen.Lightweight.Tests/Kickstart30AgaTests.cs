using System.Buffers.Binary;
using System.Security.Cryptography;
using Copper68k;
using CopperMod.Amiga.Lightweight;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed class Kickstart30AgaTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "copperscreen-aga-" + Guid.NewGuid().ToString("N"));
    public Kickstart30AgaTests() => Directory.CreateDirectory(_directory);
    private string Rom(ushort major = 39, ushort revision = 106)
    {
        var rom = new byte[524288]; BinaryPrimitives.WriteUInt32BigEndian(rom, 0x1F0000);
        BinaryPrimitives.WriteUInt32BigEndian(rom.AsSpan(4), 0xF80100);
        BinaryPrimitives.WriteUInt16BigEndian(rom.AsSpan(12), major); BinaryPrimitives.WriteUInt16BigEndian(rom.AsSpan(14), revision);
        rom[0x100] = 0x60; rom[0x101] = 0xFE;
        var path = Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".rom"); File.WriteAllBytes(path, rom); return path;
    }
    private CopperScreenSettingsDraft Draft() => new() { Id = "initial-aga", DisplayName = "Initial A1200",
        Chipset = new(DmaChipModel.AgaAlice, DisplayChipModel.AgaLisa, VideoStandard.Pal),
        ChipRamKb = 2048, PseudoFastRamKb = 0, RealFastRamKb = 0, CpuBackend = M68kBackendKind.AccurateM68EC020,
        KickstartSource = CopperScreenKickstartSource.KickstartRom, RomVersion = KickstartVersion.Kickstart30,
        KickstartRomPath = Rom() };
    [Fact]
    public void SyntheticA1200ProfileCanSaveLoadAndPresentItsNativeRaster()
    {
        var draft = Draft(); Assert.Null(CopperScreenAvailability.GetUnavailableReason(draft, _directory));
        var path = CopperScreenProfileStore.Save(draft, _directory);
        Assert.True(CopperScreenProfile.TryLoad(path, _directory, out var profile, out var error), error);
        Assert.Equal(draft.Chipset, profile.Chipset); Assert.Equal(KickstartVersion.Kickstart30, profile.KickstartVersion);
        using var session = new CopperScreenLightweightSession(CopperScreenSettingsDraft.FromProfile(profile).ToStartupOptions(_directory));
        session.RenderNextFrame(session.Framebuffer); Assert.True(session.Machine.IsAga);
        Assert.Equal(M68kCpuModel.M68EC020, session.Machine.CpuModel); Assert.Equal(1816, session.Width); Assert.Equal(626, session.Height);
        Assert.Null(session.FaultMessage); Assert.Equal(4, session.PresentationGeometry.HorizontalSamplesPerLowResPixel);
    }
    [Theory]
    [InlineData(40, 63)] [InlineData(40, 68)] [InlineData(39, 105)] [InlineData(34, 5)]
    public void A1200ProfileRequiresItsOwnRomRevision(ushort major, ushort revision)
    {
        var draft = Draft(); draft.KickstartRomPath = Rom(major, revision);
        Assert.Throws<NotSupportedException>(() => new CopperScreenLightweightSession(draft.ToStartupOptions(_directory)));
    }
    [Theory]
    [InlineData("denise")] [InlineData("agnus")] [InlineData("cpu")] [InlineData("chip")] [InlineData("slow")] [InlineData("fast")] [InlineData("ntsc")]
    public void A1200MilestoneRejectsUnvalidatedMixedLayouts(string mutation)
    {
        var draft = Draft();
        switch (mutation)
        {
            case "denise": draft.Chipset = draft.Chipset with { DisplayChip = DisplayChipModel.EcsDenise }; break;
            case "agnus": draft.Chipset = draft.Chipset with { DmaChip = DmaChipModel.Agnus8375Pal2M }; break;
            case "cpu": draft.CpuBackend = M68kBackendKind.AccurateM68000; break;
            case "chip": draft.ChipRamKb = 1024; break;
            case "slow": draft.PseudoFastRamKb = 512; break;
            case "fast": draft.RealFastRamKb = 2048; break;
            case "ntsc": draft.Chipset = draft.Chipset with { VideoStandard = VideoStandard.Ntsc }; break;
        }
        Assert.NotNull(CopperScreenAvailability.GetUnavailableReason(draft, _directory));
    }

    [NativeAgaFact]
    public void SuppliedNativeA1200BootsEightPlanesAndPersistsThroughDesktopReopen()
    {
        var rom = Environment.GetEnvironmentVariable("COPPERSCREEN_A1200_ROM")!;
        var original = File.ReadAllBytes(Environment.GetEnvironmentVariable("COPPERSCREEN_AGA_PROBE_HDF")!);
        static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        Assert.Equal("eb63ba9ceff1ac12bb025389434946101ae71dd064b8a830e8ae8fe111c1be39", Hash(File.ReadAllBytes(rom)));
        Assert.Equal("3bbab58c135844aad802ce937d49ec5da26d6407303c9d9fc543fc6cc63e9c85", Hash(original));
        Assert.Null(Kickstart31Tests.ReadFfsProof(original, "aga-screen-proof.txt"));
        var image = Path.Combine(_directory, "disposable.hdf");
        File.WriteAllBytes(image, original);
        var draft = Draft(); draft.KickstartRomPath = rom;
        draft.HardDrives.Add(new(0, image, false, 0));
        for (var boot = 0; boot < 2; boot++)
        {
            using (var session = new CopperScreenLightweightSession(draft.ToStartupOptions(_directory)))
            {
                for (var field = 0; field < 1200; field++) session.RenderNextFrame(session.Framebuffer);
                Assert.Null(session.FaultMessage);
                Assert.False(session.Machine.RomOverlayEnabled);
                Assert.Equal(1816, session.Machine.FramebufferWidth);
                Assert.Equal(313, session.Machine.FramebufferHeight);
                var chip = session.Machine.ChipRam.Span;
                var exec = (int)BinaryPrimitives.ReadUInt32BigEndian(chip.Slice(4));
                Assert.Equal(2097152u, BinaryPrimitives.ReadUInt32BigEndian(chip.Slice(exec + 62)));
                var pixels = session.Machine.Framebuffer.Span;
                for (var pen = 0; pen < 256; pen++)
                {
                    var x = 516 + pen % 16 * 80 + 40;
                    var y = 60 + pen / 16 * 15 + 7;
                    var colour = unchecked((int)0xFF000000) | pen << 16 | (pen * 37 & 255) << 8 | (pen * 73 & 255);
                    Assert.Equal(colour, pixels[y * 1816 + x]);
                }
            }
            Assert.Equal("Native AGA PAL 320x256 depth 8: all 256 RGB24 colours drawn\n",
                Kickstart31Tests.ReadFfsProof(File.ReadAllBytes(image), "aga-screen-proof.txt"));
        }
    }

    public sealed class NativeAgaFactAttribute : FactAttribute
    {
        public NativeAgaFactAttribute()
        {
            if (!File.Exists(Environment.GetEnvironmentVariable("COPPERSCREEN_A1200_ROM")) ||
                !File.Exists(Environment.GetEnvironmentVariable("COPPERSCREEN_AGA_PROBE_HDF")))
                Skip = "Supply COPPERSCREEN_A1200_ROM and a pristine COPPERSCREEN_AGA_PROBE_HDF from AgaDisplay/prepare_native.py for native AGA boot/reopen coverage.";
        }
    }
    public void Dispose() => Directory.Delete(_directory, true);
}
