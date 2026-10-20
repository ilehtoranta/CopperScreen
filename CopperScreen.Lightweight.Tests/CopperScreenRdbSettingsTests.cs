using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed class CopperScreenRdbSettingsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "copper-rdb-settings-" + Guid.NewGuid().ToString("N"));
    public CopperScreenRdbSettingsTests() => Directory.CreateDirectory(_root);
    [Theory]
    [InlineData("--hdf-rdb", true)]
    [InlineData("--hdf-rdb-writable", false)]
    [InlineData("--hdf-rdb=", true)]
    [InlineData("--hdf-rdb-writable=", false)]
    public void RdbSelectionSurvivesProfileCloneLastMachineAndPathResolution(string option, bool readOnly)
    {
        var args = new List<string> { "--copperstart", "--rom", "selected.rom" };
        args.AddRange(option.EndsWith('=') ? [option + "disk with spaces.hdf"] : new[] { option, "disk with spaces.hdf" });
        var options = CopperScreenStartupOptions.Parse(args.ToArray(), AppContext.BaseDirectory, userDataDirectory: _root);
        Assert.Null(options.Error);
        var drive = Assert.Single(options.HardDrives);
        Assert.Equal(AmigaHardfileMountMode.RigidDiskBlock, drive.Mode); Assert.Null(drive.Partition);
        Assert.Equal(readOnly, drive.ReadOnly); Assert.Equal(Path.Combine(AppContext.BaseDirectory, "disk with spaces.hdf"), drive.Path);
        var draft = CopperScreenSettingsDraft.FromStartupOptions(options);
        Assert.Null(CopperScreenAvailability.GetUnavailableReason(draft, _root));
        Assert.False(draft.NeedsRestartComparedWith(draft.Clone()));
        var clone = draft.Clone(); clone.HardDrives[0] = drive with { ReadOnly = !readOnly };
        Assert.True(draft.NeedsRestartComparedWith(clone));
        var profilePath = Path.Combine(_root, "profile.json"); CopperScreenProfileStore.SaveToPath(draft, profilePath);
        Assert.True(CopperScreenProfile.TryLoad(profilePath, _root, out var profile, out var error), error);
        Assert.Equal(drive, Assert.Single(profile.HardDrives));
        new CopperScreenRecentSession(_root).Save(CopperScreenSettingsDraft.FromProfile(profile), 100, false);
        var restored = new CopperScreenRecentSession(_root).LoadMachine(_root)!;
        Assert.Equal(drive, Assert.Single(restored.ToStartupOptions(_root).HardDrives));
        clone = draft.Clone(); clone.HardDrives[0] = drive with { Mode = AmigaHardfileMountMode.Partition };
        Assert.True(draft.NeedsRestartComparedWith(clone));
    }

    [Fact]
    public void RdbOptionsRequireAPath()
    {
        foreach (var option in new[] { "--hdf-rdb", "--hdf-rdb-writable", "--hdf-rdb=" })
        {
            var options = CopperScreenStartupOptions.Parse(["--copperstart", "--rom", "selected.rom", option], AppContext.BaseDirectory, userDataDirectory: _root);
            Assert.Contains("requires an existing RDB image path", options.Error);
        }
    }

    [Fact]
    public void InvalidRdbIsReportedBeforeRomLoadingWithoutChangingImage()
    {
        var path = Path.Combine(_root, "invalid.hdf"); var bytes = new byte[512]; File.WriteAllBytes(path, bytes);
        var options = CopperScreenStartupOptions.Parse(["--copperstart", "--rom", "missing.rom", "--hdf-rdb", path], AppContext.BaseDirectory, userDataDirectory: _root);
        Assert.Null(options.Error);
        Assert.Contains("no valid RDB", Assert.Throws<InvalidDataException>(() => CopperScreenLightweightSession.Validate(options)).Message);
        Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    [CopperStartRdbFact]
    public void PinnedRomReadsBothRdbPartitionsThroughPersistedAppSettingsAndFreshSession()
    {
        var original = File.ReadAllBytes(Environment.GetEnvironmentVariable("COPPERSCREEN_COPPERSTART_RDB")!);
        Assert.Equal("000A3AF5969E52B856F59DDD1D3298CC2FF71C60ADFDEF0136369228D5C2F368", Convert.ToHexString(SHA256.HashData(original)));
        var path = Path.Combine(_root, "native.hdf"); File.WriteAllBytes(path, original);
        var options = CopperScreenStartupOptions.Parse(["--copperstart", "--rom", Environment.GetEnvironmentVariable("COPPERSCREEN_COPPERSTART_ROM")!, "--hdf-rdb", path], AppContext.BaseDirectory, userDataDirectory: _root);
        var draft = CopperScreenSettingsDraft.FromStartupOptions(options);
        new CopperScreenRecentSession(_root).Save(draft, 100, false);
        var restored = new CopperScreenRecentSession(_root).LoadMachine(_root)!.ToStartupOptions(_root);
        for (var boot = 0; boot < 2; boot++)
        {
            using var session = new CopperScreenLightweightSession(restored);
            for (var frame = 0; frame < 3000 && BinaryPrimitives.ReadUInt32BigEndian(session.Machine.ChipRam.Span[0xB80..]) != 0x52444231; frame++)
            { session.RenderNextFrame(session.Framebuffer); Assert.Null(session.FaultMessage); Assert.Null(session.Machine.UnsupportedActiveFeature); }
            var slow = (byte[])session.Machine.GetType().GetField("_slowRam", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(session.Machine)!;
            AssertReceipt(session.Machine.ChipRam.ToArray(), slow);
            Assert.False(session.Machine.IsDriveMounted(0)); Assert.Equal(original, Backing(path));
        }
    }

    [CopperStartRdbRunnerFact]
    public async Task RunnerRdbOptionsReadBothPartitionsThroughFreshProcesses()
    {
        var original = File.ReadAllBytes(Environment.GetEnvironmentVariable("COPPERSCREEN_COPPERSTART_RDB")!);
        var path = Path.Combine(_root, "runner disk.hdf"); File.WriteAllBytes(path, original);
        foreach (var option in new[] { "--hdf-rdb", "--hdf-rdb-writable" })
        {
            var output = Path.Combine(_root, option[2..]);
            var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { Environment.GetEnvironmentVariable("COPPERSCREEN_LIGHTWEIGHT_RUNNER")!, "--rom", Environment.GetEnvironmentVariable("COPPERSCREEN_COPPERSTART_ROM")!, option, path,
                "--frames", "2000", "--boot-probe", output, "--boot-probe-extended", "--boot-probe-interval", "2000" }) start.ArgumentList.Add(arg);
            using var process = Process.Start(start)!; var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
            try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(3)); }
            finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            var text = await stdout + await stderr; Assert.True(process.ExitCode == 0, text);
            Assert.Contains($"layout=RDB readOnly={option == "--hdf-rdb"}", text);
            AssertReceipt(File.ReadAllBytes(Path.Combine(output, "frame-002000.chipram")), File.ReadAllBytes(Path.Combine(output, "frame-002000.slowram")));
            Assert.Equal(original, File.ReadAllBytes(path));
        }
    }
    private static uint Long(byte[] bytes, int at) => BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(at));
    private static byte[] Backing(string path)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var bytes = new byte[file.Length]; file.ReadExactly(bytes); return bytes;
    }
    private static void AssertReceipt(byte[] chip, byte[] slow)
    {
        Assert.Equal(0x52444231u, Long(chip, 0xB80)); Assert.Equal(2u, Long(chip, 0xB88));
        Assert.NotEqual(0u, Long(chip, 0xB8C)); Assert.NotEqual(0u, Long(chip, 0xB90));
        var expected = Enumerable.Range(0, 2049).Select(i => (byte)(i * 37 + 11)).ToArray();
        Enumerable.Range(0, 32).Select(i => (byte)(255 - i)).ToArray().CopyTo(expected, 509);
        for (var i = 0; i < 2; i++)
        {
            Assert.Equal(2u, Long(chip, 0xB98 + i * 4));
            var address = Long(chip, 0xBA0 + i * 4);
            Assert.Equal(expected, address < chip.Length ? chip.AsSpan((int)address, 2049).ToArray() : slow.AsSpan((int)(address - 0xC00000), 2049).ToArray());
            Assert.NotEqual(0u, Long(chip, 0xBC4 + i * 8)); // both ACTION_FLUSH replies
        }
    }
    public void Dispose() => Directory.Delete(_root, true);
}
public class CopperStartRdbFactAttribute : CopperStartFactAttribute
{
    public CopperStartRdbFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COPPERSCREEN_COPPERSTART_RDB"))) Skip = "Qualified original RDB fixture not supplied.";
    }
}
public sealed class CopperStartRdbRunnerFactAttribute : CopperStartRdbFactAttribute
{
    public CopperStartRdbRunnerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COPPERSCREEN_LIGHTWEIGHT_RUNNER"))) Skip = "Built Lightweight runner not supplied.";
    }
}
