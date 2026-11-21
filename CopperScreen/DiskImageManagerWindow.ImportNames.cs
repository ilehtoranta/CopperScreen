using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using CopperDisk.FileSystem;

namespace CopperScreen;

internal sealed partial class DiskImageManagerWindow
{
    private async Task<HostNameInterpretation?> ChooseImportNamesAsync(string[] paths, CancellationToken ct)
    {
        var preview = await Task.Run(() => HostNameCodec.Preview(paths), ct);
        if (preview.Count == 0) return HostNameInterpretation.RestoreEncoded;
        var dialog = OperationDialog("Restore Amiga names", 300);
        var grid = new Grid { Margin = new Thickness(12), RowDefinitions = new("Auto,*,Auto"), RowSpacing = 10 };
        grid.Children.Add(new TextBlock { Text = "Encoded host names were found. Choose how to import them." , TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        Add(grid, new ScrollViewer { Content = new TextBlock { Text = string.Join("\n", preview.Select(p => p.HostName + " → " + p.ImageName)), TextWrapping = Avalonia.Media.TextWrapping.Wrap } }, 1);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = Button("Cancel", () => { dialog.Close(null); return Task.CompletedTask; }); buttons.Children.Add(cancel);
        buttons.Children.Add(Button("Literal names", () => { dialog.Close((HostNameInterpretation?)HostNameInterpretation.Literal); return Task.CompletedTask; }));
        var restore = Button("Restore names", () => { dialog.Close((HostNameInterpretation?)HostNameInterpretation.RestoreEncoded); return Task.CompletedTask; });
        restore.IsEnabled = !preview.Any(p => p.ImageName == "(invalid encoding)"); buttons.Children.Add(restore);
        Add(grid, buttons, 2); dialog.Content = grid; dialog.Opened += (_, _) => cancel.Focus();
        return await dialog.ShowDialog<HostNameInterpretation?>(this);
    }
}
