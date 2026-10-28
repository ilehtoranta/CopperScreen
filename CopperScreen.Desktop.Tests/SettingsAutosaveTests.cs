using System.Reflection;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Automation;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed partial class LaunchWorkflowTests
{
    private static T SettingsField<T>(MainWindow window, string name) =>
        (T)typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public Task SettingsEditsSurviveClosingWithoutStartingOrSwitching(int model) => UiAsync(async () =>
    {
        var window = new MainWindow([], new CopperScreenRecentSession(_root)); window.Show();
        var cpu = model == 2 ? M68kBackendKind.AccurateM68030 : M68kBackendKind.AccurateM68010;
        try
        {
            Named<ComboBox>(window, "Start configuration").SelectedIndex = model;
            Click(Button(window, "Configure Amiga…"));
            var settings = SettingsField<Window>(window, "_settingsWindow");
            settings.GetLogicalDescendants().OfType<ComboBox>().Single(c => AutomationProperties.GetName(c) == "Amiga CPU").SelectedItem = cpu.ToString();
            SettingsField<TextBox>(window, "_realFastRamBox").Text = "2048";
            SettingsField<CheckBox>(window, "_rtcEnabledBox").IsChecked = true;
            await Task.Delay(25); // Deliver queued text-change notifications.
            var saved = new CopperScreenRecentSession(_root).LoadMachine(AppContext.BaseDirectory)!;
            Assert.Equal(cpu, saved.CpuBackend);
            Assert.Equal(2048, saved.RealFastRamKb);
            Assert.True(saved.RtcEnabled);
        }
        finally { window.Close(); }
        var reopened = new MainWindow([], new CopperScreenRecentSession(_root)); reopened.Show();
        try
        {
            Assert.Equal(model, Named<ComboBox>(reopened, "Start configuration").SelectedIndex);
            Assert.Equal(cpu, MenuDraft(reopened).CpuBackend);
            Assert.Equal(2048, MenuDraft(reopened).RealFastRamKb);
            Assert.True(MenuDraft(reopened).RtcEnabled);
            Assert.False(Named<Button>(reopened, "Pause").IsEnabled);
        }
        finally { reopened.Close(); }
    });

    [Fact]
    public Task PartialNumericEditKeepsTheLastCompleteConfiguration() => UiAsync(async () =>
    {
        var window = new MainWindow([], new CopperScreenRecentSession(_root)); window.Show();
        try
        {
            Click(Button(window, "Configure Amiga…"));
            var ram = SettingsField<TextBox>(window, "_realFastRamBox");
            ram.Text = "2048";
            await Task.Delay(25); // Save the completed edit before beginning another.
            Assert.Equal(2048, new CopperScreenRecentSession(_root).LoadMachine(AppContext.BaseDirectory)!.RealFastRamKb);
            ram.Text = "invalid";
        }
        finally { window.Close(); }
        Assert.Equal(2048, new CopperScreenRecentSession(_root).LoadMachine(AppContext.BaseDirectory)!.RealFastRamKb);
    });

    [Fact]
    public Task ClosingImmediatelyAfterTypingFlushesTheEdit() => Ui(() =>
    {
        var window = new MainWindow([], new CopperScreenRecentSession(_root)); window.Show();
        Click(Button(window, "Configure Amiga…"));
        SettingsField<TextBox>(window, "_realFastRamBox").Text = "2048";
        window.Close();
        Assert.Equal(2048, new CopperScreenRecentSession(_root).LoadMachine(AppContext.BaseDirectory)!.RealFastRamKb);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ClosingSettingsKeepsEditsWhenReopeningOrClosingApp(bool windowClose) => Ui(() =>
    {
        var window = new MainWindow([], new CopperScreenRecentSession(_root)); window.Show();
        try
        {
            Click(Button(window, "Configure Amiga…"));
            SettingsField<TextBox>(window, "_realFastRamBox").Text = "2048";
            var settings = SettingsField<Window>(window, "_settingsWindow");
            SettingsField<Slider>(window, "_masterVolumeSlider").Value = 17;
            SettingsField<CheckBox>(window, "_masterMuteBox").IsChecked = true;
            if (windowClose) settings.Close();
            else Click(SettingsField<Button>(window, "_settingsCloseButton"));
            Assert.False(settings.IsVisible);
            Assert.Equal(2048, MenuDraft(window).RealFastRamKb);
            Click(Button(window, "Configure Amiga…"));
            Assert.Equal("2048", SettingsField<TextBox>(window, "_realFastRamBox").Text);
            Assert.Equal(17, SettingsField<Slider>(window, "_masterVolumeSlider").Value);
            Assert.True(SettingsField<CheckBox>(window, "_masterMuteBox").IsChecked);
            Assert.Equal("Close", SettingsField<Button>(window, "_settingsCloseButton").Content);
            SettingsField<TextBox>(window, "_realFastRamBox").Text = "invalid";
            Click(SettingsField<Button>(window, "_settingsCloseButton"));
            Assert.Equal(2048, MenuDraft(window).RealFastRamKb);
        }
        finally { window.Close(); }
        Assert.Equal(2048, new CopperScreenRecentSession(_root).LoadMachine(AppContext.BaseDirectory)!.RealFastRamKb);
    });
}
