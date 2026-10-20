using System.Text;
using Copper68k;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

public sealed class LightweightHostPacketTests
{
    [Fact]
    public void OwnerFlushPersistsOpenGuestFilesWithoutMovingHandlesOrGuestTime()
    {
        using var fixture = new Fixture(); var name = fixture.Name("flush-proof");
        Assert.Equal((-1, 0), fixture.Packet(1006, 0x8200 >> 2, 0, name));
        var handle = fixture.Bus.ReadLong(0x8224);
        fixture.Bus.CopyToMemory(0x8800, "first"u8); Assert.Equal((5, 0), fixture.Packet(87, handle, 0x8800, 5));
        var cycle = fixture.Machine.Cycle; fixture.Machine.FlushPersistentStorage();
        Assert.Equal(cycle, fixture.Machine.Cycle);
        string ReadVisible()
        {
            using var stream = new FileStream(Path.Combine(fixture.Root, "flush-proof"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream); return reader.ReadToEnd();
        }
        Assert.Equal("first", ReadVisible());
        fixture.Bus.CopyToMemory(0x8800, "last"u8); Assert.Equal((4, 0), fixture.Packet(87, handle, 0x8800, 4));
        fixture.Machine.FlushPersistentStorage(); Assert.Equal(cycle, fixture.Machine.Cycle);
        Assert.Equal("firstlast", ReadVisible());
        Assert.Equal((-1, 0), fixture.Packet(1007, handle));
    }

    [Fact]
    public void HighRamBootstrapStringsHandlesAndFileBuffersKeepFullAddresses()
    {
        using var fixture = new Fixture(highRam: true);
        System.IO.File.WriteAllText(Path.Combine(fixture.Root, "file"), "high-memory");
        var name = fixture.Name("file", 0x40008000);
        Assert.Equal((-1, 0), fixture.Packet(1005, 0x40008200 >> 2, 0, name));
        var handle = fixture.Bus.ReadLong(0x40008224);
        Assert.Equal((11, 0), fixture.Packet(82, handle, 0x40008800, 11));
        var data = new byte[11]; fixture.Bus.CopyFromMemory(0x40008800, data);
        Assert.Equal("high-memory", Encoding.ASCII.GetString(data));
        Assert.Equal((-1, 0), fixture.Packet(1007, handle));
    }

    [Fact]
    public void ResizePreservesOtherOpenHandlePositionsAndPendingWrites()
    {
        using var fixture = new Fixture(); System.IO.File.WriteAllText(Path.Combine(fixture.Root, "file"), "abcdefgh"); var name = fixture.Name("file");
        Assert.Equal((-1, 0), fixture.Packet(1004, 0x8200 >> 2, 0, name)); var first = fixture.Bus.ReadLong(0x8224);
        Assert.Equal((-1, 0), fixture.Packet(1004, 0x8300 >> 2, 0, name)); var second = fixture.Bus.ReadLong(0x8324);
        fixture.Bus.CopyToMemory(0x8800, "WXYZ"u8); Assert.Equal((4, 0), fixture.Packet(87, second, 0x8800, 4));
        Assert.Equal((0, 0), fixture.Packet(1008, first, 8, uint.MaxValue));
        Assert.Equal((4, 0), fixture.Packet(1022, first, 2, uint.MaxValue));
        Assert.Equal((4, 0), fixture.Packet(1008, first, 0, 0)); Assert.Equal((4, 0), fixture.Packet(1008, second, 0, 0));
        Assert.Equal((-1, 0), fixture.Packet(1007, second)); Assert.Equal((2, 0), fixture.Packet(1022, first, 2, uint.MaxValue));
        Assert.Equal((-1, 0), fixture.Packet(1007, first)); Assert.Equal("WX", System.IO.File.ReadAllText(Path.Combine(fixture.Root, "file")));
    }
    [Fact]
    public void LegacyUnknownLockModesAreSharedAndStillRespectExclusiveConflicts()
    {
        using var fixture = new Fixture(); System.IO.File.WriteAllText(Path.Combine(fixture.Root, "file"), "hello");
        var name = fixture.Name("file"); var (first, error) = fixture.Packet(8, 0, name, 0x00301858);
        Assert.Equal(0, error); Assert.Equal(unchecked((uint)-2), fixture.Bus.ReadLong(((uint)first << 2) + 8));
        var (second, secondError) = fixture.Packet(8, 0, name, 0); Assert.Equal(0, secondError);
        Assert.Equal((0, 202), fixture.Packet(8, 0, name, uint.MaxValue));
        Assert.Equal((-1, 0), fixture.Packet(15, (uint)first)); Assert.Equal((-1, 0), fixture.Packet(15, (uint)second));
        var (exclusive, exclusiveError) = fixture.Packet(8, 0, name, uint.MaxValue); Assert.Equal(0, exclusiveError);
        Assert.Equal((0, 202), fixture.Packet(8, 0, name, 123)); Assert.Equal((-1, 0), fixture.Packet(15, (uint)exclusive));
    }
    [Fact]
    public void VolumeLayoutPreservesNativeLockDateAndDiskTypeFields()
    {
        using var fixture = new Fixture(); Assert.Equal((-1, 0), fixture.Packet(25, 0x8700 >> 2));
        var volume = fixture.Bus.ReadLong(0x8700 + 28) << 2;
        Assert.Equal(2u, fixture.Bus.ReadLong(volume + 4)); Assert.Equal(0u, fixture.Bus.ReadLong(volume + 12));
        Assert.True(fixture.Bus.ReadLong(volume + 16) > 0); Assert.Equal(0u, fixture.Bus.ReadLong(volume + 28));
        Assert.Equal(0x444F5300u, fixture.Bus.ReadLong(volume + 32));
        var (first, _) = fixture.Packet(8, 0, fixture.Name(""), unchecked((uint)-2));
        var (second, _) = fixture.Packet(19, (uint)first);
        Assert.Equal((uint)second, fixture.Bus.ReadLong(volume + 28)); Assert.Equal((uint)first, fixture.Bus.ReadLong((uint)second << 2));
        Assert.Equal(volume >> 2, fixture.Bus.ReadLong(((uint)first << 2) + 16));
        Assert.Equal((-1, 0), fixture.Packet(15, (uint)first)); Assert.Equal(0u, fixture.Bus.ReadLong((uint)second << 2));
        Assert.Equal((-1, 0), fixture.Packet(15, (uint)second)); Assert.Equal(0u, fixture.Bus.ReadLong(volume + 28));
        Assert.Equal(0u, fixture.Bus.ReadLong(volume + 12)); Assert.Equal(0x444F5300u, fixture.Bus.ReadLong(volume + 32));
    }
    [Fact]
    public void HostReadOnlyAttributesAndFailedReplacementPreserveTheActiveWritableSession()
    {
        using var fixture = new Fixture(); var path = Path.Combine(fixture.Root, "host-protected"); System.IO.File.WriteAllText(path, "readable");
        System.IO.File.SetAttributes(path, FileAttributes.ReadOnly);
        try
        {
            Assert.Equal((-1, 0), fixture.Packet(1005, 0x8200 >> 2, 0, fixture.Name("host-protected")));
            var handle = fixture.Bus.ReadLong(0x8224); Assert.Equal((8, 0), fixture.Packet(82, handle, 0x8800, 8));
            Assert.Equal((-1, 223), fixture.Packet(87, handle, 0x8800, 1)); Assert.Equal((-1, 0), fixture.Packet(1007, handle));
            Assert.Equal((0, 223), fixture.Packet(1006, 0x8200 >> 2, 0, fixture.Name("host-protected")));
            var (lockKey, lockError) = fixture.Packet(8, 0, fixture.Name("host-protected"), unchecked((uint)-2)); Assert.Equal(0, lockError);
            Assert.Equal((-1, 0), fixture.Packet(1026, 0x8200 >> 2, (uint)lockKey));
            var fromLock = fixture.Bus.ReadLong(0x8224); Assert.Equal((8, 0), fixture.Packet(82, fromLock, 0x8800, 8));
            Assert.Equal((-1, 223), fixture.Packet(87, fromLock, 0x8800, 1)); Assert.Equal((-1, 0), fixture.Packet(1007, fromLock));
        }
        finally { System.IO.File.SetAttributes(path, FileAttributes.Normal); }
        Assert.Equal((-1, 0), fixture.Packet(1006, 0x8200 >> 2, 0, fixture.Name("active"))); var active = fixture.Bus.ReadLong(0x8224);
        Assert.Throws<DirectoryNotFoundException>(() => fixture.Machine.CreateRestartCandidate(new() { HostDirectories = [new("HOST0", "Missing", fixture.Root + ".missing")] }));
        fixture.Bus.CopyToMemory(0x8800, "usable"u8); Assert.Equal((6, 0), fixture.Packet(87, active, 0x8800, 6));
        Assert.Equal((-1, 0), fixture.Packet(1007, active)); Assert.Equal("usable", System.IO.File.ReadAllText(Path.Combine(fixture.Root, "active")));
    }
    [Fact]
    public void ThreeOneHandleOperationsUseBptrContractsAndEnforceExclusiveAccess()
    {
        using var fixture = new Fixture(); System.IO.File.WriteAllText(Path.Combine(fixture.Root, "file"), "hello");
        var name = fixture.Name("file"); var (first, error) = fixture.Packet(8, 0, name, unchecked((uint)-2)); Assert.Equal(0, error);
        var (copy, _) = fixture.Packet(19, (uint)first);
        Assert.Equal((-1, 0), fixture.Packet(40, (uint)first, (uint)copy));
        Assert.Equal((0, 0), fixture.Packet(40, (uint)first, 0));
        Assert.Equal((0, 202), fixture.Packet(1028, 0, (uint)first, uint.MaxValue));
        Assert.Equal((-1, 0), fixture.Packet(15, (uint)copy));
        Assert.Equal((-1, 0), fixture.Packet(1028, 0, (uint)first, uint.MaxValue));
        Assert.Equal((0, 202), fixture.Packet(1005, 0x8200 >> 2, 0, name));
        Assert.Equal((-1, 0), fixture.Packet(1026, 0x8200 >> 2, (uint)first));
        Assert.Equal((0, 211), fixture.Packet(15, (uint)first));
        var handle = fixture.Bus.ReadLong(0x8224);
        Assert.Equal((0, 202), fixture.Packet(1030, handle));
        Assert.Equal((-1, 0), fixture.Packet(1034, handle, 0x8500 >> 2)); Assert.Equal(5u, fixture.Bus.ReadLong(0x857C));
        // ChangeMode is unusual: it receives the handle BPTR, not fh_Arg1.
        Assert.Equal((-1, 0), fixture.Packet(1028, 1, 0x8200 >> 2, unchecked((uint)-2)));
        var (fileLock, _) = fixture.Packet(1030, handle); Assert.True(fileLock > 0);
        Assert.Equal((0, 202), fixture.Packet(1028, 1, 0x8200 >> 2, uint.MaxValue));
        var (parent, _) = fixture.Packet(1031, handle); Assert.True(parent > 0);
        Assert.Equal((-1, 0), fixture.Packet(40, (uint)parent, 0));
        Assert.Equal((-1, 0), fixture.Packet(15, (uint)fileLock)); Assert.Equal((-1, 0), fixture.Packet(15, (uint)parent));
        Assert.Equal((-1, 0), fixture.Packet(1007, handle)); Assert.Equal((0, 211), fixture.Packet(1028, 0, 0, uint.MaxValue));
    }

    [Fact]
    public void DateStampAndMaximumCommentPreserveTheReducedOneThreeInformationBlock()
    {
        using var fixture = new Fixture(); var file = Path.Combine(fixture.Root, "file"); System.IO.File.WriteAllText(file, "date");
        var name = fixture.Name("file"); var comment = fixture.Name(new string('x', 79), 0x8100);
        Assert.Equal((-1, 0), fixture.Packet(28, 0, 0, name, comment));
        fixture.Bus.WriteLong(0x8802, 5000); fixture.Bus.WriteLong(0x8806, 754); fixture.Bus.WriteLong(0x880A, 2800);
        Assert.Equal((-1, 0), fixture.Packet(34, 0, 0, name, 0x8802)); // native V34/V40 send a direct, sometimes word-aligned pointer
        Assert.Equal(new DateTime(1978, 1, 1).AddDays(5000).AddMinutes(754).AddSeconds(56), System.IO.File.GetLastWriteTime(file));
        var (lockKey, _) = fixture.Packet(8, 0, name, unchecked((uint)-2));
        fixture.Bus.WriteLong(0x85E0, 0xDEADBEEF);
        Assert.Equal((-1, 0), fixture.Packet(23, (uint)lockKey, 0x8500 >> 2));
        Assert.Equal(0xDEADBEEFu, fixture.Bus.ReadLong(0x85E0)); Assert.Equal(79, fixture.Bus.ReadByte(0x8590));
        Assert.Equal(5000u, fixture.Bus.ReadLong(0x8584)); Assert.Equal(754u, fixture.Bus.ReadLong(0x8588)); Assert.Equal(2800u, fixture.Bus.ReadLong(0x858C));
        fixture.Bus.WriteLong(0x880A, 3000); Assert.Equal((0, 115), fixture.Packet(34, 0, 0, name, 0x8802));
    }

    [Fact]
    public void EnumerationIsStableAndFreshLookupsSeeHostChangesAndErrors()
    {
        using var fixture = new Fixture();
        System.IO.File.WriteAllText(Path.Combine(fixture.Root, "a"), "a"); System.IO.File.WriteAllText(Path.Combine(fixture.Root, "b"), "b");
        var (directory, _) = fixture.Packet(8, 0, fixture.Name(""), unchecked((uint)-2));
        Assert.Equal((-1, 0), fixture.Packet(23, (uint)directory, 0x8500 >> 2));
        System.IO.File.WriteAllText(Path.Combine(fixture.Root, "c"), "c");
        Assert.Equal((-1, 0), fixture.Packet(24, (uint)directory, 0x8500 >> 2)); Assert.Equal((byte)'a', fixture.Bus.ReadByte(0x8509));
        System.IO.File.Delete(Path.Combine(fixture.Root, "b"));
        Assert.Equal((0, 205), fixture.Packet(24, (uint)directory, 0x8500 >> 2)); Assert.Equal((0, 232), fixture.Packet(24, (uint)directory, 0x8500 >> 2));
        Assert.Equal((-1, 0), fixture.Packet(23, (uint)directory, 0x8500 >> 2));
        Assert.Equal((-1, 0), fixture.Packet(24, (uint)directory, 0x8500 >> 2)); Assert.Equal((-1, 0), fixture.Packet(24, (uint)directory, 0x8500 >> 2));
        Assert.Equal((byte)'c', fixture.Bus.ReadByte(0x8509));
        System.IO.File.WriteAllText(Path.Combine(fixture.Root, LightweightHostDirectory.MetadataName), "{}");
        Assert.Equal((0, 209), fixture.Packet(21, 0, 0, fixture.Name("a"), 1));
        Assert.Equal("{}", System.IO.File.ReadAllText(Path.Combine(fixture.Root, LightweightHostDirectory.MetadataName)));
    }

    [Fact]
    public void HostSharingRestrictionsFailedAllocationsAndResetReleaseAllResources()
    {
        using var fixture = new Fixture(); var path = Path.Combine(fixture.Root, "file"); System.IO.File.WriteAllText(path, "hello"); var name = fixture.Name("file");
        using (var busy = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Assert.Equal((0, 202), fixture.Packet(1005, 0x8200 >> 2, 0, name));
        fixture.FailNextAllocation = true;
        Assert.Equal((0, 103), fixture.Packet(22, 0, fixture.Name("not-created"))); Assert.False(Directory.Exists(Path.Combine(fixture.Root, "not-created")));
        Assert.Equal((-1, 0), fixture.Packet(1006, 0x8200 >> 2, 0, fixture.Name("output"))); var handle = fixture.Bus.ReadLong(0x8224);
        fixture.Bus.CopyToMemory(0x8800, "saved"u8); Assert.Equal((5, 0), fixture.Packet(87, handle, 0x8800, 5));
        fixture.Machine.Reset(); Assert.Equal("saved", System.IO.File.ReadAllText(Path.Combine(fixture.Root, "output")));
        using var reopened = new FileStream(Path.Combine(fixture.Root, "output"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Equal((-1, 211), fixture.Packet(82, handle, 0x8800, 1));
    }
    [Fact]
    public void FilePacketsPersistBytesSeekResizeAndRejectBadBuffersAndStaleHandles()
    {
        using var fixture = new Fixture();
        var name = fixture.Name("proof.txt"); var fh = 0x8200u;
        Assert.Equal((-1, 0), fixture.Packet(1006, fh >> 2, 0, name));
        var handle = fixture.Bus.ReadLong(fh + 36);
        fixture.Bus.CopyToMemory(0x8800, Encoding.ASCII.GetBytes("abcdef"));
        Assert.Equal((6, 0), fixture.Packet(87, handle, 0x8800, 6));
        Assert.Equal((-1, 115), fixture.Packet(87, handle, 0x7FFFF, 6));
        Assert.Equal((6, 0), fixture.Packet(1008, handle, 0, uint.MaxValue));
        Assert.Equal((6, 0), fixture.Packet(82, handle, 0x8900, 9));
        Assert.Equal("abcdef", Encoding.ASCII.GetString(fixture.Machine.ChipRam.Span.Slice(0x8900, 6)));
        Assert.Equal((0, 0), fixture.Packet(82, handle, 0x8900, 1));
        Assert.Equal((-1, 219), fixture.Packet(1008, handle, 10, uint.MaxValue));
        Assert.Equal((3, 0), fixture.Packet(1022, handle, 3, uint.MaxValue));
        Assert.Equal((-1, 0), fixture.Packet(1007, handle));
        Assert.Equal((-1, 211), fixture.Packet(82, handle, 0x8900, 1));
        Assert.Equal("abc", System.IO.File.ReadAllText(Path.Combine(fixture.Root, "proof.txt")));
        Assert.Equal((0, 209), fixture.Packet(987654));
    }

    [Fact]
    public void LockEnumerationMetadataAndMutationsUseNativePacketResults()
    {
        using var fixture = new Fixture(); var folder = fixture.Name("drawer");
        var (directory, error) = fixture.Packet(22, 0, folder); Assert.Equal(0, error);
        Assert.Equal((0, 202), fixture.Packet(16, 0, folder));
        var name = fixture.Name("CON", 0x8100); var fh = 0x8200u;
        Assert.Equal((-1, 0), fixture.Packet(1006, fh >> 2, (uint)directory, name)); var handle = fixture.Bus.ReadLong(fh + 36);
        Assert.Equal((-1, 0), fixture.Packet(1007, handle));
        var file = fixture.Name("drawer/CON"); var comment = fixture.Name("kept comment", 0x8100);
        Assert.Equal((-1, 0), fixture.Packet(28, 0, 0, file, comment));
        Assert.Equal((-1, 0), fixture.Packet(21, 0, 0, file, 0x41));
        fixture.Bus.WriteLong(0x8500 + 224, 0x12345678); // live V34 caller stack, outside its reduced FIB
        Assert.Equal((-1, 0), fixture.Packet(23, (uint)directory, 0x8500 >> 2));
        Assert.Equal((-1, 0), fixture.Packet(24, (uint)directory, 0x8500 >> 2));
        Assert.Equal(0x41u, fixture.Bus.ReadLong(0x8500 + 116)); Assert.Equal(3, fixture.Bus.ReadByte(0x8500 + 8));
        Assert.Equal(0x12345678u, fixture.Bus.ReadLong(0x8500 + 224));
        Assert.Equal((0, 232), fixture.Packet(24, (uint)directory, 0x8500 >> 2));
        Assert.Equal((0, 222), fixture.Packet(16, 0, file));
        Assert.Equal((-1, 0), fixture.Packet(21, 0, 0, file, 0x40));
        var renamed = fixture.Name("drawer/result", 0x8100);
        Assert.Equal((-1, 0), fixture.Packet(17, 0, file, 0, renamed));
        Assert.Equal((-1, 0), fixture.Packet(16, 0, renamed));
        Assert.Equal((-1, 0), fixture.Packet(15, (uint)directory));
        Assert.Equal((0, 211), fixture.Packet(15, (uint)directory));
        fixture.Name("drawer"); Assert.Equal((-1, 0), fixture.Packet(16, 0, folder));
        Assert.False(Directory.Exists(Path.Combine(fixture.Root, "drawer")));
    }

    [Fact]
    public void ReadOnlyMountAndFailedRestartCannotModifyBackingFiles()
    {
        using var fixture = new Fixture(true);
        System.IO.File.WriteAllText(Path.Combine(fixture.Root, "proof.txt"), "original"); var name = fixture.Name("proof.txt");
        Assert.Equal((0, 214), fixture.Packet(1006, 0x8200 >> 2, 0, name));
        Assert.Equal((0, 214), fixture.Packet(16, 0, name));
        Assert.Equal((0, 214), fixture.Packet(21, 0, 0, name, 1));
        Assert.Equal("original", System.IO.File.ReadAllText(Path.Combine(fixture.Root, "proof.txt")));
        Assert.Throws<DirectoryNotFoundException>(() => fixture.Machine.CreateRestartCandidate(new() { HostDirectories = [new("HOST1", "Missing", fixture.Root + ".missing")] }));
        Assert.Equal((-1, 0), fixture.Packet(1005, 0x8200 >> 2, 0, name)); var handle = fixture.Bus.ReadLong(0x8224);
        Assert.Equal((-1, 214), fixture.Packet(87, handle, 0x8800, 1)); Assert.Equal((-1, 0), fixture.Packet(1007, handle));
        fixture.Machine.Reset(); Assert.Equal((0, 211), fixture.Packet(15, 0x4000));
        Assert.False(System.IO.File.Exists(Path.Combine(fixture.Root, LightweightHostDirectory.MetadataName)));
    }

    [Theory]
    [InlineData(uint.MaxValue)] [InlineData(0x3FFFFFFF)] [InlineData(0x80000000)]
    public void InvalidPacketNamesDoNotCreateFiles(uint name)
    {
        using var fixture = new Fixture(); Assert.Equal((0, 115), fixture.Packet(1006, 0x8200 >> 2, 0, name)); Assert.Empty(Directory.GetFiles(fixture.Root));
    }

    private sealed class Fixture : IDisposable
    {
        internal string Root { get; } = LightweightHostDirectoryTests.Temporary();
        internal LightweightA500Machine Machine { get; }
        internal LightweightHdfBus Bus => Machine.HardfileBus!;
        private readonly M68kCpuState _cpu = new(); private uint _allocation = 0x10000;
        internal bool FailNextAllocation;
        internal Fixture(bool readOnly = false, bool highRam = false)
        {
            Machine = new(new() { CpuModel = highRam ? M68kCpuModel.M68020 : M68kCpuModel.M68000,
                FastRamBytes = highRam ? 16 * 1048576 : 0,
                HostDirectories = [new("HOST0", "Exchange", Root, readOnly)] });
            var ram = highRam ? 0x40000000u : 0u;
            if (highRam) { long cycle = 0; new LightweightAcceleratorBus(Machine, M68kCpuModel.M68020).WriteWord(0xFF000044, 0x4000, ref cycle, M68kBusAccessKind.CpuDataWrite); }
            _allocation += ram;
            var fs = Bus.HostFs!; fs.Configure(0xE90000);
            for (var i = 0; i < 4096; i++) Bus.WriteByte(ram + 0x3000u + (uint)i, fs.ReadBoardByte(0xE94000u + (uint)i));
            Bus.WriteLong(4, ram + 0x1000); Bus.WriteLong(ram + 0x117A, ram + 0x1200); Bus.WriteLong(ram + 0x120A, ram + 0x1400);
            Bus.CopyToMemory(ram + 0x1400, Encoding.ASCII.GetBytes("dos.library\0")); Bus.WriteLong(ram + 0x1222, ram + 0x1800); Bus.WriteLong(ram + 0x1818, (ram + 0x1B00) >> 2);
            Bus.WriteLong(ram + 0x204A, ram + 0x204E); Bus.WriteLong(ram + 0x2052, ram + 0x204A);
            _cpu.A[2] = ram + 0x3000; _cpu.A[3] = ram + 0x2800; _cpu.A[5] = ram + 0x2000; _cpu.A[6] = ram + 0x1000;
            Assert.True(Bus.TryInvokeHostGateway(ram + 0x3020, Bus.ReadLong(ram + 0x3022), _cpu));
            Assert.True(Bus.TryInvokeHostGateway(ram + 0x3140, Bus.ReadLong(ram + 0x3142), _cpu));
            _cpu.A[3] = 0x7000; _cpu.A[4] = 0x6000;
            Assert.Equal((-1, 0), Packet(0, 0, 0, (ram + 0x3400) >> 2));
        }
        internal uint Name(string name, uint address = 0x8000)
        { Bus.WriteByte(address, (byte)name.Length); Bus.CopyToMemory(address + 1, Encoding.Latin1.GetBytes(name)); return address >> 2; }
        internal (int Result, int Error) Packet(int type, params uint[] args)
        {
            Bus.ClearMemory(0x6000, 48); Bus.WriteLong(0x6008, (uint)type);
            for (var i = 0; i < args.Length; i++) Bus.WriteLong(0x6014 + (uint)i * 4, args[i]);
            _cpu.D[1] = _cpu.D[2] = 0; Bus.HostFs!.Packet(_cpu);
            if ((int)_cpu.D[0] > 0) { _cpu.D[1] = FailNextAllocation ? 0 : _allocation; FailNextAllocation = false; _allocation += 24; _cpu.D[2] = 1; Bus.HostFs.Packet(_cpu); }
            return (unchecked((int)Bus.ReadLong(0x600C)), (int)Bus.ReadLong(0x6010));
        }
        public void Dispose() { Machine.Dispose(); Directory.Delete(Root, true); }
    }
}
