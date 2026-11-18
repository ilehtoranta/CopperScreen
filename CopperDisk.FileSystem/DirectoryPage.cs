namespace CopperDisk.FileSystem;

public sealed record DirectoryPage(Guid Generation, IReadOnlyList<DiskEntry> Entries, int? NextOffset);

public sealed partial class DiskImageSession
{
    public void Refresh()
    {
        RequireNoReaders();
        CloseVolumes(); Generation = Guid.NewGuid();
    }
    public static DiskEntry ResolvePath(DiskVolume volume, string path, CancellationToken ct = default)
    {
        if (!path.StartsWith('/') || path.Split('/').Any(p => p is "." or "..")) throw new ArgumentException("Invalid image path.");
        var current = volume.Root;
        foreach (var name in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
            current = volume.ReadDirectory(current, ct).Single(e => e.Name == name && e.IsDirectory && !e.IsLink);
        return current;
    }

    public DirectoryPage Enumerate(DiskVolume volume, DiskEntry directory, Guid expectedGeneration, int offset = 0, int count = 128, CancellationToken ct = default)
    {
        RequireCurrent(volume);
        if (expectedGeneration != Generation) throw new InvalidOperationException("The image changed; refresh the directory.");
        if (offset < 0 || count is < 1 or > 128) throw new ArgumentOutOfRangeException(nameof(count));
        var entries = volume.ReadDirectory(directory, ct);
        return new(Generation, entries.Skip(offset).Take(count).ToArray(), entries.Count > offset + count ? checked(offset + count) : null);
    }
}
