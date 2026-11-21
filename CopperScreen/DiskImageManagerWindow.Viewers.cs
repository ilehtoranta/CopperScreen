using Avalonia.Controls;

namespace CopperScreen;

internal sealed partial class DiskImageManagerWindow
{
    private static string? ViewUnavailable(Pane pane)
    {
        if (pane.IsHost && pane.HostPath.Text != pane.HostDirectory) return "Press Enter to open the entered host folder.";
        if (!pane.IsHost && pane.Volume?.IsMounted != true) return "Select a mounted image volume.";
        return pane.List.SelectedItems is { Count: 1 } &&
            (pane.List.SelectedItem is HostItem { IsDirectory: false } or ImageItem { Entry.IsDirectory: false, Entry.IsLink: false })
            ? null : "Select one ordinary file to view.";
    }

    private async Task ShowFileAsync(Pane pane, bool hex)
    {
        if (_busy || ViewUnavailable(pane) != null) return;
        var selected = pane.List.SelectedItem;
        CopperScreenFileViewSource? source = null; FileViewPage? page = null;
        await RunAsync(async ct =>
        {
            (source, page) = await Task.Run(() =>
            {
                var input = selected is HostItem h ? CopperScreenFileViewSource.Host(h.Path)
                    : CopperScreenFileViewSource.Image(((ImageItem)selected!).Entry);
                return (input, input.ReadPage(0, ct));
            }, ct);
            _status.Text = $"Viewing {source.Name}.";
        });
        if (_disposed || source == null || page == null) return;
        var viewer = new DiskImageFileViewerWindow(source, page, hex, async (offset, cancellation) =>
        {
            FileViewPage? result = null;
            await RunAsync(async ct =>
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, cancellation);
                result = await Task.Run(() => source.ReadPage(offset, linked.Token), linked.Token);
                _status.Text = $"Viewing {source.Name}.";
            });
            return new(result, result == null ? _status.Text : null);
        });
        await viewer.ShowDialog(this);
    }
}
