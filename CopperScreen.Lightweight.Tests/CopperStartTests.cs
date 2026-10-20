using System.Buffers.Binary;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using CopperMod.Amiga.Lightweight;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed class CopperStartTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "copperscreen-copperstart-" + Guid.NewGuid().ToString("N"));
    public CopperStartTests() => Directory.CreateDirectory(_directory);
    private static string Rom => Environment.GetEnvironmentVariable("COPPERSCREEN_COPPERSTART_ROM")!;
    private CopperScreenStartupOptions Options(string? rom = null, string? disk = null)
        => CopperScreenStartupOptions.Parse(new[] { "--copperstart", "--rom", rom ?? Rom }
            .Concat(disk is null ? [] : new[] { disk }).ToArray(), AppContext.BaseDirectory, userDataDirectory: _directory);

    [Fact]
    public void ExplicitBootChoiceAndPersonalSettingsRetainStandaloneRom()
    {
        var options = Options("selected.rom");
        Assert.Null(options.Error);
        Assert.Equal(CopperScreenEngine.Lightweight, options.Engine);
        Assert.Equal("lightweight-a500-copperstart", options.Profile.Id);
        Assert.True(options.Profile.UsesKickstartRom);
        var draft = CopperScreenSettingsDraft.FromStartupOptions(options);
        Assert.Null(CopperScreenAvailability.GetUnavailableReason(draft, _directory));
        draft.Id = "personal-copperstart";
        var path = CopperScreenProfileStore.Save(draft, AppContext.BaseDirectory, _directory);
        Assert.True(CopperScreenProfile.TryLoad(path, _directory, out var loaded, out var error, _directory), error);
        Assert.Equal(CopperScreenKickstartSource.CopperStart, loaded.KickstartSource);
        Assert.Equal(options.KickstartRomPath, loaded.KickstartRomPath);
        new CopperScreenRecentSession(_directory).Save(draft, 37, true, AppContext.BaseDirectory);
        var history = new CopperScreenRecentSession(_directory);
        var resumed = history.LoadMachine(AppContext.BaseDirectory)!;
        Assert.Equal(CopperScreenKickstartSource.CopperStart, resumed.KickstartSource);
        Assert.Equal(options.KickstartRomPath, resumed.KickstartRomPath);
        Assert.Equal(37, history.Volume);
        Assert.True(history.Muted);
    }

    [Theory]
    [InlineData("cpu")]
    [InlineData("chip")]
    [InlineData("slow")]
    [InlineData("fast")]
    [InlineData("drives")]
    [InlineData("rtc")]
    [InlineData("aga")]
    public void UnqualifiedHardwareIsRejectedBeforeRomLoading(string change)
    {
        var draft = CopperScreenSettingsDraft.FromStartupOptions(Options("missing.rom"));
        switch (change)
        {
            case "cpu": draft.CpuBackend = M68kBackendKind.AccurateM68010; break;
            case "chip": draft.ChipRamKb = 1024; break;
            case "slow": draft.PseudoFastRamKb = 0; break;
            case "fast": draft.RealFastRamKb = 512; break;
            case "drives": draft.FloppyDriveCount = 2; break;
            case "rtc": draft.RtcEnabled = true; break;
            case "aga": draft.Chipset = new(DmaChipModel.AgaAlice, DisplayChipModel.AgaLisa, VideoStandard.Pal); break;
        }
        Assert.Contains("CopperStart standalone ROM preset", CopperScreenAvailability.GetUnavailableReason(draft, _directory));
        Assert.Throws<NotSupportedException>(() => CopperScreenSession.Create(draft.ToStartupOptions(_directory)));
    }

    [Fact]
    public void MissingDamagedNativeAndAmbiguousRomsHaveRecoveryGuidance()
    {
        Assert.Contains(CopperScreenCopperStartRom.Version, Assert.Throws<FileNotFoundException>(() => CopperScreenCopperStartRom.Read(null)).Message);
        var path = Path.Combine(_directory, "wrong.rom");
        File.WriteAllBytes(path, new byte[20]);
        Assert.Contains("wrong size", Assert.Throws<InvalidOperationException>(() => CopperScreenCopperStartRom.Read(path)).Message);
        var native = new byte[524288]; native[13] = 40; native[15] = 63;
        File.WriteAllBytes(path, native);
        Assert.Contains("unqualified version", Assert.Throws<InvalidOperationException>(() => CopperScreenSession.Create(Options(path))).Message);
        var zip = Path.Combine(_directory, "ambiguous.zip");
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
            foreach (var name in new[] { "CopperStart.rom", "kickstart.rom" })
                using (var entry = archive.CreateEntry(name).Open()) entry.Write(native);
        Assert.Contains("exactly one", Assert.Throws<InvalidOperationException>(() => CopperScreenCopperStartRom.Read(zip)).Message);
        File.WriteAllBytes(zip, new byte[16]);
        Assert.Contains(CopperScreenCopperStartRom.Recovery, Assert.Throws<InvalidOperationException>(() => CopperScreenCopperStartRom.Read(zip)).Message);
        var legacy = CopperScreenSettingsDraft.FromStartupOptions(Options(path)); legacy.Engine = CopperScreenEngine.Legacy;
        Assert.Contains("Legacy support is not included", Assert.Throws<NotSupportedException>(() => CopperScreenSession.Create(legacy.ToStartupOptions(_directory))).Message);
    }

    [Fact]
    public void ModelSelectionDoesNotSubstituteProprietaryFirmware()
    {
        var draft = CopperScreenSettingsDraft.FromStartupOptions(Options("CopperStart.rom"));
        foreach (var model in Enum.GetValues<CopperScreenMachineModel>())
        {
            var changed = CopperScreenMachinePresets.Apply(draft, model);
            Assert.Equal(CopperScreenKickstartSource.CopperStart, changed.KickstartSource);
            Assert.Equal(draft.KickstartRomPath, changed.KickstartRomPath);
        }
        Assert.Equal(new[] { "AccurateM68000" }, CopperScreenMachinePresets.CpuChoices(draft));
        Assert.Equal(new[] { 0 }, CopperScreenMachinePresets.FastRamChoices(draft));
        draft.DriveWriteProtected[0] = false;
        Assert.Contains("read-only DF0", CopperScreenAvailability.GetUnavailableReason(draft, _directory));
    }

    private static void Run(CopperScreenLightweightSession session, int fields)
    {
        for (var i = 0; i < fields; i++) session.RenderNextFrame(session.Framebuffer);
        Assert.False(session.IsPaused);
        Assert.Null(session.FaultMessage);
        Assert.Null(session.Machine.UnsupportedActiveFeature);
    }

    private string OriginalDisk()
    {
        // Original guest program: prove real DF0 boot and hardware input, no host trap.
        var disk = new byte[901120]; "DOS\0"u8.CopyTo(disk);
        new CopperScreenBootAssembler(12).Assemble("""
            move.l #$43535452,$3000
            loop:
            move.b $bfe001,$3004
            move.w $dff00a,$3006
            move.w $dff00c,$3008
            bra loop
            """).CopyTo(disk, 12);
        uint sum = 0;
        for (var i = 0; i < 1024; i += 4)
        {
            var next = unchecked(sum + BinaryPrimitives.ReadUInt32BigEndian(disk.AsSpan(i)));
            if (next < sum) next++;
            sum = next;
        }
        BinaryPrimitives.WriteUInt32BigEndian(disk.AsSpan(4), ~sum);
        var path = Path.Combine(_directory, "original-boot-input.adf"); File.WriteAllBytes(path, disk); return path;
    }
    private static void AssertBooted(CopperScreenLightweightSession session)
        => Assert.Equal(0x43535452u, BinaryPrimitives.ReadUInt32BigEndian(session.Machine.ChipRam.Span[0x3000..]));

    [CopperStartFact]
    public void QualifiedRomColdBootResetReinsertAndRestartUseStandardSession()
    {
        var disk = OriginalDisk();
        using (var created = CopperScreenSession.Create(Options(disk: disk)))
        {
            var session = Assert.IsType<CopperScreenLightweightSession>(created);
            var resetPc = session.Machine.Cpu.ProgramCounter;
            Run(session, 600); AssertBooted(session);
            session.SetJoystickPort(1, false, false, false, false, true, false);
            session.SetMouseButtons(true, false); session.MoveMousePort(19, 7);
            Run(session, 2);
            Assert.Equal(0, session.Machine.ChipRam.Span[0x3004] & 0xC0);
            Assert.Equal(0x0713, BinaryPrimitives.ReadUInt16BigEndian(session.Machine.ChipRam.Span[0x3006..]));
            session.Reset(); Assert.Equal(resetPc, session.Machine.Cpu.ProgramCounter);
            Run(session, 600); AssertBooted(session);
            // Reset must restore neutral input and keep executing the guest's
            // input loop. A PC sample within an arbitrary byte window depends
            // on firmware/interrupt phase and is not a lifecycle contract.
            Assert.Equal(0xC0, session.Machine.ChipRam.Span[0x3004] & 0xC0);
            session.SetJoystickPort(1, false, false, false, false, true, false);
            session.SetMouseButtons(true, false); session.MoveMousePort(19, 7);
            Run(session, 2);
            Assert.Equal(0, session.Machine.ChipRam.Span[0x3004] & 0xC0);
            Assert.Equal(0x0713, BinaryPrimitives.ReadUInt16BigEndian(session.Machine.ChipRam.Span[0x3006..]));
            session.KeyDown(AmigaRawKey.Control); session.KeyDown(AmigaRawKey.LeftAmiga); session.KeyDown(AmigaRawKey.RightAmiga);
            Assert.Equal(resetPc, session.Machine.Cpu.ProgramCounter);
            session.KeyUp(AmigaRawKey.RightAmiga); session.KeyUp(AmigaRawKey.LeftAmiga); session.KeyUp(AmigaRawKey.Control);
            Run(session, 600); AssertBooted(session);
            session.TogglePaused();
            using var replacement = new CopperScreenLightweightSession(Options(disk: disk), session);
            Run(replacement, 600); AssertBooted(replacement);
        }
        // Stop/dispose then cold restart, and insertion while waiting with no disk.
        using var restarted = Assert.IsType<CopperScreenLightweightSession>(CopperScreenSession.Create(Options()));
        Run(restarted, 300);
        Assert.NotEqual(0x43535452u, BinaryPrimitives.ReadUInt32BigEndian(restarted.Machine.ChipRam.Span[0x3000..]));
        Assert.True(restarted.InsertLoadedDisk(disk, CopperScreenDiskImageArchive.LoadDiskImage(disk), true));
        Run(restarted, 600); AssertBooted(restarted);
        var zip = Path.Combine(_directory, "qualified.zip");
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create)) archive.CreateEntryFromFile(Rom, "CopperStart.rom");
        Assert.Equal(File.ReadAllBytes(Rom), CopperScreenCopperStartRom.Read(zip));
    }

    [CopperStartReplayFact]
    public void LotusOpeningRaceThroughAppSessionMatchesScalarGuestExecution()
    {
        var disk = Environment.GetEnvironmentVariable("COPPERSCREEN_COPPERSTART_LOTUS")!;
        var script = Path.Combine(AppContext.BaseDirectory, "Workloads", "corpus-lotus2.json");
        Assert.Equal("b54d47135be66b7949a3c5fc10daad342801789570ce27799c6ed7cb15768fd3", Hash(script));
        Assert.Equal("82DC1FAF56823653CD3B0B299E179932C1FAE117B03CAE220E121D96AE8B3A06",
            Convert.ToHexString(SHA256.HashData(CopperScreenDiskImageArchive.LoadDiskImage(disk).Data)));
        using var json = JsonDocument.Parse(File.ReadAllText(script));
        var events = json.RootElement.EnumerateArray().ToArray();
        using var created = CopperScreenSession.Create(Options(disk: disk));
        var session = Assert.IsType<CopperScreenLightweightSession>(created);
        using var scalar = (LightweightA500Machine)Activator.CreateInstance(typeof(LightweightA500Machine),
            BindingFlags.Instance | BindingFlags.NonPublic, null,
            new object[] { new LightweightA500Configuration { ChipRamBytes = 524288, SlowRamBytes = 524288, FramebufferWidth = 908 }, false }, null)!;
        scalar.LoadKickstart(CopperScreenCopperStartRom.Read(Rom));
        CopperScreenDiskImageArchive.LoadDiskImage(disk).Mount(scalar, 0);
        scalar.SetDriveWriteProtected(0, true);
        var output = Environment.GetEnvironmentVariable("COPPERSCREEN_COPPERSTART_OUTPUT") ?? Path.Combine(_directory, "replay");
        Directory.CreateDirectory(output);
        var snapshots = new List<object>();
        var pcm = new float[4096]; var convertedPcm = new short[4096]; var next = 0; var audible = 0;
        var fields = new[] { 1, 3000, 6000, 9000, 10000, 11000, 12000, 12800, 14000 };
        for (var frame = 0; frame < 14000; frame++)
        {
            // The script's empty events release previously held joystick input.
            if (next < events.Length && events[next].GetProperty("frame").GetInt32() == frame)
            {
                var e = events[next++];
                var joy = e.TryGetProperty("joystickPort1", out var value) ? value.GetInt32() : 0;
                session.SetJoystickPort(1, (joy & 1) != 0, (joy & 2) != 0, (joy & 4) != 0, (joy & 8) != 0, (joy & 16) != 0, (joy & 32) != 0);
                scalar.SubmitInput(new(0, (byte)joy, 0, 0, 0, 0));
            }
            session.RenderNextFrame(session.Framebuffer); scalar.ExecuteFrame();
            Assert.False(session.IsPaused); Assert.Null(session.FaultMessage); Assert.Null(scalar.UnsupportedActiveFeature);
            var count = session.RenderAudio(pcm, 48000, 2) * 2;
            Assert.Equal(scalar.AudioSamples.Length, count);
            for (var i = 0; i < count; i++) convertedPcm[i] = (short)(pcm[i] * 32768);
            Assert.True(scalar.AudioSamples.Span.SequenceEqual(convertedPcm.AsSpan(0, count)), $"PCM divergence at field {frame + 1}");
            if (pcm.AsSpan(0, count).ContainsAnyExcept(0f)) audible++;
            if (!fields.Contains(frame + 1)) continue;
            Assert.Equal(scalar.Cycle, session.Machine.Cycle);
            Assert.Equal(scalar.Cpu.ProgramCounter, session.Machine.Cpu.ProgramCounter);
            Assert.Equal(scalar.Cpu.D, session.Machine.Cpu.D); Assert.Equal(scalar.Cpu.A, session.Machine.Cpu.A);
            Assert.Equal(scalar.ChipRam.ToArray(), session.Machine.ChipRam.ToArray());
            for (var y = 0; y < scalar.FramebufferHeight; y++)
            {
                Assert.True(scalar.Framebuffer.Span.Slice(y * 908, 908).SequenceEqual(session.Framebuffer.AsSpan(y * 1816, 908)));
                Assert.True(scalar.Framebuffer.Span.Slice(y * 908, 908).SequenceEqual(session.Framebuffer.AsSpan(y * 1816 + 908, 908)));
            }
            var image = Path.Combine(output, $"field-{frame + 1:D5}.png");
            NativeCapture.WritePng(image, session.Framebuffer, session.Width, session.Height);
            snapshots.Add(new { field = frame + 1, session.Width, session.Height, cycle = session.Machine.Cycle,
                pc = session.Machine.Cpu.ProgramCounter.ToString("X8"), pixelsSha256 = Hash(image),
                chipSha256 = Convert.ToHexString(SHA256.HashData(session.Machine.ChipRam.Span)), scalarMatches = true });
        }
        Assert.Equal(events.Length, next); Assert.True(audible > 0);
        File.WriteAllText(Path.Combine(output, "app-session.json"), JsonSerializer.Serialize(new
        {
            protocol = "copperstart-app-session-v1", classification = "unreviewed-captures", firmware = CopperScreenCopperStartRom.Version,
            romSha256 = Hash(Rom), disk, mediaSha256 = Convert.ToHexString(SHA256.HashData(CopperScreenDiskImageArchive.LoadDiskImage(disk).Data)),
            inputSha256 = Hash(script), fields = 14000, audibleFields = audible, snapshots,
            binaries = new[] { typeof(CopperScreenSession).Assembly.Location, typeof(LightweightA500Machine).Assembly.Location, typeof(Copper68k.M68kCpuModel).Assembly.Location }
                .Select(path => new { name = Path.GetFileName(path), sha256 = Hash(path) })
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    public void Dispose() => Directory.Delete(_directory, true);
}

public class CopperStartFactAttribute : FactAttribute
{
    public CopperStartFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COPPERSCREEN_COPPERSTART_ROM")))
            Skip = "Qualified standalone CopperStart ROM not supplied; native boot coverage unavailable.";
    }
}
public sealed class CopperStartReplayFactAttribute : CopperStartFactAttribute
{
    public CopperStartReplayFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COPPERSCREEN_COPPERSTART_LOTUS")))
            Skip = "Lotus 2 disk not supplied; opening-race coverage unavailable.";
    }
}
