using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed class CopperStartRdbWriteTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "copper-rdb-write-" + Guid.NewGuid().ToString("N"));
    public CopperStartRdbWriteTests() => Directory.CreateDirectory(_root);
    private string Output(string name)
    {
        var path = Path.Combine(Environment.GetEnvironmentVariable("COPPERSCREEN_RDB_WRITE_OUTPUT") ?? _root, name);
        Directory.CreateDirectory(path); return path;
    }
    private CopperScreenStartupOptions Options(string path, bool writable = true) => CopperScreenStartupOptions.Parse(
        ["--copperstart", "--rom", Environment.GetEnvironmentVariable("COPPERSCREEN_COPPERSTART_ROM")!,
            writable ? "--hdf-rdb-writable" : "--hdf-rdb", path], AppContext.BaseDirectory);
    private static byte[] Slow(CopperScreenLightweightSession session) =>
        ((byte[])session.Machine.GetType().GetField("_slowRam", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session.Machine)!).ToArray();
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static CopperScreenRuntime Runtime(CopperScreenLightweightSession session)
    {
        var runtime = CopperScreenRuntime.CreateForTests(session);
        long last = 0;
        // Supply the ordinary presentation consumer, otherwise the silent
        // runtime correctly waits for its full display queue to be drained.
        runtime.FramePublished += () =>
        {
            while (runtime.TryAcquireNextPresentationFrame(ref last) is { } frame) frame.Dispose();
        };
        return runtime;
    }
    private static async Task PauseAtReceipt(CopperScreenRuntime runtime, CopperScreenLightweightSession session, uint expected, uint phase = 1)
    {
        // Enter with a running runtime. Command results precede publication of
        // the next UI snapshot; CurrentState may still describe the pre-reset
        // pause and must not decide whether to toggle again here.
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromMinutes(3))
        {
            await Task.Delay(100);
            var paused = await runtime.TogglePausedAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(paused.Success, paused.Message); Assert.True(paused.State.IsPaused);
            Assert.True(session.IsPaused); Assert.Null(session.FaultMessage); Assert.Null(session.Machine.UnsupportedActiveFeature);
            var chip = session.Machine.ChipRam.ToArray();
            var marker = FfsHardDiskProbe.Long(chip, 0xB80);
            if (marker == RdbFfsProbe.Failed) { Assert.Equal(expected, marker); return; }
            // Reset preserves RAM. Require the newly entered readback branch and
            // its completed CMD_UPDATE, not the previous boot's completion word.
            if (marker == RdbFfsProbe.Complete && FfsHardDiskProbe.Long(chip, 0xB98) == phase && FfsHardDiskProbe.Long(chip, 0xB9C) == phase && RdbFfsProbe.HasCompletedFlush(chip, Slow(session)))
            { Assert.Equal(expected, marker); return; }
            var running = await runtime.TogglePausedAsync();
            Assert.True(running.Success, running.Message); Assert.False(running.State.IsPaused);
        }
        throw new TimeoutException("Native HDF probe did not reach its receipt through the app runtime.");
    }
    private static byte[] Observe(CopperScreenLightweightSession session, string image, byte[] pristine, uint phase, string output, string name)
    {
        Assert.True(session.IsPaused);
        var chip = session.Machine.ChipRam.ToArray(); var slow = Slow(session);
        RdbFfsProbe.AssertReceipt(chip, slow, phase);
        Assert.False(session.Machine.IsDriveMounted(0));
        var backing = FfsHardDiskProbe.Backing(image); // Before host Save or Dispose.
        RdbFfsProbe.AssertDisk(pristine, backing);
        File.WriteAllBytes(Path.Combine(output, name + ".chipram"), chip);
        File.WriteAllBytes(Path.Combine(output, name + ".slowram"), slow);
        File.WriteAllBytes(Path.Combine(output, name + ".hdf"), backing);
        return backing;
    }

    [CopperStartRdbFact]
    public async Task AppWritesGuestFlushHostSaveResetReplacementAndFreshBootReadback()
    {
        var output = Output("app");
        var pristine = RdbFfsProbe.Pristine();
        var image = Path.Combine(_root, "app.hdf"); File.WriteAllBytes(image, pristine);
        var draft = CopperScreenSettingsDraft.FromStartupOptions(Options(image));
        Assert.False(Assert.Single(draft.HardDrives).ReadOnly);
        var profilePath = Path.Combine(_root, "profile.json"); CopperScreenProfileStore.SaveToPath(draft, profilePath);
        Assert.True(CopperScreenProfile.TryLoad(profilePath, AppContext.BaseDirectory, out var profile, out var error), error);
        new CopperScreenRecentSession(_root).Save(CopperScreenSettingsDraft.FromProfile(profile), 100, false);
        var restored = new CopperScreenRecentSession(_root).LoadMachine(AppContext.BaseDirectory)!;
        Assert.False(Assert.Single(restored.HardDrives).ReadOnly);
        Assert.Equal(AmigaHardfileMountMode.RigidDiskBlock, Assert.Single(restored.HardDrives).Mode);
        var options = restored.ToStartupOptions(AppContext.BaseDirectory);
        byte[] persisted;
        CopperScreenLightweightSession replacement;
        using (var first = new CopperScreenLightweightSession(options))
        using (var runtime = Runtime(first))
        {
            runtime.Start();
            await PauseAtReceipt(runtime, first, RdbFfsProbe.Complete);
            persisted = Observe(first, image, pristine, 1, output, "written-before-host-save");
            Assert.NotEqual(Hash(pristine), Hash(persisted));
            var cycle = first.Machine.Cycle;
            Assert.True((await runtime.SavePersistentStorageAsync()).Success);
            Assert.Equal(cycle, first.Machine.Cycle); // Host flush does not execute guest cycles.
            Assert.Equal(persisted, FfsHardDiskProbe.Backing(image));
            var reset = await runtime.ResetAsync();
            Assert.True(reset.Success, reset.Message); Assert.False(reset.State.IsPaused);
            await PauseAtReceipt(runtime, first, RdbFfsProbe.Complete, 2);
            Assert.Equal(persisted, Observe(first, image, pristine, 2, output, "reset-readback"));
            Assert.True((await runtime.SavePersistentStorageAsync()).Success);
            replacement = new CopperScreenLightweightSession(options, first); // Standard app replacement / shared backing lease.
        }
        using (replacement)
        using (var runtime = Runtime(replacement))
        {
            runtime.Start();
            await PauseAtReceipt(runtime, replacement, RdbFfsProbe.Complete, 2);
            Assert.Equal(persisted, Observe(replacement, image, pristine, 2, output, "replacement-readback"));
        }
        Assert.Equal(persisted, File.ReadAllBytes(image));
        // Reopen the persisted bytes with a new machine and protected backing.
        var freshImage = Path.Combine(_root, "fresh.hdf"); File.WriteAllBytes(freshImage, persisted);
        using (var fresh = new CopperScreenLightweightSession(Options(freshImage, writable: false)))
        using (var runtime = Runtime(fresh))
        {
            runtime.Start(); await PauseAtReceipt(runtime, fresh, RdbFfsProbe.Complete, 2);
            Assert.Equal(persisted, Observe(fresh, freshImage, pristine, 2, output, "fresh-protected-readback"));
        }
        Assert.Equal(persisted, File.ReadAllBytes(freshImage));
        WriteResult(output, new { passed = true, frontend = "app-runtime", boots = 4, partitions = 2, sysIdentity = 2, payloadBytesPerPartition = 2049,
            guestCmdUpdateBeforeHostSave = true, hostSaveSucceeded = true, hostSaveAdvancedCycles = false,
            reset = true, sessionReplacement = true, freshProtectedBoot = true,
            profileAndLastMachineRestored = true, pristineSha256 = Hash(pristine), savedSha256 = Hash(persisted) });
    }

    [CopperStartRdbFact]
    public async Task AppProtectedPristinePartitionRejectsFirstMutationWithoutChangingBacking()
    {
        var pristine = RdbFfsProbe.Pristine(); var image = Path.Combine(_root, "protected.hdf"); File.WriteAllBytes(image, pristine);
        using var session = new CopperScreenLightweightSession(Options(image, writable: false));
        using var runtime = Runtime(session); runtime.Start();
        await PauseAtReceipt(runtime, session, RdbFfsProbe.Failed);
        Assert.Equal(214u, FfsHardDiskProbe.Long(session.Machine.ChipRam.ToArray(), 0xB84));
        Assert.Equal(pristine, FfsHardDiskProbe.Backing(image));
        Assert.True((await runtime.SavePersistentStorageAsync()).Success);
        Assert.Equal(pristine, FfsHardDiskProbe.Backing(image));
    }

    private async Task<string> Runner(string image, bool writable, string name, int frames = 2000)
    {
        var output = Output(name);
        var info = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        string[] arguments = [Environment.GetEnvironmentVariable("COPPERSCREEN_LIGHTWEIGHT_RUNNER")!,
            "--rom", Environment.GetEnvironmentVariable("COPPERSCREEN_COPPERSTART_ROM")!,
            writable ? "--hdf-rdb-writable" : "--hdf-rdb", image,
            "--frames", frames.ToString(System.Globalization.CultureInfo.InvariantCulture), "--boot-probe", output, "--boot-probe-interval", frames.ToString(System.Globalization.CultureInfo.InvariantCulture), "--boot-probe-extended", "--boot-probe-storage"];
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(3)); }
        finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        var text = await stdout + await stderr;
        File.WriteAllText(Path.Combine(output, "runner.log"), text);
        Assert.True(process.ExitCode == 0, text);
        Assert.Contains($"layout=RDB readOnly={!writable}", text);
        var stem = Path.Combine(output, $"frame-{frames:D6}");
        using var state = JsonDocument.Parse(File.ReadAllText(stem + ".json"));
        Assert.Equal(JsonValueKind.Null, state.RootElement.GetProperty("unsupported").ValueKind);
        Assert.Equal(JsonValueKind.Null, state.RootElement.GetProperty("videoUnsupported").ValueKind);
        Assert.False(state.RootElement.GetProperty("drives")[0].GetProperty("Mounted").GetBoolean());
        return stem;
    }
    private static byte[] ObserveRunner(string stem, byte[] pristine, uint phase)
    {
        RdbFfsProbe.AssertReceipt(File.ReadAllBytes(stem + ".chipram"), File.ReadAllBytes(stem + ".slowram"), phase);
        var backing = File.ReadAllBytes(stem + ".hdf0"); // Taken while the runner still owned the open image, without a host flush.
        RdbFfsProbe.AssertDisk(pristine, backing); return backing;
    }

    [CopperStartRdbRunnerFact]
    public async Task CommandLineWritesFlushBeforeExitRestartAndFreshProtectedBootReadback()
    {
        var pristine = RdbFfsProbe.Pristine(); var image = Path.Combine(_root, "runner.hdf"); File.WriteAllBytes(image, pristine);
        var first = await Runner(image, true, "runner-write", frames: 3000);
        var persisted = ObserveRunner(first, pristine, 1);
        Assert.NotEqual(Hash(pristine), Hash(persisted)); Assert.Equal(persisted, File.ReadAllBytes(image));
        Assert.Equal(persisted, ObserveRunner(await Runner(image, true, "runner-restart"), pristine, 2));
        Assert.Equal(persisted, File.ReadAllBytes(image));
        var fresh = Path.Combine(_root, "runner-fresh.hdf"); File.WriteAllBytes(fresh, persisted);
        Assert.Equal(persisted, ObserveRunner(await Runner(fresh, false, "runner-fresh-protected"), pristine, 2));
        Assert.Equal(persisted, File.ReadAllBytes(fresh));
        WriteResult(Output("runner"), new { passed = true, frontend = "command-line-process", boots = 3, partitions = 2, sysIdentity = 2, payloadBytesPerPartition = 2049,
            guestCmdUpdateBeforeExit = true, liveBackingSnapshotWithoutHostFlush = true, writableProcessRestart = true,
            freshProtectedBoot = true, pristineSha256 = Hash(pristine), savedSha256 = Hash(persisted),
            runnerSha256 = Hash(File.ReadAllBytes(Environment.GetEnvironmentVariable("COPPERSCREEN_LIGHTWEIGHT_RUNNER")!)) });
    }

    [CopperStartRdbRunnerFact]
    public async Task CommandLineProtectedPristinePartitionRejectsWritesWithoutChangingBacking()
    {
        var pristine = RdbFfsProbe.Pristine(); var image = Path.Combine(_root, "runner-protected.hdf"); File.WriteAllBytes(image, pristine);
        var stem = await Runner(image, false, "runner-protected-rejection");
        var chip = File.ReadAllBytes(stem + ".chipram");
        Assert.Equal(RdbFfsProbe.Failed, FfsHardDiskProbe.Long(chip, 0xB80));
        Assert.Equal(214u, FfsHardDiskProbe.Long(chip, 0xB84));
        Assert.Equal(pristine, File.ReadAllBytes(stem + ".hdf0")); Assert.Equal(pristine, File.ReadAllBytes(image));
    }
    [CopperStartRdbFact]
    public async Task AppReversedBootPrioritiesSelectDh0AndWriteBothVolumes()
    {
        var pristine = RdbFfsProbe.BootFromDh0(RdbFfsProbe.Pristine());
        var image = Path.Combine(_root, "reversed.hdf"); File.WriteAllBytes(image, pristine);
        using var session = new CopperScreenLightweightSession(Options(image));
        using var runtime = Runtime(session); runtime.Start();
        await PauseAtReceipt(runtime, session, RdbFfsProbe.Complete);
        var output = Output("app-reversed-priority");
        var chip = session.Machine.ChipRam.ToArray(); var slow = Slow(session);
        RdbFfsProbe.AssertReceipt(chip, slow, 1, identity: 1);
        var backing = FfsHardDiskProbe.Backing(image); RdbFfsProbe.AssertDisk(pristine, backing);
        File.WriteAllBytes(Path.Combine(output, "before-host-save.chipram"), chip);
        File.WriteAllBytes(Path.Combine(output, "before-host-save.slowram"), slow);
        File.WriteAllBytes(Path.Combine(output, "before-host-save.hdf"), backing);
        WriteResult(output, new { passed = true, boots = 1, sysIdentity = 1, partitions = 2,
            guestCmdUpdateBeforeHostSave = true, pristineSha256 = Hash(pristine), savedSha256 = Hash(backing) });
    }

    [CopperStartRdbRunnerFact]
    public async Task RunnerReversedBootPrioritiesSelectDh0AndWriteBothVolumes()
    {
        var pristine = RdbFfsProbe.BootFromDh0(RdbFfsProbe.Pristine());
        var image = Path.Combine(_root, "reversed-runner.hdf"); File.WriteAllBytes(image, pristine);
        var stem = await Runner(image, true, "runner-reversed-priority", frames: 3000);
        RdbFfsProbe.AssertReceipt(File.ReadAllBytes(stem + ".chipram"), File.ReadAllBytes(stem + ".slowram"), 1, identity: 1);
        var backing = File.ReadAllBytes(stem + ".hdf0"); RdbFfsProbe.AssertDisk(pristine, backing);
        Assert.Equal(backing, File.ReadAllBytes(image));
        WriteResult(Output("runner-reversed-priority"), new { passed = true, boots = 1, sysIdentity = 1, partitions = 2,
            liveBackingSnapshotWithoutHostFlush = true, pristineSha256 = Hash(pristine), savedSha256 = Hash(backing) });
    }

    private static void WriteResult(string output, object result) => File.WriteAllText(Path.Combine(output, "result.json"),
        JsonSerializer.Serialize(new { protocol = "copperstart-frontend-rdb-writes-v1", firmware = CopperScreenCopperStartRom.Version,
            romSha256 = CopperScreenCopperStartRom.Sha256, result,
            binaries = new[] { typeof(CopperScreenSession).Assembly.Location, typeof(CopperMod.Amiga.Lightweight.LightweightA500Machine).Assembly.Location, typeof(CopperDisk.AmigaHardfileConfiguration).Assembly.Location }
                .Select(path => new { name = Path.GetFileName(path), sha256 = Hash(File.ReadAllBytes(path)) }) }, new JsonSerializerOptions { WriteIndented = true }));
    public void Dispose() => Directory.Delete(_root, true);
}
