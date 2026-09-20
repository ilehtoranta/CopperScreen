using System.Buffers.Binary;
using CopperMod.Amiga.Lightweight;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed class CopperScreenLightweightSessionTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "copperscreen-lightweight-" + Guid.NewGuid().ToString("N"));
    private readonly string _rom;

    public CopperScreenLightweightSessionTests()
    {
        Directory.CreateDirectory(_directory);
        _rom = Path.Combine(_directory, "fixture.rom");
        var bytes = new byte[262144];
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(0), 0x70000);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(4), 0xFC0100);
        bytes[13] = 34; // Synthetic v34-tagged ROM loop, not native boot evidence.
        bytes[0x100] = 0x60; bytes[0x101] = 0xFE;
        File.WriteAllBytes(_rom, bytes);
    }

    private CopperScreenStartupOptions Options(params string[] extra)
        => CopperScreenStartupOptions.Parse(
            new[] { "--profile", "lightweight-a500-kickstart13", "--engine", "Lightweight", "--rom", _rom }.Concat(extra).ToArray(),
            AppContext.BaseDirectory);

    [Fact]
    public async Task SelectedZipIpfSupportsEveryDriveAndRejectsSaveAndWriteEnable()
    {
        var draft = CopperScreenSettingsDraft.FromStartupOptions(Options()); draft.FloppyDriveCount = 4;
        var path = Path.Combine(_directory, "protected.zip");
        var bytes = CopperMod.Amiga.Lightweight.Tests.IpfFixture.Create(new CopperMod.Amiga.Lightweight.Tests.IpfFixture.Track(100003, new byte[12501]));
        using (var archive = System.IO.Compression.ZipFile.Open(path, System.IO.Compression.ZipArchiveMode.Create))
        {
            using (var stream = archive.CreateEntry("disk.ipf").Open()) stream.Write(bytes);
            using (var stream = archive.CreateEntry("second.adf").Open()) stream.Write(new byte[901120]);
        }
        var original = File.ReadAllBytes(path);
        var session = new CopperScreenLightweightSession(draft.ToStartupOptions(_directory)); session.TogglePaused();
        using var runtime = CopperScreenRuntime.CreateForTests(session); runtime.Start();
        for (var i = 0; i < 4; i++)
        {
            var inserted = await runtime.InsertDriveDiskAsync(i, path + "#/disk.ipf", false);
            Assert.True(inserted.Success, inserted.Message);
            Assert.Equal(LightweightFloppyFormat.Ipf, inserted.State.Drives[i].Format);
            Assert.True(inserted.State.Drives[i].WriteProtected);
            Assert.False(inserted.State.Drives[i].CanExportAdf);
            Assert.False((await runtime.SetDriveWriteProtectedAsync(i, false)).Success);
            Assert.False((await runtime.SaveAdfAsync(i, Path.Combine(_directory, $"invalid{i}.adf"))).Success);
        }
        var invalid = Path.Combine(_directory, "bad.ipf"); File.WriteAllBytes(invalid, new byte[8]);
        var failed = await runtime.InsertDriveDiskAsync(0, invalid, false);
        Assert.False(failed.Success); Assert.Equal(LightweightFloppyFormat.Ipf, failed.State.Drives[0].Format);
        Assert.Equal(original, File.ReadAllBytes(path));
    }

    [Fact]
    public async Task DirtyAdfSurvivesFailedSaveResetAndRejectedReplacementThenSavesAtomically()
    {
        var session = new CopperScreenLightweightSession(Options());
        var bytes = new byte[901120];
        Assert.True(session.InsertLoadedDisk("original.zip", CopperScreenAdfImage.FromAdfBytes(bytes, "disk.adf"), false));
        session.SetDriveWriteProtected(0, false);
        var machine = session.Machine;
        void WriteByte(uint address, byte value) { var cycle = machine.Cpu.Cycles; machine.WriteByte(address, value, ref cycle, Copper68k.M68kBusAccessKind.CpuDataWrite); machine.Cpu.Cycles = cycle; }
        void WriteWord(uint address, ushort value) { var cycle = machine.Cpu.Cycles; machine.WriteWord(address, value, ref cycle, Copper68k.M68kBusAccessKind.CpuDataWrite); machine.Cpu.Cycles = cycle; }
        WriteByte(0xBFD100, 0x77); WriteByte(0xBFD300, 255);
        for (var i = 0; i < 31; i++) session.RenderNextFrame(session.Framebuffer);
        // Write a complete valid track through Paula DMA, including padding.
        var track = CopperDisk.AmigaDosTrackEncoder.EncodeTrack(CopperDisk.AmigaDiskLoader.FromAdfBytes(bytes), 0, 0);
        for (var i = 0; i < track.Length; i += 2)
            WriteWord((uint)(0x20000 + i), (ushort)((track[i] << 8) | track[i + 1]));
        WriteWord((uint)(0x20000 + track.Length), 0xAAAA);
        WriteWord(0xDFF020, 2); WriteWord(0xDFF022, 0);
        WriteWord(0xDFF096, 0x8210); WriteWord(0xDFF09E, 0x8100);
        var length = (ushort)(0xC000 | (track.Length / 2 + 1));
        WriteWord(0xDFF024, length); WriteWord(0xDFF024, length);
        for (var i = 0; i < 12; i++) session.RenderNextFrame(session.Framebuffer);
        Assert.True(machine.IsDriveDirty(0)); Assert.Equal(bytes, machine.ExportAdf());
        session.TogglePaused();
        using var runtime = CopperScreenRuntime.CreateForTests(session); runtime.Start();
        var destination = Path.Combine(_directory, "saved.adf"); File.WriteAllBytes(destination, [1, 2, 3]);
        using (var locked = new FileStream(destination, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var failed = await runtime.SaveAdfAsync(0, destination);
            Assert.False(failed.Success); Assert.True(failed.State.Drives[0].HasUnsavedChanges);
        }
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(destination));
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
        Assert.False((await runtime.EjectDiskAsync()).Success);
        Assert.Throws<InvalidOperationException>(() => CopperScreenRuntime.CreateReplacement(runtime, () => throw new Exception("Must not construct a replacement")));
        var reset = await runtime.ResetAsync(); Assert.True(reset.State.Drives[0].HasUnsavedChanges);
        await runtime.TogglePausedAsync();
        var saved = await runtime.SaveAdfAsync(0, destination);
        Assert.True(saved.Success, saved.Message); Assert.False(saved.State.Drives[0].HasUnsavedChanges);
        Assert.Equal(bytes, File.ReadAllBytes(destination)); Assert.Equal("original.zip", saved.State.DiskPath);
        using var reopened = new CopperScreenLightweightSession(Options(destination));
        Assert.Equal(bytes, reopened.ExportAdf(0));
    }

    [Fact]
    public void DefaultIsLightweightAndExplicitLegacyReportsUnavailable()
    {
        Assert.Equal(CopperScreenEngine.Lightweight, CopperScreenStartupOptions.Parse([], AppContext.BaseDirectory).Engine);
        var defaults = CopperScreenStartupOptions.Default(AppContext.BaseDirectory);
        Assert.Equal(CopperScreenEngine.Lightweight, defaults.Engine);
        Assert.Equal("lightweight-a500-kickstart13", defaults.Profile.Id);
        Assert.True(defaults.HardwareSpecialization);
        var options = Options("--engine", "Legacy");
        var error = Assert.Throws<NotSupportedException>(() => CopperScreenSession.Create(options));
        Assert.Contains("Legacy/CopperStart support is not included", error.Message);
        var roundtrip = CopperScreenSettingsDraft.FromStartupOptions(options).ToStartupOptions(AppContext.BaseDirectory);
        Assert.Equal(CopperScreenEngine.Legacy, roundtrip.Engine);
    }

    [Fact]
    public void RomOnlyStartupCreatesLightweightWithoutChangingDefaultHardware()
    {
        var options = CopperScreenStartupOptions.Parse(["--rom", _rom], AppContext.BaseDirectory);
        Assert.Null(options.Error);
        Assert.False(options.HasExplicitProfile);
        Assert.Equal("lightweight-a500-kickstart13", options.Profile.Id);
        Assert.False(options.Profile.RtcEnabled);
        Assert.Equal(1, options.Profile.FloppyDriveCount);
        using var session = CopperScreenSession.Create(options);
        Assert.IsType<CopperScreenLightweightSession>(session);
    }

    [Theory]
    [InlineData("expanded-copperstart")]
    [InlineData("expanded-m68040-kickstart-rom")]
    [InlineData("expanded-m68040-jit-kickstart31-rtg")]
    [InlineData("vanilla-kickstart13")]
    public void UnsupportedProfilesKeepTheirHardwareAndNeverSelectLegacyAutomatically(string id)
    {
        var options = CopperScreenStartupOptions.Parse(["--profile", id, "--rom", _rom], AppContext.BaseDirectory);
        // CopperStart with --rom is a parser error; don't obscure the routing check.
        if (id == "expanded-copperstart")
            options = CopperScreenStartupOptions.Parse(["--profile", id], AppContext.BaseDirectory);
        Assert.Equal(id, options.Profile.Id);
        Assert.Equal(CopperScreenEngine.Lightweight, options.Engine);
        var roundtrip = CopperScreenSettingsDraft.FromStartupOptions(options).ToStartupOptions(AppContext.BaseDirectory);
        Assert.Equal(options.Profile.CpuBackend, roundtrip.Profile.CpuBackend);
        Assert.Equal(options.Profile.Chipset, roundtrip.Profile.Chipset);
        Assert.Equal(options.Profile.KickstartVersion, roundtrip.Profile.KickstartVersion);
        Assert.Equal(options.Profile.RtcEnabled, roundtrip.Profile.RtcEnabled);
        Assert.Equal(options.Profile.FloppyDriveCount, roundtrip.Profile.FloppyDriveCount);
        var error = Assert.Throws<NotSupportedException>(() => CopperScreenSession.Create(roundtrip));
        Assert.Contains("No automatic Legacy fallback", error.Message);
        Assert.Contains("Legacy/CopperStart support is not included", error.Message);
    }

    [Fact]
    public void MissingRomExplainsHowToConfigureItWithoutStartingLegacy()
    {
        var options = CopperScreenStartupOptions.Default(AppContext.BaseDirectory);
        var error = Assert.Throws<NotSupportedException>(() => CopperScreenSession.Create(options));
        Assert.Contains("--kickstart <path>", error.Message);
        Assert.Contains("Settings > Setup", error.Message);
    }

    [Theory]
    [InlineData("EcsAgnus", "EcsDenise", "Pal")]
    [InlineData("AgaAlice", "AgaLisa", "Pal")]
    [InlineData("OcsAgnus", "OcsDenise", "Ntsc")]
    public void SettingsAndSavePreserveUnsupportedChipsets(string dma, string display, string video)
    {
        var draft = CopperScreenSettingsDraft.FromStartupOptions(Options());
        draft.Chipset = new AmigaChipset(Enum.Parse<DmaChipModel>(dma), Enum.Parse<DisplayChipModel>(display), Enum.Parse<VideoStandard>(video));
        draft.KickstartSource = CopperScreenKickstartSource.KickstartRom;
        draft.RomVersion = KickstartVersion.Kickstart31;
        draft.Id = "unsupported-" + dma + "-" + video;
        var options = draft.ToStartupOptions(_directory);
        Assert.Equal(draft.Chipset, options.Profile.Chipset);
        Assert.Equal(KickstartVersion.Kickstart31, options.Profile.KickstartVersion);
        Assert.Throws<NotSupportedException>(() => CopperScreenSession.Create(options));
        var path = CopperScreenProfileStore.Save(draft, _directory);
        Assert.True(CopperScreenProfile.TryLoad(path, _directory, out var reloaded, out var error), error);
        Assert.Equal(draft.Chipset, reloaded.Chipset);
        Assert.Equal(KickstartVersion.Kickstart31, reloaded.KickstartVersion);
    }

    [Fact]
    public void NewDraftAndFallbackProfileUseNativeA500Defaults()
    {
        var draft = new CopperScreenSettingsDraft();
        Assert.Equal(CopperScreenEngine.Lightweight, draft.Engine);
        Assert.False(draft.RtcEnabled);
        Assert.Equal(1, draft.FloppyDriveCount);
        Assert.Equal(CopperScreenKickstartSource.Kickstart13Rom, draft.KickstartSource);
        var fallback = CopperScreenProfile.LoadDefault(_directory, out var error);
        Assert.NotNull(error); // Missing profile file is not silently declared successful.
        Assert.Equal("lightweight-a500-kickstart13", fallback.Id);
        Assert.False(fallback.RtcEnabled);
        Assert.Equal(1, fallback.FloppyDriveCount);
        Assert.Equal(CopperScreenKickstartSource.Kickstart13Rom, fallback.KickstartSource);
        Assert.Equal(fallback.Id, CopperScreenProfile.NormalizeProfileId("default"));
    }

    [Theory]
    [InlineData("KickstartRom")]
    [InlineData("Kickstart13Rom")]
    public void UnsupportedRomVersionIsNotDowngradedBySettings(string source)
    {
        var draft = CopperScreenSettingsDraft.FromStartupOptions(Options());
        draft.KickstartSource = Enum.Parse<CopperScreenKickstartSource>(source);
        draft.RomVersion = KickstartVersion.Kickstart31;
        var options = draft.ToStartupOptions(_directory);
        Assert.Equal(KickstartVersion.Kickstart31, options.Profile.KickstartVersion);
        Assert.Throws<NotSupportedException>(() => CopperScreenSession.Create(options));
    }

    [Fact]
    public void ExplicitSelectionCreatesOnlyLightweightAndSurvivesSettingsRoundtrip()
    {
        var options = Options();
        var roundtrip = CopperScreenSettingsDraft.FromStartupOptions(options).ToStartupOptions(AppContext.BaseDirectory);
        Assert.Equal(CopperScreenEngine.Lightweight, roundtrip.Engine);
        using var session = CopperScreenSession.Create(roundtrip);
        Assert.IsType<CopperScreenLightweightSession>(session);
        Assert.Equal(48000, session.AudioSampleRate);
        Assert.Null(typeof(CopperScreenSession).Assembly.GetType("CopperScreen.CopperScreenEmulator"));
    }

    [Theory]
    [InlineData("--profile", "vanilla-kickstart13")]
    [InlineData("--profile", "expanded-copperstart")]
    [InlineData("--cpu", "jit-m68000")]
    [InlineData("--agnus-slot-kernel", "")]
    [InlineData("--cpu-deferred-bus-batch", "")]
    [InlineData("--engine", "typo")]
    public void UnsupportedExplicitRequestsNeverFallBack(string option, string value)
    {
        Assert.ThrowsAny<Exception>(() => CopperScreenSession.Create(Options(option, value)));
    }

    [Fact]
    public void WritableDriveConfigurationIsAppliedAtMachineCreation()
    {
        var draft = CopperScreenSettingsDraft.FromStartupOptions(Options());
        draft.DriveWriteProtected[0] = false;
        using var session = new CopperScreenLightweightSession(draft.ToStartupOptions(AppContext.BaseDirectory));
        Assert.False(session.Machine.IsDriveWriteProtected(0));
    }

    [Fact]
    public void SessionCopiesAllPixelsAndExactVariableLengthPcmWithoutExtraClockAdvance()
    {
        using var session = new CopperScreenLightweightSession(Options());
        var destination = new int[session.Width * session.Height];
        var audio = new float[session.AudioFramesPerAppFrame(48000) * 2];
        for (var frame = 0; frame < 4; frame++)
        {
            session.RenderNextFrame(destination);
            var cycle = session.Machine.Cycle;
            var native = session.Machine.Framebuffer.Span;
            for (var row = 0; row < 313; row++)
            {
                Assert.True(native.Slice(row * 908, 908).SequenceEqual(destination.AsSpan(row * 1816, 908)));
                Assert.True(native.Slice(row * 908, 908).SequenceEqual(destination.AsSpan(row * 1816 + 908, 908)));
            }
            var count = session.RenderAudio(audio, 48000, 2);
            Assert.Equal(session.Machine.AudioSamples.Length / 2, count);
            for (var i = 0; i < count * 2; i++)
                Assert.Equal(session.Machine.AudioSamples.Span[i] / 32768f, audio[i]);
            Assert.Equal(0, session.RenderAudio(audio, 48000, 2));
            Assert.Equal(cycle, session.Machine.Cycle);
        }
        Assert.Throws<NotSupportedException>(() => session.RenderAudio(audio, 44100, 2));
    }

    [Fact]
    public void MissingSyncUsesAdvertisedAudioCapacityAndKeepsSessionResponsive()
    {
        using var session = new CopperScreenLightweightSession(Options());
        var audio = new float[session.AudioFramesPerAppFrame(48000) * 2];
        var cycle = session.Machine.Cycle;
        session.Machine.WriteWord(0xDFF100, 2, ref cycle, Copper68k.M68kBusAccessKind.CpuDataWrite);
        for (var i = 0; i < 3; i++)
        {
            session.RenderNextFrame(session.Framebuffer);
            var count = session.RenderAudio(audio, 48000, 2);
            Assert.False(session.Machine.BeamSyncRunning);
            Assert.InRange(count, 1010, audio.Length / 2);
            Assert.False(session.IsPaused);
        }
        cycle = session.Machine.Cycle;
        session.Machine.WriteWord(0xDFF100, 0, ref cycle, Copper68k.M68kBusAccessKind.CpuDataWrite);
        for (var i = 0; i < 3; i++)
        {
            session.RenderNextFrame(session.Framebuffer);
            Assert.InRange(session.RenderAudio(audio, 48000, 2), 0, audio.Length / 2);
        }
        Assert.True(session.Machine.BeamSyncRunning);
    }

    [Fact]
    public void InputCommandsDoNotReplayMouseDeltasOnFollowingFrames()
    {
        using var session = new CopperScreenLightweightSession(Options());
        session.MoveMousePort(5, -3);
        var input = session.Machine.Input;
        session.SetMousePresentationPosition(200, 100);
        session.SetMousePortPosition(100, 50);
        Assert.Equal(input, session.Machine.Input); // Absolute host hints must not double relative motion.
        session.SetMouseButtons(true, false);
        Assert.Equal((short)0, session.Machine.Input.MouseDeltaX);
        Assert.Equal((byte)1, session.Machine.Input.MouseButtons);
        session.RenderNextFrame(session.Framebuffer);
        Assert.Equal((short)0, session.Machine.Input.MouseDeltaX);
        session.SetJoystickPort(1, true, false, false, true, true, false);
        Assert.Equal((byte)25, session.Machine.Input.JoystickPort1);
        session.Reset();
        Assert.False(session.IsPrimaryFirePressed);
        Assert.Equal(0, session.Machine.CompletedFrames);
    }

    [Fact]
    public void ReadOnlyMediaEjectRemountAndRejectedReplacementPreserveOwnership()
    {
        using var session = new CopperScreenLightweightSession(Options());
        var disk = CopperScreenAdfImage.FromAdfBytes(new byte[901120], "blank.adf");
        Assert.True(session.InsertLoadedDisk("blank.adf", disk, false));
        Assert.True(session.Machine.IsAdfMounted);
        Assert.True(session.SetDriveWriteProtected(0, false));
        var bad = CopperScreenAdfImage.FromAdfBytes(new byte[901120], "not-adf.ipf");
        Assert.Throws<NotSupportedException>(() => session.InsertLoadedDisk("bad.ipf", bad, true));
        Assert.True(session.Machine.IsAdfMounted);
        Assert.True(session.EjectDisk(0));
        Assert.False(session.Machine.IsAdfMounted);
        Assert.True(session.InsertLoadedDisk("blank.adf", disk, false));
        session.Reset();
        Assert.True(session.Machine.IsAdfMounted);
        Assert.False(session.InsertLoadedDisk(1, "blank.adf", disk, false));
        session.InsertLoadedDisk("blank.adf", disk, true);
        for (var frame = 0; frame < 25; frame++) session.RenderNextFrame(session.Framebuffer);
        Assert.False(session.Machine.IsAdfMounted);
        session.RenderNextFrame(session.Framebuffer);
        Assert.True(session.Machine.IsAdfMounted);
        Assert.False(session.IsDiskSwapPending);
    }

    [Fact]
    public void SteadyAdapterExecutionDoesNotAllocate()
    {
        using var session = new CopperScreenLightweightSession(Options());
        var audio = new float[1924];
        for (var frame = 0; frame < 20; frame++)
        { session.RenderNextFrame(session.Framebuffer); session.RenderAudio(audio, 48000, 2); }
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var frame = 0; frame < 10; frame++)
        { session.RenderNextFrame(session.Framebuffer); session.RenderAudio(audio, 48000, 2); }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void UnsupportedExecutionStopsWithoutFallbackAndRequiresReset()
    {
        using var session = new CopperScreenLightweightSession(Options());
        session.CaptureFatalException(new NotSupportedException("unsupported test edge"));
        Assert.True(session.TogglePaused());
        session.SetStatusText("must not hide fault");
        Assert.Contains("unsupported test edge", session.StatusText);
        Assert.Equal("unsupported test edge", session.FaultMessage);
        var cycle = session.Machine.Cycle;
        session.RenderNextFrame(session.Framebuffer);
        Assert.Equal(cycle, session.Machine.Cycle);
        session.Reset();
        Assert.False(session.IsPaused);
        Assert.Null(session.FaultMessage);
    }

    [Fact]
    public async Task FaultReasonSurvivesRuntimePublicationUntilReset()
    {
        var session = new CopperScreenLightweightSession(Options());
        session.CaptureFatalException(new NotSupportedException("clocked CIA serial output transmission"));
        using var runtime = CopperScreenRuntime.CreateForTests(session, null);
        runtime.Start();
        var paused = await runtime.TogglePausedAsync();
        Assert.True(paused.State.IsPaused);
        Assert.Equal("clocked CIA serial output transmission", paused.State.FaultMessage);
        Assert.Null((await runtime.ResetAsync()).State.FaultMessage);
    }

    [Fact]
    public async Task RuntimeUsesLightweightAudioRateAndSerializesCommands()
    {
        var session = new CopperScreenLightweightSession(Options());
        using var audio = new AudioSink();
        using var runtime = CopperScreenRuntime.CreateForTests(session, audio);
        runtime.Start();
        var limit = DateTime.UtcNow.AddSeconds(5);
        while (Volatile.Read(ref audio.Submissions) == 0 && DateTime.UtcNow < limit) await Task.Delay(10);
        Assert.True(Volatile.Read(ref audio.Submissions) > 0);
        Assert.InRange(audio.LastSampleCount, 1920, 1924);
        Assert.True((await runtime.TogglePausedAsync()).State.IsPaused);
        Assert.Equal(1, audio.DiscardCount);
        Assert.True((await runtime.EjectDiskAsync()).Success);
        Assert.True((await runtime.ResetAsync()).Success);
        Assert.Equal(2, audio.DiscardCount);
        Assert.Contains("Lightweight", runtime.CurrentState.ProfileName);
        Assert.DoesNotContain("48 kHz stereo required", runtime.CurrentState.StatusText);
    }

    [Fact]
    public void FourDriveStartupAndIndependentPendingSwapsRetainCorrectPaths()
    {
        var draft = CopperScreenSettingsDraft.FromStartupOptions(Options());
        draft.FloppyDriveCount = 4;
        for (var i = 0; i < 4; i++)
        {
            var path = Path.Combine(_directory, $"disk{i}.adf");
            File.WriteAllBytes(path, new byte[901120]);
            draft.DriveDiskPaths[i] = path;
        }
        using var session = new CopperScreenLightweightSession(draft.ToStartupOptions(_directory));
        var states = new CopperScreenDriveState[4];
        session.CaptureDriveStates(states);
        for (var i = 0; i < 4; i++)
        {
            Assert.True(states[i].Connected && states[i].HasDisk && states[i].WriteProtected);
            Assert.Equal(draft.DriveDiskPaths[i], states[i].DiskPath);
            Assert.True(session.SetDriveWriteProtected(i, false));
        }
        var disk = CopperScreenAdfImage.FromAdfBytes(new byte[901120], "replacement.adf");
        Assert.True(session.InsertLoadedDisk(1, "one.adf", disk, true));
        Assert.True(session.InsertLoadedDisk(2, "two.adf", disk, true));
        Assert.True(session.InsertLoadedDisk(3, "three.adf", disk, true));
        Assert.True(session.EjectDisk(2)); // Cancel only DF2's pending insertion.
        session.CaptureDriveStates(states);
        Assert.False(states[0].IsSwapPending);
        Assert.True(states[1].IsSwapPending);
        Assert.False(states[2].IsSwapPending);
        Assert.True(states[3].IsSwapPending);
        Assert.Equal(draft.DriveDiskPaths[0], session.DiskPath);
        var invalid = CopperScreenAdfImage.FromAdfBytes(new byte[901120], "bad.ipf");
        Assert.Throws<NotSupportedException>(() => session.InsertLoadedDisk(3, "bad.ipf", invalid, true));
        for (var frame = 0; frame < 25; frame++) session.RenderNextFrame(session.Framebuffer);
        Assert.False(session.Machine.IsDriveMounted(1));
        Assert.False(session.Machine.IsDriveMounted(3));
        session.RenderNextFrame(session.Framebuffer);
        Assert.False(session.IsDiskSwapPending);
        session.CaptureDriveStates(states);
        Assert.True(states[0].HasDisk && states[1].HasDisk && states[3].HasDisk);
        Assert.False(states[2].HasDisk);
        Assert.Equal("three.adf", states[3].DiskPath);
        Assert.True(session.InsertLoadedDisk(2, "reset.adf", disk, true));
        session.Reset();
        for (var i = 0; i < 4; i++) Assert.True(session.Machine.IsDriveMounted(i));
        Assert.False(session.IsDiskSwapPending);
        Assert.False(session.EjectDisk(-1));
        Assert.False(session.InsertLoadedDisk(4, "invalid.adf", disk, false));
    }

    [Fact]
    public void DisconnectedDriveMediaIsRejectedAndDriveCountSurvivesProfileSave()
    {
        var draft = CopperScreenSettingsDraft.FromStartupOptions(Options());
        draft.DriveDiskPaths[3] = "missing.adf";
        Assert.Throws<NotSupportedException>(() => CopperScreenLightweightSession.Validate(draft.ToStartupOptions(_directory)));
        draft.FloppyDriveCount = 4;
        CopperScreenLightweightSession.Validate(draft.ToStartupOptions(_directory));
        draft.DriveDiskPaths[3] = null;
        draft.Id = "four-drive-test";
        var path = CopperScreenProfileStore.Save(draft, _directory);
        Assert.True(CopperScreenProfile.TryLoad(path, _directory, out var loaded, out var error), error);
        Assert.Equal(4, loaded.FloppyDriveCount);
    }

    [Fact]
    public async Task RuntimeRoutesExternalDriveCommandsToTheirOwnPublishedState()
    {
        var draft = CopperScreenSettingsDraft.FromStartupOptions(Options());
        draft.FloppyDriveCount = 4;
        var session = new CopperScreenLightweightSession(draft.ToStartupOptions(_directory));
        using var runtime = CopperScreenRuntime.CreateForTests(session, null);
        runtime.Start();
        await runtime.TogglePausedAsync();
        var path = Path.Combine(_directory, "external.adf");
        File.WriteAllBytes(path, new byte[901120]);
        for (var i = 1; i < 4; i++)
        {
            var result = await runtime.InsertDriveDiskAsync(i, path, markChanged: false);
            Assert.True(result.Success);
            Assert.True(result.State.Drives[i].HasDisk);
            Assert.Equal(path, result.State.Drives[i].DiskPath);
            Assert.Null(result.State.DiskPath);
        }
        var ejected = await runtime.EjectDiskAsync(2);
        Assert.True(ejected.Success);
        Assert.True(ejected.State.Drives[1].HasDisk && ejected.State.Drives[3].HasDisk);
        Assert.False(ejected.State.Drives[2].HasDisk);
    }

    private sealed class AudioSink : ICopperScreenAudioOutput
    {
        public int Submissions, LastSampleCount, DiscardCount;
        public int QueuedBufferCount => Math.Min(8, Volatile.Read(ref Submissions));
        public bool Submit(ReadOnlySpan<float> samples)
        { LastSampleCount = samples.Length; Interlocked.Increment(ref Submissions); return true; }
        public void Dispose() { }
        public void DiscardQueuedSamples() => Interlocked.Increment(ref DiscardCount);
    }

    [Fact]
    public async Task RuntimeExportsAdfAndReportsSaveErrorsWithoutChangingMountedSource()
    {
        var session = new CopperScreenLightweightSession(Options());
        var bytes = new byte[901120];
        bytes[20] = 0xA5;
        session.InsertLoadedDisk("original.zip", CopperScreenAdfImage.FromAdfBytes(bytes, "original.adf"), false);
        session.TogglePaused();
        using var runtime = CopperScreenRuntime.CreateForTests(session);
        runtime.Start();
        var savedPath = Path.Combine(_directory, "saved.adf");
        var result = await runtime.SaveAdfAsync(0, savedPath);
        Assert.True(result.Success);
        Assert.Equal(bytes, File.ReadAllBytes(savedPath));
        Assert.Equal("original.zip", result.State.DiskPath);
        var wrongExtension = await runtime.SaveAdfAsync(0, Path.Combine(_directory, "wrong.zip"));
        Assert.False(wrongExtension.Success);
        Assert.True(wrongExtension.State.Drives[0].HasDisk);
        var absent = await runtime.SaveAdfAsync(0, Path.Combine(_directory, "missing", "saved.adf"));
        Assert.False(absent.Success);
        Assert.Equal(bytes, File.ReadAllBytes(savedPath));
    }

    [Fact]
    public async Task InvalidReplacementKeepsTheRunningSessionUsable()
    {
        var session = new CopperScreenLightweightSession(Options());
        session.TogglePaused();
        using var current = CopperScreenRuntime.CreateForTests(session);
        current.Start();
        var invalid = Options("--cpu", "m68040");
        Assert.Throws<NotSupportedException>(() => CopperScreenRuntime.CreateReplacement(current,
            () => CopperScreenRuntime.CreateForTests(CopperScreenSession.Create(invalid))));
        Assert.True(current.CurrentState.IsPaused);
        Assert.True((await current.EjectDiskAsync()).Success);
        Assert.True((await current.ResetAsync()).Success);
    }

    [Fact]
    public async Task SuccessfulReplacementStopsOldOwnerBeforeNewWorkerStarts()
    {
        using var current = CopperScreenRuntime.CreateForTests(new CopperScreenLightweightSession(Options()));
        current.Start();
        using var next = CopperScreenRuntime.CreateReplacement(current,
            () => CopperScreenRuntime.CreateForTests(new CopperScreenLightweightSession(Options())));
        Assert.Throws<ObjectDisposedException>(() => current.Start());
        Assert.Equal(0, next.CurrentState.FramesRendered);
        next.Start();
        Assert.True((await next.EjectDiskAsync()).Success);
    }

    [Fact]
    public void RomPickerValidationUsesTheSameFormatChecksAsStartup()
    {
        Assert.Equal(File.ReadAllBytes(_rom), CopperScreenKickstartRomArchive.ReadNative13Rom(_rom,
            CopperScreenKickstartSource.Kickstart13Rom, KickstartVersion.Kickstart13));
        var wrong = Path.Combine(_directory, "wrong.rom");
        File.WriteAllBytes(wrong, new byte[1024]);
        Assert.Throws<NotSupportedException>(() => CopperScreenKickstartRomArchive.ReadNative13Rom(wrong,
            CopperScreenKickstartSource.Kickstart13Rom, KickstartVersion.Kickstart13));
        var bytes = File.ReadAllBytes(_rom);
        bytes[13] = 40;
        File.WriteAllBytes(wrong, bytes);
        Assert.Throws<NotSupportedException>(() => CopperScreenKickstartRomArchive.ReadNative13Rom(wrong,
            CopperScreenKickstartSource.Kickstart13Rom, KickstartVersion.Kickstart13));
    }

    [NativeLightweightFact]
    public void NativeGameplayThroughHostAdapterPreservesValidatedCpuAndOutput()
        => RunNativeReplay(keyboard: false);

    [NativeLightweightFact]
    public void NativeKeyboardPowerUpCapsLockAndA500ResetAreAcknowledged()
    {
        var rom = Environment.GetEnvironmentVariable("COPPERSCREEN_LIGHTWEIGHT_NATIVE_ROM")!;
        var options = CopperScreenStartupOptions.Parse(["--rom", rom], AppContext.BaseDirectory);
        using var session = new CopperScreenLightweightSession(options);
        void Run(int fields)
        {
            for (var i = 0; i < fields; i++) session.RenderNextFrame(session.Framebuffer);
            Assert.False(session.IsPaused);
            Assert.Null(session.Machine.UnsupportedActiveFeature);
        }
        void AssertAcknowledged()
        {
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            Assert.Equal(long.MaxValue, typeof(LightweightA500Machine).GetProperty("KeyboardNextCycle", flags)!.GetValue(session.Machine));
        }
        Run(600);
        AssertAcknowledged();
        session.KeyDown(AmigaRawKey.CapsLock);
        session.KeyDown(AmigaRawKey.CapsLock); // Host repeat must be suppressed.
        session.KeyUp(AmigaRawKey.CapsLock);
        Run(20);
        Assert.True(session.Machine.KeyboardCapsLockOn);
        AssertAcknowledged();
        session.KeyDown(AmigaRawKey.CapsLock);
        session.KeyUp(AmigaRawKey.CapsLock);
        Run(20);
        Assert.False(session.Machine.KeyboardCapsLockOn);
        AssertAcknowledged();
        var cycle = session.Machine.Cycle;
        session.KeyDown(AmigaRawKey.Control);
        session.KeyDown(AmigaRawKey.LeftAmiga);
        session.KeyDown(AmigaRawKey.RightAmiga);
        Assert.Equal(cycle, session.Machine.Cycle);
        Assert.Equal(0x2700, session.Machine.Cpu.StatusRegister);
        Assert.True(session.Machine.RomOverlayEnabled);
        session.KeyUp(AmigaRawKey.RightAmiga);
        session.KeyUp(AmigaRawKey.LeftAmiga);
        session.KeyUp(AmigaRawKey.Control);
        Run(600);
        Assert.True(session.Machine.Cycle > cycle);
        AssertAcknowledged();
    }

    [NativeLightweightFact]
    public void NativeKeyboardPressAndReleaseAreAcknowledgedAndGameplayContinues()
        => RunNativeReplay(keyboard: true);

    private void RunNativeReplay(bool keyboard)
    {
        var rom = Environment.GetEnvironmentVariable("COPPERSCREEN_LIGHTWEIGHT_NATIVE_ROM")!;
        var disk = Environment.GetEnvironmentVariable("COPPERSCREEN_LIGHTWEIGHT_NATIVE_ADF")!;
        var script = Environment.GetEnvironmentVariable("COPPERSCREEN_LIGHTWEIGHT_NATIVE_SCRIPT")!;
        using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(script));
        var entries = json.RootElement.EnumerateArray().ToArray();
        // Exercise the application default route, not a directly constructed adapter.
        var options = CopperScreenStartupOptions.Parse(["--rom", rom, disk], AppContext.BaseDirectory);
        using var created = CopperScreenSession.Create(options);
        var session = Assert.IsType<CopperScreenLightweightSession>(created);
        var audio = new float[session.AudioFramesPerAppFrame(48000) * 2];
        var output = 1469598103934665603UL;
        const ulong prime = 1099511628211UL;
        var next = 0;
        var activeFields = 0;
        for (var frame = 0; frame < 14520; frame++)
        {
            if (keyboard && frame == 7900) session.KeyDown((AmigaRawKey)0x44);
            if (keyboard && frame == 7906) session.KeyUp((AmigaRawKey)0x44);
            while (next < entries.Length && entries[next].GetProperty("frame").GetInt32() == frame)
            {
                var entry = entries[next++];
                if (entry.TryGetProperty("ejectAdf", out var eject) && eject.GetBoolean()) session.EjectDisk(0);
                if (entry.TryGetProperty("adfPath", out var mount))
                {
                    var path = Path.GetFullPath(mount.GetString()!, Path.GetDirectoryName(script)!);
                    session.InsertLoadedDisk(path, CopperScreenDiskImageArchive.LoadDiskImage(path), false);
                }
                if (entry.TryGetProperty("mouseButtons", out var buttons))
                    session.SetMouseButtons((buttons.GetInt32() & 1) != 0, (buttons.GetInt32() & 2) != 0);
                var dx = entry.TryGetProperty("mouseDeltaX", out var x) ? x.GetInt32() : 0;
                var dy = entry.TryGetProperty("mouseDeltaY", out var y) ? y.GetInt32() : 0;
                session.MoveMousePort(dx, dy);
            }
            session.RenderNextFrame(session.Framebuffer);
            var frames = session.RenderAudio(audio, 48000, 2);
            Assert.False(session.IsPaused);
            Assert.False(session.IsInterlaced); // This frozen workload is progressive.
            if (keyboard && frame == 7915)
            {
                // Test-only inspection keeps serial internals out of the host API.
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                Assert.Equal(false, typeof(LightweightA500Machine).GetProperty("KeyboardWaitingForHandshake", flags)!.GetValue(session.Machine));
                Assert.Equal(long.MaxValue, typeof(LightweightA500Machine).GetProperty("KeyboardNextCycle", flags)!.GetValue(session.Machine));
            }
            if (frame < 10920) continue;
            var active = false;
            for (var i = 0; i < frames * 2; i++) if (audio[i] != 0) active = true;
            if (active) activeFields++;
            output = (output ^ 284204u) * prime;
            output = (output ^ (uint)(frames * 2)) * prime;
            var pixel = ((frame - 10920) * 131) % 284204;
            for (var i = 0; i < 257; i++)
            {
                // Consume the adapter's output, mapped back from host doubled rows.
                output = (output ^ (uint)session.Framebuffer[(pixel / 908) * 1816 + pixel % 908]) * prime;
                pixel = (pixel + 557) % 284204;
            }
            var sample = ((frame - 10920) * 17) % (frames * 2);
            for (var i = 0; i < 61; i++)
            {
                output = (output ^ (ushort)(short)(audio[sample] * 32768f)) * prime;
                sample = (sample + 31) % (frames * 2);
            }
        }
        var cpu = session.Machine.Cpu;
        var cpuHash = (1469598103934665603UL ^ cpu.ProgramCounter) * prime;
        cpuHash = (cpuHash ^ cpu.StatusRegister) * prime;
        cpuHash = (cpuHash ^ (ulong)cpu.Cycles) * prime;
        foreach (var value in cpu.D) cpuHash = (cpuHash ^ value) * prime;
        foreach (var value in cpu.A) cpuHash = (cpuHash ^ value) * prime;
        // ERSY absent-source correction changes Kickstart's genlock detection.
        // The unchanged script still enters level one and assigns the digger.
        // Previous expectations and the hardware reason are retained in
        // docs/engine/BEAM_SYNC.md; the frozen performance protocol is unchanged.
        Assert.Equal(2063189682, session.Machine.Cycle);
        if (!keyboard) Assert.Equal(0xBCF441F9BABF1113UL, cpuHash);
        Assert.Equal(0xF7328C7C24582FCBUL, output);
        Assert.True(activeFields > 0);
        Assert.Equal(entries.Length, next);
        Assert.Null(session.Machine.UnsupportedActiveFeature);
    }

    public sealed class NativeLightweightFactAttribute : FactAttribute
    {
        public NativeLightweightFactAttribute()
        {
            if (new[] { "ROM", "ADF", "SCRIPT" }.Any(suffix =>
                string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COPPERSCREEN_LIGHTWEIGHT_NATIVE_" + suffix))))
                Skip = "Set COPPERSCREEN_LIGHTWEIGHT_NATIVE_ROM, _ADF and _SCRIPT for native adapter replay (not a throughput gate).";
        }
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
