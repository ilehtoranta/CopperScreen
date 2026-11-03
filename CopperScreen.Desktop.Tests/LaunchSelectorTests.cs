using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed partial class LaunchWorkflowTests
{
    [Theory]
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
            var boot = Named<ComboBox>(window, "Boot method");
            var card = Avalonia.LogicalTree.LogicalExtensions.GetLogicalAncestors(boot).OfType<Border>().First();
            var size = card.Bounds.Size;
            boot.SelectedItem = "CopperStart";
            window.UpdateLayout();
            Assert.Equal(size, card.Bounds.Size);
            var draft = MenuDraft(window);
            Assert.Equal(model, (int)CopperScreenMachinePresets.Identify(draft));
            Assert.Null(CopperScreenAvailability.GetUnavailableReason(draft, AppContext.BaseDirectory));
            Assert.NotEmpty(CopperScreenCopperStartRom.Read(draft.KickstartRomPath, model == 2));
            disk = Assert.Single(draft.HardDrives).Path;
            Assert.True(Button(window, "Start CopperOS").IsEnabled);
            boot.SelectedItem = "KickstartRom";
            window.UpdateLayout(); Assert.Equal(size, card.Bounds.Size);
            boot.SelectedItem = "CopperStart";
            Assert.Equal(disk, Assert.Single(MenuDraft(window).HardDrives).Path);
        }
        finally { window.Close(); }
        var reopened = new MainWindow([], new CopperScreenRecentSession(_root)); reopened.Show();
        try
        {
            Assert.Equal(model, Named<ComboBox>(reopened, "Amiga model").SelectedIndex);
            Assert.Equal("CopperStart", Named<ComboBox>(reopened, "Boot method").SelectedItem);
            Assert.Equal(disk, Assert.Single(MenuDraft(reopened).HardDrives).Path);
        }
        finally { reopened.Close(); }
    });

    [Fact]
    public Task ExistingA500CopperStartProfileIsDisplayedWithoutConversion() => Ui(() =>
    {
        var draft = CopperScreenLaunchWorkflow.CopperStart(new());
        draft.KickstartRomPath = Path.Combine(_root, "user-copperstart.rom");
        new CopperScreenRecentSession(_root).Save(draft, 100, false, AppContext.BaseDirectory);
        var window = new MainWindow([], new CopperScreenRecentSession(_root)); window.Show();
        try
        {
            Assert.Equal(0, Named<ComboBox>(window, "Amiga model").SelectedIndex);
            Assert.Equal("CopperStart", Named<ComboBox>(window, "Boot method").SelectedItem);
            Assert.Equal(draft.KickstartRomPath, MenuDraft(window).KickstartRomPath);
            Assert.Equal(CopperScreenKickstartSource.CopperStart, MenuDraft(window).KickstartSource);
        }
        finally { window.Close(); }
    });
}
