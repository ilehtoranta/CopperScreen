using System.Security.Cryptography;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace CopperScreen;

// Snapshot the exact host tree before copying. Removal never recursively deletes
// an unexamined directory or follows links, and never clears read-only attributes.
internal sealed class CopperScreenHostMove : IDisposable
{
    private sealed record Item(string Path, bool Directory, long Length, DateTime Modified, byte[] Hash);
    private readonly List<Item> _items = new();
    private readonly List<FileStream> _locks = new();

    internal CopperScreenHostMove(IEnumerable<string> paths, CancellationToken ct)
    {
        try
        {
            var pending = new Stack<string>(paths);
            while (pending.TryPop(out var path))
            {
                ct.ThrowIfCancellationRequested();
                CopperScreenDiskImage.RequireOrdinarySource(path);
                if ((File.GetAttributes(path) & FileAttributes.ReadOnly) != 0) throw new IOException("A selected host entry is read-only: " + path);
                var directory = Directory.Exists(path);
                var input = directory ? null : new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (input != null) _locks.Add(input);
                _items.Add(new(path, directory, input?.Length ?? 0, File.GetLastWriteTimeUtc(path), input == null ? [] : Hash(input, ct)));
                if (directory) foreach (var child in System.IO.Directory.EnumerateFileSystemEntries(path)) pending.Push(child);
            }
        }
        catch { Dispose(); throw; }
    }

    internal void Remove(CancellationToken ct)
    {
        var paths = _items.Select(i => i.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        // Validate the complete snapshot before deleting anything. New children are
        // retained even if one appears after this check: directories are deleted empty only.
        foreach (var item in _items)
        {
            ct.ThrowIfCancellationRequested();
            CopperScreenDiskImage.RequireOrdinarySource(item.Path);
            if (Directory.Exists(item.Path) != item.Directory || File.GetLastWriteTimeUtc(item.Path) != item.Modified ||
                (File.GetAttributes(item.Path) & FileAttributes.ReadOnly) != 0) throw new IOException("The host source changed: " + item.Path);
            if (item.Directory)
            {
                if (Directory.EnumerateFileSystemEntries(item.Path).Any(p => !paths.Contains(p))) throw new IOException("The host source gained entries: " + item.Path);
            }
            else
            {
                using var input = new FileStream(item.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (input.Length != item.Length || !Hash(input, ct).SequenceEqual(item.Hash)) throw new IOException("The host source changed: " + item.Path);
            }
        }
        // Readers kept source bytes immutable throughout the copy. Release our
        // readers before reacquiring each file with checked delete access.
        Dispose();
        foreach (var item in _items.AsEnumerable().Reverse())
        {
            ct.ThrowIfCancellationRequested(); CopperScreenDiskImage.RequireOrdinarySource(item.Path);
            if (item.Directory) Directory.Delete(item.Path, false); else RemoveFile(item, ct);
        }
    }

    public void Dispose() { foreach (var input in _locks) input.Dispose(); _locks.Clear(); }

    private static void RemoveFile(Item item, CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows())
        {
            using (var input = new FileStream(item.Path, FileMode.Open, FileAccess.Read, FileShare.Read)) VerifyFile(input, item, ct);
            File.Delete(item.Path); return;
        }
        // Hold read+delete access without sharing writes/deletes. Verify and mark
        // the very same file handle for deletion, closing the path-replacement race.
        using var handle = CreateFile(item.Path, 0x80010000, 1, IntPtr.Zero, 3, 0x08200000, IntPtr.Zero);
        if (handle.IsInvalid) throw new IOException("Cannot remove host source: " + item.Path, new Win32Exception(Marshal.GetLastWin32Error()));
        using var stream = new FileStream(handle, FileAccess.Read);
        CopperScreenDiskImage.RequireOrdinarySource(item.Path);
        if ((File.GetAttributes(item.Path) & FileAttributes.ReadOnly) != 0) throw new IOException("Host source is read-only: " + item.Path);
        VerifyFile(stream, item, ct); ct.ThrowIfCancellationRequested();
        var delete = true;
        if (!SetFileInformationByHandle(handle, 4, ref delete, 4))
            throw new IOException("Cannot remove host source: " + item.Path, new Win32Exception(Marshal.GetLastWin32Error()));
    }

    private static void VerifyFile(Stream input, Item item, CancellationToken ct)
    {
        if (input.Length != item.Length || !Hash(input, ct).SequenceEqual(item.Hash)) throw new IOException("The host source changed: " + item.Path);
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int informationClass, [MarshalAs(UnmanagedType.Bool)] ref bool information, uint size);

    private static byte[] Hash(Stream stream, CancellationToken ct)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[65536]; int count;
        while ((count = stream.Read(buffer)) != 0) { ct.ThrowIfCancellationRequested(); hash.AppendData(buffer, 0, count); }
        return hash.GetHashAndReset();
    }
}
