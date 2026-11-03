using Avalonia.Controls;
using Avalonia.LogicalTree;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed partial class LaunchWorkflowTests
{
    [Fact, Trait("Area", "BootOptions")]
    public Task BootOptionsFollowBuildAndRememberIndependentSetupsAcrossBothScreens() => Ui(() =>
    {
        var window = new MainWindow([], new CopperScreenRecentSession(_root)); window.Show();
        var native = Path.Combine(_root, "native.adf");
        var minimal = Path.Combine(_root, "minimal.adf");
        try
        {
            var boot = Named<ComboBox>(window, "Boot method");
            Assert.Equal(CopperScreenBootMethods.CopperStartEnabled, boot.Items.Contains("CopperStart"));
            Assert.Contains("MinimalDiskBoot", boot.Items);
            Assert.Contains("KickstartRom", boot.Items);
            var draft = MenuDraft(window); draft.DriveDiskPaths[0] = native; draft.RealFastRamKb = 2048;
            SetMenuDraft(window, draft);
            boot.SelectedItem = "MinimalDiskBoot";
            Assert.Null(MenuDraft(window).KickstartRomPath);
            Assert.Equal(0, MenuDraft(window).RealFastRamKb);
            Assert.False(Button(window, "Choose ROM…").IsEffectivelyVisible);
            Assert.Contains("no DOS", Named<TextBlock>(window, "ROM requirements").Text);
            Capture(window, "minimal-boot");
            draft = MenuDraft(window); draft.DriveDiskPaths[0] = minimal; SetMenuDraft(window, draft);
            boot.SelectedItem = "KickstartRom";
            Assert.Equal(native, MenuDraft(window).DriveDiskPaths[0]);
            Assert.Equal(2048, MenuDraft(window).RealFastRamKb);
            Click(Button(window, "Configure Amiga…"));
            var settingsBoot = SettingsField<ComboBox>(window, "_kickstartSourceBox");
            Assert.Equal(CopperScreenBootMethods.CopperStartEnabled, settingsBoot.Items.Contains("CopperStart"));
            settingsBoot.SelectedItem = "MinimalDiskBoot";
            Assert.Equal(minimal, MenuDraft(window).DriveDiskPaths[0]);
            Assert.Equal(0, MenuDraft(window).RealFastRamKb);
            Capture(SettingsField<Window>(window, "_settingsWindow"), "configure-minimal-boot");
            settingsBoot.SelectedItem = "KickstartRom";
            Assert.Equal(native, MenuDraft(window).DriveDiskPaths[0]);
            settingsBoot.SelectedItem = "MinimalDiskBoot";
            Click(SettingsField<Button>(window, "_settingsCloseButton"));
        }
        finally { window.Close(); }
        var reopened = new MainWindow([], new CopperScreenRecentSession(_root)); reopened.Show();
        try
        {
            Assert.Equal("MinimalDiskBoot", Named<ComboBox>(reopened, "Boot method").SelectedItem);
            Assert.Equal(minimal, MenuDraft(reopened).DriveDiskPaths[0]);
            Named<ComboBox>(reopened, "Boot method").SelectedItem = "KickstartRom";
            Assert.Equal(native, MenuDraft(reopened).DriveDiskPaths[0]);
            Assert.Equal(2048, MenuDraft(reopened).RealFastRamKb);
            Assert.False(Directory.Exists(Path.Combine(_root, "CopperOS")));
        }
        finally { reopened.Close(); }
    });

    [Theory, Trait("Area", "BootOptions")]
    [InlineData(1)]
    [InlineData(2)]
    public Task MinimalBootIsSelectableAndReadyOnExtendedModels(int model) => Ui(() =>
    {
        var window = new MainWindow([], new CopperScreenRecentSession(_root)); window.Show();
        try
        {
            Named<ComboBox>(window, "Boot method").SelectedItem = "MinimalDiskBoot";
            Named<ComboBox>(window, "Amiga model").SelectedIndex = model;
            Assert.Equal(model, (int)CopperScreenMachinePresets.Identify(MenuDraft(window)));
            Assert.Equal(CopperScreenKickstartSource.MinimalDiskBoot, MenuDraft(window).KickstartSource);
            Assert.False(Button(window, "Start Amiga").IsEnabled); // A startup disk is still required.
            Assert.Contains("Startup ADF required", Named<TextBlock>(window, "ROM requirements").Text);
            var disk = new byte[901120]; "DOS\0"u8.CopyTo(disk);
            new CopperScreenBootAssembler(CopperScreenMinimalBoot.BootAddress + 12).Assemble("loop:\nbra loop").CopyTo(disk, 12);
            uint sum = 0;
            for (var i = 0; i < 1024; i += 4)
            {
                var next = unchecked(sum + System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(disk.AsSpan(i, 4)));
                if (next < sum) next++;
                sum = next;
            }
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(disk.AsSpan(4), ~sum);
            var path = Path.Combine(_root, "minimal-model.adf"); File.WriteAllBytes(path, disk);
            var draft = MenuDraft(window); draft.DriveDiskPaths[0] = path; SetMenuDraft(window, draft);
            Assert.True(Button(window, "Start Amiga").IsEnabled);
            Capture(window, "minimal-ready-" + model);
            Click(Button(window, "Configure Amiga…"));
            var boot = SettingsField<ComboBox>(window, "_kickstartSourceBox");
            boot.SelectedItem = "KickstartRom";
            boot.SelectedItem = "MinimalDiskBoot";
            Assert.Equal("MinimalDiskBoot", boot.SelectedItem);
            Assert.Equal(model, (int)CopperScreenMachinePresets.Identify(MenuDraft(window)));
            Assert.Equal(path, MenuDraft(window).DriveDiskPaths[0]);
            Assert.True(SettingsField<Button>(window, "_settingsStartButton").IsEnabled);
            // Expanded hardware remains unsupported and must be explained rather than silently changed.
            SettingsField<TextBox>(window, "_realFastRamBox").Text = "2048";
            SettingsCall(window, "UpdateSettingsStatus");
            Assert.False(SettingsField<Button>(window, "_settingsStartButton").IsEnabled);
            Assert.Contains("Fast RAM", SettingsField<TextBlock>(window, "_settingsStatus").Text);
        }
        finally { window.Close(); }
    });

    [Fact, Trait("Area", "BootOptions")]
    public Task PartialSettingsEditDoesNotSwitchOrOverwriteBootSetups() => Ui(() =>
    {
        var window = new MainWindow([], new CopperScreenRecentSession(_root)); window.Show();
        try
        {
            Click(Button(window, "Configure Amiga…"));
            SettingsField<TextBox>(window, "_realFastRamBox").Text = "invalid";
            var boot = SettingsField<ComboBox>(window, "_kickstartSourceBox");
            boot.SelectedItem = "MinimalDiskBoot";
            Assert.Equal("KickstartRom", boot.SelectedItem);
            Assert.Equal("invalid", SettingsField<TextBox>(window, "_realFastRamBox").Text);
            Assert.NotEqual(CopperScreenKickstartSource.MinimalDiskBoot, SettingsField<CopperScreenSettingsDraft>(window, "_settingsDraft").KickstartSource);
        }
        finally { window.Close(); }
    });

    [Fact, Trait("Area", "BootOptions")]
    public Task DisabledBuildKeepsSavedCopperStartUnavailableWithoutExtractingPayload() => Ui(() =>
    {
        var resources = typeof(CopperScreenBootMethods).Assembly.GetManifestResourceNames();
        Assert.Equal(CopperScreenBootMethods.CopperStartEnabled, resources.Contains("CopperScreen.Bundled.CopperStart-A1200.rom"));
        Assert.Equal(CopperScreenBootMethods.CopperStartEnabled, resources.Contains("CopperScreen.Bundled.CopperOS.hdf"));
        if (CopperScreenBootMethods.CopperStartEnabled) return;
        var draft = CopperScreenLaunchWorkflow.SelectStartChoice(new(), 3);
        new CopperScreenRecentSession(_root).Save(draft, 100, false);
        var window = new MainWindow([], new CopperScreenRecentSession(_root)); window.Show();
        try
        {
            Assert.DoesNotContain("CopperStart", Named<ComboBox>(window, "Boot method").Items);
            Assert.Null(Named<ComboBox>(window, "Boot method").SelectedItem);
            Assert.Contains("not enabled", CopperScreenAvailability.GetUnavailableReason(MenuDraft(window), AppContext.BaseDirectory));
            Assert.False(Directory.Exists(Path.Combine(_root, "Firmware")));
            Assert.Throws<NotSupportedException>(() => CopperScreenSession.Create(MenuDraft(window).ToStartupOptions(AppContext.BaseDirectory)));
        }
        finally { window.Close(); }
    });
}
