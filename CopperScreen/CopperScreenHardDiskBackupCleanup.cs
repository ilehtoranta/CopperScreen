using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace CopperScreen;

// Only recorded, still-identical, unpublished objects can be reclaimed. A name
// pattern or age is never evidence of ownership. The journal is also the lease.
internal static class CopperScreenHardDiskBackupCleanup
{
    internal sealed record Result(int Cleaned, int Deferred);
    private sealed record Header(int Version, string Kind, string Path);
    internal sealed record Identity(ulong Volume, ulong Low, ulong High, bool Directory);
    private sealed record Entry(string Name, Identity Identity);
    private static string Root(string data) => Path.Combine(Path.GetFullPath(data), "HardDiskBackupOperations");

    internal sealed class Operation : IDisposable
    {
        private readonly FileStream _lease;
        private readonly string _journal;
        private readonly Identity _journalIdentity;
        private readonly string _artifact;
        internal Operation(string data, string kind, string path, string id)
        {
            _artifact = path;
            var root = Root(data);
            CopperScreenAtomicFile.ValidateDestination(root);
            Directory.CreateDirectory(root);
            _journal = Path.Combine(root, id + ".journal");
            _lease = new FileStream(_journal, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            try
            {
                _journalIdentity = ReadIdentity(_lease.SafeFileHandle);
                Append(new Header(1, kind, path));
            }
            catch { _lease.Dispose(); throw; }
        }
        private void Append<T>(T value)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
            _lease.Write(bytes); _lease.WriteByte(10); _lease.Flush(flushToDisk: true);
        }
        internal void TrackRoot(SafeFileHandle handle) => TrackFile("", handle);
        internal void TrackDirectory()
        {
            using var handle = Open(_artifact, delete: false);
            TrackRoot(handle);
        }
        internal void TrackFile(string name, SafeFileHandle handle)
        {
            Identity identity;
            try { identity = ReadIdentity(handle); }
            catch (Win32Exception ex) when (ex.NativeErrorCode is 1 or 50 or 87)
            {
                // Some destination filesystems cannot provide a stable identity.
                // Copy normally, but leave interrupted objects without that proof.
                return;
            }
            Append(new Entry(name, identity));
        }
        public void Dispose()
        {
            _lease.Dispose();
            // A published object moved out of its temporary path. Never inspect
            // or delete the final backup or restored-directory path here.
            if (IsMissing(_artifact)) TryDeleteJournal(_journal, _journalIdentity);
        }
    }

    internal static Operation? Begin(string data, string kind, string path, string id)
        => OperatingSystem.IsWindows() ? new(data, kind, path, id) : null;

    internal static Result Cleanup(string data)
    {
        if (!OperatingSystem.IsWindows()) return new(0, 0);
        var root = Root(data);
        if (!Directory.Exists(root)) return new(0, 0);
        var cleaned = 0; var deferred = 0;
        try
        {
            CopperScreenAtomicFile.ValidateDestination(root);
            foreach (var journal in Directory.GetFiles(root, "*.journal", SearchOption.TopDirectoryOnly))
            {
                if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(journal), "N", out var guid)) continue;
                Identity? journalIdentity = null;
                var completed = false;
                try
                {
                    CopperScreenAtomicFile.ValidateDestination(journal);
                    // An active process, including one blocked before publication,
                    // holds FileShare.None for its whole operation.
                    using var lease = new FileStream(journal, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                    journalIdentity = ReadIdentity(lease.SafeFileHandle);
                    if (lease.Length == 0 || lease.Length > 1024 * 1024) throw new InvalidDataException("Incomplete ownership journal.");
                    var bytes = new byte[(int)lease.Length]; lease.ReadExactly(bytes);
                    if (bytes[^1] != 10) throw new InvalidDataException("Interrupted ownership record.");
                    var lines = new UTF8Encoding(false, true).GetString(bytes).Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length == 0) throw new InvalidDataException();
                    var header = JsonSerializer.Deserialize<Header>(lines[0]) ?? throw new InvalidDataException();
                    var entries = lines.Skip(1).Select(l => JsonSerializer.Deserialize<Entry>(l) ?? throw new InvalidDataException()).ToDictionary(e => e.Name, e => e.Identity, StringComparer.Ordinal);
                    Validate(header, guid.ToString("N"), data, entries);
                    completed = Reclaim(header, entries);
                    if (!completed) deferred++;
                }
                catch (Exception ex) when (IsFileError(ex)) { deferred++; }
                if (completed && journalIdentity != null)
                {
                    TryDeleteJournal(journal, journalIdentity);
                    cleaned++;
                }
            }
        }
        catch (Exception ex) when (IsFileError(ex)) { deferred++; }
        return new(cleaned, deferred);
    }

    private static void Validate(Header header, string id, string data, Dictionary<string, Identity> entries)
    {
        if (entries.Values.Any(v => v == null)) throw new InvalidDataException();
        if (header.Version != 1 || !Path.IsPathFullyQualified(header.Path) || Path.GetFullPath(header.Path) != header.Path) throw new InvalidDataException();
        if (header.Kind == "export")
        {
            if (Path.GetFileName(header.Path) != ".hdf-backup-" + id + ".tmp" || entries.Keys.Any(n => n != "") || (entries.TryGetValue("", out var root) && root.Directory)) throw new InvalidDataException();
        }
        else if (header.Kind == "restore")
        {
            if (!string.Equals(header.Path, Path.Combine(Path.GetFullPath(data), "HardDiskRestores", ".restore-" + id), StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException();
            if (entries.TryGetValue("", out var root) && !root.Directory) throw new InvalidDataException();
            foreach (var entry in entries.Where(e => e.Key != ""))
                if (entry.Value.Directory || !IsRestoreFile(entry.Key)) throw new InvalidDataException();
        }
        else throw new InvalidDataException();
        CopperScreenAtomicFile.ValidateDestination(header.Path);
    }

    private static bool IsRestoreFile(string name) => name == "profile.json" ||
        (name.StartsWith("unit-", StringComparison.Ordinal) && name.EndsWith(".hdf", StringComparison.Ordinal) &&
         int.TryParse(name.AsSpan(5, name.Length - 9), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var unit) && unit >= 0 && name == $"unit-{unit}.hdf");

    private static bool Reclaim(Header header, Dictionary<string, Identity> entries)
    {
        if (IsMissing(header.Path)) return true;
        if (!entries.TryGetValue("", out var expected)) return false;
        using var root = Open(header.Path, delete: true, directory: expected.Directory);
        if (ReadIdentity(root) != expected) return false;
        if (header.Kind == "export") { Delete(root); return true; }

        // No recursive delete. Unknown children (including links/subdirectories)
        // cause the entire candidate to be left alone. Open all owned files before
        // removing any; delete through those same verified handles, not path names.
        var names = Directory.GetFileSystemEntries(header.Path).Select(Path.GetFileName).ToArray();
        if (names.Any(n => n == null || !entries.ContainsKey(n))) return false;
        var handles = new List<SafeFileHandle>();
        try
        {
            foreach (var name in names)
            {
                var path = Path.Combine(header.Path, name!);
                CopperScreenAtomicFile.ValidateDestination(path);
                var handle = Open(path, delete: true); handles.Add(handle);
                if (ReadIdentity(handle) != entries[name!]) return false;
            }
            foreach (var handle in handles) Delete(handle);
        }
        finally { foreach (var handle in handles) handle.Dispose(); }
        Delete(root); // Fails safely if an unknown child raced with enumeration.
        return true;
    }

    private static void TryDeleteJournal(string journal, Identity expected)
    {
        try
        {
            CopperScreenAtomicFile.ValidateDestination(journal);
            using var handle = Open(journal, delete: true);
            if (ReadIdentity(handle) == expected) Delete(handle);
        }
        catch (Exception ex) when (IsFileError(ex)) { /* Retry next startup/operation. */ }
    }
    private static bool IsFileError(Exception ex) => ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException or JsonException or Win32Exception;

    private static bool IsMissing(string path)
    {
        try { using var handle = Open(path, delete: false); return false; }
        catch (Win32Exception ex) when (ex.NativeErrorCode is 2 or 3)
        { return Directory.Exists(Path.GetDirectoryName(path)); }
        catch (Win32Exception) { return false; }
    }
    private static SafeFileHandle Open(string path, bool delete, bool directory = false)
    {
        // FileStream supports long paths; native handle opens must do so too.
        // Installed-package LocalState paths can exceed MAX_PATH even for a
        // normal restore. Keep UNC paths in their extended UNC namespace.
        path = Path.GetFullPath(path);
        if (!path.StartsWith(@"\\?\", StringComparison.Ordinal))
            path = path.StartsWith(@"\\", StringComparison.Ordinal) ? @"\\?\UNC\" + path[2..] : @"\\?\" + path;
        var handle = CreateFile(path, 0x80u | (delete ? 0x10000u : 0), delete ? (directory ? 3u : 0u) : 7u, IntPtr.Zero, 3, 0x02200000, IntPtr.Zero);
        if (!handle.IsInvalid) return handle;
        var error = Marshal.GetLastWin32Error(); handle.Dispose(); throw new Win32Exception(error);
    }
    private static Identity ReadIdentity(SafeFileHandle handle)
    {
        if (!GetFileInformationByHandle(handle, out var info) || !GetFileInformationByHandleEx(handle, 18, out var id, 24)) throw new Win32Exception(Marshal.GetLastWin32Error());
        if ((info.Attributes & 0x400) != 0) throw new IOException("Linked backup temporary object is not owned.");
        return new(id.Volume, id.Low, id.High, (info.Attributes & 0x10) != 0);
    }
    private static void Delete(SafeFileHandle handle)
    {
        var info = new Disposition { DeleteFile = 1 };
        if (!SetFileInformationByHandle(handle, 4, ref info, 1)) throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    [StructLayout(LayoutKind.Sequential)] private struct FileId { internal ulong Volume, Low, High; }
    [StructLayout(LayoutKind.Sequential)] private struct FileInfo { internal uint Attributes, CreationLow, CreationHigh, AccessLow, AccessHigh, WriteLow, WriteHigh, Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow; }
    [StructLayout(LayoutKind.Sequential)] private struct Disposition { internal byte DeleteFile; }
    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInfo info);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetFileInformationByHandleEx(SafeFileHandle handle, int kind, out FileId info, uint size);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int kind, ref Disposition info, uint size);
}
