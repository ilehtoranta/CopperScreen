using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Reflection;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed class CopperScreenHardDiskSettingsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "copperscreen-hdf-" + Guid.NewGuid().ToString("N"));
    public CopperScreenHardDiskSettingsTests() => Directory.CreateDirectory(_directory);
    private CopperScreenSettingsDraft Draft() => CopperScreenSettingsDraft.FromStartupOptions(
        CopperScreenStartupOptions.Parse(["--copperstart", "--rom", "selected.rom"], AppContext.BaseDirectory, userDataDirectory: _directory));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExplicitFfsGeometrySurvivesProfileCloneLastMachineAndPathNormalization(bool readOnly)
    {
        var draft = Draft();
        var drive = CopperScreenHardfileSettings.Ffs(0, "disk.hdf", readOnly, "DH0:2:32:4:58");
        drive = drive with { Partition = drive.Partition! with { SectorOrigin = 0, PreAllocBlocks = 0, Interleave = 0, NumBuffers = 30, BufferMemoryType = 1, BootPriority = -5 } };
        draft.HardDrives.Add(drive);
        Assert.Null(CopperScreenAvailability.GetUnavailableReason(draft, _directory));
        var clone = draft.Clone();
        Assert.False(draft.NeedsRestartComparedWith(clone));
        clone.HardDrives[0] = drive with { Partition = drive.Partition! with { HighCylinder = 59 } };
        Assert.True(draft.NeedsRestartComparedWith(clone));
        Assert.Equal(58u, draft.HardDrives[0].Partition!.HighCylinder);

        var path = Path.Combine(_directory, "profile.json");
        CopperScreenProfileStore.SaveToPath(draft, path);
        Assert.True(CopperScreenProfile.TryLoad(path, _directory, out var profile, out var error), error);
        Assert.Equal(drive, Assert.Single(profile.HardDrives));
        Assert.Equal(drive with { Path = Path.Combine(_directory, "disk.hdf") }, Assert.Single(CopperScreenSettingsDraft.FromProfile(profile).ToStartupOptions(_directory).HardDrives));
        new CopperScreenRecentSession(_directory).Save(draft, 100, false);
        var restored = new CopperScreenRecentSession(_directory).LoadMachine(_directory)!;
        Assert.Equal(drive.Partition, Assert.Single(restored.HardDrives).Partition);
        Assert.Equal(readOnly, restored.HardDrives[0].ReadOnly);
        Assert.Equal(AmigaHardfileMountMode.Partition, restored.HardDrives[0].Mode);
    }

    [Theory]
    [InlineData("--hdf-ffs", true)]
    [InlineData("--hdf-ffs-writable", false)]
    public void CommandLineSelectsExplicitFfsWithoutAFloppy(string option, bool readOnly)
    {
        var options = CopperScreenStartupOptions.Parse(["--copperstart", "--rom", "selected.rom", option, Path.Combine(_directory, "disk.hdf"), "DH0:2:32:4:58"], AppContext.BaseDirectory);
        Assert.Null(options.Error);
        Assert.All(options.DriveDiskPaths, path => Assert.Null(path));
        Assert.Equal(CopperScreenHardfileSettings.Ffs(0, Path.Combine(_directory, "disk.hdf"), readOnly, "DH0:2:32:4:58"), Assert.Single(options.HardDrives));
        CopperScreenLightweightSession.Validate(options, requireRomPath: false);
    }

    [Theory]
    [InlineData("auto")]
    [InlineData("ofs")]
    [InlineData("missing-geometry")]
    [InlineData("block-count-overflow")]
    [InlineData("overflow")]
    [InlineData("unit")]
    [InlineData("multiple")]
    public void UnsupportedCopperStartMountsAreReportedBeforeRomLoading(string change)
    {
        var draft = Draft();
        var drive = CopperScreenHardfileSettings.Ffs(0, "disk.hdf", true, "DH0:2:32:4:58");
        drive = change switch
        {
            "auto" => drive with { Mode = AmigaHardfileMountMode.Auto },
            "ofs" => drive with { Partition = drive.Partition! with { DosType = 0x444F5300 } },
            "missing-geometry" => drive with { Partition = drive.Partition! with { HighCylinder = null } },
            "block-count-overflow" => drive with { Partition = drive.Partition! with { LowCylinder = 0, HighCylinder = 67108863 } },
            "overflow" => drive with { Partition = drive.Partition! with { Surfaces = uint.MaxValue, BlocksPerTrack = uint.MaxValue, HighCylinder = uint.MaxValue } },
            "unit" => drive with { Unit = 1 },
            _ => drive,
        };
        draft.HardDrives.Add(drive);
        if (change == "multiple") draft.HardDrives.Add(drive with { Unit = 1, Partition = drive.Partition! with { DeviceName = "DH1" } });
        Assert.NotNull(CopperScreenAvailability.GetUnavailableReason(draft, _directory));
    }

    [Fact]
    public void ImageExtentIsCheckedBeforeRomLoading()
    {
        var draft = Draft();
        var path = Path.Combine(_directory, "short.hdf"); File.WriteAllBytes(path, new byte[512]);
        draft.HardDrives.Add(CopperScreenHardfileSettings.Ffs(0, path, true, "DH0:2:32:4:58"));
        Assert.Contains("entire configured FFS partition", Assert.Throws<InvalidOperationException>(() => CopperScreenLightweightSession.Validate(draft.ToStartupOptions(_directory))).Message);
    }

    [Theory]
    [InlineData("DH0:2:32:0:131071")]
    [InlineData("DH0:2:32:131068:131122")]
    [InlineData("DH0:2:32:131076:131130")]
    [InlineData("DH0:2:32:4:135171")]
    public void Td64GeometryIsAvailableAndSurvivesSavedSettings(string geometry)
    {
        var draft = Draft();
        var drive = CopperScreenHardfileSettings.Ffs(0, "large.hdf", false, geometry);
        draft.HardDrives.Add(drive);
        Assert.Null(CopperScreenAvailability.GetUnavailableReason(draft, _directory));
        new CopperScreenRecentSession(_directory).Save(draft, 100, false);
        var restored = new CopperScreenRecentSession(_directory).LoadMachine(_directory)!;
        Assert.Equal(drive.Partition, Assert.Single(restored.HardDrives).Partition);
        CopperScreenLightweightSession.Validate(restored.ToStartupOptions(_directory), requireRomPath: false);
    }

    [CopperStartHardDiskFact]
    public void QualifiedHardDiskFreshBootReadsThroughPersistedAppSettings()
    {
        var original = File.ReadAllBytes(Environment.GetEnvironmentVariable("COPPERSCREEN_COPPERSTART_HDF")!);
        Assert.Equal("74E8A6F56EC6941E66AFF1B653CB13975213026526E4F16BDD8915E55A750046", Convert.ToHexString(SHA256.HashData(original)));
        var path = Path.Combine(_directory, "qualified.hdf"); File.WriteAllBytes(path, original);
        var options = CopperScreenStartupOptions.Parse(["--copperstart", "--rom", Environment.GetEnvironmentVariable("COPPERSCREEN_COPPERSTART_ROM")!, "--hdf-ffs", path, "DH0:2:32:4:58"], AppContext.BaseDirectory);
        Assert.Null(options.Error);
        var draft = CopperScreenSettingsDraft.FromStartupOptions(options);
        new CopperScreenRecentSession(_directory).Save(draft, 100, false);
        var restored = new CopperScreenRecentSession(_directory).LoadMachine(_directory)!;
        for (var boot = 0; boot < 2; boot++)
        {
            using var session = new CopperScreenLightweightSession(restored.ToStartupOptions(_directory));
            uint Read(int address) => BinaryPrimitives.ReadUInt32BigEndian(session.Machine.ChipRam.Span[address..]);
            for (var frame = 0; frame < 3000 && Read(0xB00) is not (0x46465357 or 0x4641494C); frame++)
            {
                session.RenderNextFrame(session.Framebuffer);
                Assert.Null(session.FaultMessage); Assert.Null(session.Machine.UnsupportedActiveFeature);
            }
            Assert.Equal(0x46465357u, Read(0xB00));
            Assert.Equal(2u, Read(0xB08)); // Native WriteProbe's fresh-readback branch.
            Assert.Equal(2049u, Read(0xB14));
            var expected = Enumerable.Range(0, 2049).Select(i => (byte)(i * 37 + 11)).ToArray();
            Enumerable.Range(0, 32).Select(i => (byte)(255 - i)).ToArray().CopyTo(expected, 509);
            var address = (int)Read(0xB04);
            // Inspect fitted A501 RAM without performing guest bus reads.
            var actual = address < session.Machine.ChipRam.Length
                ? session.Machine.ChipRam.Span.Slice(address, expected.Length).ToArray()
                : ((byte[])session.Machine.GetType().GetField("_slowRam", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session.Machine)!)
                    .AsSpan(address - 0xC00000, expected.Length).ToArray();
            Assert.Equal(expected, actual);
            Assert.False(session.Machine.IsDriveMounted(0));
        }
        Assert.Equal(original, File.ReadAllBytes(path));
    }

    public void Dispose() => Directory.Delete(_directory, true);
}

public class CopperStartHardDiskFactAttribute : CopperStartFactAttribute
{
    public CopperStartHardDiskFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COPPERSCREEN_COPPERSTART_HDF")))
            Skip = "Qualified original CopperHDF fixture not supplied; full-ROM hard-disk coverage unavailable.";
    }
}
