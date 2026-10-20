using System.Buffers.Binary;
using System.Text.Json.Nodes;
using CopperMod.Amiga.Lightweight;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed class CopperScreenHdDriveSettingsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "copperscreen-hd-" + Guid.NewGuid().ToString("N"));
    private readonly string _rom;
    public CopperScreenHdDriveSettingsTests()
    {
        Directory.CreateDirectory(_directory);
        _rom = Path.Combine(_directory, "fixture.rom");
        var bytes = new byte[262144];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, 0x70000);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(4), 0xFC0100);
        bytes[13] = 34; bytes[0x100] = 0x60; bytes[0x101] = 0xFE;
        File.WriteAllBytes(_rom, bytes); // Host settings/session fixture, not native ROM evidence.
    }

    private CopperScreenSettingsDraft Draft() => CopperScreenSettingsDraft.FromStartupOptions(
        CopperScreenStartupOptions.Parse(["--profile", "lightweight-a500-kickstart13", "--rom", _rom], AppContext.BaseDirectory));

    [Fact]
    public void ProfilesClonesAndLastMachineRetainTypesIncludingDisconnectedDrives()
    {
        var initial = Draft();
        var draft = initial.Clone();
        draft.FloppyDriveTypes[0] = LightweightFloppyDriveType.HighDensity;
        draft.FloppyDriveTypes[3] = LightweightFloppyDriveType.HighDensity;
        Assert.True(draft.NeedsRestartComparedWith(initial));
        Assert.All(initial.FloppyDriveTypes, type => Assert.Equal(LightweightFloppyDriveType.DoubleDensity, type));
        var clone = draft.Clone();
        Assert.Equal(draft.FloppyDriveTypes, clone.FloppyDriveTypes);
        Assert.False(draft.NeedsRestartComparedWith(clone));
        clone.FloppyDriveTypes[3] = LightweightFloppyDriveType.DoubleDensity;
        Assert.Equal(LightweightFloppyDriveType.HighDensity, draft.FloppyDriveTypes[3]);
        var path = Path.Combine(_directory, "profile.json");
        CopperScreenProfileStore.SaveToPath(draft, path);
        Assert.True(CopperScreenProfile.TryLoad(path, _directory, out var loaded, out var error), error);
        Assert.Equal(draft.FloppyDriveTypes, loaded.FloppyDriveTypes);
        var recent = new CopperScreenRecentSession(_directory);
        recent.Save(draft, 100, false);
        var restored = new CopperScreenRecentSession(_directory).LoadMachine(_directory)!;
        Assert.Equal(1, restored.FloppyDriveCount);
        Assert.Equal(draft.FloppyDriveTypes, restored.FloppyDriveTypes);
        restored.FloppyDriveCount = 4;
        using var session = new CopperScreenLightweightSession(restored.ToStartupOptions(_directory));
        session.Machine.MountAdf(3, new byte[1802240]);
        Assert.Equal(1802240, session.Machine.ExportAdf(3).Length);
    }

    [Fact]
    public void OlderProfilesDefaultToDdAndPartialTypeListsDefaultRemainingDrives()
    {
        var path = Path.Combine(_directory, "older.json");
        CopperScreenProfileStore.SaveToPath(Draft(), path);
        var json = JsonNode.Parse(File.ReadAllText(path))!;
        json["Machine"]!.AsObject().Remove("FloppyDriveTypes");
        File.WriteAllText(path, json.ToJsonString());
        Assert.True(CopperScreenProfile.TryLoad(path, _directory, out var older, out var error), error);
        Assert.All(older.FloppyDriveTypes, type => Assert.Equal(LightweightFloppyDriveType.DoubleDensity, type));
        json["Machine"]!["FloppyDriveTypes"] = new JsonArray("hd");
        File.WriteAllText(path, json.ToJsonString());
        Assert.True(CopperScreenProfile.TryLoad(path, _directory, out var partial, out error), error);
        Assert.Equal(LightweightFloppyDriveType.HighDensity, partial.FloppyDriveTypes[0]);
        Assert.All(partial.FloppyDriveTypes.Skip(1), type => Assert.Equal(LightweightFloppyDriveType.DoubleDensity, type));
    }

    [Theory]
    [InlineData("[\"unknown\"]")]
    [InlineData("[\"HD\",\"DD\",\"DD\",\"DD\",\"HD\"]")]
    [InlineData("[null]")]
    public void InvalidSavedTypesAreReported(string types)
    {
        var path = Path.Combine(_directory, "invalid.json");
        CopperScreenProfileStore.SaveToPath(Draft(), path);
        var json = JsonNode.Parse(File.ReadAllText(path))!;
        json["Machine"]!["FloppyDriveTypes"] = JsonNode.Parse(types);
        File.WriteAllText(path, json.ToJsonString());
        Assert.False(CopperScreenProfile.TryLoad(path, _directory, out _, out var error));
        Assert.Contains("floppyDriveTypes", error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DdDriveRejectsHdInsertionBeforeEjectingItsCurrentMedium(bool delayed)
    {
        using var session = new CopperScreenLightweightSession(Draft().ToStartupOptions(_directory));
        var dd = new byte[901120]; dd[42] = 37;
        Assert.True(session.InsertLoadedDisk(0, "dd.adf", CopperScreenAdfImage.FromAdfBytes(dd, "dd.adf"), false));
        Assert.False(session.InsertLoadedDisk(0, "hd.adf", CopperScreenAdfImage.FromAdfBytes(new byte[1802240], "hd.adf"), delayed));
        Assert.Equal(dd, session.Machine.ExportAdf());
        Assert.Null(session.FaultMessage);
    }

    [Fact]
    public void StartupAndRestartApplyEachSelectedDriveType()
    {
        var draft = Draft(); draft.FloppyDriveCount = 2;
        draft.FloppyDriveTypes[1] = LightweightFloppyDriveType.HighDensity;
        var hd = Path.Combine(_directory, "hd.adf"); File.WriteAllBytes(hd, new byte[1802240]);
        draft.DriveDiskPaths[1] = hd;
        using var previous = new CopperScreenLightweightSession(draft.ToStartupOptions(_directory));
        Assert.Equal(1802240, previous.Machine.ExportAdf(1).Length);
        Assert.Throws<ArgumentException>(() => previous.Machine.MountAdf(0, new byte[1802240]));
        previous.TogglePaused();
        draft.FloppyDriveTypes[0] = LightweightFloppyDriveType.HighDensity;
        draft.DriveDiskPaths[0] = hd;
        using var restarted = new CopperScreenLightweightSession(draft.ToStartupOptions(_directory), previous);
        Assert.Equal(1802240, restarted.Machine.ExportAdf(0).Length);
        Assert.Equal(1802240, restarted.Machine.ExportAdf(1).Length);
        Assert.Throws<ArgumentException>(() => previous.Machine.MountAdf(0, new byte[1802240]));
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
