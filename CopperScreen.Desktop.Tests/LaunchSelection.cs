using System.Reflection;
using System.Buffers.Binary;
using Avalonia.Controls;
using CopperScreen;

namespace CopperScreen.Tests;

internal static class LaunchSelection
{
    private const BindingFlags Methods = BindingFlags.Instance | BindingFlags.NonPublic;

    public static MainWindow RunningWithOriginalRom(string directory)
    {
        Directory.CreateDirectory(directory);
        var rom = new byte[262144];
        BinaryPrimitives.WriteUInt32BigEndian(rom, 0x70000);
        BinaryPrimitives.WriteUInt32BigEndian(rom.AsSpan(4), 0xFC0100);
        BinaryPrimitives.WriteUInt16BigEndian(rom.AsSpan(12), 34);
        rom[0x100] = 0x60; rom[0x101] = 0xFE;
        var romPath = Path.Combine(directory, "original.rom");
        File.WriteAllBytes(romPath, rom);
        var diskPath = Path.Combine(directory, "original.adf");
        File.WriteAllBytes(diskPath, new byte[901120]);
        var window = new MainWindow(["--profile", "lightweight-a500-kickstart13", "--rom", romPath, diskPath], new CopperScreenRecentSession(directory));
        window.Show();
        return window;
    }

    public static void Select(Window window, int choice)
    {
        typeof(MainWindow).GetMethod("SelectRomSetup", Methods)!.Invoke(window, [choice]);
        Assert.Equal(choice, Current(window));
    }

    public static int Current(Window window) => CopperScreenLaunchWorkflow.StartChoice(
        (CopperScreenSettingsDraft)typeof(MainWindow).GetMethod("ReadSettingsDraft", Methods)!.Invoke(window, null)!);
}
