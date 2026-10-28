using System.Buffers.Binary;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed partial class LaunchWorkflowTests
{
    private CopperScreenSettingsDraft LegacyBundledShell()
    {
        var draft = CopperScreenBundledCopperOs.Prepare(CopperScreenLaunchWorkflow.SelectStartChoice(new(), 3), _root);
        var directory = Path.Combine(_root, "CopperOS", "A1200");
        Directory.CreateDirectory(directory);
        var image = Path.Combine(directory, "CopperOS.hdf");
        File.Copy(draft.HardDrives[0].Path, image);
        // Include a user edit. Migration must not depend on pristine disk bytes.
        using (var file = File.OpenWrite(image)) { file.Position = file.Length - 1; file.WriteByte(0x5A); }
        File.WriteAllText(Path.Combine(directory, "seed.json"), """
            {"SeedId":"copperos-a1200-hdf-2026-10-22","Bytes":8388608,"Sha256":"54c6f78a7480437069b66a5f913c5ccde76fc3e604babeb1dc22674360242175"}
            """);
        draft.HardDrives[0] = draft.HardDrives[0] with { Path = image };
        return draft;
    }

    [Fact]
    public Task BundledShellUpgradesOnReopenAndReselectionWithoutChangingOldDisk() => Ui(() =>
    {
        var legacy = LegacyBundledShell();
        var oldPath = legacy.HardDrives[0].Path;
        var original = File.ReadAllBytes(oldPath);
        var recent = new CopperScreenRecentSession(_root);
        recent.Save(legacy, 50, false, AppContext.BaseDirectory);
        var window = new MainWindow([], new CopperScreenRecentSession(_root)); window.Show();
        try
        {
            var image = MenuDraft(window).HardDrives.Single().Path;
            Assert.NotEqual(oldPath, image);
            Assert.Contains("A1200-Desktop", image);
            Named<ComboBox>(window, "Start configuration").SelectedIndex = 0;
            Named<ComboBox>(window, "Start configuration").SelectedIndex = 3;
            Assert.Equal(image, MenuDraft(window).HardDrives.Single().Path);
            Assert.Equal(original, File.ReadAllBytes(oldPath));
            Assert.True(Button(window, "Start CopperOS").IsEnabled);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void BundledMigrationLeavesCustomDisksAndUnmarkedWorkspacesAlone()
    {
        var draft = LegacyBundledShell();
        File.Delete(Path.Combine(_root, "CopperOS", "A1200", "seed.json"));
        Assert.Same(draft, CopperScreenBundledCopperOs.PrepareForLaunch(draft, _root));
        draft.HardDrives[0] = draft.HardDrives[0] with { Path = Path.Combine(_root, "Custom.hdf"), ReadOnly = true };
        Assert.Same(draft, CopperScreenBundledCopperOs.PrepareForLaunch(draft, _root));
        Assert.True(draft.HardDrives[0].ReadOnly);
    }

    [Fact]
    public Task BundledWorkspaceSurvivesReselectionAndNewWindow() => Ui(() =>
    {
        var window = new MainWindow([], new CopperScreenRecentSession(_root)); window.Show();
        string image;
        try
        {
            Named<ComboBox>(window, "Start configuration").SelectedIndex = 3;
            var draft = MenuDraft(window);
            image = Assert.Single(draft.HardDrives).Path;
            Assert.NotEqual(Path.GetDirectoryName(typeof(MainWindow).Assembly.Location), Path.GetDirectoryName(image));
            // Simulate saved data in an unused block. Preparing the workspace
            // again must not reset a disk that no longer matches its seed.
            using (var stream = File.OpenWrite(image)) { stream.Position = stream.Length - 1; stream.WriteByte(0x5A); }
            var expected = File.ReadAllBytes(image);
            Named<ComboBox>(window, "Start configuration").SelectedIndex = 0;
            Assert.True(Button(window, "Choose ROM…").IsEffectivelyVisible);
            Named<ComboBox>(window, "Start configuration").SelectedIndex = 3;
            Assert.Equal(image, Assert.Single(MenuDraft(window).HardDrives).Path);
            Assert.Equal(expected, File.ReadAllBytes(image));
            new CopperScreenRecentSession(_root).Save(MenuDraft(window), 50, false, AppContext.BaseDirectory);
        }
        finally { window.Close(); }
        var reopened = new MainWindow([], new CopperScreenRecentSession(_root)); reopened.Show();
        try
        {
            Assert.Equal(3, Named<ComboBox>(reopened, "Start configuration").SelectedIndex);
            Assert.True(Button(reopened, "Start CopperOS").IsEnabled);
            Assert.Equal(image, Assert.Single(MenuDraft(reopened).HardDrives).Path);
            Assert.Equal(0x5A, File.ReadAllBytes(image)[^1]);
        }
        finally { reopened.Close(); }
    });

    [Fact]
    public void BundledFirmwareUpgradePreservesEditedDiskAndWriteProtection()
    {
        var draft = CopperScreenBundledCopperOs.Prepare(CopperScreenLaunchWorkflow.SelectStartChoice(new(), 3), _root);
        var image = draft.HardDrives[0].Path;
        using (var stream = File.OpenWrite(image)) { stream.Position = stream.Length - 1; stream.WriteByte(0x5A); }
        var bytes = File.ReadAllBytes(image);
        draft.HardDrives[0] = draft.HardDrives[0] with { ReadOnly = true };
        var disk = draft.HardDrives[0];
        draft.KickstartRomPath = Path.Combine(_root, "previous-version.rom");
        File.WriteAllBytes(draft.KickstartRomPath, [0]);
        var updated = CopperScreenBundledCopperOs.PrepareForLaunch(draft, _root);
        Assert.NotEqual(draft.KickstartRomPath, updated.KickstartRomPath);
        Assert.Equal(1048576, CopperScreenCopperStartRom.Read(updated.KickstartRomPath, a1200: true).Length);
        Assert.Equal(disk, updated.HardDrives.Single());
        Assert.Equal(bytes, File.ReadAllBytes(image));
        Assert.Equal(new byte[] { 0 }, File.ReadAllBytes(draft.KickstartRomPath));
    }

    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)]
    [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)]
    public void PreviousDesktopMarkerUpgradesFirmwareAndRespectsDiskWriteProtection(bool readOnly, int generation)
    {
        var draft = CopperScreenBundledCopperOs.Prepare(CopperScreenLaunchWorkflow.SelectStartChoice(new(), 3), _root);
        var image = draft.HardDrives[0].Path; var bytes = File.ReadAllBytes(image);
        draft.HardDrives[0] = draft.HardDrives[0] with { ReadOnly = readOnly };
        var directory = Path.GetDirectoryName(image)!;
        var marker = Path.Combine(directory, "seed.json");
        var previous = generation switch
        {
            0 => """{"SeedId":"copperos-a1200-desktop-2026-10-25","Bytes":8388608,"Sha256":"e113a115bc2518014b79526f00e07c8bccacac3dc2537f30734115f4085f82ce"}""",
            1 => """{"SeedId":"copperos-a1200-desktop-2026-10-25-blitter.3","Bytes":8388608,"Sha256":"11f825cd5ec50786b9779449c1eefd1a2088a9bab502c2be326db94cbc968236"}""",
            _ => """{"SeedId":"copperos-a1200-desktop-2026-10-25-text.4","Bytes":8388608,"Sha256":"0eca0b4e67b3a1c0c16528d2543f01d436a5e10cf39cd1e542236a83225fed15"}"""
        };
        File.WriteAllText(marker, previous); draft.KickstartRomPath = Path.Combine(_root, "older-firmware.rom");
        var updated = CopperScreenBundledCopperOs.PrepareForLaunch(draft, _root);
        Assert.Equal(draft.HardDrives, updated.HardDrives);
        Assert.NotEqual(draft.KickstartRomPath, updated.KickstartRomPath);
        Assert.Equal(bytes, File.ReadAllBytes(image)); // fixture already has current components
        Assert.Equal(readOnly, File.ReadAllText(marker) == previous);
        Assert.Empty(Directory.GetFiles(directory, "CopperOS.before-drawing-*.hdf"));
    }

    [Fact]
    public void MissingPersonalDiskIsNeverSilentlyReplacedWithSeed()
    {
        var draft = CopperScreenBundledCopperOs.Prepare(CopperScreenLaunchWorkflow.SelectStartChoice(new(), 3), _root);
        var image = Assert.Single(draft.HardDrives).Path;
        File.Delete(image);
        Assert.Throws<FileNotFoundException>(() => CopperScreenBundledCopperOs.Prepare(draft, _root));
        Assert.False(File.Exists(image));
    }

    [Fact]
    public void BundledRomCacheCanBeRepairedWithoutReplacingThePersonalDisk()
    {
        var draft = CopperScreenBundledCopperOs.Prepare(CopperScreenLaunchWorkflow.SelectStartChoice(new(), 3), _root);
        var image = draft.HardDrives.Single().Path;
        using (var stream = File.OpenWrite(image)) { stream.Position = stream.Length - 1; stream.WriteByte(0x5A); }
        File.WriteAllBytes(draft.KickstartRomPath!, [0]);
        var repaired = CopperScreenBundledCopperOs.Prepare(draft, _root);
        Assert.Equal(1048576, CopperScreenCopperStartRom.Read(repaired.KickstartRomPath, a1200: true).Length);
        Assert.Equal(0x5A, File.ReadAllBytes(image)[^1]);
    }

    [Fact]
    public async Task ConcurrentFirstUseCreatesOneCompleteWorkspace()
    {
        var drafts = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Task.Run(() =>
            CopperScreenBundledCopperOs.Prepare(CopperScreenLaunchWorkflow.SelectStartChoice(new(), 3), _root))));
        Assert.Single(drafts.Select(d => d.HardDrives.Single().Path).Distinct());
        using var stream = File.OpenRead(drafts[0].HardDrives[0].Path);
        Assert.Equal("DC597FA79C5479D43E98ABDEDFCB885056F9C22AFFA161126FF1B27C39C3CFA7",
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)));
        Assert.Empty(Directory.GetDirectories(Path.Combine(_root, "CopperOS"), ".a1200-*"));
    }

    [Fact]
    public Task BundledCopperOsStartsWithoutFileSelectionAndReachesNativeDesktop() => UiAsync(async () =>
    {
        var window = new MainWindow([], new CopperScreenRecentSession(_root)); window.Show();
        try
        {
            Named<ComboBox>(window, "Start configuration").SelectedIndex = 3;
            var draft = MenuDraft(window);
            var image = Assert.Single(draft.HardDrives).Path;
            Assert.False(draft.HardDrives[0].ReadOnly);
            Assert.False(Button(window, "Choose ROM…").IsEffectivelyVisible);
            Assert.False(Button(window, "Choose disk…").IsEffectivelyVisible);
            Assert.True(Button(window, "Start CopperOS").IsEnabled);
            Capture(window, "copperos-hdf-ready");
            Click(Button(window, "Start CopperOS"));
            await Until(() => Named<Button>(window, "Pause").IsEnabled);
            var runtime = (CopperScreenRuntime)typeof(MainWindow).GetField("_runtime", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            while (runtime.CurrentState.FrameNumber < 1200)
            {
                Assert.Null(runtime.CurrentState.FaultMessage);
                await Task.Delay(100, timeout.Token);
            }
            await runtime.TogglePausedAsync();
            // Read only after the runtime acknowledges pause. Require the native
            // desktop port and window rather than accepting a Shell prompt.
            var session = (CopperScreenLightweightSession)typeof(CopperScreenRuntime).GetField("_emulator", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runtime)!;
            var ram = session.Machine.ChipRam.ToArray();
            uint Long(uint address) => BinaryPrimitives.ReadUInt32BigEndian(ram.AsSpan((int)address, 4));
            uint Find(int list, string name)
            {
                var node = Long(Long(4) + (uint)list);
                for (var n = 0; n < 256 && node != 0 && Long(node) != 0; n++, node = Long(node))
                {
                    var p = (int)Long(node + 10); var end = p;
                    if (p <= 0 || p >= ram.Length) continue;
                    while (end < ram.Length && ram[end] != 0) end++;
                    if (System.Text.Encoding.ASCII.GetString(ram[p..end]) == name) return node;
                }
                return 0;
            }
            Assert.NotEqual(0u, Find(392, "CopperOS.Desktop"));
            var intuition = Find(378, "intuition.library");
            Assert.NotEqual(0u, intuition);
            Assert.NotEqual(0u, Long(Long(intuition + 60) + 4));
            Capture(window, "copperos-hdf-native-desktop");
            Click(Named<Button>(window, "Stop"));
            await Until(() => window.GetLogicalDescendants().OfType<Button>().Any(b => b.Content is "Start CopperOS" && b.IsEffectivelyVisible && b.IsEnabled));
            Assert.Equal(3, Named<ComboBox>(window, "Start configuration").SelectedIndex);
            var restored = new CopperScreenRecentSession(_root).LoadMachine(AppContext.BaseDirectory)!;
            Assert.Equal(image, Assert.Single(restored.HardDrives).Path);
            Assert.All(restored.DriveDiskPaths, Assert.Null);
        }
        finally { window.Close(); }
    });

    private static CopperScreenSettingsDraft MenuDraft(MainWindow window) =>
        (CopperScreenSettingsDraft)typeof(MainWindow).GetMethod("ReadSettingsDraft", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null)!;

    private static void SetMenuDraft(MainWindow window, CopperScreenSettingsDraft draft) =>
        typeof(MainWindow).GetMethod("SetWelcomeDraft", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [draft]);

    [Theory]
    [InlineData(0, 262144, 34, 5, "Kickstart 1.3")]
    [InlineData(1, 524288, 37, 175, "Kickstart 2.04")]
    [InlineData(2, 524288, 39, 106, "Kickstart 3.0")]
    public Task MachineChoiceCanLaunchWithRomAndNoAdf(int choice, int size, ushort major, ushort minor, string requirement) => UiAsync(async () =>
    {
        // Synthetic reset loops test menu admission and Start/Stop wiring only.
        // Native Kickstart boot screens have separate media replay coverage.
        var rom = new byte[size];
        BinaryPrimitives.WriteUInt32BigEndian(rom, 0x70000);
        BinaryPrimitives.WriteUInt32BigEndian(rom.AsSpan(4), 0xF80100);
        BinaryPrimitives.WriteUInt16BigEndian(rom.AsSpan(12), major);
        BinaryPrimitives.WriteUInt16BigEndian(rom.AsSpan(14), minor);
        rom[0x100] = 0x60; rom[0x101] = 0xFE;
        var path = Path.Combine(_root, "synthetic.rom"); File.WriteAllBytes(path, rom);
        var window = new MainWindow([], new CopperScreenRecentSession(_root)); window.Show();
        try
        {
            Named<ComboBox>(window, "Start configuration").SelectedIndex = choice;
            var draft = MenuDraft(window);
            Assert.Equal(choice, CopperScreenLaunchWorkflow.StartChoice(draft));
            draft.KickstartRomPath = path;
            SetMenuDraft(window, draft);
            Assert.Contains(requirement, Named<TextBlock>(window, "ROM requirements").Text);
            Assert.True(Button(window, "Start Amiga").IsEnabled);
            Assert.All(MenuDraft(window).DriveDiskPaths, Assert.Null);
            Capture(window, $"machine-{choice}-ready-no-adf");
            Click(Button(window, "Start Amiga"));
            await Until(() => Named<Button>(window, "Pause").IsEnabled);
            Click(Named<Button>(window, "Stop"));
            await Until(() => window.GetLogicalDescendants().OfType<Button>().Any(b => b.Content is "Start Amiga" && b.IsEffectivelyVisible && b.IsEnabled));
            var saved = new CopperScreenRecentSession(_root).LoadMachine(AppContext.BaseDirectory)!;
            Assert.Equal(choice, CopperScreenLaunchWorkflow.StartChoice(saved));
            Assert.Equal(draft.RomVersion, saved.RomVersion);
            Assert.All(saved.DriveDiskPaths, Assert.Null);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task RemovingFloppyAndSwitchingMachinesKeepsRequirementsConsistent() => UiAsync(async () =>
    {
        var path = Path.Combine(_root, "disk.adf"); File.WriteAllBytes(path, new byte[901120]);
        var window = new MainWindow([], new CopperScreenRecentSession(_root)); window.Show();
        try
        {
            await window.InsertDriveDiskAsync(0, path);
            Assert.True(Button(window, "Remove floppy disk").IsEffectivelyVisible);
            Click(Button(window, "Remove floppy disk"));
            Assert.Null(MenuDraft(window).DriveDiskPaths[0]);
            Assert.False(Button(window, "Remove floppy disk").IsEffectivelyVisible);
            var draft = MenuDraft(window); draft.KickstartRomPath = "a500.rom";
            SetMenuDraft(window, draft);
            Named<ComboBox>(window, "Start configuration").SelectedIndex = 1;
            Assert.Null(MenuDraft(window).KickstartRomPath);
            Assert.Equal(KickstartVersion.Kickstart20, MenuDraft(window).RomVersion);
            Named<ComboBox>(window, "Start configuration").SelectedIndex = 3;
            draft = MenuDraft(window);
            Assert.Equal(CopperScreenKickstartSource.CopperStart, draft.KickstartSource);
            Assert.Equal(M68kBackendKind.AccurateM68EC020, draft.CpuBackend);
            Assert.Equal(2048, draft.ChipRamKb);
            Assert.Equal("none", draft.Input.Port2ProfileId);
            Assert.All(draft.DriveDiskPaths, Assert.Null);
            Assert.True(Button(window, "Start CopperOS").IsEnabled);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task HdfSelectionUsesQualifiedGeometryAndPreservesMediaAndWriteSetting() => Ui(() =>
    {
        var path = Path.Combine(_root, "CopperOS.hdf");
        var bytes = new byte[8 * 1024 * 1024]; "DOS\u0001"u8.CopyTo(bytes); File.WriteAllBytes(path, bytes);
        var invalid = Path.Combine(_root, "wrong.hdf"); File.WriteAllBytes(invalid, new byte[512]);
        var window = new MainWindow([], new CopperScreenRecentSession(_root)); window.Show();
        try
        {
            Named<ComboBox>(window, "Start configuration").SelectedIndex = 3;
            var draft = CopperScreenLaunchWorkflow.SelectCopperOsHdf(MenuDraft(window), path);
            Assert.True(Assert.Single(draft.HardDrives).ReadOnly);
            Assert.Null(CopperScreenAvailability.GetUnavailableReason(draft, AppContext.BaseDirectory));
            Assert.Throws<InvalidDataException>(() => CopperScreenLaunchWorkflow.SelectCopperOsHdf(draft, invalid));
            SetMenuDraft(window, draft);
            draft = MenuDraft(window);
            draft.HardDrives[0] = draft.HardDrives[0] with { ReadOnly = false };
            SetMenuDraft(window, draft);
            Assert.False(Assert.Single(draft.HardDrives).ReadOnly);
            new CopperScreenRecentSession(_root).Save(draft, 50, false, AppContext.BaseDirectory);
            var restored = new CopperScreenRecentSession(_root).LoadMachine(AppContext.BaseDirectory)!;
            Assert.Equal(draft.HardDrives[0], restored.HardDrives[0]);
            Assert.Equal(bytes, File.ReadAllBytes(path));
            Named<ComboBox>(window, "Start configuration").SelectedIndex = 2;
            Assert.Empty(MenuDraft(window).HardDrives);
            Assert.True(Button(window, "Choose disk…").IsEffectivelyVisible);
            Assert.True(Button(window, "Choose ROM…").IsEffectivelyVisible);
        }
        finally { window.Close(); }
    });
}
