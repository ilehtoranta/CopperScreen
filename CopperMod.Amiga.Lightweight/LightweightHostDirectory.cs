using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace CopperMod.Amiga.Lightweight;

internal sealed class HostDosException(int error, string message) : IOException(message)
{
    internal int Error { get; } = error;
}

// Host paths never come directly from a packet. A path is assembled from resolved
// directory entries under Root. On Windows directory leases prevent parent
// replacement while a request is accessing a backing object.
internal sealed class LightweightHostDirectory
{
    internal const string MetadataName = ".copperfs.json";
    private const string InternalPrefix = ".copperfs-";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    internal LightweightHostDirectoryConfiguration Configuration { get; }
    internal string Root { get; }
    internal sealed record Entry(string Name, string HostName, uint Protection = 0, string Comment = "");
    internal sealed class Metadata
    {
        public int Version { get; set; } = 1;
        public List<Entry> Entries { get; set; } = [];
    }

    internal LightweightHostDirectory(LightweightHostDirectoryConfiguration configuration)
    {
        configuration.Validate();
        Configuration = configuration;
        Root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(configuration.Path));
        if (!Directory.Exists(Root)) throw new DirectoryNotFoundException($"Shared folder does not exist: {Root}");
        using var lease = Lease(Root);
        _ = ReadMetadata(Root);
    }

    internal void Writable()
    {
        if (Configuration.ReadOnly) throw new HostDosException(214, "Shared folder is read-only.");
    }

    internal static bool Internal(string name) => name.Equals(MetadataName, StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith(InternalPrefix, StringComparison.OrdinalIgnoreCase);

    private static void ValidGuestName(string name)
    {
        if (string.IsNullOrEmpty(name) || name.Length > 107 || name.Any(c => c < 32 || c > 255 || c is ':' or '/' or '\\'))
            throw new HostDosException(210, "Invalid Amiga filename.");
    }

    internal Metadata ReadMetadata(string directory)
    {
        var path = Path.Combine(directory, MetadataName);
        if (!File.Exists(path)) return new();
        RejectLink(path);
        try
        {
            if (new FileInfo(path).Length > 4 * 1024 * 1024) throw new InvalidDataException("Metadata is too large.");
            using var stream = Open(path, FileMode.Open, FileAccess.Read);
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("Version", out _) || !document.RootElement.TryGetProperty("Entries", out _))
                throw new InvalidDataException("Missing metadata version or entries.");
            var metadata = document.Deserialize<Metadata>(JsonOptions);
            if (metadata is null || metadata.Version != 1 || metadata.Entries is null) throw new InvalidDataException("Unknown metadata version.");
            var guests = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in metadata.Entries)
            {
                if (entry is null) throw new InvalidDataException("Null metadata entry.");
                ValidGuestName(entry.Name);
                if (string.IsNullOrWhiteSpace(entry.HostName) || Path.GetFileName(entry.HostName) != entry.HostName ||
                    entry.HostName is "." or ".." || entry.HostName.Length > 255 || entry.HostName.Any(c => c < 32 || c is ':' or '/' or '\\' or '<' or '>' or '"' or '|' or '?' or '*') ||
                    entry.HostName.EndsWith('.') || entry.HostName.EndsWith(' ') ||
                    WindowsDeviceName(entry.HostName) ||
                    entry.HostName.Equals(MetadataName, StringComparison.OrdinalIgnoreCase) ||
                    entry.Comment is null || entry.Comment.Length > 79 || entry.Comment.Any(c => c < 32 || c > 255) ||
                    !guests.Add(entry.Name) || !hosts.Add(entry.HostName)) throw new InvalidDataException("Invalid or duplicate metadata entry.");
            }
            return metadata;
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or HostDosException)
        { throw new HostDosException(209, $"Invalid shared-folder metadata at {path}: {ex.Message}"); }
    }

    internal void SaveMetadata(string directory, Metadata metadata)
    {
        Writable();
        using var lease = Lease(directory);
        var destination = Path.Combine(directory, MetadataName);
        if (File.Exists(destination)) RejectLink(destination);
        var temporary = Path.Combine(directory, InternalPrefix + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { JsonSerializer.Serialize(stream, metadata, JsonOptions); stream.Flush(true); }
            File.Move(temporary, destination, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    internal List<Entry> Enumerate(string directory)
    {
        using var lease = Lease(directory);
        var metadata = ReadMetadata(directory);
        var entries = new List<Entry>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.EnumerateFileSystemEntries(directory))
        {
            var host = Path.GetFileName(path);
            var mapped = metadata.Entries.FirstOrDefault(e => e.HostName.Equals(host, StringComparison.OrdinalIgnoreCase));
            if (mapped is null && Internal(host)) continue;
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) continue;
            var entry = mapped ?? new Entry(host, host);
            // Host Unicode names outside the classic Amiga encoding cannot be
            // silently truncated or aliased on a read-only volume.
            if (entry.Name.Length > 107 || entry.Name.Any(c => c < 32 || c > 255 || c is ':' or '/' or '\\')) continue;
            if (!names.Add(entry.Name)) throw new HostDosException(203, "Ambiguous case-insensitive host filenames.");
            entries.Add(entry);
        }
        entries.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name));
        return entries;
    }

    internal string Resolve(string directory, string guestPath, bool allowMissing = false)
    {
        EnsureContained(directory);
        if (guestPath.Contains('\\')) throw new HostDosException(210, "Host paths are not Amiga paths.");
        var colon = guestPath.IndexOf(':');
        if (colon >= 0)
        {
            var volume = guestPath[..colon];
            if (volume.Length != 0 && !volume.Equals(Configuration.DeviceName, StringComparison.OrdinalIgnoreCase) &&
                !volume.Equals(Configuration.VolumeName, StringComparison.OrdinalIgnoreCase)) throw new HostDosException(218, "Wrong volume.");
            directory = Root; guestPath = guestPath[(colon + 1)..];
        }
        if (guestPath.Contains(':')) throw new HostDosException(210, "Invalid Amiga path.");
        if (guestPath.Length == 0) { using var lease = Lease(directory); return directory; }
        var parts = guestPath.Split('/');
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (part.Length == 0)
            {
                // A trailing slash is just a directory separator. Leading or
                // doubled slashes are the native Amiga parent convention.
                if (i != parts.Length - 1)
                {
                    if (directory.Equals(Root, StringComparison.OrdinalIgnoreCase)) throw new HostDosException(205, "Path leaves shared folder.");
                    directory = Path.GetDirectoryName(directory)!;
                }
                continue;
            }
            if (part is "." or "..") throw new HostDosException(210, "Use Amiga parent syntax.");
            ValidGuestName(part);
            var entry = Enumerate(directory).FirstOrDefault(e => e.Name.Equals(part, StringComparison.OrdinalIgnoreCase));
            if (entry is null)
            {
                if (!allowMissing || i != parts.Length - 1) throw new HostDosException(205, "Object not found.");
                var host = HostName(part);
                var result = Path.Combine(directory, host);
                if (File.Exists(result) || Directory.Exists(result)) throw new HostDosException(203, "Host filename collision.");
                return result;
            }
            directory = Path.Combine(directory, entry.HostName);
            EnsureContained(directory); RejectLink(directory);
        }
        return directory;
    }

    private static string HostName(string name)
    {
        return WindowsDeviceName(name) || Internal(name) || name.EndsWith('.') || name.EndsWith(' ') || name.IndexOfAny(['<', '>', '"', '|', '?', '*']) >= 0
            ? InternalPrefix + Convert.ToHexString(Encoding.Latin1.GetBytes(name)) : name;
    }

    private static bool WindowsDeviceName(string name)
    {
        var stem = name.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
        return stem is "CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$" ||
            stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && (char.IsAsciiDigit(stem[3]) || stem[3] is '¹' or '²' or '³');
    }

    internal Entry Describe(string path)
    {
        if (path.Equals(Root, StringComparison.OrdinalIgnoreCase)) return new(Configuration.VolumeName, "", 0, "");
        var host = Path.GetFileName(path);
        return ReadMetadata(Path.GetDirectoryName(path)!).Entries.FirstOrDefault(e => e.HostName.Equals(host, StringComparison.OrdinalIgnoreCase)) ?? new(host, host);
    }

    internal void Record(string path, string guestName, uint? protection = null, string? comment = null)
    {
        Writable();
        if (path.Equals(Root, StringComparison.OrdinalIgnoreCase)) throw new HostDosException(202, "Cannot change root metadata.");
        ValidGuestName(guestName);
        var directory = Path.GetDirectoryName(path)!;
        var metadata = ReadMetadata(directory);
        var host = Path.GetFileName(path);
        var old = metadata.Entries.FirstOrDefault(e => e.HostName.Equals(host, StringComparison.OrdinalIgnoreCase));
        metadata.Entries.RemoveAll(e => e.HostName.Equals(host, StringComparison.OrdinalIgnoreCase));
        metadata.Entries.Add(new(guestName, host, protection ?? old?.Protection ?? 0, comment ?? old?.Comment ?? ""));
        SaveMetadata(directory, metadata);
    }

    internal void Forget(string path)
    {
        var directory = Path.GetDirectoryName(path)!;
        var metadata = ReadMetadata(directory);
        if (metadata.Entries.RemoveAll(e => e.HostName.Equals(Path.GetFileName(path), StringComparison.OrdinalIgnoreCase)) != 0)
            SaveMetadata(directory, metadata);
    }

    internal void Protected(string path, uint bit, int error)
    {
        if ((Describe(path).Protection & bit) != 0) throw new HostDosException(error, "Amiga protection denies operation.");
    }

    internal FileStream Open(string path, FileMode mode, FileAccess access)
    {
        EnsureContained(path);
        if (!OperatingSystem.IsWindows())
        {
            if (File.Exists(path)) RejectLink(path);
            return new(path, mode, access, FileShare.ReadWrite, 65536, FileOptions.RandomAccess);
        }
        // Open the object itself, never a substituted reparse target. Keeping
        // delete sharing disabled also prevents replacement during this IO.
        var handle = OpenHandle(path, (access.HasFlag(FileAccess.Read) ? 0x80000000u : 0) |
            (access.HasFlag(FileAccess.Write) ? 0x40000000u : 0), mode == FileMode.CreateNew ? 1u : 3u);
        try { return new(handle, access, 65536); } catch { handle.Dispose(); throw; }
    }

    internal void DeleteObject(string path)
    {
        Writable(); EnsureContained(path);
        if (!OperatingSystem.IsWindows()) { RejectLink(path); if (Directory.Exists(path)) Directory.Delete(path); else File.Delete(path); return; }
        using var handle = OpenHandle(path, 0x10000u | 0x80u, 3);
        byte[] disposition = [1, 0, 0, 0];
        if (!SetFileInformationByHandle(handle, 4, disposition, disposition.Length)) throw HostError();
    }

    internal void RenameObject(string source, string destination)
    {
        Writable(); EnsureContained(source); EnsureContained(destination);
        if (!OperatingSystem.IsWindows()) { RejectLink(source); if (Directory.Exists(source)) Directory.Move(source, destination); else File.Move(source, destination); return; }
        using var handle = OpenHandle(source, 0x10000u | 0x80u, 3);
        var name = Encoding.Unicode.GetBytes(Path.GetFullPath(destination));
        var offset = IntPtr.Size == 8 ? 20 : 12;
        var information = new byte[offset + name.Length];
        BitConverter.GetBytes(name.Length).CopyTo(information, offset - 4); name.CopyTo(information, offset);
        if (!SetFileInformationByHandle(handle, 3, information, information.Length)) throw HostError();
    }

    internal void SetDate(string path, DateTime date)
    {
        Writable(); EnsureContained(path);
        if (!OperatingSystem.IsWindows()) { RejectLink(path); File.SetLastWriteTime(path, date); return; }
        using var handle = OpenHandle(path, 0x100u | 0x80u, 3);
        File.SetLastWriteTime(handle, date);
    }

    private SafeFileHandle OpenHandle(string path, uint access, uint disposition)
    {
        var handle = CreateFileW(path, access, 3, IntPtr.Zero, disposition, 0x02200000, IntPtr.Zero);
        try
        {
            if (handle.IsInvalid) throw HostError();
            if (!GetFileInformationByHandleEx(handle, 9, out var attributes, 8)) throw HostError();
            if ((attributes.Attributes & 0x400) != 0) throw new HostDosException(205, "Shared folders do not follow links.");
            ValidateHandle(handle); return handle;
        }
        catch { handle.Dispose(); throw; }
    }

    private static IOException HostError()
    {
        var error = Marshal.GetLastWin32Error();
        return new IOException(new System.ComponentModel.Win32Exception(error).Message, unchecked((int)(0x80070000u | (uint)error)));
    }

    internal IDisposable Lease(string directory)
    {
        EnsureContained(directory);
        var handles = new List<SafeFileHandle>();
        try
        {
            var current = Root;
            var paths = new List<string> { Root };
            var relative = Path.GetRelativePath(Root, directory);
            if (relative != ".") foreach (var component in relative.Split(Path.DirectorySeparatorChar))
            { current = Path.Combine(current, component); paths.Add(current); }
            foreach (var path in paths)
            {
                RejectLink(path);
                if (OperatingSystem.IsWindows())
                {
                    var handle = CreateFileW(path, 0x80, 3, IntPtr.Zero, 3, 0x02200000, IntPtr.Zero);
                    if (handle.IsInvalid) { var error = HostError(); handle.Dispose(); throw error; }
                    handles.Add(handle); RejectLink(path); ValidateHandle(handle);
                }
                else if (!Directory.Exists(path)) throw new DirectoryNotFoundException(path);
            }
            return new HandleLease(handles);
        }
        catch { foreach (var handle in handles) handle.Dispose(); throw; }
    }

    internal void ValidateHandle(SafeFileHandle handle)
    {
        if (!OperatingSystem.IsWindows()) return;
        var text = new StringBuilder(32768);
        var length = GetFinalPathNameByHandleW(handle, text, (uint)text.Capacity, 0);
        if (length == 0 || length >= text.Capacity) throw new IOException("Cannot validate shared-folder handle.");
        var path = text.ToString();
        if (path.StartsWith("\\\\?\\UNC\\", StringComparison.OrdinalIgnoreCase)) path = "\\\\" + path[8..];
        else if (path.StartsWith("\\\\?\\")) path = path[4..];
        EnsureContained(path);
    }

    internal void EnsureContained(string path)
    {
        var full = Path.GetFullPath(path);
        if (!full.Equals(Root, StringComparison.OrdinalIgnoreCase) &&
            !full.StartsWith(Root.EndsWith(Path.DirectorySeparatorChar) ? Root : Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new HostDosException(205, "Path leaves shared folder.");
    }
    internal static void RejectLink(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new HostDosException(205, "Shared folders do not follow links.");
    }
    private sealed class HandleLease(List<SafeFileHandle> handles) : IDisposable
    { public void Dispose() { foreach (var handle in handles) handle.Dispose(); } }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandleW(SafeFileHandle file, StringBuilder path, uint length, uint flags);
    [StructLayout(LayoutKind.Sequential)] private struct AttributeTag { internal uint Attributes, Tag; }
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle file, int informationClass, out AttributeTag information, uint size);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle file, int informationClass, byte[] information, int size);
}
