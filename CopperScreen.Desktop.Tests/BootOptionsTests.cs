using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed partial class LaunchWorkflowTests
{
    [Theory, Trait("Area", "BootOptions")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public Task FreshLauncherOffersTheDefaultRomAndAnOriginalRomOnEveryModel(int model) => UiAsync(async () =>
    {
        var window = new MainWindow([], new CopperScreenRecentSession(_root));
        window.Show();
        try
        {
            Named<ComboBox>(window, "Amiga model").SelectedIndex = model;
            var draft = MenuDraft(window);
            Assert.Equal(model, (int)CopperScreenMachinePresets.Identify(draft));
            Assert.Null(draft.KickstartRomPath);
            Assert.Equal(CopperScreenBundledMiniRom.Included, draft.KickstartSource == CopperScreenKickstartSource.MinimalDiskBoot);
            Assert.Equal(CopperScreenBundledMiniRom.Included, Button(window, "Start Amiga").IsEnabled);
            Assert.Equal(CopperScreenBundledMiniRom.Included ? "CopperStart Mini (built in)" : "No ROM selected",
                Named<TextBlock>(window, "Selected ROM").Text);
            Assert.DoesNotContain(window.GetLogicalDescendants().OfType<ComboBox>(), control => AutomationProperties.GetName(control) == "Boot method");
            Assert.True(Button(window, "Choose ROM…").IsEffectivelyVisible);
            Click(Button(window, "Configure Amiga…"));
            var settings = SettingsField<Window>(window, "_settingsWindow");
            Assert.DoesNotContain(settings.GetLogicalDescendants().OfType<ComboBox>(), control => AutomationProperties.GetName(control) == "Boot method");
            Assert.True(SettingsNamed<Button>(settings, "Browse for boot ROM").IsEffectivelyVisible);
            var cpu = SettingsNamed<ComboBox>(settings, "Amiga CPU");
            Assert.True(cpu.IsEffectivelyVisible);
            Assert.Equal(model == 2 ? "AccurateM68EC020" : "AccurateM68000", cpu.SelectedItem);
            Assert.True(cpu.IsEnabled);
            var cpuRequirements = SettingsNamed<TextBlock>(settings, "CPU requirements");
            Assert.False(cpuRequirements.IsEffectivelyVisible);
            Assert.Equal(CopperScreenBundledMiniRom.Included, SettingsField<Button>(window, "_settingsStartButton").IsEnabled);
            if (CopperScreenBundledMiniRom.Included)
            {
                cpu.SelectedItem = "AccurateM68060";
                Assert.Equal("AccurateM68060", cpu.SelectedItem);
                Assert.True(cpuRequirements.IsEffectivelyVisible);
                if (model == 2)
                {
                    Assert.Contains("FPU disabled", cpuRequirements.Text);
                }
                else
                {
                    Assert.Equal(CopperScreenMachinePresets.Cpu68060Requirement, cpuRequirements.Text);
                    Assert.False(SettingsField<Button>(window, "_settingsStartButton").IsEnabled);
                    cpu.SelectedItem = "AccurateM68000";
                }
                Click(SettingsField<Button>(window, "_settingsCloseButton"));
                var start = Button(window, "Start Amiga");
                Click(start);
                await Until(() => Named<Button>(window, "Pause").IsEnabled && window.Title?.Contains("CopperStart Mini") == true);
                Click(Named<Button>(window, "Stop"));
                await Until(() => start.IsEffectivelyVisible && start.IsEnabled && start.Content is "Start Amiga");
            }
        }
        finally { window.Close(); }
    });

    [Fact, Trait("Area", "BootOptions")]
    public Task A1200ShowsDisabledOlderCpusWithPlainLabels() => UiAsync(async () =>
    {
        var window = new MainWindow([], new CopperScreenRecentSession(_root));
        window.Show();
        try
        {
            Named<ComboBox>(window, "Amiga model").SelectedIndex = (int)CopperScreenMachineModel.Amiga1200;
            window.SelectOriginalRom(DefaultRomFixture(2, "a1200-cpu-labels", kickstart31: true));
            Click(Button(window, "Configure Amiga…"));
            var settings = SettingsField<Window>(window, "_settingsWindow");
            var cpu = SettingsNamed<ComboBox>(settings, "Amiga CPU");
            Assert.True(cpu.IsEnabled);
            Assert.Equal("AccurateM68EC020", cpu.SelectedItem);
            cpu.IsDropDownOpen = true;
            await Until(() => cpu.ContainerFromIndex(2) is ComboBoxItem);

            var labels = new[] { "Motorola 68000", "Motorola 68010" };
            for (var index = 0; index < labels.Length; index++)
            {
                var item = Assert.IsType<ComboBoxItem>(cpu.ContainerFromIndex(index));
                Assert.False(item.IsEnabled);
                Assert.Equal(labels[index], AutomationProperties.GetName(item));
                Assert.Equal(labels[index], Assert.Single(item.GetVisualDescendants().OfType<TextBlock>()).Text);
            }
            Assert.True(Assert.IsType<ComboBoxItem>(cpu.ContainerFromIndex(2)).IsEnabled);
        }
        finally { window.Close(); }
    });

    [Fact, Trait("Area", "BootOptions")]
    public Task Selecting68060LetsTheUserFixTheModelAndRom() => Ui(() =>
    {
        var window = new MainWindow([], new CopperScreenRecentSession(_root));
        window.Show();
        try
        {
            window.SelectOriginalRom(DefaultRomFixture(0, "68060-kickstart-13"));
            Click(Button(window, "Configure Amiga…"));
            var settings = SettingsField<Window>(window, "_settingsWindow");
            var cpu = SettingsNamed<ComboBox>(settings, "Amiga CPU");
            var requirements = SettingsNamed<TextBlock>(settings, "CPU requirements");
            var start = SettingsField<Button>(window, "_settingsStartButton");
            Assert.True(start.IsEnabled);

            cpu.SelectedItem = "AccurateM68060";
            Assert.Equal("AccurateM68060", cpu.SelectedItem);
            Assert.Equal(CopperScreenMachinePresets.Cpu68060Requirement, requirements.Text);
            Assert.False(start.IsEnabled);
            Assert.Equal(M68kBackendKind.AccurateM68060,
                new CopperScreenRecentSession(_root).LoadMachine(AppContext.BaseDirectory)!.CpuBackend);

            SettingsNamed<ComboBox>(settings, "Amiga model").SelectedItem = CopperScreenMachineModel.Amiga1200;
            Assert.Equal("AccurateM68060", cpu.SelectedItem);
            Assert.False(start.IsEnabled);

            window.SelectOriginalRom(DefaultRomFixture(2, "68060-kickstart-31", kickstart31: true));
            Assert.Equal("AccurateM68060", cpu.SelectedItem);
            Assert.Contains("FPU disabled", requirements.Text);
            Assert.True(start.IsEnabled);
        }
        finally { window.Close(); }
    });

    [Fact, Trait("Area", "BootOptions")]
    public Task SelectingAnOriginalRomPreservesTheDiskAndTheChoiceOnReopen() => UiAsync(async () =>
    {
        var rom = DefaultRomFixture(0, "original-rom");
        var disk = Path.Combine(_root, "startup.adf"); File.WriteAllBytes(disk, new byte[901120]);
        var window = new MainWindow([], new CopperScreenRecentSession(_root));
        window.Show();
        try
        {
            await window.InsertDriveDiskAsync(0, disk);
            window.SelectOriginalRom(rom);
            Assert.Equal(CopperScreenKickstartSource.KickstartRom, MenuDraft(window).KickstartSource);
            Assert.Equal(disk, MenuDraft(window).DriveDiskPaths[0]);
            Assert.True(Button(window, "Start Amiga").IsEnabled);
            window.SelectOriginalRom(Path.Combine(_root, "missing.rom"));
            Assert.Equal(rom, MenuDraft(window).KickstartRomPath);
        }
        finally { window.Close(); }

        var reopened = new MainWindow([], new CopperScreenRecentSession(_root));
        reopened.Show();
        try
        {
            Assert.Equal(Path.GetFileName(rom), Named<TextBlock>(reopened, "Selected ROM").Text);
            Assert.Equal(rom, MenuDraft(reopened).KickstartRomPath);
            var reset = Button(reopened, "Use built-in ROM");
            Assert.Equal(CopperScreenBundledMiniRom.Included, reset.IsEffectivelyVisible);
            if (CopperScreenBundledMiniRom.Included)
            {
                Click(reset);
                Assert.Equal(CopperScreenKickstartSource.MinimalDiskBoot, MenuDraft(reopened).KickstartSource);
                Assert.Null(MenuDraft(reopened).ToStartupOptions(AppContext.BaseDirectory).KickstartRomPath);
                Assert.Equal(disk, MenuDraft(reopened).DriveDiskPaths[0]);
                Named<ComboBox>(reopened, "Amiga model").SelectedIndex = 1;
                Named<ComboBox>(reopened, "Amiga model").SelectedIndex = 0;
                Assert.Equal("CopperStart Mini (built in)", Named<TextBlock>(reopened, "Selected ROM").Text);
            }
        }
        finally { reopened.Close(); }
    });

    [Fact, Trait("Area", "BootOptions")]
    public Task APartialSettingsEditDoesNotReplaceTheRom() => Ui(() =>
    {
        var first = DefaultRomFixture(0, "first-rom");
        var replacement = DefaultRomFixture(0, "replacement-rom");
        var window = new MainWindow([], new CopperScreenRecentSession(_root));
        window.Show();
        try
        {
            window.SelectOriginalRom(first);
            Click(Button(window, "Configure Amiga…"));
            SettingsField<TextBox>(window, "_realFastRamBox").Text = "invalid";
            window.SelectOriginalRom(replacement);
            Assert.Equal(first, SettingsField<TextBox>(window, "_kickstartRomBox").Text);
            Assert.Equal("invalid", SettingsField<TextBox>(window, "_realFastRamBox").Text);
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
        var window = new MainWindow([], new CopperScreenRecentSession(_root));
        window.Show();
        try
        {
            Assert.False(Button(window, "Use CopperOS").IsEffectivelyVisible);
            Assert.Contains("not enabled", CopperScreenAvailability.GetUnavailableReason(MenuDraft(window), AppContext.BaseDirectory));
            Assert.False(Directory.Exists(Path.Combine(_root, "Firmware")));
            Assert.Throws<NotSupportedException>(() => CopperScreenSession.Create(MenuDraft(window).ToStartupOptions(AppContext.BaseDirectory)));
            Click(Button(window, "Configure Amiga…"));
            Assert.False(SettingsField<Button>(window, "_settingsStartButton").IsEnabled);
        }
        finally { window.Close(); }
    });
}
