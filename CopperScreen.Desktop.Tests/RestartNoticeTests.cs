using System.Buffers.Binary;
using Avalonia;
using Avalonia.Controls;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed partial class LaunchWorkflowTests
{
    [Theory]
    [InlineData(712, false, false)]
    [InlineData(1040, true, false)]
    [InlineData(712, false, true)]
    [InlineData(1040, true, true)]
    public Task MainNoticePersistsUntilPendingChangesAreRevertedOrRestarted(int width, bool toolbarRestart, bool paused) => UiAsync(async () =>
    {
        var rom = DefaultRomFixture(0, "restart-notice");
        var bytes = File.ReadAllBytes(rom);
        BinaryPrimitives.WriteUInt32BigEndian(bytes, 0x70000);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(4), 0xF80100);
        bytes[0x100] = 0x60; bytes[0x101] = 0xFE; File.WriteAllBytes(rom, bytes);
        var window = new MainWindow([], new CopperScreenRecentSession(_root)); window.Show();
        try
        {
            window.Width = width;
            var draft = MenuDraft(window); draft.KickstartRomPath = rom; SetMenuDraft(window, draft);
            Click(Button(window, "Start Amiga"));
            await Until(() => Named<Button>(window, "Pause").IsEnabled);
            var original = SettingsField<CopperScreenRuntime>(window, "_runtime");
            if (paused) await original.TogglePausedAsync();
            Assert.Equal(paused, original.CurrentState.IsPaused);
            Assert.False(Named<Border>(window, "Pending restart notice").IsVisible);
            void EditHardware(string cpu, string ram)
            {
                Click(Button(window, "Configure Amiga…"));
                SettingsField<ComboBox>(window, "_cpuBackendBox").SelectedItem = cpu;
                SettingsField<TextBox>(window, "_realFastRamBox").Text = ram;
                SettingsField<ComboBox>(window, "_pixelAspectModeBox").SelectedIndex = 1;
                SettingsField<Slider>(window, "_masterVolumeSlider").Value = 31;
                Click(SettingsField<Button>(window, "_settingsCloseButton"));
            }
            EditHardware("AccurateM68010", "2048");
            var notice = Named<Border>(window, "Pending restart notice");
            var restart = Named<Button>(window, "Restart Amiga to apply changes");
            Assert.True(notice.IsEffectivelyVisible);
            Assert.Contains("CPU", Named<TextBlock>(window, "Pending restart changes").Text);
            Assert.Contains("RAM", Named<TextBlock>(window, "Pending restart changes").Text);
            await Task.Delay(100);
            Assert.True(notice.IsEffectivelyVisible);
            window.UpdateLayout();
            var point = restart.TranslatePoint(default, window)!.Value;
            Assert.True(restart.Bounds.Width > 0 && restart.Bounds.Height > 0);
            Assert.InRange(point.X, 0, window.ClientSize.Width - restart.Bounds.Width + 1);
            Assert.InRange(point.Y, 0, window.ClientSize.Height - restart.Bounds.Height + 1);
            var revert = Named<Button>(window, "Revert pending changes");
            var revertPoint = revert.TranslatePoint(default, window)!.Value;
            Assert.InRange(revertPoint.X, 0, window.ClientSize.Width - revert.Bounds.Width + 1);
            Assert.InRange(revertPoint.Y, 0, window.ClientSize.Height - revert.Bounds.Height + 1);
            Capture(window, $"pending-restart-{width}");
            Assert.True(revert.IsEnabled);
            if (toolbarRestart)
            {
                Click(Button(window, "Configure Amiga…"));
                var settings = SettingsField<Window>(window, "_settingsWindow");
                var settingsRevert = SettingsField<Button>(window, "_settingsRevertButton");
                Assert.True(settingsRevert.IsVisible);
                Assert.True(settingsRevert.IsEnabled);
                Capture(settings, $"settings-revert-{paused}");
                Click(settingsRevert);
                Assert.True(settings.IsVisible);
                Assert.Equal("AccurateM68000", SettingsField<ComboBox>(window, "_cpuBackendBox").SelectedItem);
                Assert.Equal("0", SettingsField<TextBox>(window, "_realFastRamBox").Text);
                Assert.False(settingsRevert.IsEnabled);
                Assert.DoesNotContain("Restart Amiga required", SettingsField<TextBlock>(window, "_settingsStatus").Text);
                Click(SettingsField<Button>(window, "_settingsCloseButton"));
            }
            else Click(revert);
            Assert.False(notice.IsVisible);
            Assert.Same(original, SettingsField<CopperScreenRuntime>(window, "_runtime"));
            Assert.Equal(paused, original.CurrentState.IsPaused);
            Assert.Equal(M68kBackendKind.AccurateM68000, MenuDraft(window).CpuBackend);
            Assert.Equal(0, MenuDraft(window).RealFastRamKb);
            Assert.Equal(CopperScreenPixelAspectMode.CrtCorrect, MenuDraft(window).PresentationOptions.PixelAspectMode);
            Assert.Equal(31, SettingsField<double>(window, "_outputVolume"));
            var saved = new CopperScreenRecentSession(_root).LoadMachine(AppContext.BaseDirectory)!;
            Assert.Equal(M68kBackendKind.AccurateM68000, saved.CpuBackend);
            Assert.Equal(0, saved.RealFastRamKb);
            Assert.Equal(CopperScreenPixelAspectMode.CrtCorrect, saved.PresentationOptions.PixelAspectMode);
            EditHardware("AccurateM68010", "2048");
            Assert.True(restart.IsEnabled);
            Click(toolbarRestart ? Named<Button>(window, "Restart") : restart);
            await Until(() => SettingsField<CopperScreenRuntime>(window, "_runtime") != original && !SettingsField<bool>(window, "_applyingSettings"));
            Assert.False(notice.IsVisible);
            Assert.Equal(M68kBackendKind.AccurateM68010, SettingsField<CopperScreenSettingsDraft>(window, "_committedSettings").CpuBackend);
            Assert.Equal(2048, SettingsField<CopperScreenSettingsDraft>(window, "_committedSettings").RealFastRamKb);
            var applied = SettingsField<CopperScreenRuntime>(window, "_runtime");
            Assert.Equal(paused, applied.CurrentState.IsPaused);
            if (paused)
            {
                await Task.Delay(50);
                Assert.Equal(0, applied.CurrentState.FramesRendered);
                Assert.Equal("Resume", Avalonia.Automation.AutomationProperties.GetName(SettingsField<Button>(window, "_pauseButton")));
            }
            Click(Named<Button>(window, "Restart"));
            await Until(() => !SettingsField<bool>(window, "_applyingSettings"));
            Assert.Same(applied, SettingsField<CopperScreenRuntime>(window, "_runtime"));
            Assert.Equal(paused, applied.CurrentState.IsPaused);
            Assert.False(notice.IsVisible);
            Click(Button(window, "Configure Amiga…"));
            SettingsField<TextBox>(window, "_kickstartRomBox").Text = System.IO.Path.Combine(_root, "missing.rom");
            if (toolbarRestart)
            {
                var settings = SettingsField<Window>(window, "_settingsWindow");
                await Until(() => SettingsField<Button>(window, "_settingsRevertButton").IsEnabled);
                Assert.True(SettingsField<Button>(window, "_settingsRevertButton").IsEnabled);
                Click(SettingsField<Button>(window, "_settingsRevertButton"));
                Assert.True(settings.IsVisible);
                Assert.Equal(rom, SettingsField<TextBox>(window, "_kickstartRomBox").Text);
                SettingsField<TextBox>(window, "_kickstartRomBox").Text = System.IO.Path.Combine(_root, "missing.rom");
            }
            Click(SettingsField<Button>(window, "_settingsCloseButton"));
            Assert.True(notice.IsVisible);
            Assert.False(restart.IsEnabled);
            Assert.True(revert.IsEnabled);
            Click(revert);
            Assert.False(notice.IsVisible);
            Assert.Equal(rom, MenuDraft(window).KickstartRomPath);
            Assert.Same(applied, SettingsField<CopperScreenRuntime>(window, "_runtime"));
            Assert.Equal(paused, applied.CurrentState.IsPaused);
        }
        finally { window.Close(); }
    });
}
