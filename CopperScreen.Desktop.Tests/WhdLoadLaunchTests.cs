using System.Buffers.Binary;
using System.Text;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using CopperDisk.FileSystem;

namespace CopperScreen.Tests;

public sealed class WhdLoadLaunchTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("CopperWhdTest-");
    public void Dispose() => _root.Delete(recursive: true);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreparesBothInputsAsIsolatedGuestLaunchesWithPersistentSaves(bool archive)
    {
        var setup = NewSetup();
        var data = Path.Combine(_root.FullName, "User");
        var title = NewGame(archive);
        var original = File.ReadAllBytes(title);
        var baseline = new CopperScreenSettingsDraft();
        baseline.HardDrives.Add(new(0, "existing.hdf", true, 0));
        string saveFolder, system;
        using (var launch = await CopperScreenWhdLoadLaunch.OpenAsync(title))
        {
            if (archive) Assert.Equal(2, launch.Slaves.Count);
            var selected = Assert.Single(launch.Slaves, path => Path.GetFileName(path) == "A title.slave");
            var draft = await launch.PrepareAsync(baseline, setup, selected, "Data", data);
            Assert.Equal(M68kBackendKind.AccurateM68010, draft.CpuBackend);
            Assert.Equal(8192, draft.RealFastRamKb);
            Assert.Empty(draft.HardDrives);
            Assert.Single(baseline.HardDrives);
            var boot = Assert.Single(draft.HostDirectories, mount => mount.BootPriority.HasValue);
            system = boot.Path;
            var script = File.ReadAllText(Path.Combine(system, "S", "Startup-Sequence"));
            Assert.Contains("CD GAME0:\nSYS:C/WHDLoad Slave=\"A title.slave\" Data=\"Data\"", script);
            Assert.Contains("SavePath=SAVE0: SaveDir=Game", script);
            Assert.Contains("QuitKey=88", script);
            Assert.DoesNotContain("OLD-STARTUP", script);
            Assert.Equal(FakeLoader, File.ReadAllBytes(Path.Combine(system, "C", "WHDLoad")));
            var game = Assert.Single(draft.HostDirectories, mount => mount.DeviceName == "GAME0");
            Assert.True(game.ReadOnly);
            Assert.Equal(new byte[] { 42 }, File.ReadAllBytes(Path.Combine(game.Path, "Data", "Disk.1")));
            saveFolder = Assert.Single(draft.HostDirectories, mount => mount.DeviceName == "SAVE0").Path;
            File.WriteAllText(Path.Combine(saveFolder, "persist"), "saved");
        }
        Assert.False(Directory.Exists(system));
        Assert.Equal(original, File.ReadAllBytes(title));
        using var reopened = await CopperScreenWhdLoadLaunch.OpenAsync(title);
        var replay = await reopened.PrepareAsync(baseline, setup, reopened.Slaves[0], null, data);
        Assert.Equal(saveFolder, replay.HostDirectories.Single(m => m.DeviceName == "SAVE0").Path);
        Assert.Equal("saved", File.ReadAllText(Path.Combine(saveFolder, "persist")));
    }

    [Fact]
    public async Task ExtractionRejectsAbsolutePathsAndKeepsRelativePathsInsideStaging()
    {
        var archive = Path.Combine(_root.FullName, "title.lha");
        WriteLha(archive, ("../outside.slave", FakeSlave));
        var staged = Directory.CreateDirectory(Path.Combine(_root.FullName, "Stage")).FullName;
        var files = await CopperScreenLhaArchive.ExtractAsync(archive, staged, default);
        Assert.Equal(Path.Combine(staged, "outside.slave"), Assert.Single(files));
        Assert.False(File.Exists(Path.Combine(_root.FullName, "outside.slave")));
        WriteLha(archive, ("/absolute.slave", FakeSlave));
        await Assert.ThrowsAnyAsync<IOException>(() => CopperScreenWhdLoadLaunch.OpenAsync(archive));
    }

    [Fact]
    public async Task OnlyOneLaunchCanWriteTheSameTitleSaves()
    {
        var setup = NewSetup();
        var title = NewGame(archive: false);
        var current = new CopperScreenSettingsDraft();
        using var first = await CopperScreenWhdLoadLaunch.OpenAsync(title);
        using var second = await CopperScreenWhdLoadLaunch.OpenAsync(title);
        _ = await first.PrepareAsync(current, setup, first.Slaves[0], null, _root.FullName);
        await Assert.ThrowsAnyAsync<IOException>(() => second.PrepareAsync(current, setup, second.Slaves[0], null, _root.FullName));
    }

    private static byte[] FakeLoader => [0, 0, 3, 0xf3, 0, 0, 0, 0];
    private static byte[] FakeSlave => [.. FakeLoader, .. "WHDLOADS"u8];

    private CopperScreenWhdLoadSetup NewSetup()
    {
        var rom = Path.Combine(_root.FullName, "native-test.rom");
        var romBytes = new byte[524288];
        BinaryPrimitives.WriteUInt16BigEndian(romBytes.AsSpan(12), 40);
        File.WriteAllBytes(rom, romBytes);
        var workbench = Path.Combine(_root.FullName, "workbench-test.adf");
        using (var disk = File.Create(workbench))
        {
            disk.SetLength(901120);
            CopperScreenFileSystemFormatter.Format(disk, 0, disk.Length, CopperScreenHardDiskSetup.FileSystem.Ffs, "Test", default);
        }
        var commands = Directory.CreateDirectory(Path.Combine(_root.FullName, "C")).FullName;
        foreach (var command in new[] { "Assign", "MakeDir", "LoadWB" }) File.WriteAllBytes(Path.Combine(commands, command), FakeLoader);
        var scripts = Directory.CreateDirectory(Path.Combine(_root.FullName, "S")).FullName;
        File.WriteAllText(Path.Combine(scripts, "Startup-Sequence"), "OLD-STARTUP");
        using (var image = DiskImageSession.Open(workbench))
        {
            var volume = Assert.Single(image.Volumes);
            volume.Mount();
            image.Import(volume, volume.Root, [commands, scripts]);
        }
        var runtime = Path.Combine(_root.FullName, "WHDLoad_usr.lha");
        WriteLha(runtime, ("WHDLoad/C/WHDLoad", FakeLoader), ("WHDLoad/C/WHDLoad.VFS", FakeLoader));
        return new(rom, workbench, runtime, "", false);
    }

    private string NewGame(bool archive)
    {
        if (archive)
        {
            var path = Path.Combine(_root.FullName, "game.lha");
            WriteLha(path, ("Game/A title.slave", FakeSlave), ("Game/B alternate.slave", FakeSlave), ("Game/Data/Disk.1", [42]));
            return path;
        }
        var folder = Directory.CreateDirectory(Path.Combine(_root.FullName, "Game", "Data")).Parent!.FullName;
        var slave = Path.Combine(folder, "A title.slave");
        File.WriteAllBytes(slave, FakeSlave);
        File.WriteAllBytes(Path.Combine(folder, "Data", "Disk.1"), [42]);
        return slave;
    }

    private static void WriteLha(string path, params (string Name, byte[] Data)[] files)
    {
        using var output = File.Create(path);
        foreach (var (name, data) in files)
        {
            var filename = Encoding.ASCII.GetBytes(name);
            var header = new byte[filename.Length + 24];
            header[0] = (byte)(header.Length - 2);
            "-lh0-"u8.CopyTo(header.AsSpan(2));
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(7), data.Length);
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(11), data.Length);
            header[19] = 0x20;
            header[21] = (byte)filename.Length;
            filename.CopyTo(header, 22);
            ushort crc = 0;
            foreach (var value in data)
            {
                crc ^= value;
                for (var bit = 0; bit < 8; bit++) crc = (ushort)((crc >> 1) ^ ((crc & 1) != 0 ? 0xa001 : 0));
            }
            BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(header.Length - 2), crc);
            header[1] = unchecked((byte)header.Skip(2).Sum(value => value));
            output.Write(header);
            output.Write(data);
        }
        output.WriteByte(0);
    }
}

public sealed partial class LaunchWorkflowTests
{
    [Fact]
    public Task OpeningAnLhaAtStartupGoesToTheWhdLoadLauncher() => UiAsync(async () =>
    {
        var title = Path.Combine(_root, "Game.lha");
        var window = new MainWindow([title], new CopperScreenRecentSession(_root));
        window.Show();
        try
        {
            await Until(() => window.OwnedWindows.Any(w => w.Title == "Open WHDLoad game"));
            var dialog = window.OwnedWindows.Single(w => w.Title == "Open WHDLoad game");
            Assert.Equal(title, SettingsNamed<TextBox>(dialog, "Game file").Text);
            Click(dialog.GetLogicalDescendants().OfType<Button>().Single(b => b.Content is "Cancel"));
            Assert.Empty(MenuDraft(window).HostDirectories);
        }
        finally { window.Close(); }
    });
}
