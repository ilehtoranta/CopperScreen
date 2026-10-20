using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CopperScreen;

// Host-side, stopped-machine backups. Images are streamed intact, including RDB
// metadata and unpartitioned space. No guest filesystem reconstruction is used.
internal static class CopperScreenHardDiskBackup
{
#if COPPERSCREEN_INSTALLED_TESTS
    internal static Action<string, string, long>? TestObserver;
#endif
    [System.Diagnostics.Conditional("COPPERSCREEN_INSTALLED_TESTS")]
    private static void Observe(string stage, string path, long bytes = 0)
    {
#if COPPERSCREEN_INSTALLED_TESTS
        TestObserver?.Invoke(stage, path, bytes);
#endif
    }
    private sealed record Image(int Unit, long Bytes, string Sha256);
    private sealed record Manifest(int Version, string ProfileSha256, Image[] Images);
    private static string ImageName(int unit) => $"disks/unit-{unit}.hdf";
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    internal static void Export(CopperScreenSettingsDraft draft, string destination, string baseDirectory, string userDataDirectory)
    {
        CopperScreenHardDiskBackupCleanup.Cleanup(userDataDirectory);
        destination = Path.GetFullPath(destination);
        CopperScreenAtomicFile.ValidateDestination(destination);
        var privateRoot = Path.GetFullPath(userDataDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (destination.StartsWith(privateRoot, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Keep the backup outside app data so it survives uninstalling.");
        var defaultRoot = Path.GetFullPath(CopperScreenUserData.DefaultDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (destination.StartsWith(defaultRoot, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Keep the backup outside app data so it survives uninstalling.");
        if (File.Exists(destination)) throw new IOException("Choose a new backup filename. Existing backups are preserved.");
        if (draft.HardDrives.Count == 0) throw new InvalidOperationException("Add a hard disk before exporting a backup.");
        if (draft.HardDrives.Select(d => d.Unit).Distinct().Count() != draft.HardDrives.Count || draft.HardDrives.Any(d => d.Unit < 0))
            throw new InvalidOperationException("Each hard disk needs a different nonnegative unit number.");
        var profile = Encoding.UTF8.GetBytes(CopperScreenProfileStore.Serialize(draft, baseDirectory));
        var id = Guid.NewGuid().ToString("N");
        var temporary = Path.Combine(Path.GetDirectoryName(destination)!, ".hdf-backup-" + id + ".tmp");
        using var operation = CopperScreenHardDiskBackupCleanup.Begin(userDataDirectory, "export", temporary, id);
        var sources = new List<FileStream>();
        try
        {
            // Hold all sources against writes/replacement for the entire snapshot.
            // A writable session in this or another process makes export fail.
            foreach (var drive in draft.HardDrives)
            {
                var source = new FileStream(CopperScreenStartupOptions.ResolveOptionalPath(drive.Path, baseDirectory)!, FileMode.Open, FileAccess.Read, FileShare.Read);
                sources.Add(source);
                if (source.Length == 0 || source.Length % 512 != 0) throw new InvalidDataException("A hard disk image must contain complete 512-byte blocks.");
            }
            Observe("export-opening", temporary);
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                operation?.TrackRoot(output.SafeFileHandle);
                using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
                {
                    using (var entry = archive.CreateEntry("profile.json", CompressionLevel.NoCompression).Open()) entry.Write(profile);
                    var images = new List<Image>();
                    for (var i = 0; i < sources.Count; i++)
                    {
                        var source = sources[i];
                        using var entry = archive.CreateEntry(ImageName(draft.HardDrives[i].Unit), CompressionLevel.NoCompression).Open();
                        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                        Copy(source, entry, source.Length, hash, "export-chunk", temporary);
                        images.Add(new(draft.HardDrives[i].Unit, source.Length, Convert.ToHexString(hash.GetHashAndReset())));
                    }
                    using var manifest = archive.CreateEntry("manifest.json").Open();
                    JsonSerializer.Serialize(manifest, new Manifest(1, Hash(profile), images.ToArray()));
                }
                output.Flush(flushToDisk: true);
            }
            Observe("export-flushed", temporary);
            CopperScreenAtomicFile.ValidateDestination(destination);
            File.Move(temporary, destination); // Publish complete backups; never replace.
        }
        finally
        {
            foreach (var source in sources) source.Dispose();
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    internal static CopperScreenSettingsDraft Restore(string backup, string userDataDirectory, string baseDirectory, Action<CopperScreenSettingsDraft>? remember = null)
    {
        CopperScreenHardDiskBackupCleanup.Cleanup(userDataDirectory);
        using var input = new FileStream(backup, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var archive = new ZipArchive(input, ZipArchiveMode.Read);
        var manifest = JsonSerializer.Deserialize<Manifest>(ReadSmall(Entry(archive, "manifest.json")))
            ?? throw new InvalidDataException("Missing backup manifest.");
        if (manifest.Version != 1 || manifest.Images == null || manifest.Images.Length == 0 ||
            manifest.Images.Any(i => i is null || i.Unit < 0 || i.Bytes <= 0 || i.Bytes % 512 != 0) ||
            manifest.Images.Select(i => i.Unit).Distinct().Count() != manifest.Images.Length)
            throw new InvalidDataException("Unsupported or invalid hard-disk backup.");
        var expected = manifest.Images.Select(i => ImageName(i.Unit)).Append("manifest.json").Append("profile.json").ToHashSet(StringComparer.Ordinal);
        if (archive.Entries.Count != expected.Count || archive.Entries.Any(e => !expected.Contains(e.FullName)))
            throw new InvalidDataException("The backup has unexpected or duplicate entries.");
        var profileBytes = ReadSmall(Entry(archive, "profile.json"));
        if (Hash(profileBytes) != manifest.ProfileSha256) throw new InvalidDataException("The saved configuration failed its integrity check.");
        var root = Path.Combine(CopperScreenUserData.GetDirectory(userDataDirectory), "HardDiskRestores");
        var id = Guid.NewGuid().ToString("N");
        var staging = Path.Combine(root, ".restore-" + id);
        var directory = Path.Combine(root, id);
        CopperScreenAtomicFile.ValidateDestination(directory);
        using var operation = CopperScreenHardDiskBackupCleanup.Begin(userDataDirectory, "restore", staging, id);
        Observe("restore-opening", staging);
        Directory.CreateDirectory(staging);
        operation?.TrackDirectory();
        var published = false;
        var completed = false;
        try
        {
            var profilePath = Path.Combine(staging, "profile.json");
            using (var profileFile = new FileStream(profilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                operation?.TrackFile("profile.json", profileFile.SafeFileHandle);
                profileFile.Write(profileBytes);
            }
            if (!CopperScreenProfile.TryLoad(profilePath, baseDirectory, out var profile, out var error, userDataDirectory))
                throw new InvalidDataException(error);
            var draft = CopperScreenSettingsDraft.FromProfile(profile);
            if (draft.HardDrives.Count != manifest.Images.Length ||
                !draft.HardDrives.Select(d => d.Unit).Order().SequenceEqual(manifest.Images.Select(i => i.Unit).Order()))
                throw new InvalidDataException("The configuration does not match the backed-up disks.");
            foreach (var image in manifest.Images)
            {
                var entry = Entry(archive, ImageName(image.Unit));
                if (entry.Length != image.Bytes) throw new InvalidDataException("The disk image length does not match the backup.");
                using var source = entry.Open();
                using var target = new FileStream(Path.Combine(staging, $"unit-{image.Unit}.hdf"), FileMode.CreateNew, FileAccess.Write, FileShare.None);
                operation?.TrackFile($"unit-{image.Unit}.hdf", target.SafeFileHandle);
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                Copy(source, target, image.Bytes, hash, "restore-chunk", staging);
                if (Convert.ToHexString(hash.GetHashAndReset()) != image.Sha256) throw new InvalidDataException("A disk image failed its integrity check. Keep the backup and try another copy.");
                target.Flush(flushToDisk: true);
            }
            for (var i = 0; i < draft.HardDrives.Count; i++)
                draft.HardDrives[i] = draft.HardDrives[i] with { Path = Path.Combine(directory, $"unit-{draft.HardDrives[i].Unit}.hdf") };
            draft.SourcePath = Path.Combine(directory, "profile.json");
            // Staging is unpublished; keep its recorded file identity stable.
            using (var profileFile = new FileStream(profilePath, FileMode.Truncate, FileAccess.Write, FileShare.None))
            {
                profileFile.Write(Encoding.UTF8.GetBytes(CopperScreenProfileStore.Serialize(draft, baseDirectory)));
                profileFile.Flush(flushToDisk: true);
            }
            Observe("restore-flushed", staging);
            CopperScreenAtomicFile.ValidateDestination(directory);
            Directory.Move(staging, directory);
            published = true;
            // The new private directory is ours. A failed machine-selection save
            // must not leave these copies selected or report a successful restore.
            remember?.Invoke(draft);
            completed = true;
            return draft;
        }
        finally
        {
            // Only our fresh, unpublished staging directory is removed on failure.
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
            if (published && !completed && Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private static ZipArchiveEntry Entry(ZipArchive archive, string name)
    {
        var entries = archive.Entries.Where(e => e.FullName == name).ToArray();
        if (entries.Length != 1) throw new InvalidDataException("Missing or duplicate backup entry: " + name);
        return entries[0];
    }

    private static byte[] ReadSmall(ZipArchiveEntry entry)
    {
        if (entry.Length > 1024 * 1024) throw new InvalidDataException("Backup configuration is too large.");
        using var stream = entry.Open(); using var memory = new MemoryStream();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Copy(stream, memory, entry.Length, hash);
        return memory.ToArray();
    }

    private static void Copy(Stream input, Stream output, long length, IncrementalHash hash, string? stage = null, string? path = null)
    {
        var buffer = new byte[128 * 1024];
        long copied = 0;
        while (length > 0)
        {
            var count = input.Read(buffer, 0, (int)Math.Min(length, buffer.Length));
            if (count == 0) throw new InvalidDataException("The backup or disk image is incomplete.");
            output.Write(buffer, 0, count); hash.AppendData(buffer, 0, count); length -= count;
            copied += count;
            if (stage != null) Observe(stage, path!, copied);
        }
        if (input.ReadByte() != -1) throw new InvalidDataException("The backup or disk image length changed.");
    }
}
