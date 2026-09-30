using System.Buffers.Binary;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed class Kickstart31Tests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "copperscreen-ks31-" + Guid.NewGuid().ToString("N"));

    public Kickstart31Tests() => Directory.CreateDirectory(_directory);

    private string Rom(int size = 524288, ushort major = 40, ushort revision = 63)
    {
        var bytes = new byte[size];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, 0x70000);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(4), size == 524288 ? 0xF80100u : 0xFC0100u);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(12), major);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(14), revision);
        bytes[0x100] = 0x60; bytes[0x101] = 0xFE;
        var path = Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".rom");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [Theory]
    [InlineData("Kickstart31Rom", 0)]
    [InlineData("Kickstart31Rom", 2048)]
    [InlineData("KickstartRom", 0)]
    public void Synthetic31SessionAndSavedProfileKeepTheSelectedVersion(string source, int fastKiB)
    {
        var draft = new CopperScreenSettingsDraft
        {
            Id = "native-31", DisplayName = "Native 3.1",
            KickstartSource = Enum.Parse<CopperScreenKickstartSource>(source),
            RomVersion = KickstartVersion.Kickstart31, KickstartRomPath = Rom(), RealFastRamKb = fastKiB
        };
        Assert.Null(CopperScreenAvailability.GetUnavailableReason(draft, _directory));
        var saved = CopperScreenProfileStore.Save(draft, _directory);
        Assert.True(CopperScreenProfile.TryLoad(saved, _directory, out var profile, out var error), error);
        Assert.Equal(KickstartVersion.Kickstart31, profile.KickstartVersion);
        var loaded = CopperScreenSettingsDraft.FromProfile(profile);
        Assert.Equal(draft.KickstartSource, loaded.KickstartSource);
        using var session = new CopperScreenLightweightSession(loaded.ToStartupOptions(_directory));
        Assert.Equal(0xF80100u, session.Machine.Cpu.ProgramCounter);
        session.RenderNextFrame(session.Framebuffer);
        Assert.Null(session.Machine.UnsupportedActiveFeature);
        Assert.Equal(fastKiB * 1024, session.Machine.FastRam.Length);
        session.Reset();
        Assert.Equal(0xF80100u, session.Machine.Cpu.ProgramCounter);
    }

    [Fact]
    public void Explicit31ProfileWorksFromTheCommandLine()
    {
        var options = CopperScreenStartupOptions.Parse(["--profile", "lightweight-a500-kickstart31", "--rom", Rom()], AppContext.BaseDirectory);
        Assert.Null(options.Error);
        Assert.Equal(KickstartVersion.Kickstart31, options.Profile.KickstartVersion);
        Assert.Equal(CopperScreenKickstartSource.Kickstart31Rom, options.Profile.KickstartSource);
        using var session = new CopperScreenLightweightSession(options);
        Assert.Equal(0xF80100u, session.Machine.Cpu.ProgramCounter);
    }

    [Theory]
    [InlineData(262144, 40, 63)]
    [InlineData(524288, 39, 106)]
    [InlineData(524288, 40, 68)]
    [InlineData(524288, 40, 70)]
    [InlineData(524288, 34, 5)]
    public void UnsupportedRomShapesAndOtherMachineRevisionsAreRejected(int size, ushort major, ushort revision)
    {
        var path = Rom(size, major, revision);
        Assert.Throws<NotSupportedException>(() => CopperScreenKickstartRomArchive.ReadNativeRom(path, CopperScreenKickstartSource.Kickstart31Rom, KickstartVersion.Kickstart31));
    }

    [Fact]
    public void NeitherTheSelectedVersionNorAnExplicit13SourceCanSilentlyUse31()
    {
        var path = Rom();
        Assert.Throws<NotSupportedException>(() => CopperScreenKickstartRomArchive.ReadNativeRom(path, CopperScreenKickstartSource.KickstartRom, KickstartVersion.Kickstart13));
        Assert.Throws<NotSupportedException>(() => CopperScreenKickstartRomArchive.ReadNativeRom(path, CopperScreenKickstartSource.Kickstart13Rom, KickstartVersion.Kickstart31));
        var draft = new CopperScreenSettingsDraft { KickstartSource = CopperScreenKickstartSource.Kickstart31Rom };
        Assert.NotNull(CopperScreenAvailability.GetUnavailableReason(draft, _directory));
    }

    [Fact]
    public void ZipSelectionUsesTheDeclaredVersionAndValidatesTheSelectedBytes()
    {
        var native31 = File.ReadAllBytes(Rom());
        var native13 = File.ReadAllBytes(Rom(262144, 34, 5));
        var zip = Path.Combine(_directory, "kickstarts.zip");
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            using (var entry = archive.CreateEntry("kickstart-1.3.rom").Open()) entry.Write(native13);
            using (var entry = archive.CreateEntry("nested/kickstart-3.1-a500.rom").Open()) entry.Write(native31);
        }
        Assert.Equal(native31, CopperScreenKickstartRomArchive.ReadNativeRom(zip, CopperScreenKickstartSource.Kickstart31Rom, KickstartVersion.Kickstart31));
        Assert.Equal(native13, CopperScreenKickstartRomArchive.ReadNativeRom(zip, CopperScreenKickstartSource.Kickstart13Rom, KickstartVersion.Kickstart13));
        Assert.Equal(KickstartVersion.Kickstart31, CopperScreenKickstartRomArchive.IdentifyNativeVersion(native31));
    }

    [NativeKickstart31Theory]
    [InlineData(0, 1420928790L)]
    [InlineData(2048, 1420928894L)]
    public void SuppliedNativeWorkbench31BootsThroughTheDesktopSession(int fastKiB, long expectedCycle)
    {
        var rom = Environment.GetEnvironmentVariable("COPPERSCREEN_KICKSTART31_ROM")!;
        var disk = Environment.GetEnvironmentVariable("COPPERSCREEN_WORKBENCH31_ADF")!;
        Assert.Equal("8c8a0cf04f91b88eaf0c4f1126041987067e2286a8ee590bdbae447a8000c5ee", Hash(File.ReadAllBytes(rom)));
        Assert.Equal("a8f167bad2897e8c7f2cfe73de7a9f8dad10c7a5b1f78ca17f818ab17278b985", Hash(File.ReadAllBytes(disk)));
        var draft = new CopperScreenSettingsDraft
        {
            KickstartSource = CopperScreenKickstartSource.Kickstart31Rom,
            RomVersion = KickstartVersion.Kickstart31, KickstartRomPath = rom, RealFastRamKb = fastKiB
        };
        draft.DriveDiskPaths[0] = disk;
        using var session = new CopperScreenLightweightSession(draft.ToStartupOptions(_directory));
        for (var field = 0; field < 10000; field++) session.RenderNextFrame(session.Framebuffer);
        Assert.Null(session.FaultMessage);
        Assert.False(session.Machine.RomOverlayEnabled);
        Assert.Equal(expectedCycle, session.Machine.Cycle);
        Assert.Equal(0xF81476u, session.Machine.Cpu.ProgramCounter);
        Assert.Equal(fastKiB == 0 ? (uint?)null : 0x200000u, session.Machine.FastRamBase);
        // Reviewed native Workbench desktop, little-endian ARGB pixels from the full beam.
        Assert.Equal("aae907938c788bd6f3f29521d2f3fffccc5ee4db0acc0634b19bc024080dd84f",
            Hash(MemoryMarshal.AsBytes(session.Machine.Framebuffer.Span)));
    }

    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    [NativeKickstart31HdTheory]
    [InlineData(0, "OcsAgnus", 512, 512)]
    [InlineData(0, "OcsAgnus", 512, 0)]
    [InlineData(2048, "OcsAgnus", 512, 512)]
    [InlineData(0, "Agnus8372A", 512, 512)]
    [InlineData(0, "Agnus8372A", 1024, 0)]
    [InlineData(0, "Agnus8375Pal2M", 1024, 0)]
    [InlineData(0, "Agnus8375Pal2M", 2048, 0)]
    public void SuppliedNativeWorkbench31HardDiskBootsAndPersistsAcrossDesktopSessions(int fastKiB, string agnus, int chipKiB, int slowKiB)
    {
        var rom = Environment.GetEnvironmentVariable("COPPERSCREEN_KICKSTART31_ROM")!;
        var original = File.ReadAllBytes(Environment.GetEnvironmentVariable("COPPERSCREEN_WORKBENCH31_HDF")!);
        Assert.Equal("8c8a0cf04f91b88eaf0c4f1126041987067e2286a8ee590bdbae447a8000c5ee", Hash(File.ReadAllBytes(rom)));
        Assert.Equal("19a78c839a20a7498df33154c3697237259b195a07f6e29059f10745c7947e9d", Hash(original));
        Assert.Null(ReadFfsProof(original, "ks31-hd-proof.txt"));
        var image = Path.Combine(_directory, "disposable.hdf");
        File.WriteAllBytes(image, original);
        var draft = new CopperScreenSettingsDraft
        {
            KickstartSource = CopperScreenKickstartSource.Kickstart31Rom,
            RomVersion = KickstartVersion.Kickstart31, KickstartRomPath = rom, RealFastRamKb = fastKiB,
            Chipset = new(Enum.Parse<DmaChipModel>(agnus), DisplayChipModel.OcsDenise, VideoStandard.Pal),
            ChipRamKb = chipKiB, PseudoFastRamKb = slowKiB
        };
        draft.HardDrives.Add(new(0, image, false, 0));
        for (var boot = 0; boot < 2; boot++)
        {
            using (var session = new CopperScreenLightweightSession(draft.ToStartupOptions(_directory)))
            {
                Assert.False(session.Machine.IsAdfMounted);
                for (var field = 0; field < 2400; field++) session.RenderNextFrame(session.Framebuffer);
                Assert.Null(session.FaultMessage);
                Assert.False(session.Machine.RomOverlayEnabled);
                if (chipKiB > 512)
                {
                    // Native exec.library's MaxLocMem and MEMF_CHIP header,
                    // from the supplied ROM's own startup memory probes.
                    var chip = session.Machine.ChipRam.Span;
                    var exec = (int)BinaryPrimitives.ReadUInt32BigEndian(chip.Slice(4));
                    Assert.Equal((uint)(chipKiB * 1024), BinaryPrimitives.ReadUInt32BigEndian(chip.Slice(exec + 62)));
                    var header = (int)BinaryPrimitives.ReadUInt32BigEndian(chip.Slice(exec + 322));
                    Assert.Equal(2, BinaryPrimitives.ReadUInt16BigEndian(chip.Slice(header + 14)) & 2);
                    Assert.Equal((uint)(chipKiB * 1024), BinaryPrimitives.ReadUInt32BigEndian(chip.Slice(header + 24)));
                }
                // Reviewed Workbench desktop, after the hard disk's own startup script.
                Assert.Equal("6d9d59261ac424a3461a96ef8e518128b5cfbbcea11e5cd69bbc61798f7a30d1",
                    Hash(MemoryMarshal.AsBytes(session.Machine.Framebuffer.Span)));
            }
            var persisted = File.ReadAllBytes(image);
            Assert.Equal("CopperHDF Kickstart 3.1 native FFS persistence\n", ReadFfsProof(persisted, "ks31-hd-proof.txt"));
            var reopened = ReadFfsProof(persisted, "ks31-hd-reopen.txt");
            if (boot == 0) Assert.Null(reopened);
            else Assert.Equal("CopperHDF Kickstart 3.1 FFS reopen verified\n", reopened);
        }
    }

    // Independent, bounded reader for this fixture's single-block root proof files.
    private static string? ReadFfsProof(byte[] image, string name)
    {
        const int origin = 32 * 512, root = 880, count = 1760;
        Assert.True(image.AsSpan(origin, 4).SequenceEqual("DOS\u0001"u8));
        uint Read(int block, int offset) => BinaryPrimitives.ReadUInt32BigEndian(image.AsSpan(origin + block * 512 + offset));
        void Check(int block)
        {
            Assert.InRange(block, 1, count - 1);
            uint sum = 0;
            for (var offset = 0; offset < 512; offset += 4) sum = unchecked(sum + Read(block, offset));
            Assert.Equal(0u, sum);
        }
        Check(root);
        var seen = new HashSet<int>();
        for (var offset = 24; offset < 312; offset += 4)
            for (var block = (int)Read(root, offset); block != 0; block = (int)Read(block, 496))
            {
                Assert.True(seen.Add(block), "Cyclic FFS directory chain");
                Check(block);
                var length = image[origin + block * 512 + 432];
                if (System.Text.Encoding.ASCII.GetString(image, origin + block * 512 + 433, length) != name) continue;
                Assert.Equal(0xfffffffdu, Read(block, 508));
                Assert.Equal((uint)root, Read(block, 500));
                Assert.Equal(1u, Read(block, 8));
                Assert.Equal(0u, Read(block, 504));
                var data = (int)Read(block, 308);
                var size = (int)Read(block, 324);
                Assert.InRange(data, 1, count - 1);
                Assert.InRange(size, 1, 512);
                return System.Text.Encoding.ASCII.GetString(image, origin + data * 512, size);
            }
        return null;
    }

    public sealed class NativeKickstart31HdTheoryAttribute : TheoryAttribute
    {
        public NativeKickstart31HdTheoryAttribute()
        {
            if (!File.Exists(Environment.GetEnvironmentVariable("COPPERSCREEN_KICKSTART31_ROM")) ||
                !File.Exists(Environment.GetEnvironmentVariable("COPPERSCREEN_WORKBENCH31_HDF")))
                Skip = "Supply COPPERSCREEN_KICKSTART31_ROM and a pristine COPPERSCREEN_WORKBENCH31_HDF from prepare_hdf.py for native HD boot/reopen coverage.";
        }
    }

    public sealed class NativeKickstart31TheoryAttribute : TheoryAttribute
    {
        public NativeKickstart31TheoryAttribute()
        {
            if (!File.Exists(Environment.GetEnvironmentVariable("COPPERSCREEN_KICKSTART31_ROM")) ||
                !File.Exists(Environment.GetEnvironmentVariable("COPPERSCREEN_WORKBENCH31_ADF")))
                Skip = "Supply COPPERSCREEN_KICKSTART31_ROM and COPPERSCREEN_WORKBENCH31_ADF for the pinned native boot replay; unavailable media is not coverage.";
        }
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
