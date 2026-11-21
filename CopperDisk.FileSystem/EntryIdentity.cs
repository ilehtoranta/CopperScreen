namespace CopperDisk.FileSystem;

public readonly record struct DiskEntryId(Guid Generation, Guid Volume, string Path);

[Flags]
public enum DiskVolumeOperations
{
    None = 0, Browse = 1, Read = 2, Import = 4, NewDirectory = 8,
    Rename = 16, Delete = 32, Move = 64, Protect = 128
}

public sealed record DiskVolumeCapabilities(DiskVolumeOperations Operations, string? ReadUnavailableReason, string? WriteUnavailableReason);

public sealed partial class DiskImageSession
{
    public DiskEntry ResolveEntry(DiskEntryId id, CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(_references == 0, this);
        if (id.Generation != Generation) throw new InvalidOperationException("The entry is stale; refresh the directory.");
        var volume = Volumes.SingleOrDefault(v => v.Id == id.Volume) ?? throw new ArgumentException("Unknown volume.");
        if (id.Path == "/") return volume.Root;
        var slash = id.Path.LastIndexOf('/');
        if (slash < 0) throw new ArgumentException("Invalid entry path.");
        var directory = ResolvePath(volume, slash == 0 ? "/" : id.Path[..slash], token);
        return volume.ReadDirectory(directory, token).Single(e => e.Path == id.Path);
    }
}
