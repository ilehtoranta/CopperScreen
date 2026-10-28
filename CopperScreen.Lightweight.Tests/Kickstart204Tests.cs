using System.Buffers.Binary;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using CopperScreen;
using Xunit.Abstractions;

namespace CopperScreen.Tests;

public sealed class Kickstart204Tests(ITestOutputHelper output) : IDisposable
{
    private readonly string _directory = CreateDirectory();

    private static string CreateDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "copperscreen-ks204-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private string Rom(int size = 524288, ushort major = 37, ushort revision = 175)
    {
        var bytes = new byte[size];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, 0xF0000);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(4), 0xF80100);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(12), major);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(14), revision);
        bytes[0x100] = 0x60; bytes[0x101] = 0xFE;
        var path = Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".rom");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static CopperScreenSettingsDraft A500Plus(string rom)
    {
        var options = CopperScreenStartupOptions.Parse(["--profile", "a500-plus-ecs-pal", "--rom", rom], AppContext.BaseDirectory);
        Assert.Null(options.Error);
        return CopperScreenSettingsDraft.FromStartupOptions(options);
    }

    [Fact]
    public void A500PlusProfileUsesExistingEcsHardwareAndKeeps204AcrossSaveAndRestart()
    {
        var draft = A500Plus(Rom());
        Assert.Equal(new AmigaChipset(DmaChipModel.Agnus8375Pal2M, DisplayChipModel.EcsDenise, VideoStandard.Pal), draft.Chipset);
        Assert.Equal(1024, draft.ChipRamKb);
        Assert.Equal(0, draft.PseudoFastRamKb);
        Assert.True(draft.RtcEnabled);
        Assert.Equal(M68kBackendKind.AccurateM68000, draft.CpuBackend);
        Assert.Equal(CopperScreenKickstartSource.KickstartRom, draft.KickstartSource);
        Assert.Equal(KickstartVersion.Kickstart20, draft.RomVersion);
        Assert.Null(CopperScreenAvailability.GetUnavailableReason(draft, _directory));
        var path = CopperScreenProfileStore.Save(draft, _directory, _directory);
        Assert.Contains("2.04", File.ReadAllText(path));
        Assert.True(CopperScreenProfile.TryLoad(path, _directory, out var profile, out var error), error);
        var restored = CopperScreenSettingsDraft.FromProfile(profile);
        Assert.Equal(draft.RomVersion, restored.RomVersion);
        Assert.Equal(draft.KickstartSource, restored.KickstartSource);
        var recent = new CopperScreenRecentSession(_directory);
        recent.Save(restored, 50, false, _directory);
        Assert.Null(recent.LastError);
        Assert.Equal(KickstartVersion.Kickstart20, recent.LoadMachine(_directory)!.RomVersion);
        using var session = new CopperScreenLightweightSession(restored.ToStartupOptions(_directory));
        Assert.False(session.Machine.IsAdfMounted);
        Assert.Equal(1048576, session.Machine.ChipRam.Length);
        session.RenderNextFrame(session.Framebuffer);
        Assert.Null(session.FaultMessage);
        session.Reset();
        Assert.Equal(0xF80100u, session.Machine.Cpu.ProgramCounter);
        var ecs = CopperScreenMachinePresets.Apply(restored, CopperScreenMachineModel.Amiga500Ecs);
        Assert.Equal(KickstartVersion.Kickstart20, ecs.RomVersion);
        Assert.Equal(CopperScreenKickstartSource.KickstartRom, ecs.KickstartSource);
    }

    [Theory]
    [InlineData(262144, 37, 175)]
    [InlineData(524288, 37, 299)]
    [InlineData(524288, 37, 300)]
    [InlineData(524288, 37, 350)]
    [InlineData(524288, 40, 63)]
    [InlineData(524288, 39, 106)]
    public void OtherRomRevisionsCannotSilentlySubstituteFor204(int size, ushort major, ushort revision)
        => Assert.Throws<NotSupportedException>(() => CopperScreenKickstartRomArchive.ReadNativeRom(
            Rom(size, major, revision), CopperScreenKickstartSource.KickstartRom, KickstartVersion.Kickstart20));

    [Fact]
    public void Explicit13And31SourcesReject204AndA1200StillRequiresItsOwnRom()
    {
        var rom = Rom();
        foreach (var source in new[] { CopperScreenKickstartSource.Kickstart13Rom, CopperScreenKickstartSource.Kickstart31Rom })
            Assert.Throws<NotSupportedException>(() => CopperScreenKickstartRomArchive.ReadNativeRom(rom, source, KickstartVersion.Kickstart20));
        Assert.Throws<NotSupportedException>(() => CopperScreenKickstartRomArchive.ReadNativeRom(rom,
            CopperScreenKickstartSource.KickstartRom, KickstartVersion.Kickstart20, a1200: true));
        var draft = A500Plus(rom);
        draft.CpuBackend = M68kBackendKind.AccurateM68020;
        draft.RealFastRamKb = 16384;
        draft.RealFastBase = CopperScreenSettingsDraft.FormatAddress(CopperScreenDefaults.GetDefaultFastRamBase(16777216));
        Assert.Contains("Kickstart 3.x", CopperScreenAvailability.GetUnavailableReason(draft, _directory));
    }

    [Fact]
    public void ZipSelectionRecognizes204AndRetains13And31Choices()
    {
        var roms = new[] { ("1.3", Rom(262144, 34, 5), KickstartVersion.Kickstart13),
            ("2.04", Rom(), KickstartVersion.Kickstart20), ("3.1", Rom(524288, 40, 63), KickstartVersion.Kickstart31) };
        var zip = Path.Combine(_directory, "roms.zip");
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            foreach (var rom in roms) archive.CreateEntryFromFile(rom.Item2, $"Kickstart-{rom.Item1}.rom");
            archive.CreateEntryFromFile(Rom(524288, 37, 350), "Kickstart-2.05.rom");
        }
        foreach (var rom in roms)
            Assert.Equal(File.ReadAllBytes(rom.Item2), CopperScreenKickstartRomArchive.ReadNativeRom(zip,
                CopperScreenKickstartSource.KickstartRom, rom.Item3));
    }

    [Native204Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Native204BootsWithEmptyDriveAndWorkbench(bool workbench)
    {
        var rom = Environment.GetEnvironmentVariable("COPPERSCREEN_KICKSTART204_ROM")!;
        var disk = Environment.GetEnvironmentVariable("COPPERSCREEN_WORKBENCH204_ADF")!;
        var draft = A500Plus(rom);
        if (workbench) draft.DriveDiskPaths[0] = disk;
        draft.DriveWriteProtected[0] = true;
        var captures = Environment.GetEnvironmentVariable("COPPERSCREEN_KICKSTART204_CAPTURE");
        using var session = new CopperScreenLightweightSession(draft.ToStartupOptions(_directory));
        Assert.Equal("d0b70e8a1772614b897f92c33cb299bed3fc8e3de488fc12f67f97fc2486eb79",
            Hash(CopperScreenKickstartRomArchive.ReadNativeRom(rom, draft.KickstartSource, draft.RomVersion)));
        if (workbench) Assert.Equal("ac6b2529b2896474401ff51c4fcf79464a5f53331b6ad872f0bb721a0a63b8ab",
            Hash(CopperScreenDiskImageArchive.LoadDiskImage(disk).Data));
        void Run(int count)
        {
            for (var i = 0; i < count; i++) session.RenderNextFrame(session.Framebuffer);
            Assert.Null(session.FaultMessage);
        }
        // Reviewed pixels of this disk's "Do you wish to continue?" keymap
        // setup prompt, excluding its blinking cursor. Synchronize on guest
        // output instead of injecting input before the native console is ready.
        string KeymapPromptHash()
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            for (var y = 252; y < 272; y++)
                hash.AppendData(MemoryMarshal.AsBytes(session.Framebuffer.AsSpan(y * session.Width + 524, 700)));
            return Convert.ToHexString(hash.GetHashAndReset());
        }
        for (var boot = 0; boot < 2; boot++)
        {
            if (boot != 0) session.Reset();
            if (workbench)
            {
                const string prompt = "5415F9158C5F65EEEF4916B60A621CD073975B68259240931C6631B8EF44062C";
                for (var field = 0; field < 10000 && KeymapPromptHash() != prompt; field += 100) Run(100);
                Assert.Equal(prompt, KeymapPromptHash());
                session.KeyDown(AmigaRawKey.Digit0); Run(4); session.KeyUp(AmigaRawKey.Digit0); Run(4);
                session.KeyDown(AmigaRawKey.Return); Run(4); session.KeyUp(AmigaRawKey.Return);
            }
            Run(2400);
            Assert.Null(session.FaultMessage);
            Assert.False(session.Machine.RomOverlayEnabled);
            var chip = session.Machine.ChipRam.Span;
            var exec = (int)BinaryPrimitives.ReadUInt32BigEndian(chip[4..]);
            Assert.InRange(exec, 0x100, chip.Length - 400);
            Assert.Equal(37, BinaryPrimitives.ReadUInt16BigEndian(chip[(exec + 20)..]));
            Assert.Equal(1048576u, BinaryPrimitives.ReadUInt32BigEndian(chip[(exec + 62)..]));
            var label = (workbench ? "workbench" : "empty-drive") + (boot == 0 ? "-cold" : "-reset");
            var receipt = new
            {
                label, romSha256 = Hash(CopperScreenKickstartRomArchive.ReadNativeRom(rom, draft.KickstartSource, draft.RomVersion)),
                diskSha256 = workbench ? Hash(CopperScreenDiskImageArchive.LoadDiskImage(disk).Data) : null,
                frameSha256 = Hash(MemoryMarshal.AsBytes(session.Machine.Framebuffer.Span)),
                pc = session.Machine.Cpu.ProgramCounter, cycle = session.Machine.Cycle,
                exec, chipBytes = chip.Length
            };
            output.WriteLine(JsonSerializer.Serialize(receipt));
            Assert.False(session.Machine.IsDriveDirty(0));
            // Reviewed complete native output: insert-disk animation or the
            // Workbench desktop with Ram Disk and Workbench2.0 icons. The
            // earlier keymap question is deliberately not a successful boot.
            Assert.Equal(workbench
                ? "3e2ad971dc013ad9260ae17849251df9e303115aa3337ffff0cdf008e5b64d75"
                : "86c3a72504cee2c5c5a621aec5d040e07402751da17612ca7a3ca6480b6ced2b", receipt.frameSha256);
            if (captures != null)
            {
                Directory.CreateDirectory(captures);
                NativeCapture.WritePng(Path.Combine(captures, label + ".png"), session.Framebuffer, session.Width, session.Height);
                File.WriteAllText(Path.Combine(captures, label + ".json"), JsonSerializer.Serialize(receipt, new JsonSerializerOptions { WriteIndented = true }));
            }
        }
    }

    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    public sealed class Native204TheoryAttribute : TheoryAttribute
    {
        public Native204TheoryAttribute()
        {
            if (!File.Exists(Environment.GetEnvironmentVariable("COPPERSCREEN_KICKSTART204_ROM")) ||
                !File.Exists(Environment.GetEnvironmentVariable("COPPERSCREEN_WORKBENCH204_ADF")))
                Skip = "Supply COPPERSCREEN_KICKSTART204_ROM and COPPERSCREEN_WORKBENCH204_ADF for native A500+ boot coverage.";
        }
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
