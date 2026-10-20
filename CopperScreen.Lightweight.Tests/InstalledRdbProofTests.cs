using CopperScreen;

namespace CopperScreen.Tests;

public sealed class InstalledRdbProofTests
{
    [RdbFixtureFact]
    public void RestoredWriteFixtureKeepsSavedDataAndCannotPassAsANewWriteBeforeTheGuestCreatesAfter()
    {
        var path = Environment.GetEnvironmentVariable("COPPERSCREEN_COPPERSTART_RDB")!;
        var original = File.ReadAllBytes(path);
        var prepared = CopperScreenRdbImageProof.ForRestoredWrites(path);
        Assert.Equal(16, original.Zip(prepared).Count(pair => pair.First != pair.Second));
        Assert.Equal(original[..131072], prepared[..131072]);
        CopperScreenRdbImageProof.AssertDisk(prepared, prepared);
        Assert.Throws<InvalidDataException>(() => CopperScreenRdbImageProof.AssertDisk(prepared, prepared, "After"));
        Assert.Throws<InvalidDataException>(() => CopperScreenRdbImageProof.AssertDisk(original, prepared));
    }

    [Theory]
    [InlineData("--hdf-rdb")]
    [InlineData("--hdf-rdb-writable")]
    [InlineData("--hdf-ffs")]
    [InlineData("--hdf-ffs-writable")]
    public void OpeningSavedHardDiskProfilesNeverDuplicatesTheRememberedDrive(string option)
    {
        var directory = Path.Combine(Path.GetTempPath(), "copper-rdb-profile-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var arguments = new List<string> { "--copperstart", "--rom", "selected.rom", option, "disk.hdf" };
            if (option.Contains("ffs")) arguments.Add("DH0:2:32:4:58");
            var options = CopperScreenStartupOptions.Parse(arguments.ToArray(), AppContext.BaseDirectory, userDataDirectory: directory);
            var expected = Assert.Single(options.HardDrives);
            var draft = CopperScreenSettingsDraft.FromStartupOptions(options);
            for (var reopen = 0; reopen < 3; reopen++)
            {
                var profile = Path.Combine(directory, "profile.json"); CopperScreenProfileStore.SaveToPath(draft, profile);
                options = CopperScreenStartupOptions.Parse(["--profile", profile], AppContext.BaseDirectory, userDataDirectory: directory);
                Assert.Null(options.Error);
                draft = CopperScreenSettingsDraft.FromStartupOptions(options);
                Assert.Equal(expected, Assert.Single(draft.HardDrives));
                var recent = new CopperScreenRecentSession(directory); recent.Save(draft, 20, false, AppContext.BaseDirectory);
                Assert.Equal(expected, Assert.Single(recent.LoadMachine(AppContext.BaseDirectory)!.HardDrives));
            }
        }
        finally { Directory.Delete(directory, true); }
    }
    [RdbFixtureFact]
    public void InstalledInspectorDerivesTheQualifiedPristineImageAndChecksSavedVolumes()
    {
        var path = Environment.GetEnvironmentVariable("COPPERSCREEN_COPPERSTART_RDB")!;
        var pristine = CopperScreenRdbImageProof.Pristine(path);
        Assert.Equal(RdbFfsProbe.Pristine(), pristine);
        CopperScreenRdbImageProof.AssertDisk(pristine, File.ReadAllBytes(path));
        Assert.Equal(RdbFfsProbe.BootFromDh0(pristine), CopperScreenRdbImageProof.BootFromDh0(pristine));
    }
    [RdbFixtureFact]
    public void InstalledInspectorRejectsPayloadBitmapAndUnrelatedBlockCorruptionOnEitherPartition()
    {
        var path = Environment.GetEnvironmentVariable("COPPERSCREEN_COPPERSTART_RDB")!;
        var pristine = CopperScreenRdbImageProof.Pristine(path); var saved = File.ReadAllBytes(path);
        for (var part = 0; part < 2; part++)
        {
            var start = (4 + part * 55) * 32768;
            uint Word(uint block, int at) => CopperScreenRdbImageProof.Long(saved, start + (int)block * 512 + at);
            uint Find(uint directory, string name)
            {
                for (var slot = 0; slot < 72; slot++)
                    for (var block = Word(directory, 24 + slot * 4); block != 0; block = Word(block, 496))
                        if (System.Text.Encoding.ASCII.GetString(saved, start + (int)block * 512 + 433, saved[start + (int)block * 512 + 432]) == name) return block;
                throw new InvalidDataException("Missing fixture entry " + name);
            }
            var data = (int)Word(Find(Find(1760, "Data"), "Saved"), 308);
            foreach (var offset in new[] { start + data * 512, start + 1761 * 512 + 4, start + 3519 * 512, 512 })
            {
                var corrupt = saved.ToArray(); corrupt[offset] ^= 1;
                Assert.Throws<InvalidDataException>(() => CopperScreenRdbImageProof.AssertDisk(pristine, corrupt));
            }
        }
    }
    [Fact]
    public void MissingOrIncompleteDeviceRequestsCannotSatisfyFlushObservation()
    {
        Assert.False(CopperScreenRdbImageProof.HasCompletedFlush(new byte[524288], new byte[524288]));
    }
}
public sealed class RdbFixtureFactAttribute : FactAttribute
{
    public RdbFixtureFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COPPERSCREEN_COPPERSTART_RDB"))) Skip = "Qualified original RDB fixture not supplied; host inspector coverage unavailable.";
    }
}
