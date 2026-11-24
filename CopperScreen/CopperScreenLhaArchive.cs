using CopperDisk.FileSystem;
using Hst.Compression.Lha;

namespace CopperScreen;

internal static class CopperScreenLhaArchive
{
    private const long MaximumBytes = 1024L * 1024 * 1024;
    private const int MaximumEntries = 32768;

    // Only write ordinary files into an empty, caller-owned staging directory.
    internal static async Task<IReadOnlyList<string>> ExtractAsync(string path, string destination, CancellationToken cancellation)
    {
        using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        Span<byte> header = stackalloc byte[7];
        if (source.Length < header.Length || source.Length > MaximumBytes)
            throw new InvalidDataException("Choose an LhA archive of up to 1 GiB.");
        source.ReadExactly(header);
        if (header[6] != (byte)'-' || !(header[2..5].SequenceEqual("-lh"u8) || header[2..5].SequenceEqual("-lz"u8) ||
            header[2..5].SequenceEqual("-pm"u8)))
            throw new InvalidDataException("The selected file does not have an LhA header.");
        source.Position = 0;

        Directory.CreateDirectory(destination);
        var files = new List<string>();
        var filePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var directories = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var buffer = new byte[65536];
        long total = 0;
        var options = LhaOptions.AmigaLhaOptions;
        var reader = new LhaReader(source, options);
        long nextHeader = 0;
        var entryCount = 0;
        while (nextHeader < source.Length)
        {
            cancellation.ThrowIfCancellationRequested();
            source.Position = nextHeader;
            if (source.ReadByte() == 0) break;
            if (++entryCount > MaximumEntries) throw new InvalidDataException("The LhA archive contains too many entries.");
            source.Position = nextHeader;
            var entry = await reader.ReadAsync(cancellation).ConfigureAwait(false) ?? throw new InvalidDataException("An LhA entry has a truncated header.");
            if (entry.HeaderOffset != nextHeader || entry.HeaderSize <= 0 || entry.HeaderSize > source.Length - nextHeader ||
                entry.PackedSize < 0 || entry.PackedSize > source.Length - nextHeader - entry.HeaderSize)
                throw new InvalidDataException("An LhA entry lies outside the archive.");
            nextHeader += entry.HeaderSize + entry.PackedSize;
            if (!string.IsNullOrEmpty(entry.LinkTarget) || (entry.UnixMode & LhaUnixMode.TypeMask) is not (0 or LhaUnixMode.Directory or LhaUnixMode.Regular))
                throw new InvalidDataException("WHDLoad archives must contain ordinary files and directories.");

            var directory = entry.Method == LhaMethod.Lhd;
            var name = entry.Name.Replace('\\', '/');
            if (name.StartsWith("./", StringComparison.Ordinal)) name = name[2..];
            if (directory)
            {
                name = name.TrimEnd('/');
                if (name is "" or ".") continue;
            }
            var parts = name.Split('/');
            if (parts.Length > 32 || !names.Add(name))
                throw new InvalidDataException("The LhA archive has duplicate paths or too many nested directories.");
            var target = destination;
            for (var i = 0; i < parts.Length; i++)
            {
                ValidateGuestName(parts[i]);
                target = Path.Combine(target, parts[i]);
                if (directory || i < parts.Length - 1)
                {
                    if (filePaths.Contains(target) || directories.TryGetValue(target, out var existing) && existing != target)
                        throw new InvalidDataException("The LhA archive has conflicting paths.");
                    if (directories.TryAdd(target, target))
                    {
                        if (directories.Count > MaximumEntries) throw new InvalidDataException("The LhA archive contains too many directories.");
                        Directory.CreateDirectory(target);
                    }
                }
            }
            if (directory) continue;
            if (directories.ContainsKey(target) || !filePaths.Add(target))
                throw new InvalidDataException("The LhA archive has conflicting paths.");
            var size = entry.UnpackedSize;
            if (size < 0 || size > MaximumBytes - total)
                throw new InvalidDataException("The unpacked LhA archive exceeds 1 GiB or has an invalid file size.");
            total += size;
            using var input = new LhaEntryStream(source, entry, options);
            using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            long written = 0;
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) != 0)
            {
                cancellation.ThrowIfCancellationRequested();
                if (read > size - written) throw new InvalidDataException("An LhA entry exceeds its declared size.");
                output.Write(buffer, 0, read);
                written += read;
            }
            if (written != size) throw new InvalidDataException("An LhA entry is truncated.");
            files.Add(target);
        }
        return files;
    }

    internal static void ValidateGuestName(string name)
    {
        DiskTransfer.ValidateName(name);
        if (name.Length > 107 || name.Any(c => c > 255) || name.StartsWith(".copperfs-", StringComparison.OrdinalIgnoreCase) ||
            name.Equals(".copperfs.json", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"The file name cannot be used on a shared Amiga volume: {name}");
    }
}
