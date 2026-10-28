using System.Buffers.Binary;
using Avalonia.Controls;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed partial class LaunchWorkflowTests
{
    private string DefaultRomFixture(int choice, string name, bool kickstart31 = false)
    {
        var bytes = new byte[choice == 0 && !kickstart31 ? 262144 : 524288];
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(12), (ushort)(kickstart31 ? 40 : choice == 0 ? 34 : choice == 1 ? 37 : 39));
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(14), (ushort)(kickstart31 ? 63 : choice == 0 ? 5 : choice == 1 ? 175 : 106));
        var path = Path.Combine(_root, name + ".rom");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [Fact]
    public Task SwitchingModelsRestoresEachRomAndReplacementAcrossWindows() => Ui(() =>
    {
        var paths = Enumerable.Range(0, 3).Select(i => DefaultRomFixture(i, "model-" + i)).ToArray();
        var window = new MainWindow([], new CopperScreenRecentSession(_root)); window.Show();
        try
        {
            for (var i = 0; i < 3; i++)
            {
                Named<ComboBox>(window, "Start configuration").SelectedIndex = i;
                var draft = MenuDraft(window); draft.KickstartRomPath = paths[i];
                SetMenuDraft(window, draft);
            }
            // Going through CopperOS must not replace A1200's native ROM default.
            Named<ComboBox>(window, "Start configuration").SelectedIndex = 3;
            for (var i = 0; i < 3; i++)
            {
                Named<ComboBox>(window, "Start configuration").SelectedIndex = i;
                Assert.Equal(paths[i], MenuDraft(window).KickstartRomPath);
                Assert.True(Button(window, "Start Amiga").IsEnabled);
            }
            Named<ComboBox>(window, "Start configuration").SelectedIndex = 1;
            paths[1] = DefaultRomFixture(1, "replacement-204");
            var replacement = MenuDraft(window); replacement.KickstartRomPath = paths[1];
            SetMenuDraft(window, replacement);
            Named<ComboBox>(window, "Start configuration").SelectedIndex = 0;
        }
        finally { window.Close(); }
        var reopened = new MainWindow([], new CopperScreenRecentSession(_root)); reopened.Show();
        try
        {
            foreach (var i in new[] { 1, 2, 0 })
            {
                Named<ComboBox>(reopened, "Start configuration").SelectedIndex = i;
                Assert.Equal(paths[i], MenuDraft(reopened).KickstartRomPath);
                Assert.True(Button(reopened, "Start Amiga").IsEnabled);
            }
        }
        finally { reopened.Close(); }
    });

    [Fact]
    public void ValidRomSelectionIsSavedBeforeBootAndInvalidSelectionsKeepTheDefault()
    {
        var recent = new CopperScreenRecentSession(_root);
        var draft = CopperScreenLaunchWorkflow.SelectStartChoice(new(), 0);
        draft.KickstartRomPath = DefaultRomFixture(0, "a500-31", kickstart31: true);
        draft.KickstartSource = CopperScreenKickstartSource.Kickstart31Rom;
        draft.RomVersion = KickstartVersion.Kickstart31;
        recent.RememberKickstart(draft, AppContext.BaseDirectory);
        var expected = draft.KickstartRomPath;
        draft.KickstartRomPath = DefaultRomFixture(1, "wrong-model");
        recent.RememberKickstart(draft, AppContext.BaseDirectory);
        var restored = new CopperScreenRecentSession(_root).RestoreKickstart(CopperScreenLaunchWorkflow.SelectStartChoice(new(), 0));
        Assert.Equal(expected, restored.KickstartRomPath);
        Assert.Equal(KickstartVersion.Kickstart31, restored.RomVersion);
        Assert.Equal(CopperScreenKickstartSource.Kickstart31Rom, restored.KickstartSource);
        File.Delete(expected!);
        restored = new CopperScreenRecentSession(_root).RestoreKickstart(CopperScreenLaunchWorkflow.SelectStartChoice(new(), 0));
        Assert.Equal(expected, restored.KickstartRomPath); // Retain a missing default for repair.
        Assert.Null(recent.RestoreKickstart(CopperScreenLaunchWorkflow.SelectStartChoice(new(), 3)).KickstartRomPath);
    }

    [Fact]
    public void OldLastMachineSeedsDefaultAndAudioSavesPreserveIt()
    {
        var draft = CopperScreenLaunchWorkflow.SelectStartChoice(new(), 1);
        draft.KickstartRomPath = DefaultRomFixture(1, "legacy-204");
        CopperScreenProfileStore.SaveToPath(draft, Path.Combine(_root, "last-machine.json"), baseDirectory: AppContext.BaseDirectory);
        File.WriteAllText(Path.Combine(_root, "recent-session.json"), "{\"Version\":1,\"Volume\":37,\"Muted\":true}");
        var recent = new CopperScreenRecentSession(_root);
        Assert.NotNull(recent.LoadMachine(AppContext.BaseDirectory));
        recent.Save(null, 65, false);
        var reopened = new CopperScreenRecentSession(_root);
        Assert.Equal(draft.KickstartRomPath, reopened.RestoreKickstart(CopperScreenLaunchWorkflow.SelectStartChoice(new(), 1)).KickstartRomPath);
        Assert.Equal(65, reopened.Volume);
        Assert.False(reopened.Muted);
    }
}
