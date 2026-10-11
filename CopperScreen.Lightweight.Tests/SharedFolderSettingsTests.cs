using CopperMod.Amiga.Lightweight;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed class SharedFolderSettingsTests
{
    [Fact]
    public void SharedFoldersSurviveSaveLoadCloneAndRestartComparison()
    {
        var directory = Path.Combine(Path.GetTempPath(), "copperfs-profile-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            var draft = new CopperScreenSettingsDraft(); var baseline = draft.Clone();
            draft.HostDirectories.Add(new("DH0", "Workbench", "SystemFolder", false, 0));
            draft.HostDirectories.Add(new("HOST0", "Exchange", "ExchangeFolder"));
            var clone = draft.Clone(); Assert.Equal(draft.HostDirectories, clone.HostDirectories); Assert.True(clone.NeedsRestartComparedWith(baseline));
            var path = Path.Combine(directory, "test.json"); CopperScreenProfileStore.SaveToPath(clone, path);
            Assert.True(CopperScreenProfile.TryLoad(path, directory, out var profile, out var error), error);
            Assert.Equal(clone.HostDirectories, profile.HostDirectories);
            var options = CopperScreenSettingsDraft.FromProfile(profile).ToStartupOptions(directory);
            Assert.Equal(Path.Combine(directory, "SystemFolder"), options.HostDirectories[0].Path);
            Assert.Equal(options.HostDirectories, CopperScreenSettingsDraft.FromStartupOptions(options).HostDirectories);
            Assert.Contains("hostDirectories", System.IO.File.ReadAllText(path));
            var overridden = CopperScreenStartupOptions.Parse(["--host-dir-boot-priority", "HOST0", "7", "--profile", path], directory);
            Assert.Null(overridden.Error); Assert.Equal(7, overridden.HostDirectories.Single(m => m.DeviceName == "HOST0").BootPriority);
            var document = System.Text.Json.Nodes.JsonNode.Parse(System.IO.File.ReadAllText(path))!;
            var entry = document["hostDirectories"]![0]!.AsObject(); entry.Remove("ReadOnly"); entry.Remove("BootPriority");
            System.IO.File.WriteAllText(path, document.ToJsonString());
            Assert.True(CopperScreenProfile.TryLoad(path, directory, out var defaults, out error), error);
            Assert.True(defaults.HostDirectories[0].ReadOnly); Assert.Null(defaults.HostDirectories[0].BootPriority);
        }
        finally { Directory.Delete(directory, true); }
    }
    [Fact]
    public void CommandLineMountsOverrideProfilesAndRetainProtectionAndBootPriority()
    {
        var options = CopperScreenStartupOptions.Parse(["--host-dir-rw", "DH0", "Workbench", "SystemFolder", "--host-dir-boot-priority", "DH0", "-5", "--host-dir", "HOST0", "Exchange", "ExchangeFolder"], AppContext.BaseDirectory);
        Assert.Null(options.Error); Assert.Equal(2, options.HostDirectories.Count);
        Assert.False(options.HostDirectories[0].ReadOnly); Assert.Equal(-5, options.HostDirectories[0].BootPriority);
        Assert.True(options.HostDirectories[1].ReadOnly); Assert.Null(options.HostDirectories[1].BootPriority);
        Assert.True(Path.IsPathFullyQualified(options.HostDirectories[0].Path));
        Assert.NotNull(CopperScreenStartupOptions.Parse(["--host-dir"], AppContext.BaseDirectory).Error);
        Assert.NotNull(CopperScreenStartupOptions.Parse(["--host-dir", "DF0", "Exchange", "x"], AppContext.BaseDirectory).Error);
        Assert.NotNull(CopperScreenStartupOptions.Parse(["--host-dir-boot-priority", "Missing", "0"], AppContext.BaseDirectory).Error);
        Assert.NotNull(CopperScreenStartupOptions.Parse(["--host-dir", "HOST0", "One", "x", "--host-dir", "host0", "Two", "y"], AppContext.BaseDirectory).Error);
        Assert.Null(CopperScreenStartupOptions.Parse(["--host-dir-boot-priority", "HOST0", "0", "--host-dir", "HOST0", "Exchange", "x"], AppContext.BaseDirectory).Error);
    }
    [Fact]
    public void MinimalDiskBootCannotUseSharedFolders()
    {
        var draft = new CopperScreenSettingsDraft { KickstartSource = CopperScreenKickstartSource.MinimalDiskBoot };
        draft.HostDirectories.Add(new("HOST0", "Exchange", AppContext.BaseDirectory));
        var error = Assert.Throws<NotSupportedException>(() => CopperScreenLightweightSession.Validate(draft.ToStartupOptions(AppContext.BaseDirectory), requireRomPath: false));
        Assert.Contains("Minimal disk boot", error.Message);
    }
}
