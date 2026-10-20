using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed partial class CopperStartHardDiskWriteTests
{
    [CopperStartHardDiskFact]
    public void Td64LargeVolumeReadsUpperFilesFromProtectedBoot()
    {
        var path = Path.Combine(_root, "large-read.hdf");
        var disk = new LargeFfsFixture(path, existingSaved: true);
        var before = disk.Observe();
        var options = CopperScreenStartupOptions.Parse(
            ["--copperstart", "--rom", Environment.GetEnvironmentVariable("COPPERSCREEN_COPPERSTART_ROM")!,
                "--hdf-ffs", path, LargeFfsFixture.Geometry], AppContext.BaseDirectory);
        using var session = new CopperScreenLightweightSession(options);
        RunLargeSession(session, 2, "td64-large-read");
        FfsHardDiskProbe.AssertReceipt(session.Machine.ChipRam.ToArray(), Slow(session), 2,
            (ulong)LargeFfsFixture.Start, LargeFfsFixture.Blocks);
        Assert.Equal(before, disk.Observe());
        WriteResult(Output("td64-large-read"), new { passed = true, boots = 1,
            blockCount = LargeFfsFixture.Blocks, upperFileBlocks = true, savedSha256 = before,
            protectedBackingUnchanged = true, guestAddedCacheBuffers = 32 });
    }

    [CopperStartTd64LargeWriteFact]
    public void Td64LargeVolumeBootsUpperFilesWritesFlushesAndFreshBootReads()
        => RunLargeVolume(false);

    [CopperStartTd64StressFact]
    public void Td64AllocationStressFillsLowerFourGiBAndWritesAboveIt()
        => RunLargeVolume(true);

    private void RunLargeVolume(bool forceUpperAllocation)
    {
        var path = Path.Combine(_root, "large.hdf"); var disk = new LargeFfsFixture(path, forceUpperAllocation);
        var output = forceUpperAllocation ? "td64-allocation-stress" : "td64-large-volume";
        CopperScreenStartupOptions Options(bool write) => CopperScreenStartupOptions.Parse(
            ["--copperstart", "--rom", Environment.GetEnvironmentVariable("COPPERSCREEN_COPPERSTART_ROM")!,
                write ? "--hdf-ffs-writable" : "--hdf-ffs", path, LargeFfsFixture.Geometry], AppContext.BaseDirectory);
        string saved; var timer = Stopwatch.StartNew();
        using (var session = new CopperScreenLightweightSession(Options(true)))
        {
            RunLargeSession(session, 1, output);
            AssertGuestCache(session);
            FfsHardDiskProbe.AssertReceipt(session.Machine.ChipRam.ToArray(), Slow(session), 1, (ulong)LargeFfsFixture.Start, LargeFfsFixture.Blocks);
            saved = disk.Observe(); // Before host flush or disposal.
            var cycle = session.Machine.Cycle; session.SavePersistentStorage();
            Assert.Equal(cycle, session.Machine.Cycle); Assert.Equal(saved, disk.Observe());
        }
        var writeSeconds = timer.Elapsed.TotalSeconds;
        using (var fresh = new CopperScreenLightweightSession(Options(false)))
        {
            RunLargeSession(fresh, 2, output);
            AssertGuestCache(fresh);
            FfsHardDiskProbe.AssertReceipt(fresh.Machine.ChipRam.ToArray(), Slow(fresh), 2, (ulong)LargeFfsFixture.Start, LargeFfsFixture.Blocks);
            Assert.Equal(saved, disk.Observe());
        }
        WriteResult(Output(output), new
        {
            passed = true,
            boots = 2,
            blockCount = LargeFfsFixture.Blocks,
            savedSha256 = saved,
            guestFlushBeforeHostSave = true,
            upperStartupFiles = true,
            guestAddedCacheBuffers = 32,
            upperAllocation = forceUpperAllocation,
            writeSeconds,
            execution = "app session driven at full frame boundaries without host wall-clock pacing"
        });
    }
    private static void AssertGuestCache(CopperScreenLightweightSession session)
    {
        var chip = session.Machine.ChipRam.ToArray(); var slow = Slow(session);
        uint Read(uint address) => address < chip.Length ? FfsHardDiskProbe.Long(chip, (int)address)
            : FfsHardDiskProbe.Long(slow, checked((int)(address - 0xC00000)));
        Assert.Equal(uint.MaxValue, Read(0xB40)); // DOS AddBuffers returns DOSTRUE.
        var handler = Read(Read(0x880) + 12);
        Assert.Equal(32u, Read(handler + 48)); // Pinned private extra-cache count.
    }
    private void RunLargeSession(CopperScreenLightweightSession session, uint phase, string output)
    {
        // Execute every guest frame normally. The two small-volume app cases
        // cover the runtime owner thread; this larger filesystem replay omits
        // only the host's 50 Hz pacing, not guest instructions or chipset work.
        var timer = Stopwatch.StartNew(); var nextProgress = 30d; var cacheValidated = false;
        for (var frame = 0; frame < 1_000_000 && timer.Elapsed < TimeSpan.FromMinutes(20); frame++)
        {
            session.RenderNextFrame(session.Framebuffer);
            Assert.Null(session.FaultMessage); Assert.Null(session.Machine.UnsupportedActiveFeature);
            var chip = session.Machine.ChipRam.Span;
            var marker = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(chip[0xB00..]);
            if (!cacheValidated && System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(chip[0xB08..]) is 1 or 2)
            {
                AssertGuestCache(session); cacheValidated = true;
            }
            if (marker == FfsHardDiskProbe.Failed) Assert.Fail($"Native FFS failure: {Convert.ToHexString(chip.Slice(0xB00, 64))}");
            if (marker == FfsHardDiskProbe.Complete && System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(chip[0xB08..]) == phase)
            {
                File.WriteAllText(Path.Combine(Output(output), $"phase-{phase}.json"), System.Text.Json.JsonSerializer.Serialize(new
                {
                    frame, cycle = session.Machine.Cycle, seconds = timer.Elapsed.TotalSeconds,
                    receipt = Convert.ToHexString(chip.Slice(0xB00, 64))
                }));
                return;
            }
            if (timer.Elapsed.TotalSeconds >= nextProgress)
            {
                File.WriteAllText(Path.Combine(Output(output), "progress.json"), System.Text.Json.JsonSerializer.Serialize(new
                {
                    frame,
                    cacheValidated,
                    seconds = timer.Elapsed.TotalSeconds,
                    cycle = session.Machine.Cycle,
                    pc = session.Machine.Cpu.ProgramCounter,
                    receipt = Convert.ToHexString(chip.Slice(0xB00, 64))
                }));
                nextProgress += 30;
            }
        }
        Assert.Fail("Large-volume native replay did not reach completion; see its bounded progress receipt.");
    }
    // Keep the proven guest HUNK and FFS volume, relocating only the host extent.
    // The alias guard is exactly where a truncated TD64 offset would write.
    private sealed class SparsePartition
    {
        public readonly string Path, Geometry;
        public readonly long Start;
        public readonly byte[] Pristine;
        private readonly byte[] guard = Enumerable.Repeat((byte)0xA7, 4 * 1024 * 1024).ToArray();
        private readonly byte[] edge = Enumerable.Repeat((byte)0x5C, 512).ToArray();
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeviceIoControl(SafeFileHandle file, uint code, IntPtr input, int inputBytes,
            IntPtr output, int outputBytes, out int returned, IntPtr overlapped);
        public SparsePartition(string path, uint low)
        {
            Path = path; Start = (long)low * 32768; Geometry = $"DH0:2:32:{low}:{low + 54}";
            Pristine = FfsHardDiskProbe.Pristine();
            using var file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite);
            if (OperatingSystem.IsWindows() && !DeviceIoControl(file.SafeFileHandle, 0x900C4, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            file.SetLength(Start + FfsHardDiskProbe.Bytes + 512);
            file.Write(guard); file.Position = Start - 512; file.Write(edge);
            file.Position = Start; file.Write(Pristine.AsSpan(FfsHardDiskProbe.Start, FfsHardDiskProbe.Bytes));
            file.Write(edge); file.Flush(true);
        }
        public byte[] ReadVolume()
        {
            using var file = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            byte[] Read(long at, int count) { file.Position = at; var bytes = new byte[count]; file.ReadExactly(bytes); return bytes; }
            Assert.Equal(Start + FfsHardDiskProbe.Bytes + 512, file.Length);
            Assert.Equal(guard, Read(0, guard.Length));
            Assert.Equal(edge, Read(Start - 512, 512)); Assert.Equal(edge, Read(Start + FfsHardDiskProbe.Bytes, 512));
            var result = Pristine.ToArray(); Read(Start, FfsHardDiskProbe.Bytes).CopyTo(result, FfsHardDiskProbe.Start);
            return result;
        }
        public byte[] Observe(byte[] chip, byte[] slow, uint phase)
        {
            FfsHardDiskProbe.AssertReceipt(chip, slow, phase, (ulong)Start);
            var result = ReadVolume(); FfsHardDiskProbe.AssertDisk(Pristine, result); return result;
        }
    }
    private CopperScreenStartupOptions Td64Options(SparsePartition disk, bool write) => CopperScreenStartupOptions.Parse(
        ["--copperstart", "--rom", Environment.GetEnvironmentVariable("COPPERSCREEN_COPPERSTART_ROM")!,
            write ? "--hdf-ffs-writable" : "--hdf-ffs", disk.Path, disk.Geometry], AppContext.BaseDirectory);

    [CopperStartHardDiskFact]
    public async Task Td64AppCrossingAndAboveFourGiBWritesFlushResetAndFreshBoot()
    {
        foreach (var low in new[] { 131068u, 131076u })
        {
            var disk = new SparsePartition(Path.Combine(_root, $"app-{low}.hdf"), low);
            var profilePath = Path.Combine(_root, $"profile-{low}.json");
            CopperScreenProfileStore.SaveToPath(CopperScreenSettingsDraft.FromStartupOptions(Td64Options(disk, true)), profilePath);
            Assert.True(CopperScreenProfile.TryLoad(profilePath, AppContext.BaseDirectory, out var profile, out var error), error);
            var draft = CopperScreenSettingsDraft.FromProfile(profile);
            new CopperScreenRecentSession(_root).Save(draft, 100, false);
            var options = new CopperScreenRecentSession(_root).LoadMachine(AppContext.BaseDirectory)!.ToStartupOptions(AppContext.BaseDirectory);
            byte[] saved;
            using (var session = new CopperScreenLightweightSession(options))
            using (var runtime = Runtime(session))
            {
                runtime.Start(); await PauseAtReceipt(runtime, session, FfsHardDiskProbe.Complete);
                saved = disk.Observe(session.Machine.ChipRam.ToArray(), Slow(session), 1); // Live backing, before host Save.
                Assert.True((await runtime.SavePersistentStorageAsync()).Success);
                Assert.Equal(saved, disk.ReadVolume());
                Assert.True((await runtime.ResetAsync()).Success);
                await PauseAtReceipt(runtime, session, FfsHardDiskProbe.Complete, 2);
                Assert.Equal(saved, disk.Observe(session.Machine.ChipRam.ToArray(), Slow(session), 2));
            }
            using (var fresh = new CopperScreenLightweightSession(Td64Options(disk, false)))
            using (var runtime = Runtime(fresh))
            {
                runtime.Start(); await PauseAtReceipt(runtime, fresh, FfsHardDiskProbe.Complete, 2);
                Assert.Equal(saved, disk.Observe(fresh.Machine.ChipRam.ToArray(), Slow(fresh), 2));
            }
            WriteResult(Output($"td64-app-{low}"), new
            {
                passed = true,
                boots = 3,
                disk.Start,
                disk.Geometry,
                guestFlushBeforeHostSave = true,
                lowAliasGuardUnchanged = true,
                savedSha256 = Hash(saved)
            });
        }
    }

    private async Task<string> Td64Runner(SparsePartition disk, bool writable, string name, int frames)
    {
        var output = Output(name);
        var info = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        string[] arguments = [Environment.GetEnvironmentVariable("COPPERSCREEN_LIGHTWEIGHT_RUNNER")!, "--rom", Environment.GetEnvironmentVariable("COPPERSCREEN_COPPERSTART_ROM")!,
            writable ? "--hdf-ffs-writable" : "--hdf-ffs", disk.Path, disk.Geometry, "--frames", frames.ToString(),
            "--boot-probe", output, "--boot-probe-interval", frames.ToString(), "--boot-probe-extended"];
        foreach (var arg in arguments) info.ArgumentList.Add(arg);
        using var process = Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(3)); }
        finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        var log = await stdout + await stderr; File.WriteAllText(Path.Combine(output, "runner.log"), log);
        Assert.True(process.ExitCode == 0, log); return Path.Combine(output, $"frame-{frames:D6}");
    }
    [CopperStartHardDiskRunnerFact]
    public async Task Td64RunnerCrossingAndAboveFourGiBWritesRestartAndFreshProtectedBoot()
    {
        foreach (var low in new[] { 131068u, 131076u })
        {
            var disk = new SparsePartition(Path.Combine(_root, $"runner-{low}.hdf"), low);
            byte[] Observe(string stem, uint phase) => disk.Observe(File.ReadAllBytes(stem + ".chipram"), File.ReadAllBytes(stem + ".slowram"), phase);
            var saved = Observe(await Td64Runner(disk, true, $"td64-runner-{low}-write", 3000), 1);
            Assert.Equal(saved, Observe(await Td64Runner(disk, true, $"td64-runner-{low}-restart", 1500), 2));
            Assert.Equal(saved, Observe(await Td64Runner(disk, false, $"td64-runner-{low}-protected", 1500), 2));
            WriteResult(Output($"td64-runner-{low}"), new
            {
                passed = true,
                boots = 3,
                disk.Start,
                disk.Geometry,
                savedSha256 = Hash(saved),
                lowAliasGuardUnchanged = true
            });
        }
    }
}

public sealed class CopperStartTd64StressFactAttribute : CopperStartHardDiskFactAttribute
{
    public CopperStartTd64StressFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("COPPERSCREEN_TD64_ALLOCATION_STRESS") != "1")
            Skip = "Nearly-full large-volume allocation stress is opt-in; it is separate from TD64 qualification.";
    }
}

public sealed class CopperStartTd64LargeWriteFactAttribute : CopperStartHardDiskFactAttribute
{
    public CopperStartTd64LargeWriteFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("COPPERSCREEN_TD64_LARGE_WRITES") != "1")
            Skip = "Large-volume full-ROM writes are opt-in because the replay audits multi-gigabyte images.";
    }
}
