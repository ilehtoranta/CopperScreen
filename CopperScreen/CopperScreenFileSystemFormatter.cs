using System.Buffers.Binary;
using System.Text;
using Amiga;
using CopperSFS;
using CopperStart.Dos;
using CopperCPX;

namespace CopperScreen;

internal static class CopperScreenFileSystemFormatter
{
    internal static void Format(FileStream disk, long start, long length, CopperScreenHardDiskSetup.FileSystem fs,
        string label, CancellationToken cancellation)
    {
        if (start < 0 || length < 512 || start % 512 != 0 || length % 512 != 0 || start > disk.Length - length)
            throw new ArgumentException("The filesystem must fit entirely in its partition.");
        if (fs == CopperScreenHardDiskSetup.FileSystem.Cpx)
        { FormatCpx(disk, start, length, label, cancellation); return; }
        if (length / 512 > uint.MaxValue || !AmigaVolumeGeometry.Create((uint)(length / 512), 512, 2, out var geometry))
            throw new ArgumentException("The partition exceeds the filesystem's supported block count.");
        geometry.StartByte = (ulong)start;
        var ram = new byte[4096]; Encoding.Latin1.GetBytes(label).CopyTo(ram, 2048);
        var platform = new Platform(disk, ram, geometry, cancellation);
        var scratch = APTR.FromPointer(512); var name = APTR.FromPointer(2048);
        var date = DateTime.UtcNow - new DateTime(1978, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var type = CopperScreenHardDiskSetup.DosType(fs);
        bool ok;
        if (fs == CopperScreenHardDiskSetup.FileSystem.Ffs)
            ok = AmigaDiskFileSystemFormatCore.Format(ref platform, type, name, (uint)label.Length,
                (uint)date.Days, (uint)(date.Hours * 60 + date.Minutes), (uint)(date.Seconds * 50), scratch, out _);
        else ok = AmigaSmartFileSystemFormatCore.Format(ref platform, type, name, (uint)label.Length, (uint)date.TotalSeconds, scratch, out _);
        if (!ok) throw new InvalidDataException("CopperFS could not format this partition geometry.");
        // Reopen through the maintained readers before publishing any new image.
        var mounted = fs == CopperScreenHardDiskSetup.FileSystem.Ffs
            ? AmigaDiskFileSystemCore.Mount(ref platform, scratch, out _)
            : AmigaSmartFileSystemReadOnlyCore.Mount(ref platform, scratch, out _);
        if (!mounted) throw new InvalidDataException("The newly formatted volume could not be mounted for verification.");
    }

    private static void FormatCpx(FileStream disk, long start, long length, string label, CancellationToken cancellation)
    {
        var bytes = new byte[100000]; Encoding.Latin1.GetBytes(label).CopyTo(bytes, 90000);
        var ram = new CpxMemory(bytes); var engine = new CpxEngine<CpxMemory>(ram, 32);
        var sectors = (ulong)length / 512;
        var idBytes = Guid.NewGuid().ToByteArray();
        var id = new Wide(BinaryPrimitives.ReadUInt32BigEndian(idBytes), BinaryPrimitives.ReadUInt32BigEndian(idBytes.AsSpan(4)));
        var error = engine.BeginFormat(512, 512, new((uint)(sectors >> 32), (uint)sectors), id, 1, 90000, (uint)label.Length);
        if (error != CpxError.None) throw new ArgumentException("CPX requires a partition of at least 4 MiB with supported geometry: " + error);
        DriveCpx(ref engine, bytes, disk, start, length, cancellation);
        if (engine.Initialize(512, 512, new((uint)(sectors >> 32), (uint)sectors), 2, true) != CpxError.None ||
            engine.BeginOperation(CpxOperation.Mount) != CpxError.None) throw new InvalidDataException("CPX verification could not start.");
        DriveCpx(ref engine, bytes, disk, start, length, cancellation);
        if (!engine.IsMounted) throw new InvalidDataException("The new CPX volume could not be mounted for verification.");
    }
    private static void DriveCpx(ref CpxEngine<CpxMemory> engine, byte[] ram, FileStream disk,
        long start, long length, CancellationToken cancellation)
    {
        while (true)
        {
            cancellation.ThrowIfCancellationRequested();
            var step = engine.Step();
            if (step == StepResult.Complete) return;
            if (step == StepResult.Failed) throw new InvalidDataException("CPX setup failed: " + engine.Error);
            if (step != StepResult.NeedIo) continue;
            var io = engine.Request;
            if (io.Kind == IoKind.Flush) disk.Flush(true);
            else if (io.Kind is IoKind.Read or IoKind.Write)
            {
                var sector = ((ulong)io.Sector.High << 32) | io.Sector.Low;
                if (sector > (ulong)length / 512 || io.Bytes > (ulong)length - sector * 512)
                    throw new InvalidDataException("CPX requested I/O outside its partition.");
                disk.Position = checked(start + (long)(sector * 512));
                var buffer = ram.AsSpan(checked((int)io.Buffer), checked((int)io.Bytes));
                if (io.Kind == IoKind.Read) disk.ReadExactly(buffer); else disk.Write(buffer);
            }
            else throw new InvalidDataException("Unexpected discard during CPX format.");
            if (engine.CompleteIo(io.Token, io.Bytes, true) != CpxError.None) throw new IOException("CPX I/O completion failed.");
        }
    }
    private readonly struct CpxMemory(byte[] bytes) : ICpxMemory
    {
        public byte Read8(uint p) => bytes[p];
        public void Write8(uint p, byte value) => bytes[p] = value;
        public uint Read32(uint p) => BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan((int)p));
        public void Write32(uint p, uint value) => BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan((int)p), value);
    }

    private struct Platform(FileStream disk, byte[] ram, AmigaVolumeGeometry geometry, CancellationToken cancellation) : IAmigaFormatFileSystemPlatform
    {
        public AmigaVolumeGeometry Geometry => geometry;
        public uint BlockBytes => geometry.BlockBytes;
        private bool Transfer(uint block, uint count, APTR buffer, bool write)
        {
            cancellation.ThrowIfCancellationRequested();
            if (block >= geometry.BlockCount || count > geometry.BlockCount - block) throw new InvalidDataException("Filesystem I/O exceeded its partition.");
            disk.Position = checked((long)(geometry.StartByte + (ulong)block * 512));
            var bytes = ram.AsSpan((int)buffer.Raw, checked((int)count * 512));
            if (write) disk.Write(bytes); else disk.ReadExactly(bytes);
            return true;
        }
        public bool ReadBlock(uint block, APTR target) => Transfer(block, 1, target, false);
        public bool WriteFormatBlock(uint block, APTR source) => Transfer(block, 1, source, true);
        public bool WriteBlock(uint block, APTR source) => Transfer(block, 1, source, true);
        public bool WriteDataBlocks(uint block, uint count, APTR source) => Transfer(block, count, source, true);
        public uint ReadDataBlocks(uint block, uint count, APTR target) => Transfer(block, count, target, false) ? count : 0;
        public uint ReadDataBlocks(uint block, uint count, APTR target, bool readAhead) => ReadDataBlocks(block, count, target);
        public bool ReadMetadataBlock(uint block, uint id, APTR target) => AmigaFileSystemBlockIo.ReadMetadata(ref this, block, id, target);
        public APTR ReadMetadataView(uint block, uint id, APTR scratch) => ReadMetadataBlock(block, id, scratch) ? scratch : APTR.Null;
        public bool WriteProtected() => false;
        public bool Flush() { cancellation.ThrowIfCancellationRequested(); disk.Flush(true); return true; }
        public byte ReadUInt8(APTR p, int offset = 0) => ram[(int)p.Raw + offset];
        public ushort ReadUInt16(APTR p, int offset = 0) => BinaryPrimitives.ReadUInt16BigEndian(ram.AsSpan((int)p.Raw + offset));
        public uint ReadUInt32(APTR p, int offset = 0) => BinaryPrimitives.ReadUInt32BigEndian(ram.AsSpan((int)p.Raw + offset));
        public void WriteUInt8(APTR p, int offset, byte value) => ram[(int)p.Raw + offset] = value;
        public void WriteUInt16(APTR p, int offset, ushort value) => BinaryPrimitives.WriteUInt16BigEndian(ram.AsSpan((int)p.Raw + offset), value);
        public void WriteUInt32(APTR p, int offset, uint value) => BinaryPrimitives.WriteUInt32BigEndian(ram.AsSpan((int)p.Raw + offset), value);
        public bool IsMapped(APTR p, uint bytes) => p.Raw <= ram.Length && bytes <= ram.Length - p.Raw;
        public void Clear(APTR p, uint bytes) => ram.AsSpan((int)p.Raw, (int)bytes).Clear();
        public void Copy(APTR source, APTR target, uint bytes) => ram.AsSpan((int)source.Raw, (int)bytes).CopyTo(ram.AsSpan((int)target.Raw));
    }
}
