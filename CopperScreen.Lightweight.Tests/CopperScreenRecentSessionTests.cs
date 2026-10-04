using CopperScreen;

namespace CopperScreen.Tests;

public sealed class CopperScreenRecentSessionTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "CopperScreenRecentSessionTests", Guid.NewGuid().ToString("N"));

    public CopperScreenRecentSessionTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void SavedMachineAndHostPreferencesRoundTripWithoutChangingProfiles()
    {
        var disk = Path.Combine(_directory, "game.adf");
        File.WriteAllBytes(disk, []);
        var draft = new CopperScreenSettingsDraft { Id = "my-amiga", DisplayName = "My Amiga", ChipRamKb = 1024, PseudoFastRamKb = 0, FloppyDriveCount = 2, KickstartRomPath = Path.Combine(_directory, "kickstart.rom"), Input = CopperScreenInputOptions.Default.WithPortAssignment(2, "wasd-joystick") };
        draft.DriveDiskPaths[0] = disk;
        draft.DriveWriteProtected[0] = false;
        var session = new CopperScreenRecentSession(_directory);
        session.RememberDisk(disk);
        session.Save(draft, 37, true);
        var loadedSession = new CopperScreenRecentSession(_directory);
        var loaded = Assert.IsType<CopperScreenSettingsDraft>(loadedSession.LoadMachine(_directory));
        Assert.Equal(draft.Id, loaded.Id);
        Assert.Equal(draft.DisplayName, loaded.DisplayName);
        Assert.Equal(1024, loaded.ChipRamKb);
        Assert.Equal(2, loaded.FloppyDriveCount);
        Assert.Equal(draft.KickstartRomPath, loaded.KickstartRomPath);
        Assert.Equal(disk, loaded.DriveDiskPaths[0]);
        Assert.False(loaded.DriveWriteProtected[0]);
        Assert.Equal("wasd-joystick", loaded.Input.Port2ProfileId);
        Assert.Equal(37, loadedSession.Volume);
        Assert.True(loadedSession.Muted);
        Assert.Equal(new[] { disk }, loadedSession.RecentDisks);
        Assert.False(Directory.Exists(Path.Combine(_directory, "Profiles")));
    }

    [Fact]
    public void RemovedDiskDoesNotPreventLoadingTheLastMachine()
    {
        var draft = new CopperScreenSettingsDraft();
        draft.DriveDiskPaths[0] = Path.Combine(_directory, "removed.adf");
        var session = new CopperScreenRecentSession(_directory);
        session.Save(draft, 100, false);
        Assert.Null(Assert.IsType<CopperScreenSettingsDraft>(session.LoadMachine(_directory)).DriveDiskPaths[0]);
    }

    [Fact]
    public void RecentDisksKeepOrderRemoveDuplicatesAndStayBounded()
    {
        var session = new CopperScreenRecentSession(_directory);
        for (var i = 0; i < 12; i++) session.RememberDisk($"disk{i}.adf");
        session.RememberDisk("DISK7.ADF");
        session.Save(null, 100, false);
        var reloaded = new CopperScreenRecentSession(_directory);
        Assert.Equal(8, reloaded.RecentDisks.Count);
        Assert.Equal("DISK7.ADF", reloaded.RecentDisks[0]);
        Assert.Equal(session.RecentDisks, reloaded.RecentDisks);
        Assert.Null(reloaded.LoadMachine(_directory));
    }

    [Theory]
    [InlineData("broken json")]
    [InlineData("{\"Version\":99,\"Muted\":true,\"RecentDisks\":[\"game.adf\"]}")]
    public void CorruptOrUnknownHistoryFallsBackToFirstLaunch(string json)
    {
        File.WriteAllText(Path.Combine(_directory, "recent-session.json"), json);
        File.WriteAllText(Path.Combine(_directory, "last-machine.json"), "broken json");
        var session = new CopperScreenRecentSession(_directory);
        Assert.Equal(100, session.Volume);
        Assert.False(session.Muted);
        Assert.Empty(session.RecentDisks);
        Assert.Null(session.LoadMachine(_directory));
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
