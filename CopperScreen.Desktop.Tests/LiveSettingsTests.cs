using System.Buffers.Binary;
using Avalonia.Controls;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed partial class LaunchWorkflowTests
{
    [Fact]
    public Task LiveChangesApplyAlongsidePendingHardwareAndClearWhenReverted() => UiAsync(async () =>
    {
        var rom = DefaultRomFixture(0, "live-settings");
        var bytes = File.ReadAllBytes(rom);
        BinaryPrimitives.WriteUInt32BigEndian(bytes, 0x70000);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(4), 0xF80100);
        bytes[0x100] = 0x60; bytes[0x101] = 0xFE;
        File.WriteAllBytes(rom, bytes);
        var disk = Path.Combine(_root, "live.adf"); File.WriteAllBytes(disk, new byte[901120]);
        var window = new MainWindow([], new CopperScreenRecentSession(_root)); window.Show();
        try
        {
            var draft = MenuDraft(window); draft.KickstartRomPath = rom; SetMenuDraft(window, draft);
            Click(Button(window, "Start Amiga"));
            await Until(() => Named<Button>(window, "Pause").IsEnabled);
            Click(Button(window, "Configure Amiga…"));
            SettingsField<ComboBox>(window, "_cpuBackendBox").SelectedItem = "AccurateM68010";
            SettingsField<TextBox>(window, "_realFastRamBox").Text = "2048";
            SettingsField<Slider>(window, "_masterVolumeSlider").Value = 31;
            SettingsField<CheckBox>(window, "_masterMuteBox").IsChecked = true;
            SettingsField<ComboBox>(window, "_pixelAspectModeBox").SelectedIndex = 1;
            await Task.Delay(50);
            var committed = SettingsField<CopperScreenSettingsDraft>(window, "_committedSettings");
            Assert.Equal(M68kBackendKind.AccurateM68000, committed.CpuBackend);
            Assert.Equal(0, committed.RealFastRamKb);
            Assert.Equal(CopperScreenPixelAspectMode.CrtCorrect, committed.PresentationOptions.PixelAspectMode);
            Assert.Equal(31, SettingsField<double>(window, "_outputVolume"));
            Assert.True(SettingsField<bool>(window, "_outputMuted"));
            var status = SettingsField<TextBlock>(window, "_settingsStatus").Text;
            Assert.Contains("Restart Amiga required", status);
            Assert.Contains("CPU", status); Assert.Contains("RAM", status);
            Assert.DoesNotContain("Apply", status);
            SettingsField<TextBox>(window, "_setupDiskBox").Text = disk;
            await Until(() => SettingsField<CopperScreenRuntime>(window, "_runtime").CurrentState.Drives[0].DiskPath == disk);
            SettingsField<ComboBox>(window, "_cpuBackendBox").SelectedItem = "AccurateM68000";
            SettingsField<TextBox>(window, "_realFastRamBox").Text = "0";
            await Task.Delay(50);
            Assert.Contains("Changes apply immediately", SettingsField<TextBlock>(window, "_settingsStatus").Text);
            Click(SettingsField<Button>(window, "_settingsCloseButton"));
            Assert.Equal(CopperScreenPixelAspectMode.CrtCorrect, MenuDraft(window).PresentationOptions.PixelAspectMode);
        }
        finally { window.Close(); }
    });
}
