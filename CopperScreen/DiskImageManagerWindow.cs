using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace CopperScreen;

internal sealed class DiskImageManagerWindow : Window
{
    private readonly Grid _body = new() { RowDefinitions = new("Auto,*"), RowSpacing = 16 };
    private readonly ListBox _host = new() { SelectionMode = SelectionMode.Multiple }, _entries = new() { SelectionMode = SelectionMode.Multiple };
    private readonly TextBox _hostPath = new();
    private readonly TextBlock _imageName = Note("No image opened"), _imagePath = Note("/"), _details = Note("");
    private readonly TextBlock _status = Note("Open an ADF or HDF image to browse, extract or import files.");
    private readonly ComboBox _volumes = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly Button _copy, _import, _cancel;
    private readonly Stack<DiskImageEntry> _directory = new();
    private CopperScreenDiskImage? _image;
    private DiskImageVolume? _volume;
    private CancellationTokenSource? _operation;
    private bool _busy, _closeRequested;
    private string _destination = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private sealed record HostItem(string Name, string Path, bool IsDirectory, long Size)
    {
        public override string ToString() => IsDirectory ? $"▸ {Name}/" : $"{Name}    {Size:N0} bytes";
    }
    private sealed record ImageItem(DiskImageEntry Entry)
    {
        public override string ToString() => Entry.IsLink ? $"{Entry.Name}    [link — unavailable]"
            : Entry.IsDirectory ? $"▸ {Entry.Name}/" : $"{Entry.Name}    {Entry.Size:N0} bytes";
    }
    private sealed record VolumeItem(DiskImageVolume Volume)
    {
        public override string ToString() => Volume.UnsupportedReason == null
            ? $"{Volume.DeviceName}: {Volume.DisplayName} · {Volume.FileSystemName}"
            : $"{Volume.DeviceName}: unavailable";
    }

    internal DiskImageManagerWindow()
    {
        Title = "Disk Image Manager";
        Width = 1060; Height = 720; MinWidth = 760; MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = CopperScreenAppearance.Background;
        var root = new Grid { Margin = new Thickness(20), RowDefinitions = new("*,Auto,Auto"), RowSpacing = 12 };
        var heading = new StackPanel { Spacing = 6 };
        heading.Children.Add(new TextBlock { Text = "Disk Image Manager", FontSize = 23, FontWeight = FontWeight.SemiBold });
        heading.Children.Add(Note("Browse OFS/FFS, SFS and SFS2 images. Copy files and directories between the host and supported images."));
        heading.Children.Add(Note("Existing entries are preserved. Links are not followed; comments and protection bits are not mapped."));
        _details.MaxLines = 2;
        _details.TextTrimming = TextTrimming.CharacterEllipsis;
        _details.PropertyChanged += (_, e) => { if (e.Property == TextBlock.TextProperty) ToolTip.SetTip(_details, _details.Text); };
        _body.Children.Add(heading);

        var panes = new Grid { ColumnDefinitions = new("*,*"), ColumnSpacing = 16 };
        var hostPane = new Grid { RowDefinitions = new("Auto,Auto,Auto,*"), RowSpacing = 8 };
        hostPane.Children.Add(Label("Host folder"));
        _hostPath.Text = _destination;
        AutomationProperties.SetName(_hostPath, "Host folder path");
        Add(hostPane, _hostPath, 1);
        var hostCommands = Commands(
            Button("Choose folder…", async () =>
            {
                var picked = await StorageProvider.OpenFolderPickerAsync(new() { Title = "Choose host destination", AllowMultiple = false });
                if (picked.FirstOrDefault()?.TryGetLocalPath() is { } path) await NavigateHostAsync(path);
            }),
            Button("Go", () => NavigateHostAsync(_hostPath.Text ?? "")),
            Button("Up", () => NavigateHostAsync(Path.GetDirectoryName(_destination) ?? _destination)),
            Button("Refresh", () => NavigateHostAsync(_destination)));
        Add(hostPane, hostCommands, 2);
        Add(hostPane, _host, 3);
        AutomationProperties.SetName(_host, "Host files");
        _host.SelectionChanged += (_, _) => UpdateCopy();
        _host.DoubleTapped += async (_, _) => await OpenHostSelectionAsync();
        _host.KeyDown += async (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; await OpenHostSelectionAsync(); } };
        _hostPath.KeyDown += async (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; await NavigateHostAsync(_hostPath.Text ?? ""); } };

        var imagePane = new Grid { RowDefinitions = new("Auto,Auto,Auto,Auto,*,Auto"), RowSpacing = 8 };
        imagePane.Children.Add(Label("Disk image · imports saved only after success"));
        _imageName.TextWrapping = TextWrapping.NoWrap; _imageName.TextTrimming = TextTrimming.CharacterEllipsis;
        _imagePath.TextWrapping = TextWrapping.NoWrap; _imagePath.TextTrimming = TextTrimming.CharacterEllipsis;
        Add(imagePane, ActionRow(Button("Open ADF/HDF…", ChooseImageAsync), _imageName), 1);
        AutomationProperties.SetName(_volumes, "Image volumes");
        Add(imagePane, _volumes, 2);
        Add(imagePane, ActionRow(Button("Parent directory", ParentImageAsync), _imagePath), 3);
        Add(imagePane, _entries, 4);
        Add(imagePane, _details, 5);
        AutomationProperties.SetName(_entries, "Image files");
        _volumes.SelectionChanged += async (_, _) =>
        {
            if (_volumes.SelectedItem is VolumeItem selected) await RunAsync(ct => SelectVolumeAsync(selected.Volume, ct));
        };
        _entries.SelectionChanged += (_, _) =>
        {
            var selected = _entries.SelectedItems?.OfType<ImageItem>().ToArray() ?? [];
            _details.Text = selected.Length == 1 ? Details(selected[0].Entry) : $"{selected.Length} entries selected";
            UpdateCopy();
        };
        _entries.DoubleTapped += async (_, _) => await OpenImageSelectionAsync();
        _entries.KeyDown += async (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; await OpenImageSelectionAsync(); } };
        panes.Children.Add(hostPane); Grid.SetColumn(imagePane, 1); panes.Children.Add(imagePane);
        Add(_body, panes, 1);
        root.Children.Add(_body);
        _copy = Button("← Copy selected to host", CopySelectedAsync);
        _copy.IsEnabled = false;
        _import = Button("Copy host selection to image →", CopyHostSelectedAsync);
        _import.IsEnabled = false;
        _cancel = Button("Cancel", () => { _operation?.Cancel(); return Task.CompletedTask; });
        _cancel.IsEnabled = false;
        _hostPath.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) UpdateCopy(); };
        Add(root, Commands(_copy, _import, _cancel, Button("Close", () => { Close(); return Task.CompletedTask; })), 1);
        AutomationProperties.SetName(_status, "Disk manager status");
        Add(root, _status, 2);
        Content = root;
        Opened += async (_, _) => await NavigateHostAsync(_destination);
        Closing += (_, e) =>
        {
            if (!_busy) return;
            e.Cancel = true; _closeRequested = true; _operation?.Cancel();
        };
        Closed += (_, _) => _image?.Dispose();
    }

    private static string Details(DiskImageEntry e) =>
        $"{e.Name} · {(e.Modified?.ToString("yyyy-MM-dd HH:mm:ss") ?? "date unavailable")} · Protection: {e.Protection:X8}" +
        (e.Comment.Length == 0 ? "" : $"\n{e.Comment}");

    private async Task ChooseImageAsync()
    {
        var picked = await StorageProvider.OpenFilePickerAsync(new()
        {
            Title = "Open disk image for browsing", AllowMultiple = false,
            FileTypeFilter = [new("Sector disk images") { Patterns = ["*.adf", "*.hdf", "*.img"] }]
        });
        if (picked.FirstOrDefault()?.TryGetLocalPath() is { } path) await OpenImageAsync(path);
    }

    internal Task OpenImageAsync(string path) => RunAsync(async ct =>
    {
        var opened = await Task.Run(() => CopperScreenDiskImage.Open(path), ct);
        _image?.Dispose(); _image = opened;
        _imageName.Text = Path.GetFileName(path); ToolTip.SetTip(_imageName, path);
        _volumes.ItemsSource = opened.Volumes.Select(v => new VolumeItem(v)).ToArray();
        var first = opened.Volumes.FirstOrDefault(v => v.UnsupportedReason == null) ?? opened.Volumes[0];
        _volumes.SelectedIndex = opened.Volumes.ToList().IndexOf(first);
        await SelectVolumeAsync(first, ct);
    });

    private async Task SelectVolumeAsync(DiskImageVolume volume, CancellationToken ct)
    {
        _volume = volume; _directory.Clear(); _entries.ItemsSource = null; _details.Text = ""; _imagePath.Text = "/";
        var entries = await Task.Run(() => { volume.Mount(ct); return volume.ReadDirectory(volume.Root, ct); }, ct);
        _directory.Push(volume.Root);
        var selectedIndex = _volumes.SelectedIndex;
        _volumes.ItemsSource = _image!.Volumes.Select(v => new VolumeItem(v)).ToArray();
        _volumes.SelectedIndex = selectedIndex;
        SetImageEntries(entries);
        _status.Text = $"{volume.Root.Name} · {volume.FileSystemName}. " +
            (volume.ImportUnavailableReason ?? "Select host entries to import, or image entries to extract.");
    }

    private Task OpenImageSelectionAsync() => RunAsync(async ct =>
    {
        if (_entries.SelectedItem is not ImageItem selected || !selected.Entry.IsDirectory || _volume == null) return;
        var children = await Task.Run(() => _volume.ReadDirectory(selected.Entry, ct), ct);
        _directory.Push(selected.Entry); SetImageEntries(children);
    });

    private Task ParentImageAsync() => RunAsync(async ct =>
    {
        if (_directory.Count <= 1 || _volume == null) return;
        var parent = _directory.Skip(1).First();
        var entries = await Task.Run(() => _volume.ReadDirectory(parent, ct), ct);
        _directory.Pop(); SetImageEntries(entries);
    });

    private void SetImageEntries(IReadOnlyList<DiskImageEntry> entries)
    {
        _entries.ItemsSource = entries.Select(e => new ImageItem(e)).ToArray();
        _imagePath.Text = "/" + string.Join("/", _directory.Reverse().Skip(1).Select(e => e.Name));
        ToolTip.SetTip(_imagePath, _imagePath.Text);
        _status.Text = $"{entries.Count} entries · {_volume!.FileSystemName}";
    }

    internal Task NavigateHostAsync(string path) => RunAsync(async ct =>
    {
        var fullPath = Path.GetFullPath(path);
        var items = await Task.Run(() => Directory.EnumerateFileSystemEntries(fullPath).Select(p =>
        {
            ct.ThrowIfCancellationRequested();
            var directory = Directory.Exists(p);
            return new HostItem(Path.GetFileName(p), p, directory, directory ? 0 : new FileInfo(p).Length);
        }).OrderByDescending(e => e.IsDirectory).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToArray(), ct);
        _destination = fullPath; _hostPath.Text = fullPath; _host.ItemsSource = items;
        _status.Text = $"Host destination: {fullPath}";
    });

    private async Task OpenHostSelectionAsync()
    {
        if (_host.SelectedItem is HostItem { IsDirectory: true } item) await NavigateHostAsync(item.Path);
    }

    internal async Task CopySelectedAsync()
    {
        if (_busy || _volume == null || _volume.UnsupportedReason != null) return;
        if (_hostPath.Text != _destination)
        {
            _status.Text = "Choose Go to open the entered host folder before copying.";
            return;
        }
        var selected = _entries.SelectedItems?.OfType<ImageItem>().Select(i => i.Entry).ToArray() ?? [];
        if (selected.Length == 0) { _status.Text = "Select files or directories to copy."; return; }
        var volume = _volume;
        var destination = _destination;
        DiskExtractionProgress result = default;
        var completed = false;
        await RunAsync(async ct =>
        {
            var progress = new Progress<DiskExtractionProgress>(p =>
            {
                if (_busy) _status.Text = $"Copying: {p.Files:N0} files · {p.Bytes:N0} bytes · {p.Name}";
            });
            result = await Task.Run(() => CopperScreenDiskExtraction.Extract(volume, selected, destination, progress, ct), ct);
            completed = true;
        });
        if (_closeRequested) return;
        if (completed)
        {
            await NavigateHostAsync(destination);
            _status.Text = $"Copied {selected.Length} entries ({result.Files:N0} files, {result.Bytes:N0} bytes) to {destination}.";
        }
    }

    internal Task CopyHostSelectedAsync() => RunAsync(async ct =>
    {
        if (_image == null || _volume == null || _directory.Count == 0) return;
        if (_hostPath.Text != _destination) throw new InvalidOperationException("Choose Go to open the entered host folder before copying.");
        if (_volume.ImportUnavailableReason is { } reason) throw new NotSupportedException(reason);
        var sources = _host.SelectedItems?.OfType<HostItem>().Select(i => i.Path).ToArray() ?? [];
        if (sources.Length == 0) throw new InvalidOperationException("Select host files or directories to copy.");
        var index = _image.Volumes.ToList().IndexOf(_volume); var path = _directory.Peek().Path;
        var progress = new Progress<DiskExtractionProgress>(p =>
        {
            if (_busy) _status.Text = $"Importing: {p.Files:N0} files · {p.Bytes:N0} bytes · {p.Name}";
        });
        var result = await Task.Run(() => _image.Import(_volume, _directory.Peek(), sources, ct, progress), ct);
        // Publication has completed; finish refreshing the saved image even if cancellation arrives now.
        ct = CancellationToken.None;
        _volumes.ItemsSource = _image.Volumes.Select(v => new VolumeItem(v)).ToArray(); _volumes.SelectedIndex = index;
        await SelectVolumeAsync(_image.Volumes[index], ct);
        foreach (var part in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var next = (await Task.Run(() => _volume!.ReadDirectory(_directory.Peek(), ct), ct)).Single(e => e.Name == part && e.IsDirectory);
            var entries = await Task.Run(() => _volume!.ReadDirectory(next, ct), ct);
            _directory.Push(next); SetImageEntries(entries);
        }
        _status.Text = $"Imported {sources.Length} entries ({result.Files:N0} files, {result.Bytes:N0} bytes). Image saved.";
    });

    private async Task RunAsync(Func<CancellationToken, Task> action)
    {
        if (_busy) return;
        _busy = true; _body.IsEnabled = false; _copy.IsEnabled = false; _import.IsEnabled = false; _cancel.IsEnabled = true;
        using var operation = new CancellationTokenSource(); _operation = operation;
        _status.Text = "Working…";
        var resetVolume = false;
        try
        {
            await action(operation.Token);
            if (_status.Text == "Working…") _status.Text = "Ready.";
        }
        catch (OperationCanceledException) { resetVolume = true; _status.Text = "Cancelled. Files already copied to the destination are retained."; }
        catch (TimeoutException ex) { resetVolume = true; _status.Text = ex.Message; }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or NotSupportedException or ArgumentException or OverflowException or InvalidOperationException || _volume?.IsFaulted == true)
        { _status.Text = ex.Message; }
        finally
        {
            if (_volume != null && (resetVolume || _volume.IsFaulted || !_volume.IsMounted))
            {
                _volume.Close(); _directory.Clear(); _entries.ItemsSource = null; _details.Text = ""; _imagePath.Text = "/";
                _volumes.SelectedIndex = -1;
                _volumes.ItemsSource = _image?.Volumes.Select(v => new VolumeItem(v)).ToArray();
                _status.Text += " Reselect the volume to start a fresh filesystem session.";
            }
            _busy = false; _operation = null; _body.IsEnabled = true; _cancel.IsEnabled = false; UpdateCopy();
            if (_closeRequested) Close();
        }
    }

    private void UpdateCopy()
    {
        _copy.IsEnabled = !_busy && _hostPath.Text == _destination && _volume is { UnsupportedReason: null, IsMounted: true } &&
            _entries.SelectedItems is { Count: > 0 } selected && selected.OfType<ImageItem>().All(i => !i.Entry.IsLink);
        _import.IsEnabled = !_busy && _hostPath.Text == _destination && _volume is { ImportUnavailableReason: null } && _host.SelectedItems is { Count: > 0 };
        ToolTip.SetTip(_import, _volume?.ImportUnavailableReason ?? "Copy into the current image directory. Existing entries are preserved.");
    }

    private static TextBlock Note(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Foreground = CopperScreenAppearance.MutedText };
    private static TextBlock Label(string text) => new() { Text = text, FontWeight = FontWeight.SemiBold };
    private static Button Button(string text, Func<Task> action)
    {
        var button = new Button { Content = text, Padding = new Thickness(12, 7) };
        button.Click += async (_, _) => await action();
        CopperScreenAppearance.ConfigureKeyboardFocus(button);
        return button;
    }
    private static WrapPanel Commands(params Control[] controls)
    {
        var panel = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var control in controls) { control.Margin = new Thickness(0, 0, 8, 4); panel.Children.Add(control); }
        return panel;
    }
    private static Grid ActionRow(Button button, TextBlock text)
    {
        var row = new Grid { ColumnDefinitions = new("Auto,*"), ColumnSpacing = 8 };
        text.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(button); Grid.SetColumn(text, 1); row.Children.Add(text);
        return row;
    }
    private static void Add(Grid grid, Control control, int row) { Grid.SetRow(control, row); grid.Children.Add(control); }
}
