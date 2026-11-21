using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed partial class LaunchWorkflowTests
{
    [CopperStartTheory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public Task CopperStartCanBeSelectedOnEveryModelWithoutResizingTheCard(int model) => Ui(() =>
    {
        var window = new MainWindow([], new CopperScreenRecentSession(_root)); window.Show();
        string disk;
        try
        {
            LaunchSelection.Select(window, model);
            window.UpdateLayout();
            var selector = Named<ComboBox>(window, "Amiga model");
            var card = Avalonia.LogicalTree.LogicalExtensions.GetLogicalAncestors(selector).OfType<Border>().First();
            var size = card.Bounds.Size;
            Click(Button(window, "Use CopperOS"));
            window.UpdateLayout();
            Assert.Equal(size, card.Bounds.Size);
            var draft = MenuDraft(window);
            Assert.Equal(model, (int)CopperScreenMachinePresets.Identify(draft));
            Assert.Null(CopperScreenAvailability.GetUnavailableReason(draft, AppContext.BaseDirectory));
            Assert.NotEmpty(CopperScreenCopperStartRom.Read(draft.KickstartRomPath, model == 2));
            disk = Assert.Single(draft.HardDrives).Path;
            Assert.True(Button(window, "Start CopperOS").IsEnabled);
            window.SelectOriginalRom(DefaultRomFixture(model, "original-for-layout"));
            window.UpdateLayout(); Assert.Equal(size, card.Bounds.Size);
            Click(Button(window, "Use CopperOS"));
            Assert.Equal(disk, Assert.Single(MenuDraft(window).HardDrives).Path);
        }
        finally { window.Close(); }
        var reopened = new MainWindow([], new CopperScreenRecentSession(_root)); reopened.Show();
        try
        {
            Assert.Equal(model, Named<ComboBox>(reopened, "Amiga model").SelectedIndex);
            Assert.Equal(CopperScreenKickstartSource.CopperStart, MenuDraft(reopened).KickstartSource);
            Assert.Equal(disk, Assert.Single(MenuDraft(reopened).HardDrives).Path);
        }
        finally { reopened.Close(); }
    });

    [CopperStartFact]
    public Task ExistingA500CopperStartProfileIsDisplayedWithoutConversion() => Ui(() =>
    {
        var draft = CopperScreenLaunchWorkflow.CopperStart(new());
        draft.KickstartRomPath = Path.Combine(_root, "user-copperstart.rom");
        new CopperScreenRecentSession(_root).Save(draft, 100, false, AppContext.BaseDirectory);
        var window = new MainWindow([], new CopperScreenRecentSession(_root)); window.Show();
        try
        {
            Assert.Equal(0, Named<ComboBox>(window, "Amiga model").SelectedIndex);
            Assert.Equal(draft.KickstartRomPath, MenuDraft(window).KickstartRomPath);
            Assert.Equal(CopperScreenKickstartSource.CopperStart, MenuDraft(window).KickstartSource);
        }
        finally { window.Close(); }
    });
}
