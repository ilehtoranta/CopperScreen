using System.Reflection;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using CopperMod.Amiga.Lightweight;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed partial class LaunchWorkflowTests
{
    private static void SettingsCall(MainWindow window, string method, params object[] args) =>
        typeof(MainWindow).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, args);

    private static T SettingsNamed<T>(Control root, string name) where T : Control => root.GetLogicalDescendants()
        .OfType<T>().Distinct().Single(c => AutomationProperties.GetName(c) == name);

    [Fact]
    public Task SettingsHelpWorksWithKeyboardAndPreservesDraftFocusAndExpansion() => UiAsync(async () =>
    {
        var window = new MainWindow([], new CopperScreenRecentSession(_root)); window.Show();
        try
        {
            window.SelectOriginalRom(DefaultRomFixture(0, "settings-help"));
            Click(Button(window, "Configure Amiga…"));
            var settings = SettingsField<Window>(window, "_settingsWindow");
            SettingsCall(window, "SelectSettingsPage", 2);
            settings.UpdateLayout();
            var help = SettingsNamed<Expander>(settings, "How do Chip RAM and Fast RAM work?");
            Assert.False(help.IsExpanded);
            var savedPath = Path.Combine(_root, "last-machine.json");
            var saved = File.ReadAllText(savedPath);
            var header = help.GetVisualDescendants().OfType<ToggleButton>().Single();
            Assert.InRange(header.Bounds.Height, 28, 36);
            Assert.True(header.Focus());
            settings.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null); settings.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
            settings.UpdateLayout();
            Assert.True(help.IsExpanded);
            Assert.True(header.IsFocused);
            Assert.True(Assert.IsType<TextBlock>(help.Content).IsEffectivelyVisible);
            SettingsCall(window, "SelectSettingsPage", 1);
            SettingsCall(window, "UpdateSettingsStatus");
            SettingsCall(window, "SelectSettingsPage", 2);
            await Task.Delay(25);
            Assert.True(help.IsExpanded);
            Assert.Equal(saved, File.ReadAllText(savedPath));
            Assert.Equal("Saved · Applies on start", SettingsField<TextBlock>(window, "_settingsStatus").Text);
            settings.UpdateLayout(); header.Focus();
            settings.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null); settings.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
            Assert.False(help.IsExpanded);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task BootHelpIsContextualAndErrorsStayVisibleWithHelpClosed() => Ui(() =>
    {
        var window = new MainWindow([], new CopperScreenRecentSession(_root)); window.Show();
        try
        {
            window.SelectOriginalRom(DefaultRomFixture(0, "boot-help"));
            Click(Button(window, "Configure Amiga…"));
            var settings = SettingsField<Window>(window, "_settingsWindow");
            var note = SettingsField<TextBlock>(window, "_copperStartSettingsNote");
            Assert.False(note.IsVisible);
            Assert.False(SettingsNamed<Expander>(settings, "Which boot ROM should I use?").IsExpanded);
            SettingsField<TextBox>(window, "_realFastRamBox").Text = "0";
            SettingsField<TextBox>(window, "_kickstartRomBox").Text = Path.Combine(_root, "missing.rom");
            SettingsCall(window, "UpdateSettingsStatus");
            Assert.False(note.IsVisible);
            Assert.True(SettingsField<TextBlock>(window, "_romValidation").IsEffectivelyVisible);
            Assert.False(SettingsField<Button>(window, "_settingsStartButton").IsEnabled);
            Assert.DoesNotContain("Saved", SettingsField<TextBlock>(window, "_settingsStatus").Text);
            SettingsCall(window, "SelectSettingsPage", 4);
            settings.UpdateLayout();
            var pixel = SettingsField<ComboBox>(window, "_pixelAspectModeBox");
            Assert.Equal("LCD crisp (1:1 hires / 1:2 superhires)", pixel.SelectedItem);
            Assert.Contains(pixel.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "LCD crisp");
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task PlaybackGuidanceIsInlineAndKeyBindingHintFollowsProfile() => Ui(() =>
    {
        var window = new MainWindow([], new CopperScreenRecentSession(_root)); window.Show();
        try
        {
            Click(Button(window, "Configure Amiga…"));
            var settings = SettingsField<Window>(window, "_settingsWindow");
            SettingsCall(window, "SelectSettingsPage", 5);
            var volumeHint = settings.GetLogicalDescendants().OfType<TextBlock>()
                .First(t => t.Text == "Adjusts playback volume in CopperScreen.");
            Assert.True(volumeHint.IsEffectivelyVisible);

            SettingsCall(window, "SelectSettingsPage", 6);
            var chooser = SettingsField<ComboBox>(window, "_controllerProfileChooser");
            var profiles = chooser.Items.OfType<CopperScreenControllerProfile>();
            var hint = settings.GetLogicalDescendants().OfType<TextBlock>()
                .First(t => t.Text == "Select a keyboard joystick profile to change its keys.");
            chooser.SelectedItem = profiles.First(p => p.Kind == CopperScreenControllerKind.Mouse);
            Assert.True(hint.IsEffectivelyVisible);
            chooser.SelectedItem = profiles.First(p => p.Kind == CopperScreenControllerKind.KeyboardJoystick);
            Assert.False(hint.IsVisible);
        }
        finally { window.Close(); }
    });

    [CopperStartFact]
    public Task CopperStartBootHelpAppearsOnlyForItsBootMethod() => Ui(() =>
    {
        var window = new MainWindow([], new CopperScreenRecentSession(_root)); window.Show();
        try
        {
            Click(Button(window, "Configure Amiga…"));
            var note = SettingsField<TextBlock>(window, "_copperStartSettingsNote");
            Assert.False(note.IsVisible);
            Click(SettingsField<Button>(window, "_settingsUseCopperOs"));
            Assert.True(note.IsVisible); Assert.Contains("A500", note.Text);
            Assert.False(SettingsNamed<Expander>(SettingsField<Window>(window, "_settingsWindow"), "Which boot ROM should I use?").IsExpanded);
            window.SelectOriginalRom(DefaultRomFixture(0, "return-from-copperos"));
            Assert.False(note.IsVisible);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task WritableStorageWarningsFollowLoadedAndEditedProtection() => UiAsync(async () =>
    {
        var disk = Path.Combine(_root, "warning.adf"); File.WriteAllBytes(disk, new byte[901120]);
        var draft = new CopperScreenSettingsDraft { KickstartRomPath = DefaultRomFixture(0, "warning-rom") };
        draft.DriveDiskPaths[0] = disk; draft.DriveWriteProtected[0] = false;
        draft.HardDrives.Add(new(0, Path.Combine(_root, "warning.hdf"), false, 0));
        draft.HostDirectories.Add(new LightweightHostDirectoryConfiguration("HOST0", "Shared", _root, false));
        var recent = new CopperScreenRecentSession(_root);
        recent.Save(draft, 100, false, AppContext.BaseDirectory);
        Assert.Null(recent.LastError);
        var loaded = new CopperScreenRecentSession(_root).LoadMachine(AppContext.BaseDirectory);
        Assert.NotNull(loaded);
        Assert.False(loaded.DriveWriteProtected[0]);
        var window = new MainWindow([], new CopperScreenRecentSession(_root)); window.Show();
        try
        {
            Click(Button(window, "Configure Amiga…")); SettingsCall(window, "SelectSettingsPage", 3);
            var settings = SettingsField<Window>(window, "_settingsWindow"); settings.UpdateLayout();
            var floppy = SettingsNamed<TextBlock>(settings, "DF0 write warning");
            var protect = SettingsField<CheckBox[]>(window, "_driveWriteProtectBoxes")[0];
            Assert.True(floppy.IsEffectivelyVisible, $"Visible={floppy.IsVisible}, protection={protect.IsChecked}, enabled={protect.IsEnabled}, path={SettingsField<TextBox[]>(window, "_drivePathBoxes")[0].Text}");
            protect.IsChecked = true; Assert.False(floppy.IsVisible);
            protect.IsChecked = false; Assert.True(floppy.IsVisible);
            SettingsNamed<TabControl>(settings, "Storage tabs").SelectedIndex = 1;
            settings.UpdateLayout();
            foreach (var (field, name) in new[] { ("_hardfileRows", "Hard disk 0 write warning"), ("_sharedFolderRows", "Shared folder HOST0 write warning") })
            {
                var rows = SettingsField<StackPanel>(window, field);
                var warning = SettingsNamed<TextBlock>(rows, name);
                Assert.True(warning.IsEffectivelyVisible);
                var readOnly = rows.GetLogicalDescendants().OfType<CheckBox>().Single(c => c.Content as string == "Read-only");
                readOnly.IsChecked = true; Assert.False(warning.IsVisible);
                readOnly.IsChecked = false; Assert.True(warning.IsVisible);
            }
            var hdfPath = SettingsNamed<TextBox>(SettingsField<StackPanel>(window, "_hardfileRows"), "Image path");
            hdfPath.Text = Path.Combine(_root, "different.hdf"); await Task.Delay(25);
            Assert.False(SettingsNamed<TextBlock>(settings, "Hard disk 0 write warning").IsVisible);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task FailedAutosaveCannotReportSavedEvenWhenTransientErrorIsCleared() => UiAsync(async () =>
    {
        var window = new MainWindow([], new CopperScreenRecentSession(_root)); window.Show();
        try
        {
            window.SelectOriginalRom(DefaultRomFixture(0, "save-error"));
            Click(Button(window, "Configure Amiga…"));
            Assert.Equal("Saved · Applies on start", SettingsField<TextBlock>(window, "_settingsStatus").Text);
            var path = Path.Combine(_root, "last-machine.json"); File.Delete(path); Directory.CreateDirectory(path);
            SettingsField<TextBox>(window, "_profileDescriptionBox").Text = "Cannot save this edit";
            await Task.Delay(25);
            SettingsCall(window, "ClearSettingsStartupError"); SettingsCall(window, "UpdateSettingsStatus");
            var status = SettingsField<TextBlock>(window, "_settingsStatus").Text;
            Assert.Contains("Could not save", status); Assert.DoesNotContain("Saved", status);
        }
        finally { window.Close(); }
    });
}
