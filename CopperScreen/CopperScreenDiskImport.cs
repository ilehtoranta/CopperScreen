using System.Security.Cryptography;
using CopperDisk;
using CopperStart.FileSystem;

namespace CopperScreen;

internal sealed partial class CopperScreenDiskImage
{
    // The original remains protected and unchanged until the entire selection has succeeded.
    internal DiskExtractionProgress Import(DiskImageVolume volume, DiskImageEntry directory,
        IReadOnlyList<string> sources, CancellationToken cancellationToken = default,
        IProgress<DiskExtractionProgress>? progress = null)
    {
        if (!ReferenceEquals(volume.Owner, this) || !ReferenceEquals(directory.Volume, volume) || !directory.IsDirectory)
            throw new ArgumentException("Choose a directory in this image.");
        if (volume.ImportUnavailableReason is { } reason) throw new NotSupportedException(reason);
        if (sources.Count == 0) throw new ArgumentException("Select host files or directories to copy.");
        RequireOrdinarySource(_path);
        foreach (var source in sources)
        {
            var full = Path.GetFullPath(Path.TrimEndingDirectorySeparator(source));
            if (string.Equals(full, _path, StringComparison.OrdinalIgnoreCase) ||
                Directory.Exists(full) && _path.StartsWith(full + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new IOException("The host selection must not contain the destination image itself.");
        }
        return Mutate(volume, directory, (session, parent, ct) =>
        {
            var result = new DiskExtractionProgress();
            var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var source in sources)
            {
                ct.ThrowIfCancellationRequested();
                var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(source));
                if (!roots.Add(name)) throw new IOException("Selected host names collide in the image.");
                ImportTree(session, parent, Path.GetFullPath(source), ref result, progress, ct);
            }
            return result;
        }, cancellationToken);
    }

    private DiskExtractionProgress Mutate(DiskImageVolume volume, DiskImageEntry directory,
        Func<FileSystemSession, FileSystemEntry, CancellationToken, DiskExtractionProgress> action, CancellationToken cancellationToken,
        DiskImageVolume? secondVolume = null, Action<FileSystemSession, CancellationToken>? secondAction = null)
    {
        if (!ReferenceEquals(volume.Owner, this) || !ReferenceEquals(directory.Volume, volume) || !directory.IsDirectory)
            throw new ArgumentException("Choose a directory in this image.");
        if (volume.ImportUnavailableReason is { } reason) throw new NotSupportedException(reason);
        if (secondVolume != null)
        {
            RequireCurrent(secondVolume);
            if (secondVolume.ImportUnavailableReason is { } secondReason) throw new NotSupportedException(secondReason);
        }
        RequireOrdinarySource(_path);
        var stage = Path.Combine(Path.GetDirectoryName(_path)!, ".copperscreen-import-" + Guid.NewGuid().ToString("N"));
        // Both publication and cleanup are confined to this generated sibling.
        if (Path.GetDirectoryName(Path.GetFullPath(stage)) != Path.GetDirectoryName(_path)) throw new IOException("Invalid import staging path.");
        var range = volume.Range; var targetPath = directory.Path;
        var result = new DiskExtractionProgress();
        try
        {
            using (var disk = new FileStream(stage, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                _image.CopyTo(disk, cancellationToken);
                disk.Position = 0; var originalHash = Hash(disk, cancellationToken);
                // Readers continue to use the protected original, including a
                // source directory/partition in this same image. Only the clone is written.
                var reserved = range.DosType == null && volume.FileSystemName.StartsWith("SFS", StringComparison.Ordinal) ? 1u : range.ReservedBlocks;
                var geometry = new FileSystemGeometry(checked((uint)(range.Length / range.BlockSize)), range.BlockSize, reserved);
                using (var session = new FileSystemSession(new ImportSource(disk, range), geometry,
                    new FileSystemServiceOptions { AllowWrites = true }, cancellationToken))
                {
                    var parent = ResolveDirectory(session, targetPath, cancellationToken);
                    result = action(session, parent, cancellationToken);
                }
                if (secondVolume != null)
                {
                    using (var session = StageSession(disk, secondVolume, true, cancellationToken)) secondAction!(session, cancellationToken);
                    using var checkSource = StageSession(disk, secondVolume, false, cancellationToken);
                    _ = checkSource.EnumerateDirectory(checkSource.Root, cancellationToken).ToArray();
                }
                // A fresh guest mount checks that the completed writer left a clean, browsable volume.
                using (var check = new FileSystemSession(new ImportSource(disk, range), geometry, cancellationToken: cancellationToken))
                    _ = check.EnumerateDirectory(ResolveDirectory(check, targetPath, cancellationToken), cancellationToken).ToArray();
                disk.Flush(true);
                cancellationToken.ThrowIfCancellationRequested();
                CloseVolumes(); _image.Dispose();
                try
                {
                    // Reacquire exclusive writer ownership and verify the protected snapshot before replacement.
                    using var guard = new FileStream(_path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read | FileShare.Delete);
                    if (guard.Length != disk.Length || !Hash(guard, cancellationToken).SequenceEqual(originalHash))
                        throw new IOException("The image changed while preparing the import. Reopen it and try again.");
                    cancellationToken.ThrowIfCancellationRequested();
                    disk.Dispose();
                    File.Replace(stage, _path, null);
                }
                finally
                {
                    _image = AmigaSectorImage.Open(_path);
                    Volumes = _image.Volumes.Select(v => new DiskImageVolume(this, v)).ToArray();
                }
            }
            return result;
        }
        finally { if (File.Exists(stage)) File.Delete(stage); }
    }

    private static FileSystemSession StageSession(FileStream disk, DiskImageVolume volume, bool writable, CancellationToken ct)
    {
        var range = volume.Range;
        var reserved = range.DosType == null && volume.FileSystemName.StartsWith("SFS", StringComparison.Ordinal) ? 1u : range.ReservedBlocks;
        var geometry = new FileSystemGeometry(checked((uint)(range.Length / range.BlockSize)), range.BlockSize, reserved);
        return new FileSystemSession(new ImportSource(disk, range), geometry, new FileSystemServiceOptions { AllowWrites = writable }, ct);
    }

    private static byte[] Hash(Stream stream, CancellationToken token)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[65536];
        while (true) { token.ThrowIfCancellationRequested(); var count = stream.Read(buffer); if (count == 0) break; hash.AppendData(buffer, 0, count); }
        return hash.GetHashAndReset();
    }
    internal static FileSystemEntry ResolveDirectory(FileSystemSession session, string path, CancellationToken token)
    {
        var entry = session.Root;
        foreach (var part in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
            entry = session.EnumerateDirectory(entry, token).Single(e => e.Name == part && e.Kind == FileSystemEntryKind.Directory);
        return entry;
    }
    private static void ImportTree(FileSystemSession session, FileSystemEntry parent, string source,
        ref DiskExtractionProgress result, IProgress<DiskExtractionProgress>? progress, CancellationToken token)
    {
        // Use an explicit stack to retain empty directories and avoid host or guest recursion limits.
        var pending = new Stack<(string Path, FileSystemEntry Parent, FileSystemEntry? Finish)>();
        pending.Push((source, parent, null));
        while (pending.TryPop(out var item))
        {
            token.ThrowIfCancellationRequested();
            RequireOrdinarySource(item.Path);
            if (item.Finish is { } finished) { SetDate(session, finished, Directory.GetLastWriteTime(item.Path), token); continue; }
            var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(item.Path));
            // Case collisions are refused even on a case-sensitive SFS volume so round trips remain unambiguous.
            if (session.EnumerateDirectory(item.Parent, token).Any(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase)))
                throw new IOException($"Image destination already exists: {name}. Existing entries are never overwritten or merged.");
            if (Directory.Exists(item.Path))
            {
                var created = session.CreateDirectory(item.Parent, name, token);
                pending.Push((item.Path, item.Parent, created));
                foreach (var child in Directory.EnumerateFileSystemEntries(item.Path).Reverse()) pending.Push((child, created, null));
            }
            else
            {
                using var input = new FileStream(item.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
                var length = input.Length;
                var maximum = session.Volume.DosType == 0x53465302 ? 0xFFFFFFFFFFFFL : uint.MaxValue;
                if (length > maximum) throw new NotSupportedException("The host file exceeds this filesystem's file-size limit.");
                var file = session.CopyFileFrom(item.Parent, name, input, token);
                if ((ulong)length != file.Length || input.Length != length) throw new IOException("The host source changed while copying.");
                SetDate(session, file, File.GetLastWriteTime(item.Path), token);
                result = new(result.Files + 1, result.Bytes + length, name); progress?.Report(result);
            }
        }
    }
    private static void SetDate(FileSystemSession session, FileSystemEntry entry, DateTime date, CancellationToken token)
    {
        if (date >= new DateTime(1978, 1, 1) && date < new DateTime(2114, 2, 7)) session.SetModified(entry, date, token);
    }
    internal static void RequireOrdinarySource(string path)
    {
        for (FileSystemInfo? item = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path); item != null;
            item = item is DirectoryInfo dir ? dir.Parent : ((FileInfo)item).Directory)
            if ((item.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Host symbolic links and junctions are not imported.");
    }
    private sealed class ImportSource(FileStream disk, AmigaImageVolume range) : IWritableBlockSource
    {
        public long Length => range.Length;
        private void Position(long offset, int bytes)
        {
            if (offset < 0 || offset > Length - bytes) throw new IOException("Import I/O exceeded the selected volume.");
            disk.Position = checked(range.ByteOffset + offset);
        }
        public void Read(long offset, Span<byte> destination) { Position(offset, destination.Length); disk.ReadExactly(destination); }
        public void Write(long offset, ReadOnlySpan<byte> source) { Position(offset, source.Length); disk.Write(source); }
        public void Flush() => disk.Flush(true);
    }
}
