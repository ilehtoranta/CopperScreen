using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace CopperScreen;

internal sealed partial class DiskImageManagerWindow
{
    private readonly Grid _body = new() { ColumnDefinitions = new("*,92,*") };
    private readonly Grid _toolbar = new() { ColumnDefinitions = new("Auto,*,Auto"), ColumnSpacing = 8 };
    private readonly StackPanel _commands = new() { Spacing = 3 };
    private readonly TextBlock _imageName = Note(""), _status = Note("Open an image to browse.");
    private Button _copy = null!, _cancel = null!, _help = null!, _parent = null!, _refresh = null!, _all = null!, _invert = null!, _clear = null!, _info = null!;
    private Button _newDirectory = null!, _rename = null!, _delete = null!, _move = null!, _protect = null!, _text = null!, _hex = null!;

    private sealed class Pane(bool left, bool host)
    {
        internal string Side { get; } = left ? "Left" : "Right";
        internal bool IsHost = host, SettingMode, SizeSort, Descending;
        internal string HostDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        internal CopperScreenDiskImage? Image;
        internal DiskImageVolume? Volume;
        internal readonly Stack<DiskImageEntry> Directories = new();
        internal string DirectoryPath => IsHost ? HostDirectory : Directories.Count == 0 ? "/" : Directories.Peek().Path;
        internal readonly ListBox List = new() { SelectionMode = SelectionMode.Multiple, Focusable = true };
        internal readonly ComboBox Kind = new() { ItemsSource = new[] { "Host", "Image" }, SelectedIndex = host ? 0 : 1 };
        internal readonly ComboBox Volumes = new();
        internal readonly TextBox HostPath = new();
        internal readonly TextBlock ImagePath = Note("/"), HostLabel = Note("");
        internal Border Frame = null!, Header = null!;
        internal Button NameColumn = null!, SizeColumn = null!;
    }

    private sealed record HostItem(string Name, string Path, bool IsDirectory, long Size)
    {
        public override string ToString() => IsDirectory ? $"▸ {Name}/" : $"{Name}    {Size:N0} bytes";
    }
    private sealed record ImageItem(DiskImageEntry Entry)
    {
        public override string ToString() => Entry.IsLink ? $"{Entry.Name}    [link — unavailable]"
            : Entry.IsDirectory ? $"▸ {Entry.Name}/" : $"{Entry.Name}    {Entry.Size:N0} bytes";
    }
    private sealed record VolumeItem(DiskImageVolume Volume, string ImageName)
    {
        public override string ToString() => Volume.UnsupportedReason == null
            ? $"{ImageName} · {Volume.DeviceName}: {Volume.DisplayName} · {Volume.FileSystemName}"
            : $"{ImageName} · {Volume.DeviceName}: unavailable";
    }

    private void BuildLayout()
    {
        var rowStyle = new Style(x => x.OfType<ListBoxItem>());
        rowStyle.Setters.Add(new Setter(MinHeightProperty, 22d)); rowStyle.Setters.Add(new Setter(HeightProperty, 22d));
        rowStyle.Setters.Add(new Setter(PaddingProperty, new Thickness(4, 0)));
        rowStyle.Setters.Add(new Setter(MarginProperty, new Thickness(0, 0, 16, 0)));
        rowStyle.Setters.Add(new Setter(HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch)); Styles.Add(rowStyle);
        var root = new Grid { Margin = new Thickness(6), RowDefinitions = new("28,*,22"), RowSpacing = 2 };
        _toolbar.Children.Add(Button("Open image…", () => ChooseImageAsync(_active)));
        Grid.SetColumn(_imageName, 1); _toolbar.Children.Add(_imageName);
        _help = Button("Help", () => ShowTextAsync("Disk manager help", HelpText));
        Grid.SetColumn(_help, 2); _toolbar.Children.Add(_help); root.Children.Add(_toolbar);
        foreach (var pane in _panes) BuildPane(pane);
        _body.Children.Add(_left.Frame); Grid.SetColumn(_right.Frame, 2); _body.Children.Add(_right.Frame);
        var middle = new Grid { ColumnDefinitions = new("*,80,*"), ColumnSpacing = 6 };
        Grid.SetColumnSpan(_body, 3); middle.Children.Add(_body);
        var rail = new StackPanel { Spacing = 10, Margin = new Thickness(0, 26, 0, 0) };
        var copySlot = new Grid();
        _copy = Button("← Copy", CopyActiveAsync); _copy.Background = CopperScreenAppearance.Accent; _copy.Foreground = CopperScreenAppearance.OnAccent;
        _cancel = Button("Cancel", () => { _operation?.Cancel(); return Task.CompletedTask; }); _cancel.IsVisible = false;
        AutomationProperties.SetName(_copy, "Copy active selection"); AutomationProperties.SetName(_cancel, "Cancel operation");
        copySlot.Children.Add(_copy); copySlot.Children.Add(_cancel); rail.Children.Add(copySlot);
        _parent = Button("Parent", ParentAsync); _refresh = Button("Refresh", RefreshAsync);
        _move = Button("← Move", () => MovePromptAsync(_active, Other)); AutomationProperties.SetName(_move, "Move active selection");
        _protect = Button("Protect", ProtectPromptAsync);
        _newDirectory = Button("New dir", NewDirectoryPromptAsync); _rename = Button("Rename", RenamePromptAsync); _delete = Button("Delete", DeletePromptAsync);
        _all = Button("All", () => SelectAsync(SelectionAction.All)); _invert = Button("Invert", () => SelectAsync(SelectionAction.Invert));
        _clear = Button("Clear", () => SelectAsync(SelectionAction.Clear)); _info = Button("Info", ShowInfoAsync);
        _text = Button("Text", () => ShowFileAsync(_active, false)); _hex = Button("Hex", () => ShowFileAsync(_active, true));
        ToolTip.SetTip(_parent, "Parent directory (Alt+Up)"); ToolTip.SetTip(_refresh, "Refresh active pane (Ctrl+R)");
        ToolTip.SetTip(_all, "Select all (Ctrl+A)"); ToolTip.SetTip(_invert, "Invert selection"); ToolTip.SetTip(_clear, "Clear selection");
        foreach (var command in new[] { _move, _parent, _refresh, _text, _hex, _newDirectory, _rename, _delete, _protect, _all, _invert, _clear, _info }) _commands.Children.Add(command);
        rail.Children.Add(_commands); Grid.SetColumn(rail, 1); middle.Children.Add(rail); Add(root, middle, 1);
        AutomationProperties.SetName(_status, "Disk manager status");
        _status.PropertyChanged += (_, e) => { if (e.Property == TextBlock.TextProperty) { ToolTip.SetTip(_status, _status.Text); AutomationProperties.SetHelpText(_status, _status.Text); } };
        Add(root, _status, 2); Content = root;
    }

    private void BuildPane(Pane pane)
    {
        var list = pane.List; list.Name = pane.Side + "Files";
        list.FontFamily = new FontFamily("Consolas, monospace"); list.Padding = new Thickness(0); list.BorderThickness = new Thickness(0);
        list.Background = CopperScreenAppearance.Inset;
        ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled); ScrollViewer.SetVerticalScrollBarVisibility(list, ScrollBarVisibility.Visible);
        list.ItemTemplate = new FuncDataTemplate<object>((item, _) => FileRow(item)); CopperScreenAppearance.ConfigureKeyboardFocus(list);
        ConfigureDragDrop(pane);
        var heading = new Grid { ColumnDefinitions = new("72,*,26"), ColumnSpacing = 3 };
        foreach (var combo in new[] { pane.Kind, pane.Volumes })
        {
            combo.Height = 26; combo.MinHeight = 0; combo.Padding = new Thickness(4, 1); combo.CornerRadius = new CornerRadius(2);
            combo.HorizontalAlignment = HorizontalAlignment.Stretch;
        }
        pane.Kind.Name = pane.Side + "Location"; pane.Volumes.Name = pane.Side + "Volumes";
        AutomationProperties.SetName(pane.Kind, pane.Side + " location type");
        AutomationProperties.SetName(pane.Volumes, pane.Side == "Right" ? "Image volumes" : "Left image volumes");
        pane.Volumes.ItemTemplate = new FuncDataTemplate<VolumeItem>((item, _) => { var text = Note(item?.ToString() ?? "No image"); ToolTip.SetTip(text, item?.ToString()); return text; });
        heading.Children.Add(pane.Kind);
        var location = new Grid(); location.Children.Add(pane.Volumes); location.Children.Add(pane.HostLabel);
        Grid.SetColumn(location, 1); heading.Children.Add(location);
        var choose = Button("…", () => pane.IsHost ? ChooseHostAsync(pane) : ChooseImageAsync(pane));
        AutomationProperties.SetName(choose, pane.Side + " choose location"); ToolTip.SetTip(choose, "Choose a host folder or image for this pane");
        Grid.SetColumn(choose, 2); heading.Children.Add(choose);
        pane.Header = new Border { Child = heading, Background = CopperScreenAppearance.ControlSurface, Padding = new Thickness(2, 0) };
        var columns = new Grid { ColumnDefinitions = new("*,96"), Margin = new Thickness(30, 0, 16, 0) };
        pane.NameColumn = Button("Name ↑", () => SortAsync(pane, false)); pane.SizeColumn = Button("Size", () => SortAsync(pane, true));
        pane.NameColumn.Padding = new Thickness(0, 1); pane.NameColumn.HorizontalContentAlignment = HorizontalAlignment.Left;
        pane.SizeColumn.HorizontalContentAlignment = HorizontalAlignment.Right;
        foreach (var button in new[] { pane.NameColumn, pane.SizeColumn }) { button.Height = 22; button.Background = Brushes.Transparent; button.BorderThickness = new Thickness(0); }
        ToolTip.SetTip(pane.SizeColumn, "Size in bytes");
        columns.Children.Add(pane.NameColumn); Grid.SetColumn(pane.SizeColumn, 1); columns.Children.Add(pane.SizeColumn);
        pane.HostPath.Text = pane.HostDirectory; pane.HostPath.Height = 26; pane.HostPath.MinHeight = 0;
        pane.HostPath.Padding = new Thickness(5, 2); pane.HostPath.CornerRadius = new CornerRadius(2);
        AutomationProperties.SetName(pane.HostPath, pane.Side == "Left" ? "Host folder path" : "Right host folder path");
        pane.ImagePath.Margin = new Thickness(5, 0); AutomationProperties.SetName(pane.ImagePath, pane.Side + " image directory path");
        var path = new Grid(); path.Children.Add(pane.HostPath); path.Children.Add(pane.ImagePath);
        var content = new Grid { RowDefinitions = new("28,22,*,26") };
        content.Children.Add(pane.Header); Add(content, columns, 1); Add(content, list, 2); Add(content, path, 3);
        pane.Frame = new Border { Child = content, BorderThickness = new Thickness(1), Background = CopperScreenAppearance.Inset };
        pane.Frame.AddHandler(PointerPressedEvent, (_, _) => SetActivePane(pane), RoutingStrategies.Tunnel);
        pane.Frame.PropertyChanged += (_, e) => { if (e.Property == IsKeyboardFocusWithinProperty && pane.Frame.IsKeyboardFocusWithin) SetActivePane(pane); };
        list.SelectionChanged += (_, _) => { UpdateCommands(); SelectionStatus(); };
        list.DoubleTapped += async (_, _) => await OpenSelectionAsync(pane);
        list.KeyDown += async (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; await OpenSelectionAsync(pane); } };
        pane.HostPath.KeyDown += async (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; await RunAsync(ct => ReadHostAsync(pane, pane.HostPath.Text ?? "", ct)); } };
        pane.HostPath.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) UpdateCommands(); };
        pane.Volumes.SelectionChanged += async (_, _) =>
        {
            if (pane.Volumes.SelectedItem is VolumeItem selected) await RunAsync(ct => SelectVolumeAsync(pane, selected.Volume, ct));
        };
        pane.Kind.SelectionChanged += async (_, _) =>
        {
            if (pane.SettingMode || _busy) return;
            if (pane.Kind.SelectedIndex == 1 && pane.IsHost) await ChooseImageAsync(pane);
            else if (pane.Kind.SelectedIndex == 0 && !pane.IsHost) await RunAsync(ct => ReadHostAsync(pane, pane.HostDirectory, ct));
            ApplyMode(pane);
        };
        ApplyMode(pane);
    }

    private static void ApplyMode(Pane pane)
    {
        pane.SettingMode = true; pane.Kind.SelectedIndex = pane.IsHost ? 0 : 1; pane.SettingMode = false;
        pane.HostPath.IsVisible = pane.HostLabel.IsVisible = pane.IsHost;
        pane.ImagePath.IsVisible = pane.Volumes.IsVisible = !pane.IsHost;
        AutomationProperties.SetName(pane.List, pane.Side == "Left" ? pane.IsHost ? "Host files" : "Left image files" : pane.IsHost ? "Right host files" : "Image files");
        var role = pane.IsHost ? "host" : "image";
        AutomationProperties.SetName(pane.NameColumn, $"Sort {(pane.Side == "Left" && pane.IsHost || pane.Side == "Right" && !pane.IsHost ? role : pane.Side.ToLowerInvariant() + " " + role)} by name");
        AutomationProperties.SetName(pane.SizeColumn, $"Sort {(pane.Side == "Left" && pane.IsHost || pane.Side == "Right" && !pane.IsHost ? role : pane.Side.ToLowerInvariant() + " " + role)} by size");
    }

    private Task SortAsync(Pane pane, bool size)
    {
        pane.Descending = pane.SizeSort == size && !pane.Descending; pane.SizeSort = size;
        var arrow = pane.Descending ? " ↓" : " ↑";
        pane.NameColumn.Content = "Name" + (size ? "" : arrow); pane.SizeColumn.Content = "Size" + (size ? arrow : "");
        SortItems(pane); SelectionStatus(); return Task.CompletedTask;
    }
    private static void SortItems(Pane pane)
    {
        var list = pane.List; var selected = list.SelectedItems!.Cast<object>().ToHashSet();
        string Name(object i) => i is HostItem h ? h.Name : ((ImageItem)i).Entry.Name;
        long Size(object i) => i is HostItem h ? h.Size : ((ImageItem)i).Entry.Size;
        var groups = list.Items.Cast<object>().GroupBy(i => i is HostItem h ? h.IsDirectory : ((ImageItem)i).Entry.IsDirectory).OrderByDescending(g => g.Key);
        var sorted = groups.SelectMany(g => pane.SizeSort
            ? (pane.Descending ? g.OrderByDescending(Size) : g.OrderBy(Size)).ThenBy(Name, StringComparer.OrdinalIgnoreCase)
            : pane.Descending ? g.OrderByDescending(Name, StringComparer.OrdinalIgnoreCase) : g.OrderBy(Name, StringComparer.OrdinalIgnoreCase)).ToArray();
        list.UnselectAll(); list.ItemsSource = sorted;
        foreach (var item in sorted) if (selected.Contains(item)) list.SelectedItems!.Add(item);
    }

    private static Control FileRow(object item)
    {
        var (name, directory, link, size) = item switch
        {
            HostItem h => (h.Name, h.IsDirectory, false, h.Size),
            ImageItem i => (i.Entry.Name, i.Entry.IsDirectory, i.Entry.IsLink, i.Entry.Size),
            _ => ("", false, false, 0L)
        };
        var row = new Grid { ColumnDefinitions = new("26,*,96"), Height = 22 };
        if (directory && !link) row.Children.Add(new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse("M1,3 L6,3 L8,5 L15,5 L15,13 L1,13 Z"), Stroke = CopperScreenAppearance.Accent, StrokeThickness = 1,
            Width = 15, Height = 14, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(1, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center
        });
        else row.Children.Add(Note(link ? "↗" : ""));
        var label = Note(name); label.Foreground = CopperScreenAppearance.Text; Grid.SetColumn(label, 1); row.Children.Add(label);
        var length = Note(link ? "Link" : directory ? "" : size.ToString("N0")); length.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(length, 2); row.Children.Add(length); ToolTip.SetTip(row, link ? $"{name} — link; cannot be opened or copied" : name);
        return row;
    }

    private Task ShowInfoAsync()
    {
        var selected = _active.List.SelectedItems!.Cast<object>().ToArray(); if (selected.Length != 1) return Task.CompletedTask;
        if (selected[0] is ImageItem i)
            return ShowTextAsync("Entry information", $"{i.Entry.Name} · {(i.Entry.Modified?.ToString("yyyy-MM-dd HH:mm:ss") ?? "date unavailable")} · Protection: {i.Entry.Protection:X8}\n{i.Entry.Comment}");
        try
        {
            var h = (HostItem)selected[0]; FileSystemInfo info = h.IsDirectory ? new DirectoryInfo(h.Path) : new FileInfo(h.Path);
            return ShowTextAsync("Entry information", $"{h.Name}\n{h.Path}\n{(h.IsDirectory ? "Directory" : $"{h.Size:N0} bytes")}\nModified: {info.LastWriteTime:yyyy-MM-dd HH:mm:ss}\nAttributes: {info.Attributes}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { _status.Text = ex.Message; return Task.CompletedTask; }
    }

    private Task ShowTextAsync(string title, string text)
    {
        var content = new StackPanel { Spacing = 8, MaxWidth = 520 };
        content.Children.Add(new TextBlock { Text = title, FontWeight = FontWeight.SemiBold });
        var body = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, MaxWidth = 520 }; AutomationProperties.SetName(body, title); content.Children.Add(body);
        var flyout = new Flyout { Content = new ScrollViewer { Content = content, MaxHeight = 420 } };
        Control anchor = title == "Entry information" ? _info : _help; FlyoutBase.SetAttachedFlyout(anchor, flyout); flyout.ShowAt(anchor); return Task.CompletedTask;
    }

    private Task<string?> AskNameAsync(string title, string value, string accept)
    {
        var dialog = OperationDialog(title, 160);
        var input = new TextBox { Text = value, PlaceholderText = "Name" }; AutomationProperties.SetName(input, "Entry name");
        var content = new StackPanel { Spacing = 12, Margin = new Thickness(12) }; content.Children.Add(input);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(Button("Cancel", () => { dialog.Close(null); return Task.CompletedTask; }));
        buttons.Children.Add(Button(accept, () => { if (!string.IsNullOrEmpty(input.Text)) dialog.Close(input.Text); return Task.CompletedTask; }));
        content.Children.Add(buttons); dialog.Content = content;
        input.KeyDown += (_, e) => { if (e.Key == Key.Enter && !string.IsNullOrEmpty(input.Text)) { dialog.Close(input.Text); e.Handled = true; } };
        dialog.Opened += (_, _) => { input.Focus(); input.SelectAll(); };
        return dialog.ShowDialog<string?>(this);
    }
    private Task<bool> ConfirmDeleteAsync(string text) => ConfirmAsync("Delete entries", "Delete", text);
    private Task<bool> ConfirmAsync(string title, string accept, string text)
    {
        var dialog = OperationDialog(title, 300);
        var content = new Grid { RowDefinitions = new("*,Auto"), RowSpacing = 12, Margin = new Thickness(12) };
        content.Children.Add(new ScrollViewer { Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap } });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = Button("Cancel", () => { dialog.Close(false); return Task.CompletedTask; });
        buttons.Children.Add(cancel); buttons.Children.Add(Button(accept, () => { dialog.Close(true); return Task.CompletedTask; }));
        Add(content, buttons, 1); dialog.Content = content; dialog.Opened += (_, _) => cancel.Focus(); return dialog.ShowDialog<bool>(this);
    }
    private static Window OperationDialog(string title, double height)
    {
        var dialog = new Window { Title = title, Width = 400, Height = height, CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = CopperScreenAppearance.Background, FontSize = 13 };
        AutomationProperties.SetName(dialog, title);
        dialog.KeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; dialog.Close(); } }; return dialog;
    }

    private static TextBlock Note(string text) => new() { Text = text, TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis,
        VerticalAlignment = VerticalAlignment.Center, Foreground = CopperScreenAppearance.MutedText };
    private static Button Button(string text, Func<Task> action)
    {
        var button = new Button { Content = text, Padding = new Thickness(5, 1), Height = 26, MinHeight = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center, CornerRadius = new CornerRadius(2),
            Background = CopperScreenAppearance.ControlSurface, Foreground = CopperScreenAppearance.Text, BorderBrush = CopperScreenAppearance.QuietBorder, BorderThickness = new Thickness(1) };
        AutomationProperties.SetName(button, text); ToolTip.SetShowOnDisabled(button, true);
        button.Click += async (_, _) => await action(); CopperScreenAppearance.ConfigureKeyboardFocus(button); return button;
    }
    private static void Add(Grid grid, Control control, int row) { Grid.SetRow(control, row); grid.Children.Add(control); }
    private const string HelpText = "Each pane can browse a Host folder or an Image. Choose its type and use … to open a location. Images have an independent volume selector. Open image… uses the active pane. The same image can be opened in both panes, with independent directories and partitions. Copies into the same directory or descendants are refused.\n\nSelect source entries in the highlighted pane. Copy sends them to the other pane’s current directory. Move removes the source after confirmation. Drag between panes to copy; hold Shift to move (also confirmed). Dropping host files from another app copies them into the pane; image entries cannot be dragged out to other apps. Ctrl toggles selection; Shift selects a range. Enter or double-click opens a directory or views one selected file as text. Text and Hex view a single file from either pane, including read-only images.\n\nF6  Switch panes\nF3  Text viewer\nShift+F3  Hex viewer\nF5  Copy\nShift+F5  Move (confirmation required)\nF7  New image directory\nF2  Rename image entry\nDelete  Delete image entries (confirmation required)\nAlt+Up  Parent\nCtrl+R  Refresh\nCtrl+A  Select all\nEscape  Cancel\n\nViewers read 4 KiB pages. Use the arrows or Ctrl+PageUp/PageDown, or enter a hexadecimal byte offset. Text supports UTF-8 and Amiga Latin-1; Hex shows exact bytes and ASCII. Escape closes the viewer and cancels a pending read.\n\nExisting entries are never overwritten or merged. Image edits are staged and saved after success. Delete removes entire selected trees; keep backups of valuable images. Protect changes only selected image entries; Readable/Writable/Executable/Deletable are permissions. Mixed flags can stay unchanged. Moves within one image save together. Between images or host locations, a completed copy is retained if source removal fails or is cancelled. Same-volume image moves rename entries without copying their contents. Links are not copied, followed or modified.\n\nOFS/FFS DOS0–DOS5 and clean SFS/SFS2 are supported. Image-to-image copies preserve dates, protection and comments within the destination format’s limits. Host transfers do not map Amiga protection/comments. Cancelled or faulted sessions must be reselected.";
}
