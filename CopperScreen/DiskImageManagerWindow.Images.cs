namespace CopperScreen;

internal sealed partial class DiskImageManagerWindow
{
    private sealed record ImageView(Pane Pane, CopperScreenDiskImage Image, DiskImageVolume Volume,
        int VolumeIndex, string Path, string[] SelectedPaths);
    private readonly record struct DirectoryRename(int VolumeIndex, string OldPath, string NewPath);

    private ImageView[] CaptureImageViews(CopperScreenDiskImage? image = null) => _panes
        .Where(p => p.Image != null && p.Volume != null && (image == null || ReferenceEquals(p.Image, image)))
        .Select(p => new ImageView(p, p.Image!, p.Volume!, p.Image!.Volumes.ToList().IndexOf(p.Volume!), p.DirectoryPath,
            p.List.SelectedItems!.OfType<ImageItem>().Select(i => i.Entry.Path).ToArray())).ToArray();

    private async Task RefreshImageViewsAsync(IEnumerable<ImageView> views, DirectoryRename? rename = null)
    {
        foreach (var view in views)
        {
            var pane = view.Pane;
            if (pane.IsHost || !ReferenceEquals(pane.Image, view.Image) || view.VolumeIndex < 0) continue;
            if (view.Image.Volumes.Contains(view.Volume)) continue;
            var volume = view.Image.Volumes[view.VolumeIndex];
            // A publication rebuilds the canonical volume records. Already rebound
            // views and images that were not modified need no guest work.
            if (ReferenceEquals(pane.Volume, volume)) continue;
            string Map(string path) => rename is { } r && r.VolumeIndex == view.VolumeIndex &&
                (path == r.OldPath || path.StartsWith(r.OldPath + "/", StringComparison.Ordinal))
                ? r.NewPath + path[r.OldPath.Length..] : path;
            await SelectVolumeAsync(pane, volume, CancellationToken.None, Map(view.Path));
            var selected = view.SelectedPaths.Select(Map).ToHashSet(StringComparer.Ordinal);
            foreach (var item in pane.List.Items.OfType<ImageItem>())
                if (selected.Contains(item.Entry.Path)) pane.List.SelectedItems!.Add(item);
        }
    }

    private void ReleaseImage(Pane pane)
    {
        var image = pane.Image; var volume = pane.Volume;
        pane.Image = null; pane.Volume = null;
        if (volume != null && !_panes.Any(p => ReferenceEquals(p.Volume, volume))) volume.Close();
        image?.Dispose();
    }
}
