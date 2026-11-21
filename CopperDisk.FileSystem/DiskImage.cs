using System.Buffers;
using System.Buffers.Binary;
using CopperDisk;
using CopperStart.FileSystem;

namespace CopperDisk.FileSystem;

// Image storage stays in CopperDisk; all filesystem operations execute in the packaged guest service.
public sealed partial class DiskImageSession : IDisposable
{
    private AmigaSectorImage _image;
    private readonly string _path;
    private int _references = 1;
    private int _readers;
    public Guid Generation { get; private set; } = Guid.NewGuid();
    internal void RequireNoReaders() { if (_readers != 0) throw new IOException("An image read stream is active. Finish or cancel the transfer before saving."); }
    internal Stream TrackRead(Stream stream) { _readers++; return new TrackedRead(stream, () => _readers--); }
    public IReadOnlyList<DiskVolume> Volumes { get; private set; }
    private DiskImageSession(AmigaSectorImage image, string path)
    {
        _image = image;
        _path = path;
        Volumes = image.Volumes.Select(v => new DiskVolume(this, v)).ToArray();
    }
    public static DiskImageSession Open(string path) => new(OpenStorage(path), Path.GetFullPath(path));
    private static AmigaSectorImage OpenStorage(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".ipf" => AmigaSectorImage.OpenIpf(path),
        ".dms" => AmigaSectorImage.OpenDms(path),
        _ => AmigaSectorImage.Open(path)
    };
    public string? WriteUnavailableReason => _image.WriteUnavailableReason ??
        ((File.GetAttributes(_path) & FileAttributes.ReadOnly) != 0 ? "The image file is read-only." : null);
    public bool IsReadOnly => WriteUnavailableReason != null;
    public string FilePath => _path;
    // Each pane owns one reference. Volumes keep separate guest sessions, while
    // the physical image has one protected storage handle and publication owner.
    public DiskImageSession Retain()
    {
        ObjectDisposedException.ThrowIf(_references == 0, this);
        _references = checked(_references + 1);
        return this;
    }
    public void RequireCurrent(DiskVolume volume)
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

public sealed class DiskEntry
{
    public DiskEntryId Id => new(Generation, Volume.Id, Path);
    internal DiskEntry(DiskVolume volume, FileSystemEntry entry, string path)
        => (Volume, Native, Path, Generation) = (volume, entry, path, volume.Owner.Generation);
    public Guid Generation { get; }
    public DiskVolume Volume { get; }
    internal FileSystemEntry Native { get; }
    public string Path { get; }
    public string Name => Native.Name;
    public bool IsDirectory => Native.Kind == FileSystemEntryKind.Directory;
    public bool IsLink => Native.Kind == FileSystemEntryKind.Link;
    public long Size => checked((long)Native.Length);
    public DateTime? Modified => Native.Modified;
    public uint Protection => Native.Protection;
    public string Comment => Native.Comment;
}

public sealed class DiskVolume(DiskImageSession owner, AmigaImageVolume range)
{
    public Guid Id { get; } = Guid.NewGuid();
    public DiskVolumeCapabilities Capabilities
    {
        get
        {
            var operations = IsMounted ? DiskVolumeOperations.Browse | DiskVolumeOperations.Read : DiskVolumeOperations.None;
            if (ImportUnavailableReason == null) operations |= DiskVolumeOperations.Import | DiskVolumeOperations.NewDirectory |
                DiskVolumeOperations.Rename | DiskVolumeOperations.Delete | DiskVolumeOperations.Move | DiskVolumeOperations.Protect;
            return new(operations, IsMounted ? null : UnsupportedReason ?? "Mount the volume first.", ImportUnavailableReason);
        }
    }
    public DiskImageSession Owner => owner;
    public AmigaImageVolume Range => range;
    private FileSystemSession? _session;
    private DiskEntry? _root;
    private uint? _detectedType;
    private string? _mountError;
    public string DeviceName => range.DeviceName;
    public uint BlockSize => range.BlockSize;
    public string? UnsupportedReason => range.UnsupportedReason ?? _mountError ??
        (range.DosType is { } type && !Supported(type) ? "Only OFS/FFS (DOS0–DOS5), SFS and SFS2 are supported." : null);
    public bool IsFaulted => _session?.IsFaulted == true;
    public bool IsMounted => _session != null && !_session.IsFaulted;
    public string? ImportUnavailableReason => !IsMounted ? "Select a mounted volume first." :
        owner.WriteUnavailableReason is { } readOnly ? readOnly :
        (_detectedType is >= 0x444F5300 and <= 0x444F5305 or 0x53465300 or 0x53465302 ? null : "Import supports DOS0–DOS5, SFS and SFS2.");
    public DiskEntry Root { get { if (_root == null) Mount(); return _root!; } }
    public string DisplayName => _root?.Name ?? DeviceName;
    public string FileSystemName => (_detectedType ?? range.DosType) switch
    {
        >= 0x444F5300 and <= 0x444F5305 => $"{(((_detectedType ?? range.DosType) & 1) == 0 ? "OFS" : "FFS")} (DOS{(_detectedType ?? range.DosType) & 255})",
        0x53465300 => "SFS", 0x53465302 => "SFS2", _ => "Not mounted"
    };
    private static bool Supported(uint type) => type is >= 0x444F5300 and <= 0x444F5305 or 0x53465300 or 0x53465302;

    public void Mount(CancellationToken cancellationToken = default)
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

    public IReadOnlyList<DiskEntry> ReadDirectory(DiskEntry directory, CancellationToken cancellationToken = default)
    {
        Check(directory);
        var names = new HashSet<string>(StringComparer.Ordinal);
        var entries = new List<DiskEntry>();
        foreach (var entry in _session!.EnumerateDirectory(directory.Native, cancellationToken))
        {
            // Reject ambiguous repeated rows before displaying them or starting extraction.
            if (!names.Add(entry.Name)) throw new InvalidDataException("The filesystem returned duplicate directory entries.");
            entries.Add(new(this, entry, directory.Path.TrimEnd('/') + "/" + entry.Name));
        }
        return entries.OrderByDescending(e => e.IsDirectory).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }
    public void CopyFileTo(DiskEntry file, Stream destination, CancellationToken cancellationToken = default)
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
    public Stream OpenRead(DiskEntry file, CancellationToken cancellationToken = default, long offset = 0)
    {
        Check(file);
        if (offset < 0 || offset > file.Size) throw new ArgumentOutOfRangeException(nameof(offset));
        return owner.TrackRead(_session!.OpenRead(file.Native, (ulong)offset, cancellationToken));
    }
    private void Check(DiskEntry entry)
    {
        owner.RequireCurrent(this);
        if (!ReferenceEquals(entry.Volume, this)) throw new ArgumentException("The entry belongs to another volume.", nameof(entry));
        if (entry.Generation != owner.Generation) throw new InvalidOperationException("The entry is stale; refresh the directory.");
        if (_session == null || _session.IsFaulted) throw new InvalidOperationException("Reselect the volume to start a fresh filesystem session.");
    }
    public void Close()
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
