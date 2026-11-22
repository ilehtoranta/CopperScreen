using System.Buffers.Binary;
using System.Security.Cryptography;
using Copper68k;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed class CopperStartMiniRomTests
{
    // Boot-screen receipt and state values from the pinned Mini ROM.
    private const int ScreenStateAddress = 0x740 + 12;
    private const uint Splash = 6, WaitingForDisk = 1;

    [Fact]
    public void HistoricalMiniSwitchSelectsTheBundledRom()
    {
        var options = CopperScreenStartupOptions.Parse(["--minimal-disk-boot"], AppContext.BaseDirectory);
        Assert.Null(options.Error);
        Assert.Equal(CopperScreenKickstartSource.MinimalDiskBoot, options.Profile.KickstartSource);
        Assert.Equal(KickstartVersion.CopperStartMini, options.Profile.KickstartVersion);
        Assert.Null(options.KickstartRomPath);
        Assert.Equal(CopperScreenBundledMiniRom.Included ? null : CopperScreenBundledMiniRom.UnavailableMessage,
            CopperScreenLightweightSession.GetValidationError(options));
    }

    [BundledMiniTheory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void BundledRomMatchesTheExternalSplashAndBootsAnInsertedDisk(int model)
    {
        var directory = Directory.CreateTempSubdirectory("copperscreen-mini-bundle-");
        try
        {
            var rom = CopperScreenBundledMiniRom.Read();
            Assert.Equal("3060AB0DDF37EE264C3FD804EC6E8A1A825784B55DAE92DAB0B49CDC7BE24DD4",
                Convert.ToHexString(SHA256.HashData(rom)));
            var path = Path.Combine(directory.FullName, "CopperStart-Mini.rom");
            File.WriteAllBytes(path, rom);

            var draft = CopperScreenLaunchWorkflow.SelectStartChoice(new(), 6 + model);
            var bundledOptions = draft.ToStartupOptions(AppContext.BaseDirectory);
            Assert.Null(bundledOptions.KickstartRomPath);
            draft.KickstartSource = CopperScreenKickstartSource.KickstartRom;
            draft.KickstartRomPath = path;
            draft.RomVersion = KickstartVersion.CopperStartMini;
            using var bundled = new CopperScreenLightweightSession(bundledOptions);
            using var external = new CopperScreenLightweightSession(draft.ToStartupOptions(AppContext.BaseDirectory));
            var splashFrames = RunToScreen(bundled, Splash);
            Run(external, splashFrames);
            Assert.True(bundled.Framebuffer.AsSpan().SequenceEqual(external.Framebuffer), "Splash differs from the external ROM.");
            Assert.True(bundled.Framebuffer.Distinct().Count() > 2, "The ROM splash did not render.");
            var splash = bundled.Framebuffer.ToArray();

            var promptFrames = RunToScreen(bundled, WaitingForDisk);
            Run(external, promptFrames);
            Assert.True(bundled.Framebuffer.AsSpan().SequenceEqual(external.Framebuffer), "Insert-disk screen differs from the external ROM.");
            Assert.True(bundled.Framebuffer.Distinct().Count() > 2, "The insert-disk screen did not render.");
            Assert.False(bundled.Framebuffer.AsSpan().SequenceEqual(splash), "The insert-disk screen did not replace the splash.");

            var diskPath = Path.Combine(directory.FullName, "takeover.adf");
            File.WriteAllBytes(diskPath, TakeoverDisk());
            Assert.True(bundled.InsertLoadedDisk(0, diskPath, CopperScreenDiskImageArchive.LoadDiskImage(diskPath), markChanged: true));
            RunUntil(bundled, 0x800, 0x12345678u);

            bundled.Reset();
            Assert.True(bundled.Machine.IsDriveMounted(0));
            RunToScreen(bundled, Splash);
            Assert.True(bundled.Framebuffer.Distinct().Count() > 2, "A mounted disk suppressed the splash.");
            RunUntil(bundled, 0x800, 0x12345678u);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [BundledMiniTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void A1200With68060BootsAFloppyAndBootsItAgainAfterReset(bool diskPresentAtColdStart)
    {
        var directory = Directory.CreateTempSubdirectory("copperscreen-mini-68060-");
        try
        {
            var diskPath = Path.Combine(directory.FullName, "takeover.adf");
            File.WriteAllBytes(diskPath, TakeoverDisk());
            var draft = CopperScreenLaunchWorkflow.SelectStartChoice(new(), 8);
            CopperScreenMachinePresets.SetCpu(draft, M68kBackendKind.AccurateM68060);
            if (diskPresentAtColdStart) draft.DriveDiskPaths[0] = diskPath;
            using var session = new CopperScreenLightweightSession(draft.ToStartupOptions(directory.FullName));
            Assert.Equal(M68kCpuModel.M68060, session.Machine.CpuModel);
            RunToScreen(session, Splash);
            Assert.True(session.Framebuffer.Distinct().Count() > 2, "The ROM splash did not render.");
            if (!diskPresentAtColdStart)
            {
                RunToScreen(session, WaitingForDisk);
                Assert.True(session.Framebuffer.Distinct().Count() > 2, "The insert-disk screen did not render.");
                Assert.True(session.InsertLoadedDisk(0, diskPath, CopperScreenDiskImageArchive.LoadDiskImage(diskPath), markChanged: true));
            }
            RunUntil(session, 0x800, 0x12345678u);

            // Chip RAM survives reset. Require the boot block to write a new marker.
            var cycle = session.Machine.Cycle;
            session.Machine.WriteLong(0x800, 0, ref cycle, M68kBusAccessKind.CpuDataWrite);
            Assert.Equal(0u, BinaryPrimitives.ReadUInt32BigEndian(session.Machine.ChipRam.Span[0x800..]));
            session.Reset();
            Assert.True(session.Machine.IsDriveMounted(0));
            RunToScreen(session, Splash);
            RunUntil(session, 0x800, 0x12345678u);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static int RunToScreen(CopperScreenLightweightSession session, uint state)
    {
        var frames = RunUntil(session, ScreenStateAddress, state);
        Run(session, 2); // Allow the guest's Copper list to finish presenting a complete field.
        return frames + 2;
    }

    private static byte[] TakeoverDisk()
    {
        var disk = new byte[901120];
        "DOS\0"u8.CopyTo(disk);
        new CopperScreenBootAssembler(12).Assemble("move.l #$12345678,$800\nloop:\nbra loop").CopyTo(disk, 12);
        uint sum = 0;
        for (var i = 0; i < 1024; i += 4)
        {
            var next = unchecked(sum + BinaryPrimitives.ReadUInt32BigEndian(disk.AsSpan(i, 4)));
            sum = next < sum ? next + 1 : next;
        }
        BinaryPrimitives.WriteUInt32BigEndian(disk.AsSpan(4), ~sum);
        return disk;
    }

    private static int RunUntil(CopperScreenLightweightSession session, int address, uint value)
    {
        const int frameLimit = 300;
        for (var frame = 0; frame < frameLimit; frame++)
        {
            if (BinaryPrimitives.ReadUInt32BigEndian(session.Machine.ChipRam.Span[address..]) == value) return frame;
            Run(session, 1);
        }
        throw new InvalidOperationException($"The Mini did not reach {value:X8} at ${address:X} within {frameLimit} fields. PC={session.Machine.Cpu.ProgramCounter:X8}");
    }

    private static void Run(CopperScreenLightweightSession session, int frameCount)
    {
        for (var frame = 0; frame < frameCount; frame++) session.RenderNextFrame(session.Framebuffer);
        Assert.Null(session.FaultMessage);
    }

    [Theory]
    [InlineData((int)CopperScreenMachineModel.Amiga500, 512)]
    [InlineData((int)CopperScreenMachineModel.Amiga500Ecs, 1024)]
    [InlineData((int)CopperScreenMachineModel.Amiga1200, 2048)]
    public void ExternalRomSelectionSurvivesSaveAndReset(int model, int chipKiB)
    {
        var directory = Directory.CreateTempSubdirectory("copperscreen-mini-");
        try
        {
            var rom = new byte[524288];
            BinaryPrimitives.WriteUInt32BigEndian(rom, 0x8000);
            BinaryPrimitives.WriteUInt32BigEndian(rom.AsSpan(4), 0xF80010);
            "CSMI\0\u0001\0\0"u8.CopyTo(rom.AsSpan(8));
            BinaryPrimitives.WriteUInt16BigEndian(rom.AsSpan(16), 0x60FE);
            var path = Path.Combine(directory.FullName, "CopperStart-Mini.rom");
            File.WriteAllBytes(path, rom);

            var draft = CopperScreenMachinePresets.Apply(new CopperScreenSettingsDraft(), (CopperScreenMachineModel)model);
            draft.KickstartRomPath = path;
            draft.RomVersion = CopperScreenKickstartRomArchive.IdentifyNativeVersion(rom);
            Assert.Equal(KickstartVersion.CopperStartMini, draft.RomVersion);
            var saved = CopperScreenProfileStore.Save(draft, directory.FullName, directory.FullName);
            Assert.True(CopperScreenProfile.TryLoad(saved, directory.FullName, out var profile, out var error), error);
            var restored = CopperScreenSettingsDraft.FromProfile(profile);
            Assert.Equal(KickstartVersion.CopperStartMini, restored.RomVersion);
            Assert.Equal(CopperScreenKickstartSource.KickstartRom, restored.KickstartSource);

            using var session = new CopperScreenLightweightSession(restored.ToStartupOptions(directory.FullName));
            Assert.Equal(chipKiB * 1024, session.Machine.ChipRam.Length);
            session.RenderNextFrame(session.Framebuffer);
            Assert.Null(session.FaultMessage);
            session.Reset();
            Assert.Equal(0xF80010u, session.Machine.Cpu.ProgramCounter);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}

public sealed class BundledMiniTheoryAttribute : TheoryAttribute
{
    public BundledMiniTheoryAttribute()
    {
        if (!CopperScreenBundledMiniRom.Included)
            Skip = "Stage the qualified CopperStart Mini ROM to exercise the bundled boot path.";
    }
}
