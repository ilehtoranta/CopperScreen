using System.Buffers;
using System.Buffers.Binary;
using CopperDisk;
using CopperStart.FileSystem;

namespace CopperScreen;

// Image storage stays in CopperDisk; all filesystem operations execute in the packaged guest service.
internal sealed partial class CopperScreenDiskImage : IDisposable
{
    private AmigaSectorImage _image;
    private readonly string _path;
    private int _references = 1;
    internal IReadOnlyList<DiskImageVolume> Volumes { get; private set; }
    private CopperScreenDiskImage(AmigaSectorImage image, string path)
    {
        _image = image;
        _path = path;
        Volumes = image.Volumes.Select(v => new DiskImageVolume(this, v)).ToArray();
    }
    internal static CopperScreenDiskImage Open(string path) => new(AmigaSectorImage.Open(path), Path.GetFullPath(path));
    internal bool IsReadOnly => (File.GetAttributes(_path) & FileAttributes.ReadOnly) != 0;
    internal string FilePath => _path;
    // Each pane owns one reference. Volumes keep separate guest sessions, while
    // the physical image has one protected storage handle and publication owner.
    internal CopperScreenDiskImage Retain()
    {
        ObjectDisposedException.ThrowIf(_references == 0, this);
        _references = checked(_references + 1);
        return this;
    }
    internal void RequireCurrent(DiskImageVolume volume)
    {
        ObjectDisposedException.ThrowIf(_references == 0, this);
        if (!Volumes.Contains(volume)) throw new InvalidOperationException("Reselect the volume after the image changed.");
    }
    private void CloseVolumes()
    {
        foreach (var volume in Volumes) volume.Close();
    }
    public void Dispose()
    {
        if (_references == 0 || --_references != 0) return;
        try { CloseVolumes(); }
        finally { _image.Dispose(); }
    }
}

internal sealed class DiskImageEntry(DiskImageVolume volume, FileSystemEntry entry, string path)
{
    internal DiskImageVolume Volume { get; } = volume;
    internal FileSystemEntry Native { get; } = entry;
    internal string Path { get; } = path;
    internal string Name => Native.Name;
    internal bool IsDirectory => Native.Kind == FileSystemEntryKind.Directory;
    internal bool IsLink => Native.Kind == FileSystemEntryKind.Link;
    internal long Size => checked((long)Native.Length);
    internal DateTime? Modified => Native.Modified;
    internal uint Protection => Native.Protection;
    internal string Comment => Native.Comment;
}

internal sealed class DiskImageVolume(CopperScreenDiskImage owner, AmigaImageVolume range)
{
    internal CopperScreenDiskImage Owner => owner;
    internal AmigaImageVolume Range => range;
    private FileSystemSession? _session;
    private DiskImageEntry? _root;
    private uint? _detectedType;
    private string? _mountError;
    internal string DeviceName => range.DeviceName;
    internal uint BlockSize => range.BlockSize;
    internal string? UnsupportedReason => range.UnsupportedReason ?? _mountError ??
        (range.DosType is { } type && !Supported(type) ? "Only OFS/FFS (DOS0–DOS5), SFS and SFS2 are supported." : null);
    internal bool IsFaulted => _session?.IsFaulted == true;
    internal bool IsMounted => _session != null && !_session.IsFaulted;
    internal string? ImportUnavailableReason => !IsMounted ? "Select a mounted volume first." :
        owner.IsReadOnly ? "The image file is read-only." :
        (_detectedType is >= 0x444F5300 and <= 0x444F5305 or 0x53465300 or 0x53465302 ? null : "Import supports DOS0–DOS5, SFS and SFS2.");
    internal DiskImageEntry Root { get { if (_root == null) Mount(); return _root!; } }
    internal string DisplayName => _root?.Name ?? DeviceName;
    internal string FileSystemName => (_detectedType ?? range.DosType) switch
    {
        >= 0x444F5300 and <= 0x444F5305 => $"{(((_detectedType ?? range.DosType) & 1) == 0 ? "OFS" : "FFS")} (DOS{(_detectedType ?? range.DosType) & 255})",
        0x53465300 => "SFS", 0x53465302 => "SFS2", _ => "Not mounted"
    };
    private static bool Supported(uint type) => type is >= 0x444F5300 and <= 0x444F5305 or 0x53465300 or 0x53465302;

    internal void Mount(CancellationToken cancellationToken = default)
    {
        owner.RequireCurrent(this);
        if (_session is { IsFaulted: false }) return;
        Close(); _mountError = null;
        if (UnsupportedReason is { } unavailable) throw new NotSupportedException(unavailable);
        var type = range.DosType;
        // Raw images have no DOS environment vector. Only the boot identifier is inspected for mount traits.
        if (type == null)
        {
            Span<byte> bootId = stackalloc byte[4]; range.Read(0, bootId); type = BinaryPrimitives.ReadUInt32BigEndian(bootId);
            if (!Supported(type.Value)) { _mountError = "Only OFS/FFS (DOS0–DOS5), SFS and SFS2 are supported. Custom game disks and other filesystems are unavailable."; throw new NotSupportedException(_mountError); }
        }
        var reserved = range.DosType == null && (type is 0x53465300 or 0x53465302) ? 1u : range.ReservedBlocks;
        var geometry = new FileSystemGeometry(checked((uint)(range.Length / range.BlockSize)), range.BlockSize, reserved);
        FileSystemSession? session = null;
        try
        {
            session = new(new Source(range), geometry, cancellationToken: cancellationToken);
            if (range.DosType is { } declared && session.Volume.DosType != declared)
                throw new InvalidDataException("The filesystem type does not match the RDB partition's declared DOS type.");
            _session = session; _detectedType = session.Volume.DosType; _root = new(this, session.Root, "/");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or ArgumentException)
        { session?.Dispose(); _mountError = ex.Message; throw; }
    }

    internal IReadOnlyList<DiskImageEntry> ReadDirectory(DiskImageEntry directory, CancellationToken cancellationToken = default)
    {
        Check(directory);
        var names = new HashSet<string>(StringComparer.Ordinal);
        var entries = new List<DiskImageEntry>();
        foreach (var entry in _session!.EnumerateDirectory(directory.Native, cancellationToken))
        {
            // Reject ambiguous repeated rows before displaying them or starting extraction.
            if (!names.Add(entry.Name)) throw new InvalidDataException("The filesystem returned duplicate directory entries.");
            entries.Add(new(this, entry, directory.Path.TrimEnd('/') + "/" + entry.Name));
        }
        return entries.OrderByDescending(e => e.IsDirectory).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }
    internal void CopyFileTo(DiskImageEntry file, Stream destination, CancellationToken cancellationToken = default)
    {
        Check(file); ArgumentNullException.ThrowIfNull(destination);
        using var input = _session!.OpenRead(file.Native, cancellationToken: cancellationToken);
        var buffer = ArrayPool<byte>.Shared.Rent(65536);
        try
        {
            while (true)
            {
                var read = input.ReadAsync(buffer.AsMemory(0, 65536), cancellationToken).GetAwaiter().GetResult();
                if (read == 0) break;
                destination.Write(buffer, 0, read);
            }
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }
    internal Stream OpenRead(DiskImageEntry file, CancellationToken cancellationToken)
    {
        Check(file);
        return _session!.OpenRead(file.Native, cancellationToken: cancellationToken);
    }
    private void Check(DiskImageEntry entry)
    {
        if (!ReferenceEquals(entry.Volume, this)) throw new ArgumentException("The entry belongs to another volume.", nameof(entry));
        if (_session == null || _session.IsFaulted) throw new InvalidOperationException("Reselect the volume to start a fresh filesystem session.");
    }
    internal void Close()
    {
        try { _session?.Dispose(); }
        finally { _session = null; _root = null; }
    }
    private sealed class Source(AmigaImageVolume volume) : IReadOnlyBlockSource
    {
        public long Length => volume.Length;
        public void Read(long offset, Span<byte> destination) => volume.Read(offset, destination);
    }
}
