/*
 * Copyright (C) 2026 Ilkka Lehtoranta
 * SPDX-License-Identifier: MIT
 */

using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;

namespace CopperDisk;

// Container offsets are physical bytes. Callers validate ranges against Length in guest bytes.
internal sealed class VhdImage
{
    private const int SectorSize = AmigaHardfile.SectorSize;
    private const uint Unallocated = uint.MaxValue;
    private const uint Version = 0x0001_0000;
    private const long MaxCapacity = 2L * 1024 * 1024 * 1024 * 1024;
    private const long MaxCreatedCapacity = 2040L * 1024 * 1024 * 1024;
    private const int DynamicHeaderSize = 1024;
    // Enough entries for the full VHD capacity at WinUAE's smallest block size (512 KiB).
    private const int MaxTableEntries = 4 * 1024 * 1024;

    private readonly long _dataOffset;
    private readonly byte[]? _footer;
    private readonly uint[]? _table;
    private readonly long _tableOffset;
    private readonly int _blockSize;
    private readonly long _bitmapSize;
    private long _footerOffset;
    private bool _allocationFailed;

    private VhdImage(long length, long dataOffset) => (Length, _dataOffset) = (length, dataOffset);

    private VhdImage(long length, byte[] footer, long footerOffset, uint[] table, long tableOffset, int blockSize, long bitmapSize)
        => (Length, _footer, _footerOffset, _table, _tableOffset, _blockSize, _bitmapSize) =
            (length, footer, footerOffset, table, tableOffset, blockSize, bitmapSize);

    internal long Length { get; }

    internal static VhdImage CreateDynamic(FileStream file, long capacity)
    {
        if (capacity < 1024 * 1024 || capacity > MaxCreatedCapacity || capacity % SectorSize != 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Dynamic VHD capacity must be 1 MiB to 2040 GiB in whole 512-byte sectors.");
        if (!file.CanRead || !file.CanWrite || !file.CanSeek || file.Length != 0)
            throw new ArgumentException("VHD creation requires an empty, seekable, read/write file.", nameof(file));

        const int tableOffset = SectorSize + DynamicHeaderSize;
        var blockSize = capacity < 10L * 1024 * 1024 * 1024 ? 512 * 1024 : 2 * 1024 * 1024;
        var table = new uint[(int)((capacity + blockSize - 1) / blockSize)];
        Array.Fill(table, Unallocated);
        var tableBytes = MemoryMarshal.AsBytes(table.AsSpan());
        var tableSize = AlignSector(tableBytes.Length);

        var footer = new byte[SectorSize];
        "conectix"u8.CopyTo(footer);
        Put32(footer, 8, 2); // Reserved feature bit required by the VHD format.
        Put32(footer, 12, Version);
        Put64(footer, 16, SectorSize);
        var timestamp = DateTimeOffset.UtcNow - new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
        Put32(footer, 24, (uint)Math.Max(0, timestamp.TotalSeconds));
        "CScr"u8.CopyTo(footer.AsSpan(28));
        Put32(footer, 32, Version);
        "Wi2k"u8.CopyTo(footer.AsSpan(36));
        Put64(footer, 40, (ulong)capacity);
        Put64(footer, 48, (ulong)capacity);
        WriteGeometry(footer.AsSpan(56, 4), capacity / SectorSize);
        Put32(footer, 60, 3); // Dynamic disk.
        Guid.NewGuid().TryWriteBytes(footer.AsSpan(68, 16), bigEndian: true, out _);
        Put32(footer, 64, Checksum(footer, 64));

        Span<byte> header = stackalloc byte[DynamicHeaderSize];
        header.Clear();
        "cxsparse"u8.CopyTo(header);
        Put64(header, 8, ulong.MaxValue);
        Put64(header, 16, tableOffset);
        Put32(header, 24, Version);
        Put32(header, 28, (uint)table.Length);
        Put32(header, 32, (uint)blockSize);
        Put32(header, 36, Checksum(header, 36));

        file.Position = 0;
        file.Write(footer);
        file.Write(header);
        file.Write(tableBytes); // All bits are set in unallocated entries on either byte order.
        // Reserved header bytes are zero and can fill the sector-alignment padding.
        file.Write(header[^SectorSize..][..(int)(tableSize - tableBytes.Length)]);
        file.Write(footer);
        return new VhdImage(capacity, footer, tableOffset + tableSize, table, tableOffset, blockSize, SectorSize);
    }

    // The VHD CHS fields are advisory. The footer's current size retains the exact sector count.
    private static void WriteGeometry(Span<byte> geometry, long sectors)
    {
        sectors = Math.Min(sectors, 65535L * 16 * 255);
        int heads, sectorsPerTrack;
        if (sectors >= 65535L * 16 * 63)
        {
            heads = 16;
            sectorsPerTrack = 255;
        }
        else
        {
            sectorsPerTrack = 17;
            heads = Math.Max(4, (int)((sectors / sectorsPerTrack + 1023) / 1024));
            if (heads > 16 || sectors / sectorsPerTrack >= heads * 1024)
            {
                heads = 16;
                sectorsPerTrack = sectors / 31 >= heads * 1024 ? 63 : 31;
            }
        }
        BinaryPrimitives.WriteUInt16BigEndian(geometry, (ushort)(sectors / (heads * sectorsPerTrack)));
        geometry[2] = (byte)heads;
        geometry[3] = (byte)sectorsPerTrack;
    }

    internal static VhdImage? Open(FileStream stream)
    {
        var fileLength = stream.Length;
        Span<byte> first = stackalloc byte[SectorSize];
        Span<byte> last = stackalloc byte[SectorSize];
        if (fileLength >= SectorSize)
        {
            ReadAt(stream, 0, first);
            if (first.StartsWith("vhdxfile"u8)) throw new NotSupportedException("VHDX hard disk images are not supported.");
            var footerOffset = fileLength - SectorSize;
            ReadAt(stream, footerOffset, last);
            var hasLeadingFooter = first.StartsWith("conectix"u8);
            if (last.StartsWith("conectix"u8))
            {
                if (footerOffset % SectorSize != 0 || (U32(last, 8) & 3) != 2 || U32(last, 12) != Version || Checksum(last, 64) != U32(last, 64))
                    throw Invalid("invalid footer");
                var length = (long)U64(last, 48);
                if (length <= 0 || length > MaxCapacity || length % SectorSize != 0)
                    throw Invalid("invalid virtual capacity");
                switch (U32(last, 60))
                {
                    case 2 when U64(last, 16) == ulong.MaxValue:
                        if (footerOffset == length) return new VhdImage(length, 0);
                        // WinUAE also recognizes fixed images with matching footer copies around the data.
                        if (footerOffset == length + SectorSize && first.SequenceEqual(last)) return new VhdImage(length, SectorSize);
                        throw Invalid("fixed disk size does not match its footer");
                    case 3:
                        if (!first.SequenceEqual(last)) throw Invalid("dynamic disk footer copies do not match");
                        return OpenDynamic(stream, length, footerOffset, last);
                    case 4:
                        throw new NotSupportedException("Differencing VHD images are not supported. Use a standalone VHD.");
                    default:
                        throw Invalid("unsupported disk type or footer data offset");
                }
            }
            if (hasLeadingFooter) throw Invalid("missing trailing footer");
        }
        if (HasVhdExtension(stream.Name)) throw Invalid("missing VHD footer");
        return null;
    }

    internal static bool HasVhdExtension(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".vhd", StringComparison.OrdinalIgnoreCase) || extension.Equals(".vhdx", StringComparison.OrdinalIgnoreCase);
    }

    private static VhdImage OpenDynamic(FileStream stream, long length, long footerOffset, ReadOnlySpan<byte> footer)
    {
        var headerOffset = (long)U64(footer, 16);
        if (headerOffset < SectorSize || headerOffset % SectorSize != 0 || headerOffset > footerOffset - DynamicHeaderSize)
            throw Invalid("invalid dynamic header offset");
        Span<byte> header = stackalloc byte[DynamicHeaderSize];
        ReadAt(stream, headerOffset, header);
        if (!header.StartsWith("cxsparse"u8) || U64(header, 8) != ulong.MaxValue || U32(header, 24) != Version || Checksum(header, 36) != U32(header, 36))
            throw Invalid("invalid dynamic header");

        var blockSize = (int)U32(header, 32);
        var tableCount = U32(header, 28);
        if (blockSize < SectorSize || !BitOperations.IsPow2(blockSize) ||
            tableCount < (length + blockSize - 1) / blockSize || tableCount > MaxTableEntries)
            throw Invalid("invalid block size or allocation table length");
        var tableOffset = (long)U64(header, 16);
        var tableSize = AlignSector(tableCount * 4L);
        if (tableOffset < SectorSize || tableOffset % SectorSize != 0 || tableOffset > footerOffset - tableSize ||
            Overlaps(tableOffset, tableSize, headerOffset, DynamicHeaderSize))
            throw Invalid("invalid allocation table offset");

        var table = new uint[(int)tableCount];
        var tableBytes = MemoryMarshal.AsBytes(table.AsSpan());
        ReadAt(stream, tableOffset, tableBytes);
        var allocated = new List<uint>();
        var bitmapSize = AlignSector((blockSize / SectorSize + 7L) / 8);
        var extentSize = bitmapSize + blockSize;
        for (var block = 0; block < table.Length; block++)
        {
            var sector = table[block] = U32(tableBytes, block * 4);
            if (sector == Unallocated) continue;
            var offset = sector * (long)SectorSize;
            if (offset < SectorSize || offset > footerOffset - extentSize ||
                Overlaps(offset, extentSize, headerOffset, DynamicHeaderSize) || Overlaps(offset, extentSize, tableOffset, tableSize))
                throw Invalid("allocated block overlaps metadata or extends beyond the file");
            allocated.Add(sector);
        }
        allocated.Sort();
        for (var i = 1; i < allocated.Count; i++)
            if ((allocated[i] - allocated[i - 1]) * (long)SectorSize < extentSize)
                throw Invalid("allocated blocks overlap");
        return new VhdImage(length, footer.ToArray(), footerOffset, table, tableOffset, blockSize, bitmapSize);
    }

    internal void Read(FileStream stream, long offset, Span<byte> data, IAmigaHardfileIoHook? hook = null)
    {
        if (_table == null) { ReadAt(stream, _dataOffset + offset, data, hook); return; }

        Span<byte> bitmapByte = stackalloc byte[1];
        while (!data.IsEmpty)
        {
            var withinBlock = (int)(offset % _blockSize);
            var block = _table[(int)(offset / _blockSize)];
            var count = Math.Min(data.Length, _blockSize - withinBlock);
            if (block == Unallocated) data[..count].Clear();
            else
            {
                var blockOffset = block * (long)SectorSize;
                var sector = withinBlock / SectorSize;
                ReadAt(stream, blockOffset + sector / 8, bitmapByte, hook);
                var present = (bitmapByte[0] & (0x80 >> (sector % 8))) != 0;
                // Read or clear consecutive sectors with the same bitmap state within this byte.
                var endSector = sector + 1;
                while (endSector % 8 != 0 && ((bitmapByte[0] & (0x80 >> (endSector % 8))) != 0) == present)
                    endSector++;
                count = Math.Min(count, endSector * SectorSize - withinBlock);
                if (present) ReadAt(stream, blockOffset + _bitmapSize + withinBlock, data[..count], hook);
                else data[..count].Clear();
            }
            offset += count;
            data = data[count..];
        }
    }

    internal void Write(FileStream stream, long offset, ReadOnlySpan<byte> data, IAmigaHardfileIoHook? hook)
    {
        if (_table == null) { WriteAt(stream, _dataOffset + offset, data, hook); return; }
        if (_allocationFailed) throw new IOException("VHD allocation rollback failed. Reopen the image before writing again.");

        Span<byte> bitmapByte = stackalloc byte[1];
        Span<byte> partialSector = stackalloc byte[SectorSize];
        while (!data.IsEmpty)
        {
            var index = (int)(offset / _blockSize);
            var withinBlock = (int)(offset % _blockSize);
            var withinSector = withinBlock % SectorSize;
            var sector = withinBlock / SectorSize;
            var count = Math.Min(data.Length, SectorSize - withinSector);
            var block = _table[index];
            if (block == Unallocated)
            {
                if (!data[..count].ContainsAnyExcept((byte)0))
                {
                    offset += count;
                    data = data[count..];
                    continue;
                }
                block = AllocateBlock(stream, index, hook);
            }
            var blockOffset = block * (long)SectorSize;
            var bitmapOffset = blockOffset + sector / 8;
            ReadAt(stream, bitmapOffset, bitmapByte, hook);
            var mask = (byte)(0x80 >> (sector % 8));
            var present = (bitmapByte[0] & mask) != 0;
            var dataOffset = blockOffset + _bitmapSize + withinBlock;
            if (!present && count != SectorSize)
            {
                partialSector.Clear();
                data[..count].CopyTo(partialSector[withinSector..]);
                WriteAt(stream, dataOffset - withinSector, partialSector, hook);
            }
            else WriteAt(stream, dataOffset, data[..count], hook);
            if (!present)
            {
                bitmapByte[0] |= mask;
                WriteAt(stream, bitmapOffset, bitmapByte, hook);
            }
            offset += count;
            data = data[count..];
        }
    }

    private uint AllocateBlock(FileStream stream, int index, IAmigaHardfileIoHook? hook)
    {
        var extentSize = _bitmapSize + _blockSize;
        var block = _footerOffset / SectorSize;
        if (block >= Unallocated || block + extentSize / SectorSize >= Unallocated)
            throw new IOException("VHD file exceeds the block allocation table's address range.");
        var newFooterOffset = _footerOffset + extentSize;
        var entryOffset = _tableOffset + index * 4L;
        Span<byte> entry = stackalloc byte[4];
        var publishing = false;
        try
        {
            // Append the new footer before reusing the old footer's sector as bitmap space.
            WriteAt(stream, newFooterOffset, _footer!, hook);
            Span<byte> zeros = stackalloc byte[16 * 1024];
            zeros.Clear();
            for (var position = _footerOffset; position < newFooterOffset; position += zeros.Length)
                WriteAt(stream, position, zeros[..(int)Math.Min(zeros.Length, newFooterOffset - position)], hook);
            BinaryPrimitives.WriteUInt32BigEndian(entry, (uint)block);
            publishing = true;
            WriteAt(stream, entryOffset, entry, hook);
            stream.Flush();
            _table![index] = (uint)block;
            _footerOffset = newFooterOffset;
            return (uint)block;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            try
            {
                if (publishing)
                {
                    entry.Fill(0xff);
                    WriteAt(stream, entryOffset, entry, hook);
                }
                WriteAt(stream, _footerOffset, _footer!, hook);
                stream.SetLength(_footerOffset + SectorSize);
            }
            catch (Exception rollback) when (rollback is IOException or UnauthorizedAccessException)
            {
                _allocationFailed = true;
                throw new IOException("VHD block allocation and rollback failed.", new AggregateException(error, rollback));
            }
            throw;
        }
    }

    private static long AlignSector(long bytes) => (bytes + SectorSize - 1) & ~(SectorSize - 1L);
    private static bool Overlaps(long start, long size, long other, long otherSize) => start < other + otherSize && other < start + size;
    private static uint U32(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32BigEndian(bytes[offset..]);
    private static ulong U64(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt64BigEndian(bytes[offset..]);
    private static void Put32(Span<byte> bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32BigEndian(bytes[offset..], value);
    private static void Put64(Span<byte> bytes, int offset, ulong value) => BinaryPrimitives.WriteUInt64BigEndian(bytes[offset..], value);

    private static uint Checksum(ReadOnlySpan<byte> bytes, int checksumOffset)
    {
        uint sum = 0;
        foreach (var value in bytes[..checksumOffset]) sum += value;
        foreach (var value in bytes[(checksumOffset + 4)..]) sum += value;
        return ~sum;
    }

    private static InvalidDataException Invalid(string reason) => new($"Invalid VHD image: {reason}.");

    private static void ReadAt(FileStream stream, long offset, Span<byte> data, IAmigaHardfileIoHook? hook = null)
    {
        if (hook != null) hook.Read(stream, offset, data);
        else { stream.Position = offset; stream.ReadExactly(data); }
    }

    private static void WriteAt(FileStream stream, long offset, ReadOnlySpan<byte> data, IAmigaHardfileIoHook? hook)
    {
        if (hook != null) hook.Write(stream, offset, data);
        else { stream.Position = offset; stream.Write(data); }
    }
}
