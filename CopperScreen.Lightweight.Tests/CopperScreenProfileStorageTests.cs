using System.Security.AccessControl;
using System.Security.Principal;
using System.Runtime.Versioning;
using System.Text.Json.Nodes;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed class CopperScreenProfileStorageTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "CopperScreen profile storage å", Guid.NewGuid().ToString("N"));
    private string App => Path.Combine(_directory, "Application");
    private string User => Path.Combine(_directory, "User");
    private string Presets => Path.Combine(App, "Presets");
    private string Legacy => Path.Combine(App, "Profiles");

    public CopperScreenProfileStorageTests() => Directory.CreateDirectory(Presets);

    [Fact]
    public void UnpackagedProcessRetainsTheExistingPortableUserDataRoot()
    {
        if (OperatingSystem.IsWindows()) Assert.Null(CopperScreenUserData.CurrentPackageFamily());
        Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CopperScreen"),
            CopperScreenUserData.DefaultDirectory);
    }

    private string Preset(string id = CopperScreenProfile.DefaultProfileId, string name = "Bundled Amiga")
    {
        var path = Path.Combine(Presets, id + ".json");
        CopperScreenProfileStore.SaveToPath(new() { Id = id, DisplayName = name }, path);
        return path;
    }

    private string OldProfile(string id, string name = "Portable Amiga")
    {
        var path = Path.Combine(Legacy, id + ".json");
        CopperScreenProfileStore.SaveToPath(new() { Id = id, DisplayName = name }, path);
        return path;
    }

    [Fact]
    public void SavingPresetCreatesPersonalOverrideWhileOriginalRemainsSelectable()
    {
        var bundled = Preset();
        var original = File.ReadAllBytes(bundled);
        var draft = new CopperScreenSettingsDraft { DisplayName = "My Amiga", RtcEnabled = true };
        var saved = CopperScreenProfileStore.Save(draft, App, User);
        Assert.Equal(Path.Combine(User, "Profiles", draft.Id + ".json"), saved);
        Assert.Equal(saved, draft.SourcePath);
        Assert.Equal(original, File.ReadAllBytes(bundled));
        var profiles = CopperScreenProfileStore.ListProfiles(App, out var error, User);
        Assert.Null(error);
        Assert.Equal(2, profiles.Count);
        Assert.Contains(profiles, p => p.IsBundled && p.Path == bundled);
        Assert.Contains(profiles, p => !p.IsBundled && p.Path == saved);
        var selected = CopperScreenProfile.LoadDefault(App, out error, userDataDirectory: User);
        Assert.Null(error);
        Assert.True(selected.RtcEnabled);
        Assert.Equal("My Amiga", selected.DisplayName);
        Assert.True(CopperScreenProfile.TryLoad(bundled, App, out var preset, out error, User), error);
        Assert.False(preset.RtcEnabled);
        Assert.Equal(bundled, CopperScreenSettingsDraft.FromProfile(preset).Clone().SourcePath);
    }

    [Fact]
    public void UpgradeChangesBundledDefaultsWithoutChangingPersonalProfilesOrSettings()
    {
        Preset();
        var draft = new CopperScreenSettingsDraft { DisplayName = "Personal", RtcEnabled = true };
        var path = CopperScreenProfileStore.Save(draft, App, User);
        var personal = File.ReadAllBytes(path);
        var session = new CopperScreenRecentSession(User);
        session.Save(draft, 42, true);
        var upgrade = Path.Combine(_directory, "New application");
        Directory.CreateDirectory(Path.Combine(upgrade, "Presets"));
        CopperScreenProfileStore.SaveToPath(new() { DisplayName = "New default" }, Path.Combine(upgrade, "Presets", draft.Id + ".json"));
        var profiles = CopperScreenProfileStore.ListProfiles(upgrade, User);
        Assert.Contains(profiles, p => p.IsBundled && p.DisplayName == "New default");
        Assert.Contains(profiles, p => !p.IsBundled && p.DisplayName == "Personal");
        Assert.Equal(personal, File.ReadAllBytes(path));
        var reopened = new CopperScreenRecentSession(User);
        Assert.Equal(42, reopened.Volume);
        Assert.True(reopened.Muted);
        Assert.True(reopened.LoadMachine(upgrade)!.RtcEnabled);
    }

    [Fact]
    public void TwoUserDirectoriesHaveIndependentOverridesAndPreferences()
    {
        Preset();
        var other = Path.Combine(_directory, "Another user");
        CopperScreenProfileStore.Save(new() { DisplayName = "First user" }, App, User);
        var first = CopperScreenProfile.LoadDefault(App, out var error, userDataDirectory: User);
        Assert.Null(error);
        var second = CopperScreenProfile.LoadDefault(App, out error, userDataDirectory: other);
        Assert.Null(error);
        Assert.Equal("First user", first.DisplayName);
        Assert.Equal("Bundled Amiga", second.DisplayName);
        new CopperScreenRecentSession(User).Save(null, 24, true);
        Assert.Equal(100, new CopperScreenRecentSession(other).Volume);
        Assert.False(Directory.Exists(other));
    }

    [Fact]
    public void SaveCopyAvoidsBundledAndUserCollisionsWithoutChangingEither()
    {
        Preset("my-amiga-copy");
        var first = new CopperScreenSettingsDraft { Id = "my-amiga", DisplayName = "My Amiga" };
        var path = CopperScreenProfileStore.SaveAs(first, App, User);
        Assert.Equal("my-amiga-copy-2", first.Id);
        var original = File.ReadAllBytes(path);
        var second = new CopperScreenSettingsDraft { Id = "my-amiga", DisplayName = "Other" };
        CopperScreenProfileStore.SaveAs(second, App, User);
        Assert.Equal("my-amiga-copy-3", second.Id);
        Assert.Equal(original, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("..\\outside")]
    [InlineData("C:\\outside")]
    [InlineData("/outside")]
    [InlineData("..")]
    [InlineData("CON")]
    [InlineData("nul.json")]
    [InlineData("LPT1")]
    [InlineData("bad:name")]
    public void ProfileNamesCannotEscapeUserFolderOrUseWindowsDevices(string id)
    {
        var draft = new CopperScreenSettingsDraft { Id = id };
        Assert.Throws<ArgumentException>(() => CopperScreenProfileStore.Save(draft, App, User));
        Assert.Throws<ArgumentException>(() => CopperScreenProfileStore.SaveAs(draft, App, User));
        Assert.False(Directory.Exists(User));
        Assert.Equal(id, draft.Id);
    }

    [Fact]
    public void LegacyImportCopiesCustomProfilesPreservesUnknownFieldsAndFreezesRelativePaths()
    {
        var bundled = Preset();
        Directory.CreateDirectory(Legacy);
        File.Copy(bundled, Path.Combine(Legacy, Path.GetFileName(bundled)));
        var source = OldProfile("portable");
        var json = JsonNode.Parse(File.ReadAllText(source))!.AsObject();
        json["Kickstart"]!["Path"] = "ROM/test.rom";
        json["Workbench"] = new JsonObject { ["AutoStartStartupSequence"] = false };
        json["futureSetting"] = "keep me";
        File.WriteAllText(source, json.ToJsonString());
        var original = File.ReadAllBytes(source);
        Assert.Null(CopperScreenProfileStore.MigrateLegacyProfiles(App, User));
        var importedPath = Path.Combine(User, "Profiles", "portable.json");
        var imported = JsonNode.Parse(File.ReadAllText(importedPath))!;
        Assert.Equal("keep me", imported["futureSetting"]!.GetValue<string>());
        Assert.Equal(Path.Combine(App, "ROM", "test.rom"), imported["Kickstart"]!["Path"]!.GetValue<string>());
        Assert.Equal(original, File.ReadAllBytes(source));
        Assert.False(File.Exists(Path.Combine(User, "Profiles", CopperScreenProfile.DefaultProfileId + ".json")));
        Assert.True(CopperScreenProfile.TryLoad(importedPath, App, out var loaded, out var error, User), error);
        Assert.False(loaded.AutoStartWorkbenchStartupSequence);
        var contents = File.ReadAllBytes(importedPath);
        Assert.Null(CopperScreenProfileStore.MigrateLegacyProfiles(App, User));
        Assert.Equal(contents, File.ReadAllBytes(importedPath));
    }

    [Fact]
    public void LegacyImportPreservesExistingUserProfileAndKeepsConflictingSourceSeparately()
    {
        var source = OldProfile("same-id");
        var existing = CopperScreenProfileStore.Save(new() { Id = "same-id", DisplayName = "Already saved" }, App, User);
        var before = File.ReadAllBytes(existing);
        Assert.Null(CopperScreenProfileStore.MigrateLegacyProfiles(App, User));
        Assert.Equal(before, File.ReadAllBytes(existing));
        Assert.True(File.Exists(source));
        var imported = Assert.Single(CopperScreenProfileStore.ListProfiles(App, User), p => p.DisplayName == "Portable Amiga (imported)");
        Assert.NotEqual("same-id", imported.Id);
        Assert.Null(CopperScreenProfileStore.MigrateLegacyProfiles(App, User));
        Assert.Equal(2, CopperScreenProfileStore.ListProfiles(App, User).Count);
    }

    [Fact]
    public void BrokenLegacyProfileIsKeptAndCanBeRepairedForARetry()
    {
        OldProfile("good");
        var broken = Path.Combine(Legacy, "broken.json");
        File.WriteAllText(broken, "broken json");
        var error = CopperScreenProfileStore.MigrateLegacyProfiles(App, User);
        Assert.Contains("broken.json", error);
        Assert.True(File.Exists(Path.Combine(User, "Profiles", "good.json")));
        Assert.Equal("broken json", File.ReadAllText(broken));
        OldProfile("broken");
        Assert.Null(CopperScreenProfileStore.MigrateLegacyProfiles(App, User));
        Assert.Equal(2, CopperScreenProfileStore.ListProfiles(App, User).Count);
    }

    [Fact]
    public void InvalidUserJsonReportsAnErrorWithoutOverwritingItOrHidingPresets()
    {
        Preset();
        var path = Path.Combine(User, "Profiles", "broken.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "broken json");
        var profiles = CopperScreenProfileStore.ListProfiles(App, out var error, User);
        Assert.Single(profiles);
        Assert.True(profiles[0].IsBundled);
        Assert.Contains("broken.json", error);
        Assert.Equal("broken json", File.ReadAllText(path));
    }

    [Fact]
    public void MalformedStorageEntriesAreReportedWithoutCrashingTheProfileCatalog()
    {
        Preset();
        var path = CopperScreenProfileStore.Save(new() { Id = "bad-storage" }, App, User);
        var json = JsonNode.Parse(File.ReadAllText(path))!;
        json["HardDrives"] = new JsonArray((JsonNode?)null);
        File.WriteAllText(path, json.ToJsonString());
        var profiles = CopperScreenProfileStore.ListProfiles(App, out var error, User);
        Assert.Single(profiles);
        Assert.Contains("storage entries cannot be null", error);
        Assert.True(profiles[0].IsBundled);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(false, false)]
    public void ImportedProfileProtectsHardfilesUnlessWritePermissionWasSaved(bool? writableSetting, bool expectedReadOnly)
    {
        var path = CopperScreenProfileStore.Save(new() { Id = "storage-default" }, App, User);
        var json = JsonNode.Parse(File.ReadAllText(path))!;
        var drive = new JsonObject { ["Path"] = Path.Combine(User, "original.hdf") };
        if (writableSetting.HasValue) drive["ReadOnly"] = writableSetting.Value;
        json["HardDrives"] = new JsonArray(drive); File.WriteAllText(path, json.ToJsonString());
        Assert.True(CopperScreenProfile.TryLoad(path, App, out var profile, out var error, User), error);
        Assert.Equal(expectedReadOnly, Assert.Single(profile.HardDrives).ReadOnly);
    }

    [Fact]
    public void SavedProfilePathsStayAbsoluteWhenTheApplicationMoves()
    {
        var draft = new CopperScreenSettingsDraft { Id = "paths", KickstartRomPath = "ROM/test.rom" };
        draft.DriveDiskPaths[0] = "Disks/game.adf";
        var path = CopperScreenProfileStore.Save(draft, App, User);
        Assert.True(CopperScreenProfile.TryLoad(path, App, out var profile, out var error, User), error);
        Assert.Equal(Path.Combine(App, "ROM", "test.rom"), profile.KickstartRomPath);
        Assert.Equal(Path.Combine(App, "Disks", "game.adf"), profile.MediaDrives[0].DiskPath);
        var recent = new CopperScreenRecentSession(User);
        recent.Save(draft, 80, false, App);
        var movedApp = Path.Combine(_directory, "Updated app");
        Assert.Equal(profile.KickstartRomPath, recent.LoadMachine(movedApp)!.KickstartRomPath);
    }

    [Theory]
    [InlineData("personal")]
    [InlineData("personal.json")]
    public void NamedProfileResolutionIncludesUserProfiles(string specifier)
    {
        CopperScreenProfileStore.Save(new() { Id = "personal" }, App, User);
        Assert.True(CopperScreenProfile.TryLoad(specifier, App, out var profile, out var error, User), error);
        Assert.Equal(Path.Combine(User, "Profiles", "personal.json"), profile.ConfigPath);
    }

    [Fact]
    public void InterruptedTemporaryFilesAndInvalidSavesLeavePreviousProfileUsable()
    {
        var draft = new CopperScreenSettingsDraft { Id = "saved" };
        var path = CopperScreenProfileStore.Save(draft, App, User);
        var original = File.ReadAllBytes(path);
        File.WriteAllText(path + ".interrupted.tmp", "partial json");
        draft.FloppyDriveCount = 5;
        Assert.Throws<InvalidOperationException>(() => CopperScreenProfileStore.Save(draft, App, User));
        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.Single(CopperScreenProfileStore.ListProfiles(App, User));
        Assert.True(CopperScreenProfile.TryLoad(path, App, out var profile, out var error, User), error);
        Assert.Equal(1, profile.FloppyDriveCount);
    }

    [Fact]
    public void UnavailableUserDirectoryFailsClearlyAndDoesNotWriteIntoApplication()
    {
        var preset = Preset();
        var before = File.ReadAllBytes(preset);
        File.WriteAllText(User, "not a directory");
        var draft = new CopperScreenSettingsDraft();
        Assert.Throws<IOException>(() => CopperScreenProfileStore.Save(draft, App, User));
        var profiles = CopperScreenProfileStore.ListProfiles(App, out var error, User);
        Assert.Single(profiles);
        Assert.NotNull(error);
        var recent = new CopperScreenRecentSession(User);
        recent.Save(draft, 51, false);
        Assert.NotNull(recent.LastError);
        Assert.Equal(before, File.ReadAllBytes(preset));
        Assert.False(File.Exists(Path.Combine(App, "last-machine.json")));
    }

    [WindowsFact]
    [SupportedOSPlatform("windows")]
    public void ReadOnlyApplicationDirectoryStillAllowsProfileAndSettingsPersistence()
    {
        var preset = Preset();
        var before = File.ReadAllBytes(preset);
        var info = new DirectoryInfo(App);
        var originalAcl = info.GetAccessControl();
        var denied = info.GetAccessControl();
        denied.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!,
            FileSystemRights.Write | FileSystemRights.Delete | FileSystemRights.DeleteSubdirectoriesAndFiles,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Deny));
        info.SetAccessControl(denied);
        try
        {
            Assert.Throws<UnauthorizedAccessException>(() => File.WriteAllText(Path.Combine(App, "forbidden.txt"), "test"));
            var path = CopperScreenProfileStore.Save(new() { DisplayName = "Read-only install" }, App, User);
            var recent = new CopperScreenRecentSession(User);
            recent.Save(new(), 33, true);
            Assert.Null(recent.LastError);
            Assert.Equal("Read-only install", CopperScreenProfile.LoadDefault(App, out var error, userDataDirectory: User).DisplayName);
            Assert.Null(error);
            Assert.Equal(33, new CopperScreenRecentSession(User).Volume);
            Assert.True(File.Exists(path));
            Assert.Equal(before, File.ReadAllBytes(preset));
        }
        finally
        {
            originalAcl.SetSecurityDescriptorBinaryForm(originalAcl.GetSecurityDescriptorBinaryForm(), AccessControlSections.Access);
            info.SetAccessControl(originalAcl);
        }
    }

    [WindowsFact]
    [SupportedOSPlatform("windows")]
    public void DeniedUserWritesPreserveSavedProfilesAndReportRecoverableErrors()
    {
        var path = CopperScreenProfileStore.Save(new() { Id = "saved", DisplayName = "Keep me" }, App, User);
        var before = File.ReadAllBytes(path);
        var info = new DirectoryInfo(User);
        var original = info.GetAccessControl();
        var denied = info.GetAccessControl();
        denied.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.Write,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Deny));
        info.SetAccessControl(denied);
        try
        {
            var draft = new CopperScreenSettingsDraft { Id = "saved", DisplayName = "Changed" };
            Assert.Throws<UnauthorizedAccessException>(() => CopperScreenProfileStore.Save(draft, App, User));
            Assert.Throws<UnauthorizedAccessException>(() => CopperScreenProfileStore.SaveAs(draft, App, User));
            Assert.Equal("saved", draft.Id);
            Assert.Equal(before, File.ReadAllBytes(path));
            var recent = new CopperScreenRecentSession(User);
            recent.Save(draft, 50, false);
            Assert.Contains("permissions", recent.LastError);
        }
        finally
        {
            original.SetSecurityDescriptorBinaryForm(original.GetSecurityDescriptorBinaryForm(), AccessControlSections.Access);
            info.SetAccessControl(original);
        }
        Assert.Equal("Keep me", CopperScreenProfileStore.ListProfiles(App, User)[0].DisplayName);
    }

    private sealed class WindowsFactAttribute : FactAttribute
    {
        public WindowsFactAttribute()
        {
            if (!OperatingSystem.IsWindows()) Skip = "Requires Windows directory ACLs; unavailable on other hosts.";
        }
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
