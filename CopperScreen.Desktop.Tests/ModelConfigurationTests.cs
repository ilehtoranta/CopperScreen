using Avalonia.Controls;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed partial class LaunchWorkflowTests
{
    [Fact]
    public Task WholeMachineConfigurationsSurviveSwitchesAndRestartWithoutBoot() => Ui(() =>
    {
        var expected = new CopperScreenSettingsDraft[3];
        var window = new MainWindow([], new CopperScreenRecentSession(_root)); window.Show();
        try
        {
            for (var i = 0; i < 3; i++)
            {
                Named<ComboBox>(window, "Start configuration").SelectedIndex = i;
                var draft = MenuDraft(window);
                draft.CpuBackend = i == 2 ? M68kBackendKind.AccurateM68030 : M68kBackendKind.AccurateM68010;
                draft.RealFastRamKb = i == 2 ? 8192 : 2048;
                if (i == 1) draft.ChipRamKb = 2048;
                draft.RtcEnabled = i == 0;
                draft.FloppyDriveAudio = draft.FloppyDriveAudio with { Enabled = false };
                draft.Input = draft.Input.WithPortAssignment(2, CopperScreenControllerProfile.None.Id);
                draft.Description = "Custom machine " + i;
                expected[i] = draft.Clone();
                SetMenuDraft(window, draft);
            }
            Named<ComboBox>(window, "Start configuration").SelectedIndex = 3;
            Assert.Equal(M68kBackendKind.AccurateM68EC020, MenuDraft(window).CpuBackend);
            Assert.Equal(0, MenuDraft(window).RealFastRamKb);
            foreach (var i in new[] { 0, 1, 2 })
            {
                Named<ComboBox>(window, "Start configuration").SelectedIndex = i;
                AssertConfiguration(expected[i], MenuDraft(window));
            }
        }
        finally { window.Close(); }
        var reopened = new MainWindow([], new CopperScreenRecentSession(_root)); reopened.Show();
        try
        {
            Assert.Equal(2, Named<ComboBox>(reopened, "Start configuration").SelectedIndex);
            AssertConfiguration(expected[2], MenuDraft(reopened));
            foreach (var i in new[] { 1, 0, 2 })
            {
                Named<ComboBox>(reopened, "Start configuration").SelectedIndex = i;
                AssertConfiguration(expected[i], MenuDraft(reopened));
            }
        }
        finally { reopened.Close(); }
    });

    private static void AssertConfiguration(CopperScreenSettingsDraft expected, CopperScreenSettingsDraft actual)
    {
        Assert.Equal(expected.CpuBackend, actual.CpuBackend);
        Assert.Equal(expected.Chipset, actual.Chipset);
        Assert.Equal(expected.ChipRamKb, actual.ChipRamKb);
        Assert.Equal(expected.PseudoFastRamKb, actual.PseudoFastRamKb);
        Assert.Equal(expected.PseudoFastBase, actual.PseudoFastBase);
        Assert.Equal(expected.RealFastRamKb, actual.RealFastRamKb);
        Assert.Equal(expected.RealFastBase, actual.RealFastBase);
        Assert.Equal(expected.RtcEnabled, actual.RtcEnabled);
        Assert.Equal(expected.FloppyDriveAudio, actual.FloppyDriveAudio);
        Assert.Equal(expected.Input.Port2ProfileId, actual.Input.Port2ProfileId);
        Assert.Equal(expected.PresentationOptions, actual.PresentationOptions);
        Assert.Equal(expected.Description, actual.Description);
    }
}
