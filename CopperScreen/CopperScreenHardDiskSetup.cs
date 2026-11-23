using CopperDisk;
using System.Globalization;

namespace CopperScreen;

// Offline authoring only; none of these services participate in machine execution.
internal static class CopperScreenHardDiskSetup
{
    internal enum FileSystem { Ffs, Sfs, Sfs2, Cpx }
    internal enum ImageFormat { RawHdf, DynamicVhd }
    internal static readonly string[] FileSystemNames = ["FFS", "SFS", "SFS2", "CopperNova (CNFS)"];
    internal static readonly uint[] BlockSizes = [512, 1024, 2048, 4096];
    internal const long NewCylinderBytes = 2 * 32 * 512;
    internal readonly record struct PartitionRange(uint First, uint Last, long Bytes);

    internal static long ParseSize(string? text, string unit)
    {
        const NumberStyles style = NumberStyles.AllowDecimalPoint;
        if ((!decimal.TryParse(text?.Trim(), style, CultureInfo.CurrentCulture, out var value) &&
             !decimal.TryParse(text?.Trim(), style, CultureInfo.InvariantCulture, out value)) || value <= 0)
            throw new ArgumentException("Enter a positive size in MiB or GiB.");
        var multiplier = unit switch { "MiB" => 1048576m, "GiB" => 1073741824m, _ => throw new ArgumentException("Choose MiB or GiB.") };
        // Device sectors remain 512 bytes; show the resulting aligned size in the dialog.
        return checked((long)decimal.Floor(value * multiplier / 512) * 512);
    }

    internal static long AlignNewCapacity(long requested)
    {
        var capacity = requested / NewCylinderBytes * NewCylinderBytes;
        if (capacity < 8 * 1048576L || capacity / NewCylinderBytes > uint.MaxValue)
            throw new ArgumentException("Formatted images need at least 8 MiB and a capacity within the RDB cylinder range.");
        return capacity;
    }

    internal static AmigaRdbSetupLayout NewLayout(long capacity) =>
        new(2, 32, 1, checked((uint)(AlignNewCapacity(capacity) / NewCylinderBytes - 1)), []);

    internal static PartitionRange PlanPartition(AmigaRdbSetupLayout layout, long? requestedBytes,
        uint blockSize = 512, uint? first = null, uint? last = null)
    {
        if (!BlockSizes.Contains(blockSize)) throw new ArgumentException("Choose a filesystem block size of 512, 1024, 2048 or 4096 bytes.");
        var cylinderBytes = checked((long)layout.Heads * layout.SectorsPerTrack * 512);
        if (cylinderBytes <= 0 || layout.LastCylinder < layout.FirstCylinder) throw new ArgumentException("Invalid disk geometry.");
        if (first.HasValue || last.HasValue)
        {
            if (first is not { } low || last is not { } high || low < layout.FirstCylinder || high > layout.LastCylinder || high < low ||
                layout.Partitions.Any(p => low <= p.LastCylinder && p.FirstCylinder <= high))
                throw new ArgumentException("Choose a cylinder range entirely inside one free area.");
            var bytes = checked(((long)high - low + 1) * cylinderBytes);
            if (bytes % blockSize != 0) throw new ArgumentException("The cylinder range must contain whole filesystem blocks.");
            return new(low, high, bytes);
        }
        if (requestedBytes is <= 0) throw new ArgumentException("Enter a positive partition size.");
        long a = cylinderBytes, b = blockSize;
        while (b != 0) (a, b) = (b, a % b);
        var step = blockSize / a; // Cylinder count needed for whole filesystem blocks.
        var wanted = requestedBytes / cylinderBytes;
        if (wanted.HasValue) wanted -= wanted.Value % step;
        if (wanted is <= 0) throw new ArgumentException("The partition size is smaller than one aligned cylinder range.");
        foreach (var range in FreeRanges(layout))
        {
            var count = (long)range.Last - range.First + 1;
            count -= count % step;
            if (count == 0 || wanted > count) continue;
            count = wanted ?? count;
            return new(range.First, checked((uint)(range.First + count - 1)), checked(count * cylinderBytes));
        }
        throw new ArgumentException("No contiguous free area is large enough for this partition.");
    }
    internal static uint DosType(FileSystem fs) => fs switch
    {
        FileSystem.Ffs => 0x444F5301, FileSystem.Sfs => 0x53465300,
        FileSystem.Sfs2 => 0x53465302, FileSystem.Cpx => 0x43505834,
        _ => throw new ArgumentOutOfRangeException(nameof(fs))
    };

    internal static AmigaRdbSetupLayout Inspect(string path) => Inspect(path, out _);

    internal static AmigaRdbSetupLayout Inspect(string path, out long capacity)
    {
        using var disk = AmigaHardfileImage.OpenRead(path);
        capacity = disk.Length;
        return AmigaRdbSetup.ReadLayout(disk);
    }

    internal static void ValidateFormatDestination(string path, ImageFormat format)
    {
        var extension = Path.GetExtension(path);
        if (extension.Equals(".vhdx", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Choose a .vhd or .hdf filename.");
        if (format == ImageFormat.RawHdf && extension.Equals(".vhd", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Select Dynamic VHD for a .vhd filename, or choose a .hdf filename for the raw image.");
    }

    internal static (uint First, uint Last)? FirstFreeRange(AmigaRdbSetupLayout layout)
    {
        foreach (var range in FreeRanges(layout)) return range;
        return null;
    }

    private static IEnumerable<(uint First, uint Last)> FreeRanges(AmigaRdbSetupLayout layout)
    {
        var first = layout.FirstCylinder;
        foreach (var p in layout.Partitions.OrderBy(p => p.FirstCylinder))
        {
            if (first < p.FirstCylinder) yield return (first, p.FirstCylinder - 1);
            if (p.LastCylinder == uint.MaxValue) yield break;
            first = p.LastCylinder + 1;
        }
        if (first <= layout.LastCylinder) yield return (first, layout.LastCylinder);
    }

    // Always publishes a new file. Failure or cancellation never changes the source.
    internal static void Create(string destination, long capacity, string? source, string device,
        uint? first, uint? last, FileSystem fs, string label, bool bootable, CancellationToken cancellation = default,
        uint blockSize = 512, ImageFormat format = ImageFormat.RawHdf)
    {
        _ = DosType(fs);
        if (!BlockSizes.Contains(blockSize)) throw new ArgumentException("Choose a supported filesystem block size.");
        if (string.IsNullOrWhiteSpace(label) || label.Length > 30 || label.Any(c => c > 255 || c is '\0' or ':' or '/'))
            throw new ArgumentException("Use a volume label of 1–30 Latin-1 characters without colons or slashes.");
        using var input = source == null ? null : AmigaHardfileImage.OpenRead(source);
        if (input != null) capacity = input.Length;
        else if (capacity < 8L * 1024 * 1024 || capacity % 32768 != 0)
            throw new ArgumentException("Capacity must be at least 8 MiB and contain whole 32 KiB cylinders.");
        // Reject a damaged/unsupported source before allocating its copy.
        if (input != null) _ = AmigaRdbSetup.ReadLayout(input);
        Publish(destination, capacity, format, cancellation, output =>
        {
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
            var p = AmigaRdbSetup.AddPartition(output, device, first ?? layout.FirstCylinder, last ?? layout.LastCylinder, DosType(fs), bootable, blockSize);
            var start = checked((long)p.LowCylinder!.Value * layout.Heads * layout.SectorsPerTrack * 512);
            var length = checked(((long)p.HighCylinder!.Value - p.LowCylinder.Value + 1) * layout.Heads * layout.SectorsPerTrack * 512);
            cancellation.ThrowIfCancellationRequested();
            CopperScreenFileSystemFormatter.Format(output, start, length, fs, label, cancellation, blockSize);
            _ = AmigaRdbSetup.ReadLayout(output);
        });
    }

    internal static void CreateBlankVhd(string destination, long capacity, CancellationToken cancellation = default) =>
        Publish(destination, capacity, ImageFormat.DynamicVhd, cancellation);

    private static void Publish(string destination, long capacity, ImageFormat format, CancellationToken cancellation, Action<Stream>? initialize = null)
    {
        cancellation.ThrowIfCancellationRequested();
        destination = Path.GetFullPath(destination);
        ValidateFormatDestination(destination, format);
        CopperScreenAtomicFile.ValidateDestination(destination);
        if (Path.Exists(destination)) throw new IOException("Choose a new filename. Existing files are preserved.");
        var temporary = Path.Combine(Path.GetDirectoryName(destination)!, ".disk-setup-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var backing = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                using var vhd = format == ImageFormat.DynamicVhd ? AmigaHardfileImage.CreateDynamicVhd(backing, capacity) : null;
                if (vhd == null)
                {
                    AmigaSparseFile.TryEnable(backing);
                    backing.SetLength(capacity);
                }
                initialize?.Invoke(vhd ?? backing);
                backing.Flush(flushToDisk: true);
                if (vhd != null && AmigaHardfileImage.Inspect(backing) != (capacity, true))
                    throw new InvalidDataException("The new VHD could not be verified.");
            }
            cancellation.ThrowIfCancellationRequested();
            CopperScreenAtomicFile.ValidateDestination(destination);
            File.Move(temporary, destination);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
