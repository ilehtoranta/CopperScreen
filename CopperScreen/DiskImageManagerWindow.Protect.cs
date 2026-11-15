using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;

namespace CopperScreen;

internal sealed partial class DiskImageManagerWindow
{
    private async Task ProtectPromptAsync()
    {
        if (!_protect.IsEnabled) return;
        var entries = _active.List.SelectedItems!.OfType<ImageItem>().Select(i => i.Entry).ToArray();
        var dialog = OperationDialog("Protect entries", 270);
        var content = new StackPanel { Margin = new Thickness(12), Spacing = 6 };
        content.Children.Add(new TextBlock { Text = $"{entries.Length} selected entries · contents unchanged" });
        content.Children.Add(new TextBlock { Text = "Mixed flags stay unchanged unless selected." });
        var grid = new Grid { ColumnDefinitions = new("*,*"), RowDefinitions = new("Auto,Auto,Auto,Auto"), RowSpacing = 4 };
        content.Children.Add(grid);
        var flags = new List<(CheckBox Check, uint Bit, bool Inverted)>();
        foreach (var (label, bit, inverted) in new[] {
            ("Readable", 8u, true), ("Writable", 4u, true), ("Executable", 2u, true), ("Deletable", 1u, true),
            ("Archive", 16u, false), ("Pure", 32u, false), ("Script", 64u, false), ("Hold", 128u, false) })
        {
            var values = entries.Select(e => ((e.Protection & bit) != 0) != inverted).Distinct().ToArray();
            var check = new CheckBox { Content = label, IsThreeState = entries.Length > 1, IsChecked = values.Length == 1 ? values[0] : null, MinHeight = 24 };
            AutomationProperties.SetName(check, label); Grid.SetRow(check, flags.Count % 4); Grid.SetColumn(check, flags.Count / 4);
            grid.Children.Add(check); flags.Add((check, bit, inverted));
        }
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = Button("Cancel", () => { dialog.Close(false); return Task.CompletedTask; }); buttons.Children.Add(cancel);
        uint mask = 0, value = 0;
        buttons.Children.Add(Button("Apply", () =>
        {
            foreach (var flag in flags)
                if (flag.Check.IsChecked is { } enabled)
                {
                    mask |= flag.Bit;
                    if (enabled != flag.Inverted) value |= flag.Bit;
                }
            dialog.Close(true); return Task.CompletedTask;
        }));
        content.Children.Add(buttons); dialog.Content = content; dialog.Opened += (_, _) => cancel.Focus();
        if (await dialog.ShowDialog<bool>(this))
            await EditAsync((image, volume, parent, ct) => image.Protect(volume, parent, entries, mask, value, ct), "Protection updated.");
    }
}
