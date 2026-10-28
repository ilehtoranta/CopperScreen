using System.Buffers.Binary;
using Avalonia.Controls;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed partial class LaunchWorkflowTests
{
    [Fact]
    public Task SavedA500PlusStartsWithoutADiskAndKeeps204InTheExistingLauncher() => Ui(() =>
    {
        // Synthetic reset loop verifies UI admission and persistence only; native
        // Kickstart/Workbench execution is covered by the separate replay tests.
        var rom = new byte[524288];
        BinaryPrimitives.WriteUInt32BigEndian(rom, 0xF0000);
        BinaryPrimitives.WriteUInt32BigEndian(rom.AsSpan(4), 0xF80100);
        BinaryPrimitives.WriteUInt16BigEndian(rom.AsSpan(12), 37);
        BinaryPrimitives.WriteUInt16BigEndian(rom.AsSpan(14), 175);
        rom[0x100] = 0x60; rom[0x101] = 0xFE;
        var path = Path.Combine(_root, "synthetic-204.rom");
        File.WriteAllBytes(path, rom);
        var options = CopperScreenStartupOptions.Parse(["--profile", "a500-plus-ecs-pal", "--rom", path], AppContext.BaseDirectory);
        Assert.Null(options.Error);
        var draft = CopperScreenSettingsDraft.FromStartupOptions(options);
        new CopperScreenRecentSession(_root).Save(draft, 50, false, AppContext.BaseDirectory);
        var window = new MainWindow([], new CopperScreenRecentSession(_root));
        window.Show();
        try
        {
            Assert.Equal(1, Named<ComboBox>(window, "Start configuration").SelectedIndex);
            Assert.Contains("Kickstart 2.04", Named<TextBlock>(window, "ROM requirements").Text);
            Assert.True(Button(window, "Start Amiga").IsEnabled);
            Assert.StartsWith("Optional", Named<TextBlock>(window, "Startup disk selection").Text);
            Assert.Equal(KickstartVersion.Kickstart20, new CopperScreenRecentSession(_root).LoadMachine(AppContext.BaseDirectory)!.RomVersion);
        }
        finally { window.Close(); }
    });
}
