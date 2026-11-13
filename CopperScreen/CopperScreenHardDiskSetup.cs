using CopperDisk;

namespace CopperScreen;

// Offline authoring only; none of these services participate in machine execution.
internal static class CopperScreenHardDiskSetup
{
    internal enum FileSystem { Ffs, Sfs, Sfs2, Cpx }
    internal static readonly string[] FileSystemNames = ["FFS", "SFS", "SFS2", "CPX (experimental)"];
    internal static uint DosType(FileSystem fs) => fs switch
    {
        FileSystem.Ffs => 0x444F5301, FileSystem.Sfs => 0x53465300,
        FileSystem.Sfs2 => 0x53465302, FileSystem.Cpx => 0x43505834,
        _ => throw new ArgumentOutOfRangeException(nameof(fs))
    };

    internal static AmigaRdbSetupLayout Inspect(string path)
    {
        using var disk = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return AmigaRdbSetup.ReadLayout(disk);
    }

    internal static (uint First, uint Last)? FirstFreeRange(AmigaRdbSetupLayout layout)
    {
        var first = layout.FirstCylinder;
        foreach (var p in layout.Partitions.OrderBy(p => p.FirstCylinder))
        {
            if (first < p.FirstCylinder) return (first, p.FirstCylinder - 1);
            if (p.LastCylinder == uint.MaxValue) return null;
            first = p.LastCylinder + 1;
        }
        return first <= layout.LastCylinder ? (first, layout.LastCylinder) : null;
    }

    // Always publishes a new file. Failure or cancellation never changes the source.
    internal static void Create(string destination, long capacity, string? source, string device,
        uint? first, uint? last, FileSystem fs, string label, bool bootable, CancellationToken cancellation = default)
    {
        _ = DosType(fs);
        if (string.IsNullOrWhiteSpace(label) || label.Length > 30 || label.Any(c => c > 255 || c is '\0' or ':' or '/'))
            throw new ArgumentException("Use a volume label of 1–30 Latin-1 characters without colons or slashes.");
        destination = Path.GetFullPath(destination);
        CopperScreenAtomicFile.ValidateDestination(destination);
        if (Path.Exists(destination)) throw new IOException("Choose a new filename. Existing files are preserved.");
        using var input = source == null ? null : new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input != null) capacity = input.Length;
        else if (capacity < 8L * 1024 * 1024 || capacity % 32768 != 0)
            throw new ArgumentException("Capacity must be at least 8 MiB and contain whole 32 KiB cylinders.");
        // Reject a damaged/unsupported source before allocating its copy.
        if (input != null) _ = AmigaRdbSetup.ReadLayout(input);
        var temporary = Path.Combine(Path.GetDirectoryName(destination)!, ".disk-setup-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                AmigaSparseFile.TryEnable(output); output.SetLength(capacity);
                if (input != null)
                {
                    input.Position = 0; var buffer = new byte[1024 * 1024];
                    while (input.Position < input.Length)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        var bytes = (int)Math.Min(buffer.Length, input.Length - input.Position);
                        input.ReadExactly(buffer.AsSpan(0, bytes));
                        // Retain sparse zero ranges while preserving every source byte.
                        if (buffer.AsSpan(0, bytes).ContainsAnyExcept((byte)0)) output.Write(buffer, 0, bytes);
                        else output.Position += bytes;
                    }
                }
                var layout = input == null ? AmigaRdbSetup.Initialize(output) : AmigaRdbSetup.ReadLayout(output);
                var p = AmigaRdbSetup.AddPartition(output, device, first ?? layout.FirstCylinder, last ?? layout.LastCylinder, DosType(fs), bootable);
                var start = checked((long)p.LowCylinder!.Value * layout.Heads * layout.SectorsPerTrack * 512);
                var length = checked(((long)p.HighCylinder!.Value - p.LowCylinder.Value + 1) * layout.Heads * layout.SectorsPerTrack * 512);
                cancellation.ThrowIfCancellationRequested();
                CopperScreenFileSystemFormatter.Format(output, start, length, fs, label, cancellation);
                _ = AmigaRdbSetup.ReadLayout(output);
                output.Flush(flushToDisk: true);
            }
            cancellation.ThrowIfCancellationRequested();
            CopperScreenAtomicFile.ValidateDestination(destination);
            File.Move(temporary, destination);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
