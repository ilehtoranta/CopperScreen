using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;

namespace CopperScreen;

internal sealed partial class DiskImageManagerWindow
{
    private sealed record PaneDrag(DiskImageManagerWindow Window, Pane Source, object[] Entries, string Path, DiskImageVolume? Volume);
    private static readonly DataFormat<PaneDrag> PaneDragFormat = DataFormat.CreateInProcessFormat<PaneDrag>("CopperScreen.DiskImageManager.Selection");

    private void ConfigureDragDrop(Pane pane)
    {
        var list = pane.List;
        PointerPressedEventArgs? pressed = null; Point start = default;
        list.AddHandler(PointerPressedEvent, (_, e) =>
        {
            var onRow = e.Source is Visual visual && visual.GetVisualAncestors().Prepend(visual).OfType<ListBoxItem>().Any();
            pressed = !_busy && onRow && e.GetCurrentPoint(list).Properties.IsLeftButtonPressed ? e : null;
            start = e.GetPosition(list);
        }, RoutingStrategies.Bubble, true);
        list.PointerReleased += (_, _) => pressed = null;
        list.PointerMoved += async (_, e) =>
        {
            if (pressed == null || _busy || !e.GetCurrentPoint(list).Properties.IsLeftButtonPressed ||
                Math.Abs(e.GetPosition(list).X - start.X) + Math.Abs(e.GetPosition(list).Y - start.Y) < 6 || list.SelectedItems is not { Count: > 0 }) return;
            var trigger = pressed; pressed = null;
            var payload = new PaneDrag(this, pane, list.SelectedItems.Cast<object>().ToArray(), pane.DirectoryPath, pane.Volume);
            using var data = new DataTransfer(); data.Add(DataTransferItem.Create(PaneDragFormat, payload));
            // Internal drags advertise copy/move. We perform source removal ourselves,
            // after confirmation, rather than trusting the platform's returned effect.
            await DragDrop.DoDragDropAsync(trigger, data, DragDropEffects.Copy | DragDropEffects.Move);
        };
        DragDrop.SetAllowDrop(list, true);
        list.AddHandler(DragDrop.DragOverEvent, (_, e) => { e.DragEffects = DropEffect(pane, e); e.Handled = true; });
        list.AddHandler(DragDrop.DropEvent, async (_, e) =>
        {
            var effect = DropEffect(pane, e); e.DragEffects = effect; e.Handled = true;
            if (effect == DragDropEffects.None) return;
            if (e.DataTransfer.TryGetValue(PaneDragFormat) is { } drag)
            {
                if (effect == DragDropEffects.Move) await MovePromptAsync(drag.Source, pane, drag.Entries);
                else await TransferAsync(drag.Source, pane, false, drag.Entries);
            }
            else
            {
                var paths = e.DataTransfer.TryGetFiles()!.Select(f => f.TryGetLocalPath()!).ToArray();
                await RunAsync(async ct =>
                {
                    var views = CaptureImageViews(pane.Image);
                    var interpretation = pane.IsHost ? CopperDisk.FileSystem.HostNameInterpretation.Literal : await ChooseImportNamesAsync(paths, ct);
                    if (interpretation == null) { _status.Text = "Import cancelled; no changes saved."; return; }
                    var progress = new Progress<DiskExtractionProgress>(p => { if (_busy) _status.Text = $"Copying: {p.Files:N0} files · {p.Bytes:N0} bytes · {p.Name}"; });
                    var result = await Task.Run(() => pane.IsHost ? CopperScreenDiskExtraction.CopyHost(paths, pane.HostDirectory, progress, ct)
                        : pane.Image!.Import(pane.Volume!, pane.Directories.Peek(), paths, ct, progress, interpretation.Value), ct);
                    if (pane.IsHost) await ReadHostAsync(pane, pane.HostDirectory, CancellationToken.None);
                    else await RefreshImageViewsAsync(views);
                    _status.Text = $"Copied {result.Files:N0} files · {result.Bytes:N0} bytes." + (pane.IsHost ? "" : " Image saved.");
                });
            }
        });
    }

    private DragDropEffects DropEffect(Pane destination, DragEventArgs e)
    {
        if (_busy || _disposed || OwnedWindows.Any()) return DragDropEffects.None;
        if (e.DataTransfer.TryGetValue(PaneDragFormat) is { } drag)
        {
            if (!ReferenceEquals(drag.Window, this) || ReferenceEquals(drag.Source, destination) ||
                drag.Path != drag.Source.DirectoryPath || !ReferenceEquals(drag.Volume, drag.Source.Volume) ||
                drag.Entries.Any(i => !drag.Source.List.Items.Contains(i))) return DragDropEffects.None;
            var move = (e.KeyModifiers & KeyModifiers.Shift) != 0;
            if ((move ? MoveUnavailable(drag.Source, destination) : CopyUnavailable(drag.Source, destination)) != null) return DragDropEffects.None;
            var effect = move ? DragDropEffects.Move : DragDropEffects.Copy;
            return (e.DragEffects & effect) != 0 ? effect : DragDropEffects.None;
        }
        // OS file drops copy only: never ask another application's drag source to delete.
        if ((e.DragEffects & DragDropEffects.Copy) == 0 ||
            destination.IsHost && destination.HostPath.Text != destination.HostDirectory ||
            !destination.IsHost && WriteUnavailable(destination) != null) return DragDropEffects.None;
        var files = e.DataTransfer.TryGetFiles()?.ToArray();
        return files is { Length: > 0 } && files.All(f => f.TryGetLocalPath() != null) ? DragDropEffects.Copy : DragDropEffects.None;
    }
}
