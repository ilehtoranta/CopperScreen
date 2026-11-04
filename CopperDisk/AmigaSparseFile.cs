using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace CopperDisk;

/// <summary>Host sparse-image operations. Callers own and serialize the backing file.</summary>
public static class AmigaSparseFile
{
    // A conservative common alignment, verified by the disposable-file probe.
    // Discard rounds inward; it never modifies bytes outside the supplied range.
    private const int Alignment = 65536;

    /// <summary>Enable sparse operations without changing any existing file bytes.
    /// Unsupported volumes return false. Capability is tested only on a disposable sibling.</summary>
    public static bool TryEnable(FileStream file)
    {
        if (!file.CanWrite || !(OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())) return false;
        try
        {
            var backingPath = new FileInfo(file.Name).ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? Path.GetFullPath(file.Name);
            var probePath = Path.Combine(Path.GetDirectoryName(backingPath)!, ".copper-sparse-" + Guid.NewGuid().ToString("N"));
            using (var probe = new FileStream(probePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
                Alignment, FileOptions.DeleteOnClose))
            {
                if (!MarkSparse(probe.SafeFileHandle)) return false;
                var data = new byte[Alignment * 3]; Array.Fill(data, (byte)0xA5);
                probe.Write(data); probe.Flush(flushToDisk: true);
                var before = AllocatedBytes(probe.SafeFileHandle);
                Punch(probe.SafeFileHandle, Alignment, Alignment);
                var after = AllocatedBytes(probe.SafeFileHandle);
                if (before is { } allocated && after is { } remaining && remaining >= allocated) return false;
                if (probe.Length != data.Length) return false;
                probe.Position = 0; probe.ReadExactly(data);
                for (var i = 0; i < data.Length; i++)
                    if (data[i] != (i >= Alignment && i < Alignment * 2 ? 0 : 0xA5)) return false;
            }
            return MarkSparse(file.SafeFileHandle);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception or EntryPointNotFoundException or DllNotFoundException)
        { return false; }
    }

    /// <summary>Discard complete aligned allocation units inside a free byte range.
    /// Partial boundary units are retained; logical length never changes.</summary>
    public static void Discard(FileStream file, long offset, int length)
    {
        if (!file.CanWrite) throw new UnauthorizedAccessException("The image is read-only.");
        if (offset < 0 || length <= 0 || offset > file.Length || length > file.Length - offset)
            throw new ArgumentOutOfRangeException(nameof(length));
        var end = offset + length;
        var start = checked(offset + (Alignment - offset % Alignment) % Alignment);
        end -= end % Alignment;
        if (end <= start) return;
        file.Flush(); // Publish buffered writes before the OS changes their backing ranges.
        Punch(file.SafeFileHandle, start, end - start);
        // Reset FileStream's read buffer after an operation made through the native handle.
        file.Position = file.Position;
    }

    /// <summary>Write a logical byte stream, leaving zero runs unallocated when enabled.</summary>
    public static void Write(FileStream file, ReadOnlySpan<byte> bytes, bool sparse)
    {
        // Only use this on a newly created output: seeking must never retain old bytes.
        if (sparse && !bytes.ContainsAnyExcept((byte)0)) file.Seek(bytes.Length, SeekOrigin.Current);
        else file.Write(bytes);
    }

    /// <summary>Copy into an empty output, retaining its complete logical length.</summary>
    public static bool Copy(Stream source, FileStream target)
    {
        if (target.Length != 0 || target.Position != 0) throw new ArgumentException("Sparse copy requires an empty output.", nameof(target));
        var sparse = TryEnable(target);
        var buffer = new byte[65536];
        int count;
        while ((count = source.Read(buffer)) != 0) Write(target, buffer.AsSpan(0, count), sparse);
        target.SetLength(target.Position);
        return sparse;
    }

    /// <summary>Actual host allocation, or null on an unsupported OS/ABI or failed query.</summary>
    public static long? GetAllocatedBytes(string path)
    {
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return AllocatedBytes(file.SafeFileHandle);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OverflowException or EntryPointNotFoundException or DllNotFoundException) { return null; }
    }

    private static long? AllocatedBytes(SafeFileHandle handle)
    {
        if (OperatingSystem.IsWindows())
            return GetFileInformationByHandleEx(handle, 8, out var info, 16) ? info.CompressedFileSize : null;
        if (RuntimeInformation.ProcessArchitecture is not (Architecture.X64 or Architecture.Arm64)) return null;
        var data = new byte[256];
        var ok = OperatingSystem.IsMacOS()
            ? (RuntimeInformation.ProcessArchitecture == Architecture.X64 ? DarwinStat(handle, data) : Fstat(handle, data))
            : OperatingSystem.IsLinux() ? Fstat(handle, data) : -1;
        return ok == 0 ? checked(BitConverter.ToInt64(data, OperatingSystem.IsMacOS() ? 104 : 64) * 512) : null;
    }

    private static bool MarkSparse(SafeFileHandle handle) => !OperatingSystem.IsWindows() ||
        DeviceIoControl(handle, 0x900C4, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero);

    private static void Punch(SafeFileHandle handle, long offset, long length)
    {
        bool success;
        if (OperatingSystem.IsWindows())
        {
            var range = new ZeroRange { Start = offset, End = checked(offset + length) };
            success = ZeroData(handle, 0x980C8, ref range, 16, IntPtr.Zero, 0, out _, IntPtr.Zero);
        }
        else if (OperatingSystem.IsLinux()) success = Fallocate(handle, 3, offset, length) == 0;
        else if (OperatingSystem.IsMacOS())
        {
            var range = new PunchRange { Offset = offset, Length = length };
            // Darwin ARM64 passes variadic arguments on the stack. Fill x2..x7
            // so this fixed P/Invoke places the pointer in the first stack slot.
            success = (RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                ? FcntlArm64(handle, 99, 0, 0, 0, 0, 0, 0, ref range)
                : Fcntl(handle, 99, ref range)) == 0;
        }
        else throw new PlatformNotSupportedException();
        if (!success) throw new IOException("Sparse image deallocation failed.", new Win32Exception(Marshal.GetLastPInvokeError()));
    }

    [StructLayout(LayoutKind.Sequential)] private struct ZeroRange { public long Start, End; }
    [StructLayout(LayoutKind.Sequential)] private struct PunchRange { public uint Flags, Reserved; public long Offset, Length; }
    [StructLayout(LayoutKind.Sequential)] private struct CompressionInfo
    { public long CompressedFileSize; public ushort Format; public byte UnitShift, ChunkShift, ClusterShift, Reserved0, Reserved1, Reserved2; }
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle handle, uint code, IntPtr input, uint inputBytes, IntPtr output, uint outputBytes, out uint returned, IntPtr overlapped);
    [DllImport("kernel32.dll", EntryPoint = "DeviceIoControl", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ZeroData(SafeFileHandle handle, uint code, ref ZeroRange input, uint inputBytes, IntPtr output, uint outputBytes, out uint returned, IntPtr overlapped);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle handle, int infoClass, out CompressionInfo info, uint bytes);
    [DllImport("libc", EntryPoint = "fallocate", SetLastError = true)]
    private static extern int Fallocate(SafeFileHandle handle, int mode, long offset, long length);
    [DllImport("libc", EntryPoint = "fcntl", SetLastError = true)]
    private static extern int Fcntl(SafeFileHandle handle, int command, ref PunchRange range);
    [DllImport("libc", EntryPoint = "fcntl", SetLastError = true)]
    private static extern int FcntlArm64(SafeFileHandle handle, int command,
        nint x2, nint x3, nint x4, nint x5, nint x6, nint x7, ref PunchRange range);
    [DllImport("libc", EntryPoint = "fstat", SetLastError = true)]
    private static extern int Fstat(SafeFileHandle handle, [Out] byte[] data);
    [DllImport("libc", EntryPoint = "fstat$INODE64", SetLastError = true)]
    private static extern int DarwinStat(SafeFileHandle handle, [Out] byte[] data);
}
