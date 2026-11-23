using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace CopperScreen;

internal sealed partial class DiskImageManagerWindow : Window
{
    private readonly Pane _left = new(true, true), _right = new(false, false);
    private readonly Pane[] _panes;
    private Pane _active;
    private CancellationTokenSource? _operation;
    private bool _busy, _closeRequested, _disposed;

    internal DiskImageManagerWindow()
    {
        _panes = [_left, _right]; _active = _right;
        Title = "Disk Image Manager"; Width = 1060; Height = 720; MinWidth = 760; MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = CopperScreenAppearance.Background; FontSize = 13;
        BuildLayout();
        AddHandler(KeyDownEvent, HandleCommandKey, RoutingStrategies.Bubble);
        SetActivePane(_right);
        Opened += async (_, _) => await NavigateHostAsync(_left.HostDirectory);
        Closing += (_, e) =>
        {
            if (!_busy) return;
            e.Cancel = true; _closeRequested = true; _operation?.Cancel();
        };
        Closed += (_, _) => { _disposed = true; foreach (var pane in _panes) ReleaseImage(pane); };
    }

    private Pane Other => ReferenceEquals(_active, _left) ? _right : _left;
    private Pane Side(int index) => index switch { 0 => _left, 1 => _right, _ => throw new ArgumentOutOfRangeException(nameof(index)) };

    private async Task ChooseImageAsync(Pane pane)
    {
        var picked = await StorageProvider.OpenFilePickerAsync(new()
        {
            Title = $"Open {pane.Side.ToLowerInvariant()} disk image", AllowMultiple = false,
            FileTypeFilter = [new("Amiga disk images") { Patterns = ["*.adf", "*.hdf", "*.img", "*.vhd", "*.ipf", "*.dms"] }]
        });
        if (picked.FirstOrDefault()?.TryGetLocalPath() is { } path) await OpenImageAsync(pane, path);
        ApplyMode(pane);
    }

    private async Task ChooseHostAsync(Pane pane)
    {
        var picked = await StorageProvider.OpenFolderPickerAsync(new() { Title = $"Choose {pane.Side.ToLowerInvariant()} host folder", AllowMultiple = false });
        if (picked.FirstOrDefault()?.TryGetLocalPath() is { } path) await RunAsync(ct => ReadHostAsync(pane, path, ct));
        ApplyMode(pane);
    }

    internal Task OpenImageAsync(string path) => OpenImageAsync(_right, path);
    internal Task OpenImageAsync(int pane, string path) => OpenImageAsync(Side(pane), path);
    internal Task NavigateHostAsync(string path) => NavigateHostAsync(0, path);
    internal Task NavigateHostAsync(int pane, string path) => RunAsync(ct => ReadHostAsync(Side(pane), path, ct));

    private Task OpenImageAsync(Pane pane, string path) => RunAsync(async ct =>
    {
        var full = Path.GetFullPath(path);
        var shared = _panes.Select(p => p.Image).FirstOrDefault(i => i != null && string.Equals(i.FilePath, full, StringComparison.OrdinalIgnoreCase));
        var image = shared?.Retain() ?? await Task.Run(() => CopperScreenDiskImage.Open(full), ct);
        ReleaseImage(pane); pane.Image = image; pane.IsHost = false; pane.Directories.Clear();
        pane.List.SelectedItems!.Clear(); pane.List.ItemsSource = null;
        ApplyMode(pane);
        pane.Volumes.ItemsSource = image.Volumes.Select(v => new VolumeItem(v, Path.GetFileName(full))).ToArray();
        var first = image.Volumes.FirstOrDefault(v => v.UnsupportedReason == null) ?? image.Volumes[0];
        pane.Volumes.SelectedIndex = image.Volumes.ToList().IndexOf(first);
        await SelectVolumeAsync(pane, first, ct);
    });

    private async Task SelectVolumeAsync(Pane pane, DiskImageVolume volume, CancellationToken ct, string path = "/")
    {
        if (pane.Volume is { } previous && !ReferenceEquals(previous, volume) &&
            !_panes.Any(p => !ReferenceEquals(p, pane) && ReferenceEquals(p.Volume, previous))) previous.Close();
        pane.Volume = volume; pane.Directories.Clear(); pane.List.SelectedItems!.Clear(); pane.List.ItemsSource = null; pane.ImagePath.Text = "/";
        var entries = await Task.Run(() => { volume.Mount(ct); return volume.ReadDirectory(volume.Root, ct); }, ct);
        pane.Directories.Push(volume.Root);
        foreach (var part in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var child = entries.SingleOrDefault(e => e.Name == part && e.IsDirectory && !e.IsLink);
            if (child == null) break;
            entries = await Task.Run(() => volume.ReadDirectory(child, ct), ct); pane.Directories.Push(child);
        }
        var index = pane.Image!.Volumes.ToList().IndexOf(volume);
        pane.Volumes.ItemsSource = pane.Image.Volumes.Select(v => new VolumeItem(v, Path.GetFileName(pane.Image.FilePath))).ToArray();
        pane.Volumes.SelectedIndex = index;
        SetImageEntries(pane, entries);
        if (volume.ImportUnavailableReason is { } reason) _status.Text += " · " + reason;
    }

    private async Task ReadHostAsync(Pane pane, string path, CancellationToken ct)
    {
        var full = Path.GetFullPath(path);
        var items = await Task.Run(() => Directory.EnumerateFileSystemEntries(full).Select(p =>
        {
            ct.ThrowIfCancellationRequested(); var directory = Directory.Exists(p);
            return new HostItem(Path.GetFileName(p), p, directory, directory ? 0 : new FileInfo(p).Length);
        }).ToArray(), ct);
        ReleaseImage(pane); pane.Directories.Clear(); pane.IsHost = true;
        pane.Volumes.ItemsSource = null; pane.Volumes.SelectedIndex = -1;
        pane.HostDirectory = full; pane.HostPath.Text = full; pane.HostPath.CaretIndex = full.Length;
        ToolTip.SetTip(pane.HostPath, full); pane.HostLabel.Text = Path.GetFileName(Path.TrimEndingDirectorySeparator(full));
        pane.List.SelectedItems!.Clear(); pane.List.ItemsSource = items;
        ApplyMode(pane); SortItems(pane);
        _status.Text = $"{items.Length:N0} host entries";
    }

    private void SetImageEntries(Pane pane, IReadOnlyList<DiskImageEntry> entries)
    {
        pane.List.SelectedItems!.Clear(); pane.List.ItemsSource = entries.Select(e => new ImageItem(e)).ToArray(); SortItems(pane);
        pane.ImagePath.Text = pane.DirectoryPath; ToolTip.SetTip(pane.ImagePath, pane.DirectoryPath);
        _status.Text = $"{entries.Count:N0} entries · {pane.Volume!.FileSystemName}";
    }

    private Task OpenSelectionAsync(Pane pane)
    {
        if (ViewUnavailable(pane) == null) return ShowFileAsync(pane, false);
        if (pane.IsHost)
            return pane.List.SelectedItem is HostItem { IsDirectory: true } h ? RunAsync(ct => ReadHostAsync(pane, h.Path, ct)) : Task.CompletedTask;
        return RunAsync(async ct =>
        {
            if (pane.List.SelectedItem is not ImageItem { Entry.IsDirectory: true, Entry.IsLink: false } selected || pane.Volume == null) return;
            var entries = await Task.Run(() => pane.Volume.ReadDirectory(selected.Entry, ct), ct);
            pane.Directories.Push(selected.Entry); SetImageEntries(pane, entries);
        });
    }

    private Task ParentAsync() => _active.IsHost
        ? RunAsync(ct => ReadHostAsync(_active, Path.GetDirectoryName(_active.HostDirectory) ?? _active.HostDirectory, ct))
        : RunAsync(async ct =>
        {
            var pane = _active; if (pane.Directories.Count <= 1 || pane.Volume == null) return;
            var parent = pane.Directories.Skip(1).First();
            var entries = await Task.Run(() => pane.Volume.ReadDirectory(parent, ct), ct);
            pane.Directories.Pop(); SetImageEntries(pane, entries);
        });

    private Task RefreshAsync() => RunAsync(ct => RefreshPaneAsync(_active, ct));
    private Task RefreshPaneAsync(Pane pane, CancellationToken ct)
        => pane.IsHost ? ReadHostAsync(pane, pane.HostDirectory, ct) : pane.Volume == null ? Task.CompletedTask
        : SelectVolumeAsync(pane, pane.Volume, ct, pane.DirectoryPath);

    internal Task CopySelectedAsync() => CopyAsync(_right, _left);
    internal Task CopyHostSelectedAsync() => CopyAsync(_left, _right);
    private Task CopyActiveAsync() => _copy.IsEnabled ? CopyAsync(_active, Other) : Task.CompletedTask;

    private Task CopyAsync(Pane source, Pane destination) => TransferAsync(source, destination, false);

    private Task TransferAsync(Pane source, Pane destination, bool move, object[]? selection = null) => RunAsync(async ct =>
    {
        if ((move ? MoveUnavailable(source, destination) : CopyUnavailable(source, destination)) is { } reason) throw new InvalidOperationException(reason);
        var selected = selection ?? source.List.SelectedItems!.Cast<object>().ToArray();
        var views = CaptureImageViews();
        var progress = new Progress<DiskExtractionProgress>(p =>
        {
            if (_busy) _status.Text = $"{(move ? "Moving" : "Copying")}: {p.Files:N0} files · {p.Bytes:N0} bytes · {p.Name}";
        });
        var sourceHost = source.IsHost; var destinationHost = destination.IsHost;
        var interpretation = CopperDisk.FileSystem.HostNameInterpretation.RestoreEncoded;
        if (sourceHost && !destinationHost)
        {
            var choice = await ChooseImportNamesAsync(selected.Cast<HostItem>().Select(i => i.Path).ToArray(), ct);
            if (choice == null) { _status.Text = "Import cancelled; no changes saved."; return; }
            interpretation = choice.Value;
        }
        DiskExtractionProgress result;
        try { result = await Task.Run(() =>
        {
            if (sourceHost)
            {
                var paths = selected.Cast<HostItem>().Select(i => i.Path).ToArray();
                using var removal = move ? new CopperScreenHostMove(paths, ct) : null;
                var copied = destinationHost ? CopperScreenDiskExtraction.CopyHost(paths, destination.HostDirectory, progress, ct)
                    : destination.Image!.Import(destination.Volume!, destination.Directories.Peek(), paths, ct, progress, interpretation);
                if (removal != null)
                    try { removal.Remove(ct); }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
                    { throw new IOException("Copy saved, but host source removal did not finish. Destination files are retained; check the source for remaining entries. " + ex.Message, ex); }
                return copied;
            }
            var entries = selected.Cast<ImageItem>().Select(i => i.Entry).ToArray();
            if (move && !destinationHost)
                return destination.Image!.MoveFromImage(destination.Volume!, destination.Directories.Peek(), source.Volume!, source.Directories.Peek(), entries, ct, progress);
            var copiedImage = destinationHost ? CopperScreenDiskExtraction.Extract(source.Volume!, entries, destination.HostDirectory, progress, ct)
                : destination.Image!.CopyFromImage(destination.Volume!, destination.Directories.Peek(), source.Volume!, entries, ct, progress);
            if (move)
                try { source.Image!.Delete(source.Volume!, source.Directories.Peek(), entries, ct); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException or OperationCanceledException or TimeoutException)
                { throw new IOException("Copy saved, but source removal could not be confirmed. Destination files are retained. Check the source before retrying. " + ex.Message, ex); }
            return copiedImage;
        }, ct); }
        finally
        {
            // Host removals can finish partially (for example a newly locked file).
            // Reconcile the visible tree even when the transfer reports an error.
            if (move && sourceHost) await RefreshHostSelectionAsync(source);
            if (destinationHost) await RefreshHostSelectionAsync(destination);
        }
        // A completed image publication cannot be cancelled during the listing refresh.
        await RefreshImageViewsAsync(views);
        _status.Text = move ? $"Moved {selected.Length} entries." : $"{(sourceHost && !destinationHost ? "Imported" : "Copied")} {selected.Length} entries ({result.Files:N0} files, {result.Bytes:N0} bytes).";
        if (!destinationHost || move && !sourceHost) _status.Text += " Image saved.";
    });

    private async Task RefreshHostSelectionAsync(Pane pane)
    {
        var selection = pane.List.SelectedItems!.OfType<HostItem>().Select(i => i.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        try
        {
            await ReadHostAsync(pane, pane.HostDirectory, CancellationToken.None);
            foreach (var item in pane.List.Items.OfType<HostItem>()) if (selection.Contains(item.Path)) pane.List.SelectedItems!.Add(item);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { pane.List.ItemsSource = null; _status.Text = "Could not refresh the host folder: " + ex.Message; }
    }

    private async Task MovePromptAsync(Pane source, Pane destination, object[]? selection = null)
    {
        if (_busy || MoveUnavailable(source, destination) != null) return;
        var selected = selection ?? source.List.SelectedItems!.Cast<object>().ToArray();
        string Location(Pane p) => p.IsHost ? p.HostDirectory : $"{Path.GetFileName(p.Image!.FilePath)} · {p.Volume!.DeviceName}:{p.DirectoryPath}";
        var names = string.Join("\n", selected.Take(4).Select(i => i is HostItem h ? h.Name : ((ImageItem)i).Entry.Name));
        if (!await ConfirmAsync("Move entries", "Move", $"Move {selected.Length:N0} selected entries and their contents?\n\nFrom: {Location(source)}\nTo: {Location(destination)}\n\n{names}\n\nThe source entries will be removed. Existing destinations are never replaced.")) return;
        await TransferAsync(source, destination, true, selected);
    }

    private Task EditAsync(Action<CopperScreenDiskImage, DiskImageVolume, DiskImageEntry, CancellationToken> edit, string result,
        string? selectName = null, DirectoryRename? rename = null)
        => RunAsync(async ct =>
        {
            var pane = _active;
            if (WriteUnavailable(pane) is { } reason) throw new NotSupportedException(reason);
            var views = CaptureImageViews(pane.Image);
            await Task.Run(() => edit(pane.Image!, pane.Volume!, pane.Directories.Peek(), ct), ct);
            await RefreshImageViewsAsync(views, rename);
            if (selectName != null) pane.List.SelectedItem = pane.List.Items.OfType<ImageItem>().SingleOrDefault(i => i.Entry.Name == selectName);
            _status.Text = result + " Image saved.";
        });

    internal Task CreateDirectoryAsync(string name) => EditAsync((image, volume, parent, ct) => image.CreateDirectory(volume, parent, name, ct), "Directory created.", name);
    internal Task RenameSelectedAsync(string name)
    {
        var entry = _active.List.SelectedItems?.OfType<ImageItem>().SingleOrDefault()?.Entry;
        DirectoryRename? rename = entry?.IsDirectory == true
            ? new(_active.Volumes.SelectedIndex, entry.Path, _active.DirectoryPath.TrimEnd('/') + "/" + name) : null;
        return entry == null ? Task.CompletedTask : EditAsync((image, volume, parent, ct) => image.Rename(volume, parent, entry, name, ct), "Entry renamed.", name, rename);
    }

    private async Task NewDirectoryPromptAsync()
    {
        if (!_newDirectory.IsEnabled) return;
        if (await AskNameAsync("New directory", "", "Create") is { } name) await CreateDirectoryAsync(name);
    }
    private async Task RenamePromptAsync()
    {
        if (!_rename.IsEnabled || _active.List.SelectedItem is not ImageItem entry) return;
        if (await AskNameAsync("Rename", entry.Entry.Name, "Rename") is { } name) await RenameSelectedAsync(name);
    }
    private async Task DeletePromptAsync()
    {
        if (!_delete.IsEnabled) return;
        var selected = _active.List.SelectedItems!.OfType<ImageItem>().Select(i => i.Entry).ToArray();
        var names = string.Join("\n", selected.Take(4).Select(e => e.Name)) + (selected.Length > 4 ? "\n…" : "");
        if (!await ConfirmDeleteAsync($"Delete {selected.Length:N0} selected entries and their contents from the image?\n\n{names}")) return;
        await EditAsync((image, volume, parent, ct) => image.Delete(volume, parent, selected, ct), $"Deleted {selected.Length:N0} entries.");
    }

    private async Task RunAsync(Func<CancellationToken, Task> action)
    {
        if (_busy || _disposed) return;
        Control? focused = _panes.FirstOrDefault(p => p.List.IsKeyboardFocusWithin)?.List ?? FocusManager?.GetFocusedElement() as Control;
        var views = CaptureImageViews();
        _busy = true; _body.IsEnabled = _toolbar.IsEnabled = _commands.IsEnabled = false;
        _copy.IsVisible = false; _cancel.IsVisible = true;
        using var operation = new CancellationTokenSource(); _operation = operation; _status.Text = "Working…";
        var reset = false;
        try { await action(operation.Token); if (_status.Text == "Working…") _status.Text = "Ready."; }
        catch (OperationCanceledException) { reset = true; _status.Text = "Cancelled. Files already copied to the destination are retained."; }
        catch (TimeoutException ex) { reset = true; _status.Text = ex.Message; }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or NotSupportedException or ArgumentException or OverflowException or InvalidOperationException || _panes.Any(p => p.Volume?.IsFaulted == true))
        { _status.Text = ex.Message; }
        finally
        {
            // Even a failed publication can reopen storage and replace volume
            // records. Do not leave either pane holding entries from a disposed mount.
            var message = _status.Text;
            try { await RefreshImageViewsAsync(views); _status.Text = message; }
            catch (Exception ex) when (ex is IOException or NotSupportedException or ArgumentException or InvalidOperationException or TimeoutException)
            { _status.Text = message + " Could not refresh the image: " + ex.Message; }
            var reselect = false;
            foreach (var pane in _panes)
            {
                if (pane.Volume is not { } volume || !(reset || volume.IsFaulted || !volume.IsMounted)) continue;
                volume.Close(); pane.Directories.Clear(); pane.List.SelectedItems!.Clear(); pane.List.ItemsSource = null; pane.ImagePath.Text = "/";
                pane.Volumes.SelectedIndex = -1;
                pane.Volumes.ItemsSource = pane.Image?.Volumes.Select(v => new VolumeItem(v, Path.GetFileName(pane.Image.FilePath))).ToArray();
                reselect = true;
            }
            if (reselect) _status.Text += " Reselect the volume to start a fresh filesystem session.";
            _busy = false; _operation = null; _body.IsEnabled = _toolbar.IsEnabled = _commands.IsEnabled = true;
            _copy.IsVisible = true; _cancel.IsVisible = false;
            foreach (var pane in _panes) ApplyMode(pane);
            UpdateCommands();
            if (!_closeRequested && focused is { IsEffectivelyEnabled: true, IsEffectivelyVisible: true }) focused.Focus();
            if (_closeRequested) Close();
        }
    }

    private void SetActivePane(Pane pane)
    {
        var changed = !ReferenceEquals(_active, pane); _active = pane;
        foreach (var p in _panes)
        {
            var active = ReferenceEquals(p, pane);
            p.Frame.BorderBrush = active ? CopperScreenAppearance.Accent : CopperScreenAppearance.QuietBorder;
            p.Header.Background = active ? CopperScreenAppearance.ProtectedSurface : CopperScreenAppearance.ControlSurface;
            AutomationProperties.SetHelpText(p.List, active ? "Source pane" : "Destination pane");
        }
        UpdateCommands(); if (changed) SelectionStatus();
    }

    private static string? WriteUnavailable(Pane pane) => pane.IsHost ? "Choose an image for this operation."
        : pane.Volume == null ? "Select a mounted image volume." : pane.Volume.ImportUnavailableReason;
    private string? MoveUnavailable(Pane source, Pane destination)
    {
        if (CopyUnavailable(source, destination) is { } reason) return reason;
        if (!source.IsHost) return WriteUnavailable(source);
        foreach (var item in source.List.SelectedItems!.OfType<HostItem>())
            if (_panes.Any(p => p.Image != null && (string.Equals(item.Path, p.Image.FilePath, StringComparison.OrdinalIgnoreCase) ||
                item.IsDirectory && p.Image.FilePath.StartsWith(item.Path.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))))
                return "Close images contained in the host selection before moving it.";
        return null;
    }
    private static string? CopyUnavailable(Pane source, Pane destination)
    {
        if (source.IsHost && source.HostPath.Text != source.HostDirectory || destination.IsHost && destination.HostPath.Text != destination.HostDirectory)
            return "Press Enter to open the entered host folder.";
        if (!source.IsHost && source.Volume?.IsMounted != true) return source.Volume?.UnsupportedReason ?? "Select a source volume.";
        if (!destination.IsHost && WriteUnavailable(destination) is { } reason) return reason;
        if (source.List.SelectedItems is not { Count: > 0 }) return "Select source files or directories.";
        if (source.List.SelectedItems.OfType<ImageItem>().Any(i => i.Entry.IsLink)) return "Links cannot be copied.";
        if (!source.IsHost && !destination.IsHost && ReferenceEquals(source.Volume, destination.Volume))
        {
            if (source.DirectoryPath == destination.DirectoryPath) return "Choose a different destination directory.";
            if (source.List.SelectedItems.OfType<ImageItem>().Any(i => i.Entry.IsDirectory &&
                (destination.DirectoryPath == i.Entry.Path || destination.DirectoryPath.StartsWith(i.Entry.Path + "/", StringComparison.Ordinal))))
                return "A directory cannot be copied into itself or its descendants.";
        }
        if (source.IsHost && destination.IsHost && source.HostDirectory.Equals(destination.HostDirectory, StringComparison.OrdinalIgnoreCase))
            return "Choose a different destination folder.";
        return null;
    }

    private void UpdateCommands()
    {
        if (_copy == null) return;
        _imageName.Text = _active.Image == null ? "" : Path.GetFileName(_active.Image.FilePath);
        ToolTip.SetTip(_imageName, _active.Image?.FilePath);
        _copy.Content = ReferenceEquals(_active, _left) ? "Copy →" : "← Copy";
        var reason = _busy ? "An operation is running." : CopyUnavailable(_active, Other);
        _copy.IsEnabled = reason == null;
        ToolTip.SetTip(_copy, reason ?? $"Copy {_active.Side.ToLowerInvariant()} selection to the {Other.Side.ToLowerInvariant()} directory (F5)");
        AutomationProperties.SetHelpText(_copy, reason ?? $"Copy from {_active.Side} to {Other.Side}");
        _move.Content = ReferenceEquals(_active, _left) ? "Move →" : "← Move";
        var moveReason = _busy ? "An operation is running." : MoveUnavailable(_active, Other);
        _move.IsEnabled = moveReason == null;
        ToolTip.SetTip(_move, moveReason ?? "Move selection to the other directory; confirmation required (Shift+F5)");
        _parent.IsEnabled = _active.IsHost ? Path.GetDirectoryName(_active.HostDirectory) != null : _active.Directories.Count > 1;
        _refresh.IsEnabled = _active.IsHost || _active.Volume?.IsMounted == true;
        _all.IsEnabled = _invert.IsEnabled = _active.List.ItemCount > 0;
        _clear.IsEnabled = _active.List.SelectedItems is { Count: > 0 };
        _info.IsEnabled = _active.List.SelectedItems is { Count: 1 };
        var viewReason = _busy ? "An operation is running." : ViewUnavailable(_active);
        _text.IsEnabled = _hex.IsEnabled = viewReason == null;
        ToolTip.SetTip(_text, viewReason ?? "View selected file as text (F3)");
        ToolTip.SetTip(_hex, viewReason ?? "View selected file as hexadecimal bytes (Shift+F3)");
        var unavailable = WriteUnavailable(_active);
        _newDirectory.IsEnabled = unavailable == null;
        _rename.IsEnabled = unavailable == null && _active.List.SelectedItems is { Count: 1 } && _active.List.SelectedItem is ImageItem { Entry.IsLink: false };
        _delete.IsEnabled = unavailable == null && _active.List.SelectedItems is { Count: > 0 } selected && selected.OfType<ImageItem>().All(i => !i.Entry.IsLink);
        _protect.IsEnabled = _delete.IsEnabled;
        ToolTip.SetTip(_protect, unavailable ?? "Set protection flags on selected image entries");
        foreach (var button in new[] { _newDirectory, _rename, _delete })
            ToolTip.SetTip(button, unavailable ?? (button == _newDirectory ? "Create a directory in the active image (F7)" : button == _rename ? "Rename one selected entry (F2)" : "Delete selected entries and their contents (Delete)"));
        ToolTip.SetTip(_info, _info.IsEnabled ? "View selected entry metadata" : "Select one entry to view metadata");
    }

    private void SelectionStatus()
    {
        if (_busy || (!_active.IsHost && _active.Volume?.IsMounted != true)) return;
        var selected = _active.List.SelectedItems!.Cast<object>().ToArray();
        var bytes = selected.Sum(i => i is HostItem h ? (h.IsDirectory ? 0 : h.Size) : ((ImageItem)i).Entry is { IsDirectory: false, IsLink: false } e ? e.Size : 0);
        _status.Text = $"{_active.Side} · {selected.Length:N0}/{_active.List.ItemCount:N0} selected · {bytes:N0} file bytes";
    }

    private enum SelectionAction { All, Invert, Clear }
    private Task SelectAsync(SelectionAction action)
    {
        var list = _active.List;
        if (action == SelectionAction.All) list.SelectAll();
        else if (action == SelectionAction.Clear) list.UnselectAll();
        else
        {
            var selected = list.SelectedItems!.Cast<object>().ToHashSet(); list.UnselectAll();
            foreach (var item in list.Items) if (item != null && !selected.Contains(item)) list.SelectedItems!.Add(item);
        }
        return Task.CompletedTask;
    }

    private async void HandleCommandKey(object? sender, KeyEventArgs e)
    {
        if (e.Handled) return;
        if (e.Key == Key.Escape && _busy) { _operation?.Cancel(); e.Handled = true; return; }
        if (_busy || e.Source is TextBox) return;
        Func<Task>? action = e.Key switch
        {
            Key.F6 when e.KeyModifiers == KeyModifiers.None => () => { SetActivePane(Other); _active.List.Focus(); return Task.CompletedTask; },
            Key.F5 when e.KeyModifiers == KeyModifiers.None => CopyActiveAsync,
            Key.F5 when e.KeyModifiers == KeyModifiers.Shift => () => MovePromptAsync(_active, Other),
            Key.F3 when e.KeyModifiers == KeyModifiers.None => () => ShowFileAsync(_active, false),
            Key.F3 when e.KeyModifiers == KeyModifiers.Shift => () => ShowFileAsync(_active, true),
            Key.F7 when e.KeyModifiers == KeyModifiers.None => NewDirectoryPromptAsync,
            Key.F2 when e.KeyModifiers == KeyModifiers.None => RenamePromptAsync,
            Key.Delete when e.KeyModifiers == KeyModifiers.None => DeletePromptAsync,
            Key.Up when e.KeyModifiers == KeyModifiers.Alt && _parent.IsEnabled => ParentAsync,
            Key.R when e.KeyModifiers == KeyModifiers.Control && _refresh.IsEnabled => RefreshAsync,
            Key.A when e.KeyModifiers == KeyModifiers.Control => () => SelectAsync(SelectionAction.All),
            _ => null
        };
        if (action == null) return; e.Handled = true; await action();
    }
}
