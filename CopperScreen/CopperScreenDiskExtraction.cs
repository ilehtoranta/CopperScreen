namespace CopperScreen;

internal readonly record struct DiskExtractionProgress(int Files, long Bytes, string Name);

// Host destination policy belongs to the desktop tool, not the emulated hardware.
internal static class CopperScreenDiskExtraction
{
    internal static DiskExtractionProgress CopyHost(IReadOnlyList<string> entries, string destination,
        IProgress<DiskExtractionProgress>? progress, CancellationToken token)
    {
        destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destination));
        RequireOrdinaryDirectory(destination);
        if (entries.Count == 0) throw new ArgumentException("Select files or directories to copy.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in entries)
        {
            CopperScreenDiskImage.RequireOrdinarySource(source);
            var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(source)); ValidateName(name);
            if (!names.Add(name)) throw new IOException("Selected host names collide.");
            RequireAbsent(Path.Combine(destination, name));
            if (Directory.Exists(source) && (destination.Equals(source, StringComparison.OrdinalIgnoreCase) ||
                destination.StartsWith(Path.TrimEndingDirectorySeparator(source) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                throw new IOException("A directory cannot be copied into itself or its descendants.");
        }
        var stage = Path.GetFullPath(Path.Combine(destination, ".copperscreen-extract-" + Guid.NewGuid().ToString("N")));
        if (Path.GetDirectoryName(stage) != destination) throw new IOException("Invalid staging path.");
        Directory.CreateDirectory(stage);
        try
        {
            var result = new DiskExtractionProgress(); var buffer = new byte[65536];
            var pending = new Stack<(string Source, string Parent, bool Finish)>();
            foreach (var source in entries.Reverse()) pending.Push((source, stage, false));
            while (pending.TryPop(out var item))
            {
                token.ThrowIfCancellationRequested(); CopperScreenDiskImage.RequireOrdinarySource(item.Source);
                var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(item.Source)); ValidateName(name);
                var target = Path.Combine(item.Parent, name);
                if (item.Finish) { Directory.SetLastWriteTime(target, Directory.GetLastWriteTime(item.Source)); continue; }
                RequireAbsent(target);
                if (Directory.Exists(item.Source))
                {
                    Directory.CreateDirectory(target); pending.Push((item.Source, item.Parent, true));
                    foreach (var child in Directory.EnumerateFileSystemEntries(item.Source)) pending.Push((child, target, false));
                }
                else
                {
                    using var input = new FileStream(item.Source, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    int read; long bytes = 0;
                    while ((read = input.Read(buffer)) != 0) { token.ThrowIfCancellationRequested(); output.Write(buffer, 0, read); bytes += read; }
                    output.Dispose(); File.SetLastWriteTime(target, File.GetLastWriteTime(item.Source));
                    result = new(result.Files + 1, result.Bytes + bytes, name); progress?.Report(result);
                }
            }
            token.ThrowIfCancellationRequested(); RequireOrdinaryDirectory(destination);
            foreach (var source in entries) RequireAbsent(Path.Combine(destination, Path.GetFileName(Path.TrimEndingDirectorySeparator(source))));
            foreach (var source in entries)
            {
                var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(source)); var target = Path.Combine(destination, name);
                if (Directory.Exists(source)) Directory.Move(Path.Combine(stage, name), target); else File.Move(Path.Combine(stage, name), target);
            }
            return result;
        }
        finally { Directory.Delete(stage, true); }
    }

    internal static DiskExtractionProgress Extract(DiskImageVolume volume,
        IReadOnlyList<DiskImageEntry> entries, string destination,
        IProgress<DiskExtractionProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (entries.Count == 0) throw new InvalidOperationException("Select files or directories to copy.");
        destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destination));
        RequireOrdinaryDirectory(destination);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            ValidateName(entry.Name);
            if (!names.Add(entry.Name)) throw new IOException("Selected names collide on the host.");
            RequireAbsent(Path.Combine(destination, entry.Name));
        }
        var staging = Path.GetFullPath(Path.Combine(destination, ".copperscreen-extract-" + Guid.NewGuid().ToString("N")));
        // The only recursive cleanup below is confined to this newly generated child.
        if (Path.GetDirectoryName(staging) != destination) throw new IOException("Invalid extraction staging path.");
        Directory.CreateDirectory(staging);
        var result = new DiskExtractionProgress();
        try
        {
            var seen = new HashSet<DiskImageEntry>();
            var pending = new Stack<(DiskImageEntry Entry, string Parent, bool Finish)>();
            foreach (var entry in entries.Reverse()) pending.Push((entry, staging, false));
            // Guard repeated selected paths; filesystem traversal and parent validation belong to the guest.
            var directories = new HashSet<string>(StringComparer.Ordinal);
            while (pending.TryPop(out var item))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entry = item.Entry;
                ValidateName(entry.Name);
                var path = Path.Combine(item.Parent, entry.Name);
                if (item.Finish)
                {
                    if (entry.Modified is { } date) Directory.SetLastWriteTime(path, date);
                    continue;
                }
                if (!seen.Add(entry)) throw new InvalidDataException("An entry was selected more than once.");
                if (entry.IsLink) throw new NotSupportedException($"Links are not copied: {entry.Name}");
                RequireAbsent(path);
                if (entry.IsDirectory)
                {
                    if (!directories.Add(entry.Path)) throw new InvalidDataException("A directory tree contains a repeated directory.");
                    Directory.CreateDirectory(path);
                    pending.Push((entry, item.Parent, true));
                    var children = volume.ReadDirectory(entry, cancellationToken);
                    var childNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var child in children.Reverse())
                    {
                        ValidateName(child.Name);
                        if (!childNames.Add(child.Name)) throw new IOException($"Names in {entry.Name} collide on the host.");
                        pending.Push((child, path, false));
                    }
                }
                else
                {
                    using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        volume.CopyFileTo(entry, output, cancellationToken);
                    if (entry.Modified is { } date) File.SetLastWriteTime(path, date);
                    result = new(result.Files + 1, result.Bytes + entry.Size, entry.Name);
                    progress?.Report(result);
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            RequireOrdinaryDirectory(destination);
            foreach (var entry in entries) RequireAbsent(Path.Combine(destination, entry.Name));
            foreach (var entry in entries)
            {
                var source = Path.Combine(staging, entry.Name);
                var target = Path.Combine(destination, entry.Name);
                if (entry.IsDirectory) Directory.Move(source, target);
                else File.Move(source, target); // No overwrite and no merge, even if a destination appeared meanwhile.
            }
            return result;
        }
        finally { Directory.Delete(staging, recursive: true); }
    }

    internal static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name is "." or ".." || name.EndsWith('.') || name.EndsWith(' ') ||
            name.Any(c => c < 32 || "<>:\"/\\|?*".Contains(c)))
            throw new IOException($"The Amiga name cannot be represented safely on the host: {name}");
        var stem = name.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
        if (stem is "CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$" ||
            (stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && "123456789¹²³".Contains(stem[3])))
            throw new IOException($"The Amiga name is a reserved host filename: {name}");
    }

    private static void RequireAbsent(string path)
    {
        // GetAttributes detects dangling links too, unlike File.Exists/Directory.Exists.
        try { _ = File.GetAttributes(path); }
        catch (FileNotFoundException) { return; }
        catch (DirectoryNotFoundException) { return; }
        throw new IOException($"Destination already exists: {Path.GetFileName(path)}. Choose another folder; existing entries are never overwritten or merged.");
    }

    private static void RequireOrdinaryDirectory(string path)
    {
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException("Choose an existing destination folder.");
        for (var directory = new DirectoryInfo(path); directory != null; directory = directory.Parent)
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Choose a destination without symbolic links or junctions in its path.");
    }
}
