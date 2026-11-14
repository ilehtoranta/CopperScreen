using System.Buffers.Binary;
using CopperDisk;

namespace CopperDisk.Tests;

public sealed class AmigaRdbSetupTests
{
    [Theory]
    [InlineData(512u)] [InlineData(1024u)] [InlineData(2048u)] [InlineData(4096u)]
    public void FilesystemBlockSizeIsRecordedWithoutChangingDeviceSectorGeometry(uint blockSize)
    {
        using var disk = Blank();
        AmigaRdbSetup.Initialize(disk);
        var metadata = AmigaRdbSetup.AddPartition(disk, "DH0", 1, 255, 0x444F5301, false, blockSize);
        var layout = AmigaRdbSetup.ReadLayout(disk);
        Assert.Equal(2u, layout.Heads); Assert.Equal(32u, layout.SectorsPerTrack);
        Assert.Equal(128u, metadata.SizeBlockLongs); Assert.Equal(blockSize / 512, metadata.SectorsPerBlock);
        Assert.Equal(blockSize, Assert.Single(layout.Partitions).BlockSize);
    }

    [Fact]
    public void InvalidFilesystemBlockSizeLeavesThePartitionTableUnchanged()
    {
        using var disk = Blank(); AmigaRdbSetup.Initialize(disk);
        var before = disk.ToArray();
        Assert.Throws<ArgumentException>(() => AmigaRdbSetup.AddPartition(disk, "DH0", 1, 255, 0x444F5301, false, 1000));
        Assert.Equal(before, disk.ToArray());
    }

    private static MemoryStream Blank() => new(new byte[8 * 1024 * 1024], writable: true);
    [Fact]
    public void AddsPartitionsWithoutChangingExistingMetadataOrPayload()
    {
        using var disk = Blank();
        var layout = AmigaRdbSetup.Initialize(disk);
        Assert.Equal(1u, layout.FirstCylinder); Assert.Equal(255u, layout.LastCylinder);
        AmigaRdbSetup.AddPartition(disk, "DH0", 1, 100, 0x444F5301, true);
        disk.Position = 32768; disk.Write("keep this volume"u8);
        var before = disk.ToArray();
        AmigaRdbSetup.AddPartition(disk, "DH1", 101, 255, 0x53465300, false);
        var after = disk.ToArray();
        Assert.Equal(before.AsSpan(512, 512).ToArray(), after.AsSpan(512, 512).ToArray());
        Assert.True(before.AsSpan(32768).SequenceEqual(after.AsSpan(32768)));
        layout = AmigaRdbSetup.ReadLayout(disk);
        Assert.Equal(2, layout.Partitions.Count);
        using var file = new TemporaryImage(after);
        AmigaRdbImage.Validate(file.Path);
    }

    [Theory]
    [InlineData("DH1", 0u, 10u)] [InlineData("DH1", 50u, 150u)]
    [InlineData("DH1", 101u, 256u)] [InlineData("dh0", 101u, 200u)]
    [InlineData("Bad:name", 101u, 200u)] [InlineData("DH1", 200u, 101u)]
    public void RejectsInvalidPartitionWithoutChangingDisk(string name, uint first, uint last)
    {
        using var disk = Blank(); AmigaRdbSetup.Initialize(disk);
        AmigaRdbSetup.AddPartition(disk, "DH0", 1, 100, 0x444F5301, false);
        var before = disk.ToArray();
        Assert.Throws<ArgumentException>(() => AmigaRdbSetup.AddPartition(disk, name, first, last, 0x53465300, false));
        Assert.True(before.AsSpan().SequenceEqual(disk.ToArray()));
    }

    [Fact]
    public void DisabledPartitionsStillOccupyTheirCylinders()
    {
        using var disk = Blank(); AmigaRdbSetup.Initialize(disk);
        AmigaRdbSetup.AddPartition(disk, "DH0", 1, 100, 0x444F5301, false);
        var data = disk.ToArray();
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(512 + 20), 2); Seal(data.AsSpan(512, 512));
        using var edited = new MemoryStream(data, writable: true);
        Assert.Single(AmigaRdbSetup.ReadLayout(edited).Partitions);
        Assert.Throws<ArgumentException>(() => AmigaRdbSetup.AddPartition(edited, "DH1", 50, 150, 0x53465300, false));
    }

    [Fact]
    public void CorruptOrCyclicChainsAreRejectedBeforeMutation()
    {
        using var disk = Blank(); AmigaRdbSetup.Initialize(disk);
        AmigaRdbSetup.AddPartition(disk, "DH0", 1, 100, 0x444F5301, false);
        var data = disk.ToArray(); data[520] ^= 1;
        using var damaged = new MemoryStream(data, writable: true);
        Assert.Throws<InvalidDataException>(() => AmigaRdbSetup.ReadLayout(damaged));
        data = disk.ToArray(); BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(512 + 16), 1); Seal(data.AsSpan(512, 512));
        using var cycle = new MemoryStream(data, writable: true);
        Assert.Throws<InvalidDataException>(() => AmigaRdbSetup.AddPartition(cycle, "DH1", 101, 200, 0x444F5301, false));
        Assert.True(data.AsSpan().SequenceEqual(cycle.ToArray()));
    }
    private static void Seal(Span<byte> b)
    {
        BinaryPrimitives.WriteUInt32BigEndian(b[8..], 0); uint sum = 0;
        for (var at = 0; at < 256; at += 4) sum = unchecked(sum + BinaryPrimitives.ReadUInt32BigEndian(b[at..]));
        BinaryPrimitives.WriteUInt32BigEndian(b[8..], unchecked(0u - sum));
    }

    [Fact]
    public void MetadataReferencesBeyondTheHighWaterMarkAreRejected()
    {
        using var disk = Blank(); AmigaRdbSetup.Initialize(disk);
        var data = disk.ToArray();
        // A referenced load segment must never be mistaken for a free PART slot.
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(36), 1); Seal(data.AsSpan(0, 512));
        using var source = new MemoryStream(data, writable: true);
        Assert.Throws<InvalidDataException>(() => AmigaRdbSetup.AddPartition(source, "DH0", 1, 255, 0x444F5301, false));
        Assert.True(data.AsSpan().SequenceEqual(source.ToArray()));
    }
    private sealed class TemporaryImage : IDisposable
    {
        internal string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".hdf");
        internal TemporaryImage(byte[] data) => File.WriteAllBytes(Path, data);
        public void Dispose() => File.Delete(Path);
    }
}
