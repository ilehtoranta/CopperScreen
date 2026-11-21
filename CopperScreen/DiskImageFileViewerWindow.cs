using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace CopperScreen;

internal sealed record FileViewReadResult(FileViewPage? Page, string? Error = null);

internal sealed class DiskImageFileViewerWindow : Window
{
    private readonly CopperScreenFileViewSource _source;
    private readonly Func<long, CancellationToken, Task<FileViewReadResult>> _read;
    private readonly ComboBox _mode = new() { ItemsSource = new[] { "Text", "Hex" }, Width = 76 };
    private readonly ComboBox _encoding = new() { ItemsSource = new[] { "UTF-8", "Latin-1" }, Width = 86 };
    private readonly TextBox _offset = new() { Width = 142 };
    private readonly TextBox _contents = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap,
        FontFamily = new FontFamily("Consolas, monospace"), FontSize = 13, Background = CopperScreenAppearance.Inset,
        Foreground = CopperScreenAppearance.Text, BorderBrush = CopperScreenAppearance.QuietBorder };
    private readonly TextBlock _status = new() { TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    private readonly StackPanel _navigation = new() { Orientation = Orientation.Horizontal, Spacing = 4 };
    private readonly Button _first, _previous, _next, _last, _go;
    private FileViewPage _page;
    private CancellationTokenSource? _pageCancellation;
    private Window? _closeOwner;
    private bool _loading, _closing, _failed;

    internal DiskImageFileViewerWindow(CopperScreenFileViewSource source, FileViewPage first, bool hex,
        Func<long, CancellationToken, Task<FileViewReadResult>> read)
    {
        _source = source; _page = first; _read = read;
        Title = $"{(hex ? "Hex" : "Text")} · {source.Name}";
        Width = 980; Height = 700; MinWidth = 760; MinHeight = 500; FontSize = 13;
        Background = CopperScreenAppearance.Background; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        AutomationProperties.SetName(this, "File viewer"); ToolTip.SetTip(_contents, source.Location);
        AutomationProperties.SetName(_contents, "File contents"); AutomationProperties.SetName(_status, "File viewer status");
        AutomationProperties.SetName(_mode, "View mode"); AutomationProperties.SetName(_encoding, "Text encoding");
        AutomationProperties.SetName(_offset, "Hex byte offset"); ToolTip.SetTip(_offset, "Hexadecimal byte offset; press Enter or Go");
        ToolTip.SetTip(_encoding, "UTF-8 or Amiga Latin-1. Nonprinting controls appear as dots.");
        ScrollViewer.SetHorizontalScrollBarVisibility(_contents, Avalonia.Controls.Primitives.ScrollBarVisibility.Auto);
        ScrollViewer.SetVerticalScrollBarVisibility(_contents, Avalonia.Controls.Primitives.ScrollBarVisibility.Auto);
        _mode.SelectedIndex = hex ? 1 : 0; _encoding.SelectedIndex = first.IsUtf8() ? 0 : 1;
        _first = Command("|<", "First page", () => LoadAsync(0));
        _previous = Command("<", "Previous page", () => LoadAsync(Math.Max(0, _page.Offset - CopperScreenFileViewSource.PageSize)));
        _next = Command(">", "Next page", () => LoadAsync(_page.Offset + _page.Count));
        _last = Command(">|", "Last page", () => LoadAsync(source.Length == 0 ? 0 : (source.Length - 1) / CopperScreenFileViewSource.PageSize * CopperScreenFileViewSource.PageSize));
        _go = Command("Go", "Go to offset", GoAsync);
        foreach (var combo in new[] { _mode, _encoding }) { combo.Height = 28; combo.MinHeight = 0; combo.Padding = new Thickness(4, 1); }
        _offset.Height = 28; _offset.MinHeight = 0; _offset.Padding = new Thickness(4, 2);
        foreach (var control in new Control[] { _mode, _encoding, _first, _previous, _next, _last, _offset, _go }) _navigation.Children.Add(control);
        var toolbar = new Grid { ColumnDefinitions = new("*,Auto"), ColumnSpacing = 6 };
        toolbar.Children.Add(_navigation);
        var close = Command("Close", "Close viewer", () => { Close(); return Task.CompletedTask; });
        Grid.SetColumn(close, 1); toolbar.Children.Add(close);
        var root = new Grid { RowDefinitions = new("28,*,22"), RowSpacing = 4, Margin = new Thickness(6) };
        root.Children.Add(toolbar); Grid.SetRow(_contents, 1); root.Children.Add(_contents); Grid.SetRow(_status, 2); root.Children.Add(_status); Content = root;
        _mode.SelectionChanged += (_, _) => Render(); _encoding.SelectionChanged += (_, _) => Render();
        _offset.KeyDown += async (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; await GoAsync(); } };
        AddHandler(KeyDownEvent, async (_, e) =>
        {
            if (e.Key == Key.Escape) { e.Handled = true; Close(); }
            else if (e.Key == Key.F3 && e.KeyModifiers is KeyModifiers.None or KeyModifiers.Shift)
            { e.Handled = true; _mode.SelectedIndex = e.KeyModifiers == KeyModifiers.Shift ? 1 : 0; }
            else if (e.KeyModifiers == KeyModifiers.Control && !_loading && !_failed && e.Key is Key.PageDown or Key.PageUp)
            {
                e.Handled = true;
                if (e.Key == Key.PageDown && _next.IsEnabled) await LoadAsync(_page.Offset + _page.Count);
                if (e.Key == Key.PageUp && _previous.IsEnabled) await LoadAsync(Math.Max(0, _page.Offset - CopperScreenFileViewSource.PageSize));
            }
        }, RoutingStrategies.Tunnel);
        Closing += (_, e) =>
        {
            if (!_loading) return;
            // Avalonia asks owned windows first. Retry the owner's close after
            // the cancelled read has released its stream and the child can close.
            if (e.CloseReason == WindowCloseReason.OwnerWindowClosing) _closeOwner = Owner as Window;
            e.Cancel = true; _closing = true; _pageCancellation?.Cancel(); _status.Text = "Closing…";
        };
        Opened += (_, _) => _contents.Focus(); Render();
    }

    private static Button Command(string text, string name, Func<Task> action)
    {
        var button = new Button { Content = text, Height = 28, MinHeight = 0, Padding = new Thickness(7, 1),
            Background = CopperScreenAppearance.ControlSurface, Foreground = CopperScreenAppearance.Text };
        AutomationProperties.SetName(button, name); ToolTip.SetTip(button, name);
        button.Click += async (_, _) => await action(); CopperScreenAppearance.ConfigureKeyboardFocus(button); return button;
    }

    private Task GoAsync()
    {
        var text = (_offset.Text ?? "").Trim(); if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text[2..];
        if (!long.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var offset) ||
            offset < 0 || (_source.Length == 0 ? offset != 0 : offset >= _source.Length))
        { _status.Text = "Enter a hexadecimal byte offset within the file."; ToolTip.SetTip(_status, _status.Text); return Task.CompletedTask; }
        return LoadAsync(offset);
    }

    private async Task LoadAsync(long offset)
    {
        if (_loading || _closing || _failed) return;
        _loading = true; UpdateNavigation(); _status.Text = "Reading…";
        using var cancellation = new CancellationTokenSource(); _pageCancellation = cancellation;
        try
        {
            var result = await _read(offset, cancellation.Token);
            if (_closing) return;
            if (result.Page == null)
            {
                _failed = true; _contents.Text = ""; _status.Text = result.Error ?? "The file could not be read. Close the viewer and reopen it.";
                ToolTip.SetTip(_status, _status.Text);
            }
            else { _page = result.Page; Render(); }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException or InvalidOperationException or TimeoutException)
        { _failed = true; _contents.Text = ""; _status.Text = ex.Message; ToolTip.SetTip(_status, _status.Text); }
        finally
        {
            _loading = false; _pageCancellation = null; UpdateNavigation();
            if (_closing) { Close(); if (_closeOwner?.IsVisible == true) _closeOwner.Close(); }
        }
    }

    private void Render()
    {
        if (_failed || _closing) return;
        var hex = _mode.SelectedIndex == 1;
        Title = $"{(hex ? "Hex" : "Text")} · {_source.Name}"; _encoding.IsVisible = !hex;
        _contents.Text = hex ? _page.Hex(_source.Length > uint.MaxValue) : _page.Text(_encoding.SelectedIndex == 0);
        _contents.CaretIndex = 0;
        _offset.Text = _page.Offset.ToString(_source.Length > uint.MaxValue ? "X16" : "X8", CultureInfo.InvariantCulture);
        _status.Text = _source.Length == 0 ? "Empty file · 0 bytes" : $"{_page.Offset:X8}–{_page.Offset + _page.Count - 1:X8} · {_source.Length:N0} bytes";
        ToolTip.SetTip(_status, _source.Location + "\n" + _status.Text); UpdateNavigation();
    }

    private void UpdateNavigation()
    {
        _navigation.IsEnabled = !_loading && !_closing && !_failed;
        _first.IsEnabled = _previous.IsEnabled = _page.Offset > 0;
        _next.IsEnabled = _page.Count < _source.Length - _page.Offset;
        _last.IsEnabled = _source.Length > 0 && _page.Offset != (_source.Length - 1) / CopperScreenFileViewSource.PageSize * CopperScreenFileViewSource.PageSize;
    }
}
