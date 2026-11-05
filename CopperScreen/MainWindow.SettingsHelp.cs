using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Styling;

namespace CopperScreen;

internal sealed partial class MainWindow
{
    private TextBlock _copperStartSettingsNote = null!;
    private TextBlock _bootSettingsHelp = null!;

    // Presentation only: help never participates in the settings draft or save workflow.
    private static Expander SettingsHelp(string title, string text) => SettingsHelp(title, SettingsNote(text));

    private static Expander SettingsHelp(string title, Control content)
    {
        var help = new Expander
        {
            Header = title, Content = content, IsExpanded = false, MinHeight = 32,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        help.Styles.Add(new Style(selector => selector.OfType<Expander>().Template().OfType<ToggleButton>())
        {
            Setters = { new Setter(MinHeightProperty, 32d), new Setter(PaddingProperty, new Thickness(8, 4)) }
        });
        AutomationProperties.SetName(help, title);
        return help;
    }

    private static TextBlock SettingsWriteWarning(CheckBox readOnly, string text, string name, TextBox? diskPath = null)
    {
        var warning = SettingsNote(text);
        AutomationProperties.SetName(warning, name);
        void Update() => warning.IsVisible = readOnly.IsChecked == false &&
            (diskPath == null || readOnly.IsEnabled && !string.IsNullOrWhiteSpace(diskPath.Text));
        readOnly.PropertyChanged += (_, args) =>
        {
            if (args.Property == CheckBox.IsCheckedProperty || args.Property == IsEnabledProperty) Update();
        };
        if (diskPath != null)
        {
            // TextChanged is deferred; property notifications also cover initial profile loading.
            diskPath.PropertyChanged += (_, args) => { if (args.Property == TextBox.TextProperty) Update(); };
        }
        Update();
        return warning;
    }

    private void UpdateSettingsBootHelp()
    {
        if (_bootSettingsHelp == null) return;
        var copperStart = ParseKickstartSourceSelection(_kickstartSourceBox.SelectedItem) == CopperScreenKickstartSource.CopperStart;
        var a1200 = _deniseModelBox.SelectedItem as string == "AgaLisa";
        _copperStartSettingsNote.IsVisible = copperStart;
        _copperStartSettingsNote.Text = a1200
            ? "Requires the qualified A1200 ROM pair, an OFS/FFS DD or HD floppy and/or one OFS/FFS/SFS/SFS2 hard disk. Native writes and quick formatting are supported. Disable Fast RAM, RTC and shared folders."
            : "A500/A500+ can use the bundled ROM experimentally. Compatibility is unverified. Disable Fast RAM, RTC and shared folders.";
        _bootSettingsHelp.Text = "Choose any supported licensed native Kickstart ROM. Recommended versions are 1.3 for Amiga 500, 2.04 for Amiga 500+ and 3.0 for Amiga 1200. Other model/version combinations are allowed and may have compatibility issues. " +
            "Minimal disk boot needs a standard ADF; it is experimental and has no DOS or Workbench. " +
            "Unavailable boot methods cannot be selected." +
            (copperStart
                ? "\n" + CopperScreenCopperStartRom.DescriptionFor(a1200)
                : "");
    }
}
