using System.Buffers.Binary;

namespace CopperDisk.Tests;

public sealed class VhdHardfileTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("CopperVhd-").FullName;
    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Theory]
    [InlineData(9L * 1024 * 1024 * 1024 + 512, 512 * 1024)]
    [InlineData(10L * 1024 * 1024 * 1024, 2 * 1024 * 1024)]
    public void CreatesAnEmptyWinUaeLayoutAndKeepsTheExactVirtualCapacity(long capacity, int blockSize)
    {
        var path = Path.Combine(_root, "created.vhd");
        var entries = (int)((capacity + blockSize - 1) / blockSize);
        using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
        {
            using (var disk = AmigaHardfileImage.CreateDynamicVhd(file, capacity))
            {
                Assert.True(disk.CanWrite);
                Assert.Equal(capacity, disk.Length);
                disk.Seek(-512, SeekOrigin.End);
                Assert.Equal(0, disk.ReadByte());
                Assert.Throws<IOException>(() => disk.Write(new byte[512]));
            }
            Assert.True(file.CanRead);
            Assert.Equal((capacity, true), AmigaHardfileImage.Inspect(file));
        }

        var bytes = File.ReadAllBytes(path);
        Assert.Equal(2048 + ((entries * 4 + 511) & ~511), bytes.Length);
        Assert.Equal("conectix"u8.ToArray(), bytes[..8]);
        Assert.Equal(bytes[..512], bytes[^512..]);
        Assert.Equal((uint)blockSize, BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(512 + 32)));
        Assert.False(bytes.AsSpan(1536, entries * 4).ContainsAnyExcept((byte)0xff));

        using (var hardfile = AmigaHardfile.Open(new(0, path))) hardfile.Write(capacity - 1, [42]);
        using var reopened = AmigaHardfileImage.OpenRead(path);
        reopened.Seek(-1, SeekOrigin.End);
        Assert.Equal(42, reopened.ReadByte());
    }

    [Fact]
    public void DynamicCreationRejectsInvalidCapacityAndNonemptyFilesBeforeWriting()
    {
        var path = Path.Combine(_root, "unpublished.vhd");
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite);
        Assert.Throws<ArgumentOutOfRangeException>(() => AmigaHardfileImage.CreateDynamicVhd(file, 1024 * 1024 + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => AmigaHardfileImage.CreateDynamicVhd(file, 2041L * 1024 * 1024 * 1024));
        Assert.Equal(0, file.Length);
        file.WriteByte(7);
        Assert.Throws<ArgumentException>(() => AmigaHardfileImage.CreateDynamicVhd(file, 1024 * 1024));
        file.Position = 0;
        Assert.Equal(7, file.ReadByte());
        Assert.Equal(1, file.Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FixedMountHidesMetadataAndUpdatesGuestBytesInPlace(bool leadingFooter)
    {
        var payload = new byte[4096];
        payload[0] = 17;
        payload[^1] = 23;
        var container = VhdFixture.Fixed(payload, leadingFooter);
        var path = Save("fixed.hdf", container);
        using (var disk = AmigaHardfile.Open(new(0, path) { SparseStorage = true }))
        {
            Assert.Equal(payload.Length, disk.Length);
            Assert.Equal(8, disk.SectorCount);
            Assert.False(disk.SupportsDiscard);
            Assert.Equal(payload, disk.ReadBytes(0, payload.Length));
            Assert.Throws<ArgumentOutOfRangeException>(() => disk.ReadBytes(disk.Length, 1));
            disk.Write(disk.Length - 2, [45, 46]);
            disk.Flush();
        }
        container[(leadingFooter ? 512 : 0) + payload.Length - 2] = 45;
        container[(leadingFooter ? 512 : 0) + payload.Length - 1] = 46;
        Assert.Equal(container, File.ReadAllBytes(path));
        using var readOnly = AmigaHardfile.Open(new(0, path, readOnly: true));
        Assert.Throws<UnauthorizedAccessException>(() => readOnly.Write(0, [12]));
    }

    [Theory]
    [InlineData(512 * 1024)]
    [InlineData(2 * 1024 * 1024)]
    public void DynamicMountReadsHolesAndPersistsWritesAcrossBlocksAndAboveFourGiB(int blockSize)
    {
        const long capacity = 5L * 1024 * 1024 * 1024 + 512;
        var blank = VhdFixture.Dynamic(capacity, blockSize);
        var path = Save("dynamic.vhd", blank);
        using (var disk = AmigaHardfile.Open(new(0, path) { SparseStorage = true }))
        {
            Assert.Equal(capacity, disk.Length);
            Assert.False(disk.SupportsDiscard);
            Assert.Equal(new byte[1024], disk.ReadBytes(blockSize - 512, 1024));
            disk.Write(blockSize, new byte[512]);
            Assert.Equal(blank.Length, new FileInfo(path).Length);
            disk.Write(blockSize - 1, [11, 12, 13]);
            disk.Write(capacity - 1, [14]);
            using var replacement = disk.PrepareReplacement(new(0, path));
            Assert.Equal(new byte[] { 0, 11, 12, 13, 0 }, replacement.ReadBytes(blockSize - 2, 5));
            Assert.Equal(new byte[] { 0, 14 }, replacement.ReadBytes(capacity - 2, 2));
        }
        using (var stream = AmigaHardfileImage.OpenRead(path))
        {
            Assert.Equal(capacity, stream.Length);
            Assert.Equal(capacity - 1, stream.Seek(-1, SeekOrigin.End));
            Assert.Equal(14, stream.ReadByte());
            Assert.Equal(-1, stream.ReadByte());
        }
        var container = File.ReadAllBytes(path);
        Assert.Equal(blank.Length + 3 * (blockSize + 512), container.Length);
        Assert.Equal(blank[..512], container[^512..]);
        var firstBlock = blank.Length - 512;
        Assert.Equal((uint)(firstBlock / 512), BinaryPrimitives.ReadUInt32BigEndian(container.AsSpan(1536)));
        Assert.Equal(1, container[firstBlock + blockSize / 4096 - 1]);
        Assert.Equal(11, container[firstBlock + 512 + blockSize - 1]);
        Assert.Equal(0x80, container[firstBlock + blockSize + 512]);
    }

    [Fact]
    public void ClearBitmapBitsHideBackingBytesAndPartialWritesZeroTheRestOfTheSector()
    {
        var image = VhdFixture.Dynamic(1024 * 1024, firstBlock: new byte[512]);
        const int sectorData = 2048 + 512 + 512;
        image.AsSpan(sectorData, 512).Fill(0xa5);
        var path = Save("bitmap.hdf", image);
        using (var disk = AmigaHardfile.Open(new(0, path)))
        {
            Assert.Equal(new byte[512], disk.ReadBlock(1));
            disk.Write(513, [9]);
        }
        var sector = new byte[512];
        sector[1] = 9;
        using var reopened = AmigaHardfile.Open(new(0, path));
        Assert.Equal(sector, reopened.ReadBlock(1));
    }

    [Fact]
    public void RdbDiscoveryAndVolumeReadsUseVirtualBytes()
    {
        var payload = new byte[8 * 1024 * 1024];
        using var raw = new MemoryStream(payload);
        AmigaRdbSetup.Initialize(raw);
        AmigaRdbSetup.AddPartition(raw, "DH0", 16, 255, 0x444F5301, bootable: true);
        var path = Save("rdb.hdf", VhdFixture.Dynamic(payload.Length, firstBlock: payload[..(512 * 1024)]));
        AmigaRdbImage.Validate(path);
        using var sectorImage = AmigaSectorImage.Open(path);
        var volume = Assert.Single(sectorImage.Volumes);
        Assert.Equal("DH0", volume.DeviceName);
        Assert.Equal(240 * 32768, volume.Length);
        Assert.Null(volume.UnsupportedReason);
        Assert.NotNull(sectorImage.WriteUnavailableReason);
        Span<byte> tail = stackalloc byte[512];
        volume.Read(volume.Length - tail.Length, tail);
        Assert.False(tail.ContainsAnyExcept((byte)0));
    }

    [Theory]
    [InlineData("footer")]
    [InlineData("header")]
    [InlineData("truncated")]
    [InlineData("overlap")]
    [InlineData("metadata")]
    public void DamagedContainersAreRejectedEvenWithHdfExtension(string damage)
    {
        var image = VhdFixture.Dynamic(1024 * 1024, firstBlock: new byte[512]);
        switch (damage)
        {
            case "footer": image[^1] = 1; break;
            case "header": image[1500] = 1; break;
            case "truncated": Array.Resize(ref image, image.Length - 512); break;
            case "overlap": VhdFixture.Put(image, 1540, 4); break;
            case "metadata": VhdFixture.Put(image, 1536, 3); break;
        }
        var path = Save("broken.hdf", image);
        Assert.Throws<InvalidDataException>(() => AmigaHardfile.Open(new(0, path)));
        Assert.Equal(image, File.ReadAllBytes(path));
    }

    [Fact]
    public void RejectsDifferencingAndVhdxBeforeTreatingTheirDataAsRaw()
    {
        var image = VhdFixture.Dynamic(1024 * 1024);
        VhdFixture.Put(image, 60, 4);
        VhdFixture.Checksum(image.AsSpan(0, 512), 64);
        image.AsSpan(0, 512).CopyTo(image.AsSpan(image.Length - 512));
        Assert.Throws<NotSupportedException>(() => AmigaHardfile.Open(new(0, Save("child.hdf", image))));
        "vhdxfile"u8.CopyTo(image);
        Assert.Throws<NotSupportedException>(() => AmigaHardfile.Open(new(0, Save("vhdx.hdf", image))));
    }

    [Fact]
    public void FailingAllocationRestoresFooterAndBatAndCanBeRetried()
    {
        var blank = VhdFixture.Dynamic(1024 * 1024);
        var path = Save("retry.vhd", blank);
        using (var disk = AmigaHardfile.Open(new(0, path) { IoHook = new FailFirstBatWrite() }))
        {
            Assert.Throws<IOException>(() => disk.Write(0, [5]));
            disk.Flush();
            using (var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                Assert.Equal(blank.Length, source.Length);
                var bytes = new byte[blank.Length];
                source.ReadExactly(bytes);
                Assert.Equal(blank, bytes);
            }
            Assert.Equal(new byte[1], disk.ReadBytes(0, 1));
            disk.Write(0, [5]);
        }
        using var reopened = AmigaHardfile.Open(new(0, path));
        Assert.Equal(new byte[] { 5 }, reopened.ReadBytes(0, 1));
    }

    private string Save(string name, byte[] bytes)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private sealed class FailFirstBatWrite : IAmigaHardfileIoHook
    {
        private bool _failed;
        public bool FlushOnDispose => true;
        public void Flush(FileStream backing) => backing.Flush(flushToDisk: true);
        public void Read(FileStream backing, long offset, Span<byte> destination)
        {
            backing.Position = offset;
            backing.ReadExactly(destination);
        }
        public void Write(FileStream backing, long offset, ReadOnlySpan<byte> source)
        {
            backing.Position = offset;
            if (offset == 1536 && !_failed)
            {
                _failed = true;
                backing.Write(source[..2]);
                throw new IOException("Injected BAT write failure.");
            }
            backing.Write(source);
        }
    }
}
