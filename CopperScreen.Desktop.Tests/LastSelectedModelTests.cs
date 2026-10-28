using Avalonia.Controls;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed partial class LaunchWorkflowTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task SelectedModelAndMediaReopenWithoutStarting(int choice) => Ui(() =>
    {
        var recent = new CopperScreenRecentSession(_root);
        for (var i = 0; i < 3; i++)
        {
            var draft = CopperScreenLaunchWorkflow.SelectStartChoice(new(), i);
            draft.KickstartRomPath = DefaultRomFixture(i, "remembered-" + i);
            draft.DriveDiskPaths[0] = Path.Combine(_root, $"remembered-{i}.adf");
            recent.RememberKickstart(draft, AppContext.BaseDirectory);
            recent.RememberDisks(draft, AppContext.BaseDirectory);
        }
        string? rom;
        string? disk;
        string? hardDisk;
        var window = new MainWindow([], recent); window.Show();
        try
        {
            if (choice == 0) Named<ComboBox>(window, "Start configuration").SelectedIndex = 1;
            Named<ComboBox>(window, "Start configuration").SelectedIndex = choice;
            var draft = MenuDraft(window);
            rom = draft.KickstartRomPath;
            disk = draft.DriveDiskPaths[0];
            hardDisk = draft.HardDrives.FirstOrDefault()?.Path;
            Assert.False(Named<Button>(window, "Pause").IsEnabled);
        }
        finally { window.Close(); }
        var reopened = new MainWindow([], new CopperScreenRecentSession(_root)); reopened.Show();
        try
        {
            Assert.Equal(choice, Named<ComboBox>(reopened, "Start configuration").SelectedIndex);
            var draft = MenuDraft(reopened);
            Assert.Equal(rom, draft.KickstartRomPath);
            Assert.Equal(disk, draft.DriveDiskPaths[0]);
            Assert.Equal(hardDisk, draft.HardDrives.FirstOrDefault()?.Path);
            Assert.False(Named<Button>(reopened, "Pause").IsEnabled);
        }
        finally { reopened.Close(); }
    });

    [Fact]
    public Task UnconfiguredModelSelectionAlsoSurvivesClosing() => Ui(() =>
    {
        var window = new MainWindow([], new CopperScreenRecentSession(_root)); window.Show();
        try { Named<ComboBox>(window, "Start configuration").SelectedIndex = 2; }
        finally { window.Close(); }
        var reopened = new MainWindow([], new CopperScreenRecentSession(_root)); reopened.Show();
        try
        {
            Assert.Equal(2, Named<ComboBox>(reopened, "Start configuration").SelectedIndex);
            Assert.Null(MenuDraft(reopened).KickstartRomPath);
            Assert.False(Button(reopened, "Start Amiga").IsEnabled);
        }
        finally { reopened.Close(); }
    });
}
