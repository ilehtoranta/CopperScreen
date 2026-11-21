using CopperStart.FileSystem;

namespace CopperDisk.FileSystem;

public sealed partial class DiskImageSession
{
    public void CreateDirectory(DiskVolume volume, DiskEntry directory, string name, CancellationToken token = default)
        => Mutate(volume, directory, (s, parent, ct) => { s.CreateDirectory(parent, name, ct); return default; }, token);

    public void Rename(DiskVolume volume, DiskEntry directory, DiskEntry entry, string name, CancellationToken token = default)
        => RenameTogether(volume, directory, [(entry, name)], token);

    /// <summary>Rename entries in one directory and publish only if every rename succeeds.</summary>
    public void RenameTogether(DiskVolume volume, DiskEntry directory,
        IReadOnlyList<(DiskEntry Entry, string Name)> renames, CancellationToken token = default)
    {
        if (renames.Count == 0) throw new ArgumentException("Select entries to rename.", nameof(renames));
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (entry, _) in renames)
        {
            RequireChild(volume, directory, entry);
            if (!seen.Add(entry.Path)) throw new ArgumentException("An entry can only be renamed once.", nameof(renames));
        }
        Mutate(volume, directory, (s, parent, ct) =>
        {
            foreach (var (entry, name) in renames)
            {
                ct.ThrowIfCancellationRequested();
                var target = FindChild(s, parent, entry.Name, ct);
                s.Rename(target, parent, name, ct);
            }
            return default;
        }, token);
    }

    public void Delete(DiskVolume volume, DiskEntry directory, IReadOnlyList<DiskEntry> entries, CancellationToken token = default)
    {
        if (entries.Count == 0) throw new ArgumentException("Select entries to delete.");
        foreach (var entry in entries) RequireChild(volume, directory, entry);
        Mutate(volume, directory, (s, parent, ct) =>
        {
            DeleteTrees(s, entries.Select(e => FindChild(s, parent, e.Name, ct)), ct);
            return default;
        }, token);
    }

    private static void DeleteTrees(FileSystemSession s, IEnumerable<FileSystemEntry> entries, CancellationToken ct)
    {
        var pending = new Stack<(FileSystemEntry Entry, bool Finish)>();
        foreach (var e in entries) pending.Push((e, false));
        long directories = 0;
        while (pending.TryPop(out var item))
        {
            ct.ThrowIfCancellationRequested();
            if (item.Entry.Kind == FileSystemEntryKind.Link) throw new NotSupportedException("Links are not deleted or followed.");
            if (item.Entry.Kind == FileSystemEntryKind.Directory && !item.Finish)
            {
                if (++directories > s.Volume.Geometry.BlockCount) throw new InvalidDataException("Directory traversal exceeds the volume size.");
                pending.Push((item.Entry, true));
                foreach (var child in s.EnumerateDirectory(item.Entry, ct).ToArray()) pending.Push((child, false));
            }
            else s.Delete(item.Entry, ct);
        }
    }

    private static FileSystemEntry FindChild(FileSystemSession s, FileSystemEntry parent, string name, CancellationToken ct)
        => s.EnumerateDirectory(parent, ct).Single(e => e.Name == name);

    private static void RequireChild(DiskVolume volume, DiskEntry directory, DiskEntry entry)
    {
        volume.Owner.RequireCurrent(volume);
        if (entry.Generation != volume.Owner.Generation || directory.Generation != volume.Owner.Generation)
            throw new InvalidOperationException("The selection is stale; refresh before saving.");
        if (!ReferenceEquals(directory.Volume, volume) || !directory.IsDirectory || !ReferenceEquals(entry.Volume, volume) || entry.IsLink || entry.Path == "/" ||
            entry.Path != directory.Path.TrimEnd('/') + "/" + entry.Name)
            throw new NotSupportedException("Choose an ordinary entry in the current directory.");
    }

    public DiskProgress CopyFromImage(DiskVolume target, DiskEntry directory,
        DiskVolume source, IReadOnlyList<DiskEntry> entries, CancellationToken token = default,
        IProgress<DiskProgress>? progress = null)
    {
        if (!ReferenceEquals(source.Owner, this) && string.Equals(source.Owner.FilePath, FilePath, StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("Use one shared image owner for copies within an image.");
        if (entries.Count == 0 || entries.Any(e => !ReferenceEquals(e.Volume, source))) throw new ArgumentException("Select source image entries.");
        if (ReferenceEquals(source, target))
            foreach (var entry in entries)
            {
                if (entry.Path == directory.Path.TrimEnd('/') + "/" + entry.Name)
                    throw new IOException("Choose a different destination directory.");
                if (entry.IsDirectory && (directory.Path == entry.Path || directory.Path.StartsWith(entry.Path + "/", StringComparison.Ordinal)))
                    throw new IOException("A directory cannot be copied into itself or its descendants.");
            }
        return Mutate(target, directory, (s, parent, ct) =>
        {
            return CopyTrees(s, parent, source, entries, ct, progress);
        }, token);
    }

    private static DiskProgress CopyTrees(FileSystemSession s, FileSystemEntry parent,
        DiskVolume source, IReadOnlyList<DiskEntry> entries, CancellationToken ct, IProgress<DiskProgress>? progress)
    {
        var result = new DiskProgress();
        var pending = new Stack<(DiskEntry Entry, FileSystemEntry Parent, FileSystemEntry? Finish)>();
        foreach (var entry in entries.Reverse()) pending.Push((entry, parent, null));
        long directories = 0;
        while (pending.TryPop(out var item))
        {
            ct.ThrowIfCancellationRequested();
            var entry = item.Entry;
            if (entry.IsLink) throw new NotSupportedException($"Links are not copied: {entry.Name}");
            if (item.Finish is { } finished) { Metadata(s, finished, entry, ct); continue; }
            // CopperFS applies the destination format's name comparison and refuses replacement.
            // Windows case folding must not restrict direct image-to-image transfers.
            if (entry.IsDirectory)
            {
                if (++directories > source.Range.Length / source.BlockSize) throw new InvalidDataException("Directory traversal exceeds the volume size.");
                var created = s.CreateDirectory(item.Parent, entry.Name, ct);
                pending.Push((entry, item.Parent, created));
                foreach (var child in source.ReadDirectory(entry, ct).Reverse()) pending.Push((child, created, null));
            }
            else
            {
                using var input = source.OpenRead(entry, ct);
                var created = s.CopyFileFrom(item.Parent, entry.Name, input, ct);
                if (created.Length != (ulong)entry.Size) throw new IOException("The source file length changed.");
                Metadata(s, created, entry, ct);
                result = new(result.Files + 1, result.Bytes + entry.Size, entry.Name); progress?.Report(result);
            }
        }
        return result;
    }

    public void Protect(DiskVolume volume, DiskEntry directory, IReadOnlyList<DiskEntry> entries,
        uint mask, uint value, CancellationToken token = default)
    {
        if (entries.Count == 0) throw new ArgumentException("Select entries to protect.");
        if ((mask & ~255u) != 0) throw new ArgumentException("Only HSPARWED flags can be changed.");
        foreach (var entry in entries) RequireChild(volume, directory, entry);
        Mutate(volume, directory, (s, parent, ct) =>
        {
            foreach (var entry in entries)
            {
                var target = FindChild(s, parent, entry.Name, ct);
                s.SetProtection(target, (target.Protection & ~mask) | (value & mask), ct);
            }
            return default;
        }, token);
    }

    public DiskProgress MoveFromImage(DiskVolume target, DiskEntry directory,
        DiskVolume source, DiskEntry sourceDirectory, IReadOnlyList<DiskEntry> entries,
        CancellationToken token = default, IProgress<DiskProgress>? progress = null)
    {
        if (source.ImportUnavailableReason is { } reason) throw new NotSupportedException(reason);
        if (entries.Count == 0) throw new ArgumentException("Select entries to move.");
        foreach (var entry in entries) RequireChild(source, sourceDirectory, entry);
        if (ReferenceEquals(source.Owner, this))
        {
            if (ReferenceEquals(source, target))
            {
                foreach (var entry in entries)
                    if (sourceDirectory.Path == directory.Path || entry.IsDirectory &&
                        (directory.Path == entry.Path || directory.Path.StartsWith(entry.Path + "/", StringComparison.Ordinal)))
                        throw new IOException("Choose a destination outside the selected entries and their current directory.");
                return Mutate(target, directory, (s, parent, ct) =>
                {
                    var oldParent = ResolveDirectory(s, sourceDirectory.Path, ct);
                    foreach (var entry in entries) s.Rename(FindChild(s, oldParent, entry.Name, ct), parent, entry.Name, ct);
                    return new(entries.Count(e => !e.IsDirectory), entries.Where(e => !e.IsDirectory).Sum(e => e.Size), "");
                }, token);
            }
            // Both partitions change in one clone. A failed source deletion discards the copy too.
            return Mutate(target, directory, (s, parent, ct) => CopyTrees(s, parent, source, entries, ct, progress), token,
                source, (s, ct) => DeleteTrees(s, entries.Select(e => FindChild(s, ResolveDirectory(s, sourceDirectory.Path, ct), e.Name, ct)), ct));
        }
        var result = CopyFromImage(target, directory, source, entries, token, progress);
        try { source.Owner.Delete(source, sourceDirectory, entries, token); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException or OperationCanceledException or TimeoutException)
        { throw new DiskMoveIncompleteException(result, ex); }
        return result;
    }

    private static void Metadata(FileSystemSession s, FileSystemEntry target, DiskEntry source, CancellationToken ct)
    {
        if (source.Modified is { } date) SetDate(s, target, date, ct);
        s.SetComment(target, source.Comment, ct);
        s.SetProtection(target, source.Protection, ct);
    }
}
