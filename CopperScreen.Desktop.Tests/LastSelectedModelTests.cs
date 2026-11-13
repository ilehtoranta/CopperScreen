using Avalonia.Controls;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed partial class LaunchWorkflowTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public Task SelectedModelAndMediaReopenWithoutStarting(int choice) => CheckSelectedModelAndMedia(choice);

    [CopperStartFact]
    public Task SelectedCopperOsModelAndMediaReopenWithoutStarting() => CheckSelectedModelAndMedia(3);

    private Task CheckSelectedModelAndMedia(int choice) => Ui(() =>
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
            if (choice == 0) LaunchSelection.Select(window, 1);
            LaunchSelection.Select(window, choice);
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
            Assert.Equal(choice, LaunchSelection.Current(reopened));
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
        try { LaunchSelection.Select(window, 2); }
        finally { window.Close(); }
        var reopened = new MainWindow([], new CopperScreenRecentSession(_root)); reopened.Show();
        try
        {
            Assert.Equal(2, LaunchSelection.Current(reopened));
            Assert.Null(MenuDraft(reopened).KickstartRomPath);
            Assert.False(Button(reopened, "Start Amiga").IsEnabled);
        }
        finally { reopened.Close(); }
    });
}
