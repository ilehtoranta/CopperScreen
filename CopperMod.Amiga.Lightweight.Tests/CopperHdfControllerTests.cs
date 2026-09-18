using CopperDisk;
using Copper68k;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

public sealed class CopperHdfControllerTests
{
	private const uint NullBlock = 0xFFFF_FFFF;
	private const uint Dos0 = 0x444F_5300;
	private const uint Dos6 = 0x444F_5306;

    [Fact]
    public void MalformedRdbListsAreBoundedAndImpossibleBlockSizesAreRejected()
    {
        var path = CreateTempPath();
        try
        {
            var image = new byte[4096];
            WriteBlock(image, 0, CreateRdb(2, uint.MaxValue - 1));
            WriteBlock(image, 2, CreatePart(2, 1, "DH0", CreateEnvironment(Dos0, 0)));
            File.WriteAllBytes(path, image);
            using (var file = AmigaHardfile.Open(new(0, path)))
            {
                Assert.Single(file.GetMountablePartitions());
                Assert.Equal(new uint[] { 2 }, AmigaRigidDiskBlock.FindPartitionBlocks(file));
                Assert.Empty(file.GetRigidDiskBlockFileSystems());
                Assert.Throws<ArgumentOutOfRangeException>(() => file.Read(long.MaxValue, new byte[512]));
            }
            var malformed = CreateRdb(2);
            WriteUInt32(malformed, 16, 0xFFFFFE00); SetChecksum(malformed);
            WriteBlock(image, 0, malformed); File.WriteAllBytes(path, image);
            Assert.Throws<InvalidDataException>(() => AmigaHardfile.Open(new(0, path, mountMode: AmigaHardfileMountMode.RigidDiskBlock)));
            // Failed validation must release the backing handle.
            using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        finally { DeleteTemp(path); }
    }

    [Fact]
    public void LockedReplacementFileLeavesOriginalHardfileUsable()
    {
        var path = CreateTempPath(); var lockedPath = CreateTempPath();
        try
        {
            AmigaHardfile.CreateBlank(path, 32768); AmigaHardfile.CreateBlank(lockedPath, 32768);
            using var original = new LightweightA500Machine(new() { Hardfiles = [new(0, path)] });
            using var locked = new FileStream(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert.Throws<IOException>(() => original.CreateRestartCandidate(new() { Hardfiles = [new(0, path), new(1, lockedPath)] }));
            var backing = original.HardfileBus!.CopperHdf.Hardfiles[0];
            backing.Write(0, new byte[] { 42 }); backing.Flush();
            Assert.Equal(42, backing.ReadBytes(0, 1)[0]);
        }
        finally { DeleteTemp(path); DeleteTemp(lockedPath); }
    }

    [Fact]
    public void RestartCandidateRetainsWritableBackingAndFailedReplacementLeavesOwnerUsable()
    {
        var path = CreateTempPath();
        try
        {
            AmigaHardfile.CreateBlank(path, 32768);
            using var original = new LightweightA500Machine(new() { Hardfiles = [new(0, path)] });
            var backing = original.HardfileBus!.CopperHdf.Hardfiles[0];
            backing.Write(0, new byte[] { 0x12, 0x34 });
            Assert.Throws<FileNotFoundException>(() => original.CreateRestartCandidate(new() { Hardfiles = [new(0, path), new(1, path + ".missing")] }));
            Assert.Equal(new byte[] { 0x12, 0x34 }, backing.ReadBytes(0, 2));
            Assert.Throws<InvalidDataException>(() => original.CreateRestartCandidate(new() { Hardfiles = [new(0, path, mountMode: AmigaHardfileMountMode.RigidDiskBlock)] }));
            using var candidate = original.CreateRestartCandidate(new() { Hardfiles = [new(0, path)] });
            original.Dispose();
            candidate.HardfileBus!.CopperHdf.Hardfiles[0].Write(512, new byte[] { 0x56 });
            candidate.Reset();
            Assert.False(candidate.HardfileBus.CopperHdf.IsConfigured);
            Assert.Equal(0x56, candidate.HardfileBus.CopperHdf.Hardfiles[0].ReadBytes(512, 1)[0]);
            candidate.Dispose();
            Assert.Equal(0x56, File.ReadAllBytes(path)[512]);
            Assert.Throws<IOException>(() => AmigaHardfile.CreateBlank(path, 65536));
            Assert.Equal(32768, new FileInfo(path).Length);
        }
        finally { DeleteTemp(path); }
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void RestartCanChangeWriteProtection(bool oldReadOnly, bool newReadOnly)
    {
        var path = CreateTempPath();
        try
        {
            AmigaHardfile.CreateBlank(path, 32768);
            using var original = new LightweightA500Machine(new() { Hardfiles = [new(0, path, oldReadOnly)] });
            using var candidate = original.CreateRestartCandidate(new() { Hardfiles = [new(0, path, newReadOnly)] });
            original.Dispose();
            var backing = candidate.HardfileBus!.CopperHdf.Hardfiles[0];
            if (newReadOnly) Assert.Throws<UnauthorizedAccessException>(() => backing.Write(0, new byte[512]));
            else backing.Write(0, new byte[512]);
        }
        finally { DeleteTemp(path); }
    }

    [Theory]
    [InlineData(2, 512, 0x7FF80, 0, 251)]
    [InlineData(2, 513, 0x2000, 0, 252)]
    [InlineData(2, 512, 0x2000, 1, 252)]
    [InlineData(2, 512, 0x2000, 32768, 252)]
    [InlineData(3, 512, 0x2000, 0, 28)]
    [InlineData(999, 512, 0x2000, 0, 253)]
    public void InvalidOrReadOnlyRequestsCannotModifyDiskOrGuestBuffer(int command, uint length, uint address, uint offset, int error)
    {
        var path = CreateTempPath();
        try
        {
            AmigaHardfile.CreateBlank(path, 32768);
            using var machine = new LightweightA500Machine(new() { Hardfiles = [new(0, path, true)] });
            var bus = machine.HardfileBus!;
            bus.WriteLong(0x1000, 0); bus.WriteLong(0x1100 + 24, 0x1000);
            bus.WriteWord(0x1100 + 28, (ushort)command); bus.WriteLong(0x1100 + 36, length);
            bus.WriteLong(0x1100 + 40, address); bus.WriteLong(0x1100 + 44, offset);
            bus.CopyToMemory(0x2000, Enumerable.Repeat((byte)0xA5, 512).ToArray());
            Assert.True(bus.CopperHdf.TryExecuteIoRequest(bus, 0x1100));
            Assert.Equal(error, bus.ReadByte(0x1100 + 31)); Assert.Equal(0u, bus.ReadLong(0x1100 + 32));
            Assert.All(machine.ChipRam.Span.Slice(0x2000, 512).ToArray(), b => Assert.Equal(0xA5, b));
            Assert.All(File.ReadAllBytes(path), b => Assert.Equal(0, b));
            Assert.False(bus.CopperHdf.TryExecuteIoRequest(bus, 0x7FFFF));
        }
        finally { DeleteTemp(path); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BeginIoRunsReplyMsgThroughOrdinaryGuestInstructionsUnlessQuick(bool quick)
    {
        var path = CreateTempPath();
        try
        {
            AmigaHardfile.CreateBlank(path, 32768);
            using var machine = new LightweightA500Machine(new() { Hardfiles = [new(0, path)] });
            var rom = new byte[256 * 1024];
            WriteUInt32(rom, 0, 0x7000); WriteUInt32(rom, 4, 0x6000);
            machine.LoadKickstart(rom);
            var bus = machine.HardfileBus!;
            var (_, exec, _, _) = InvokeBootstrap(bus);
            var state = new M68kCpuState(); state.A[1] = 0x5000;
            var open = bus.CopperHdf.DeviceBase - 6;
            Assert.True(bus.TryInvokeHostGateway(open, bus.ReadLong(open + 2), state));
            bus.WriteWord(0x501C, 15); bus.WriteByte(0x501E, quick ? (byte)1 : (byte)0);
            // A minimal Exec ReplyMsg fixture increments a guest counter, then RTS.
            bus.WriteWord(exec - 378, 0x5279); bus.WriteLong(exec - 376, 0x7100); bus.WriteWord(exec - 372, 0x4E75);
            ushort[] code = [0x13FC, 2, 0x00BF, 0xE001, 0x227C, 0, 0x5000, 0x2C7C, 0x1234, 0x5678, 0x4EB9,
                (ushort)((bus.CopperHdf.DeviceBase - 30) >> 16), (ushort)(bus.CopperHdf.DeviceBase - 30), 0x4E72, 0x2700];
            for (var i = 0; i < code.Length; i++) bus.WriteWord(0x6000u + (uint)(i * 2), code[i]);
            machine.ExecuteFrame();
            Assert.True(machine.Cpu.Stopped);
            Assert.Equal(quick ? 0 : 1, bus.ReadWord(0x7100));
            Assert.Equal(0x12345678u, machine.Cpu.A[6]);
            Assert.Equal(0x7000u, machine.Cpu.A[7]);
            Assert.Equal(quick ? 1 : 0, bus.ReadByte(0x501E));
            Assert.Equal(0, bus.ReadByte(0x501F));
        }
        finally { DeleteTemp(path); }
    }

	[Fact]
	public void HardfileReadsWritesAndDetectsChecksumValidRigidDiskBlock()
	{
		var path = CreateTempPath();
		try
		{
			var image = new byte[4 * AmigaHardfile.SectorSize];
			WriteBlock(image, 0, CreateRdb(partitionListBlock: 2));
			WriteBlock(image, 2, CreatePart(NullBlock, flags: 1, "DH0", CreateEnvironment(Dos0, bootPri: 0)));
			File.WriteAllBytes(path, image);

			using var hardfile = AmigaHardfile.Open(new AmigaHardfileConfiguration(0, path));
			var pattern = Enumerable.Range(0, AmigaHardfile.SectorSize).Select(value => (byte)value).ToArray();
			hardfile.Write(AmigaHardfile.SectorSize, pattern);
			var read = new byte[AmigaHardfile.SectorSize];
			hardfile.Read(AmigaHardfile.SectorSize, read);

			Assert.Equal(pattern, read);
			Assert.True(hardfile.HasRigidDiskBlock);
			Assert.Equal(new uint[] { 2 }, AmigaRigidDiskBlock.FindPartitionBlocks(hardfile));
		}
		finally
		{
			DeleteTemp(path);
		}
	}

	[Fact]
	public void RigidDiskBlockParserRejectsBadChecksumsAndSupportsLargeRdbBlocks()
	{
		var badPath = CreateTempPath();
		var largePath = CreateTempPath();
		try
		{
			var badImage = new byte[4 * AmigaHardfile.SectorSize];
			var badRdb = CreateRdb(partitionListBlock: 2);
			badRdb[0x30] ^= 0x5A;
			WriteBlock(badImage, 0, badRdb);
			WriteBlock(badImage, 2, CreatePart(NullBlock, flags: 1, "DH0", CreateEnvironment(Dos0, bootPri: 0)));
			File.WriteAllBytes(badPath, badImage);

			using (var hardfile = AmigaHardfile.Open(new AmigaHardfileConfiguration(0, badPath)))
			{
				Assert.False(hardfile.HasRigidDiskBlock);
				Assert.Empty(AmigaRigidDiskBlock.FindPartitionBlocks(hardfile));
			}

			const int rdbBlockBytes = 1024;
			var largeImage = new byte[4 * rdbBlockBytes];
			WriteRdbBlock(largeImage, 0, rdbBlockBytes, CreateRdb(partitionListBlock: 2, blockBytes: rdbBlockBytes));
			WriteRdbBlock(largeImage, 2, rdbBlockBytes, CreatePart(NullBlock, flags: 1, "DH0", CreateEnvironment(Dos0, bootPri: 0), blockBytes: rdbBlockBytes));
			File.WriteAllBytes(largePath, largeImage);

			using (var hardfile = AmigaHardfile.Open(new AmigaHardfileConfiguration(0, largePath)))
			{
				Assert.True(hardfile.HasRigidDiskBlock);
				Assert.Equal(new uint[] { 2 }, AmigaRigidDiskBlock.FindPartitionBlocks(hardfile));
			}
		}
		finally
		{
			DeleteTemp(badPath);
			DeleteTemp(largePath);
		}
	}

	[Fact]
	public void RigidDiskBlockMountingCreatesPartitionBootNodesWithExactMetadata()
	{
		var path = CreateTempPath();
		try
		{
			var image = new byte[8 * AmigaHardfile.SectorSize];
			WriteBlock(image, 0, CreateRdb(partitionListBlock: 2));
			WriteBlock(image, 2, CreatePart(3, flags: 1, "SYS", CreateEnvironment(Dos6, bootPri: 5, lowCylinder: 2, highCylinder: 9, buffers: 77)));
			WriteBlock(image, 3, CreatePart(4, flags: 0, "Work", CreateEnvironment(Dos0, bootPri: -10, lowCylinder: 10, highCylinder: 19)));
			WriteBlock(image, 4, CreatePart(NullBlock, flags: 2, "Hidden", CreateEnvironment(Dos0, bootPri: 0)));
			File.WriteAllBytes(path, image);

			using var machine = new LightweightA500Machine(new() { Hardfiles = [new AmigaHardfileConfiguration(0, path)] });
			var bus = machine.HardfileBus!;
			var (_, execBase, expansionBase, _) = InvokeBootstrap(bus);

			var firstBootNode = bus.ReadLong(expansionBase + 0x004A);
			var secondBootNode = bus.ReadLong(firstBootNode);
			Assert.NotEqual(0u, firstBootNode);
			Assert.NotEqual(expansionBase + 0x004E, secondBootNode);
			Assert.Equal(5, unchecked((sbyte)bus.ReadByte(firstBootNode + 0x09)));
			Assert.Equal(-128, unchecked((sbyte)bus.ReadByte(secondBootNode + 0x09)));

			var firstDeviceNode = bus.ReadLong(firstBootNode + 0x10);
			var firstStartup = bus.ReadLong(firstDeviceNode + 0x1C) << 2;
			var firstEnvec = bus.ReadLong(firstStartup + 0x08) << 2;
			Assert.Equal(0u, bus.ReadLong(firstStartup + 0x00));
			Assert.Equal(Dos6, bus.ReadLong(firstEnvec + 0x40));
			Assert.Equal(2u, bus.ReadLong(firstEnvec + 0x24));
			Assert.Equal(9u, bus.ReadLong(firstEnvec + 0x28));
			Assert.Equal(77u, bus.ReadLong(firstEnvec + 0x2C));
			Assert.Equal((uint)5, bus.ReadLong(firstEnvec + 0x3C));
			Assert.Equal("SYS", ReadBstr(bus, bus.ReadLong(firstDeviceNode + 0x28) << 2));

			var secondDeviceNode = bus.ReadLong(secondBootNode + 0x10);
			var secondStartup = bus.ReadLong(secondDeviceNode + 0x1C) << 2;
			var secondEnvec = bus.ReadLong(secondStartup + 0x08) << 2;
			Assert.Equal(10u, bus.ReadLong(secondEnvec + 0x24));
			Assert.Equal(19u, bus.ReadLong(secondEnvec + 0x28));
			Assert.Equal("Work", ReadBstr(bus, bus.ReadLong(secondDeviceNode + 0x28) << 2));
			Assert.Equal(bus.CopperHdf.DeviceBase, bus.ReadLong(execBase + 0x015E));
		}
		finally
		{
			DeleteTemp(path);
		}
	}

	[Fact]
	public void RigidDiskBlockFileSystemHeadersLoadIntoFileSystemResource()
	{
		var path = CreateTempPath();
		try
		{
			var hunk = CreateMinimalHunk();
			var image = new byte[10 * AmigaHardfile.SectorSize];
			WriteBlock(image, 0, CreateRdb(partitionListBlock: 2, fileSystemListBlock: 5));
			WriteBlock(image, 2, CreatePart(NullBlock, flags: 1, "SYS", CreateEnvironment(Dos6, bootPri: 0)));
			WriteBlock(image, 5, CreateFshd(6, Dos6, version: 0x0028_0001, lsegBlock: 7));
			WriteBlock(image, 6, CreateFshd(NullBlock, Dos6, version: 0x0027_0001, lsegBlock: 8));
			WriteBlock(image, 7, CreateLseg(NullBlock, hunk));
			WriteBlock(image, 8, CreateLseg(NullBlock, hunk));
			File.WriteAllBytes(path, image);

			using var machine = new LightweightA500Machine(new() { Hardfiles = [new AmigaHardfileConfiguration(0, path)] });
			var bus = machine.HardfileBus!;
			var (_, execBase, _, _) = InvokeBootstrap(bus);

			var resource = bus.ReadLong(execBase + 0x0150);
			Assert.NotEqual(0u, resource);
			Assert.Equal("FileSystem.resource", ReadCString(bus, bus.ReadLong(resource + 0x0A)));

			var entry = bus.ReadLong(resource + 0x12);
			Assert.NotEqual(0u, entry);
			Assert.Equal(Dos6, bus.ReadLong(entry + 0x0E));
			Assert.Equal(0x0028_0001u, bus.ReadLong(entry + 0x12));
			Assert.Equal(0x0000_0180u, bus.ReadLong(entry + 0x16));
			var segList = bus.ReadLong(entry + 0x36);
			Assert.NotEqual(0u, segList);

			var bootNode = bus.CopperHdf.BootNodeAddress;
			var deviceNode = bus.ReadLong(bootNode + 0x10);
			Assert.Equal(segList, bus.ReadLong(deviceNode + 0x20));
			Assert.Equal(0xFFFF_FFFFu, bus.ReadLong(deviceNode + 0x24));
		}
		finally
		{
			DeleteTemp(path);
		}
	}

	[Fact]
	public void AutoconfigMapsCopperHdfBoardIntoAssignedZorroIiBase()
	{
		var path = CreateTempPath();
		try
		{
			AmigaHardfile.CreateBlank(path, 2 * AmigaHardfile.SectorSize);
			using var machine = new LightweightA500Machine(new() { Hardfiles = [new AmigaHardfileConfiguration(0, path)] });
			var bus = machine.HardfileBus!;

			Assert.Equal(0xD0, bus.ReadByte(CopperHdfController.AutoConfigBase));
			Assert.Equal(0xBF, bus.ReadByte(CopperHdfController.AutoConfigBase + 0x04));

			bus.WriteByte(CopperHdfController.AutoConfigBase + 0x48, 0xE0, 0);
			bus.WriteByte(CopperHdfController.AutoConfigBase + 0x4A, 0xA0, 0);

			Assert.True(bus.CopperHdf.IsConfigured);
			Assert.Equal(0x00EA_0000u, bus.CopperHdf.ConfiguredBase);
			Assert.Equal((byte)'c', bus.ReadByte(0x00EA_0100));
			Assert.Equal((byte)'o', bus.ReadByte(0x00EA_0101));
		}
		finally
		{
			DeleteTemp(path);
		}
	}

	[Fact]
	public void AutoconfigMapsKickstartIoSlotValueIntoZorroIiExpansionSpace()
	{
		var path = CreateTempPath();
		try
		{
			AmigaHardfile.CreateBlank(path, 2 * AmigaHardfile.SectorSize);
			using var machine = new LightweightA500Machine(new() { Hardfiles = [new AmigaHardfileConfiguration(0, path)] });
			var bus = machine.HardfileBus!;

			bus.WriteByte(CopperHdfController.AutoConfigBase + 0x48, 0x00, 0);
			bus.WriteByte(CopperHdfController.AutoConfigBase + 0x4A, 0x90, 0);

			Assert.True(bus.CopperHdf.IsConfigured);
			Assert.Equal(0x00E9_0000u, bus.CopperHdf.ConfiguredBase);
			Assert.Equal(0x90, bus.ReadByte(0x00E9_4000));
		}
		finally
		{
			DeleteTemp(path);
		}
	}

	[Fact]
	public void BootstrapDiagnosticAreaSurvivesExpansionCopyToRam()
	{
		var path = CreateTempPath();
		try
		{
			AmigaHardfile.CreateBlank(path, 2 * AmigaHardfile.SectorSize);
			using var machine = new LightweightA500Machine(new() { Hardfiles = [new AmigaHardfileConfiguration(0, path)] });
			var bus = machine.HardfileBus!;

			bus.WriteByte(CopperHdfController.AutoConfigBase + 0x48, 0xE0, 0);
			bus.WriteByte(CopperHdfController.AutoConfigBase + 0x4A, 0xA0, 0);

			Assert.True(bus.CopperHdf.BootstrapInstalled);
			var boardBase = bus.CopperHdf.ConfiguredBase;
			var diagBase = boardBase + CopperHdfController.DiagAreaOffset;
			Assert.Equal(0x90, bus.ReadByte(diagBase));
			Assert.Equal(CopperHdfController.DiagAreaCopySize, bus.ReadWord(diagBase + 0x02));
			Assert.Equal(CopperHdfController.DiagPointOffset, bus.ReadWord(diagBase + 0x04));
			Assert.Equal(CopperHdfController.BootPointOffset, bus.ReadWord(diagBase + 0x06));
			Assert.Equal(CopperHdfController.NameOffset, bus.ReadWord(diagBase + 0x08));
			Assert.Equal(0xFF00, bus.ReadWord(diagBase + CopperHdfController.DiagPointOffset));

			var (copyBase, execBase, expansionBase, configDev) = InvokeBootstrap(bus);

			Assert.True(bus.CopperHdf.DiagBootstrapCalled);
			Assert.True(bus.CopperHdf.BootBootstrapCalled);
			Assert.True(bus.CopperHdf.DeviceRegistered);
			Assert.True(bus.CopperHdf.BootNodeRegistered);
			Assert.True(bus.CopperHdf.ResidentInitCalled);
			Assert.Equal(bus.CopperHdf.DeviceBase, bus.ReadLong(execBase + 0x015E));
			Assert.Equal(bus.CopperHdf.BootNodeAddress, bus.ReadLong(expansionBase + 0x004A));
			Assert.Equal(0, bus.ReadByte(configDev + 0x0E));
			Assert.Equal(bus.CopperHdf.DeviceBase, bus.ReadLong(configDev + 0x28));

			var resident = copyBase + CopperHdfController.ResidentOffset;
			Assert.Equal(0x4AFC, bus.ReadWord(resident));
			Assert.Equal(resident, bus.ReadLong(resident + 0x02));
			Assert.Equal(copyBase + CopperHdfController.ResidentInitOffset + 4u, bus.ReadLong(resident + 0x06));
			Assert.Equal(copyBase + CopperHdfController.NameOffset, bus.ReadLong(resident + 0x0E));
			Assert.Equal(copyBase + CopperHdfController.IdStringOffset, bus.ReadLong(resident + 0x12));

			var bootNode = bus.CopperHdf.BootNodeAddress;
			var deviceNode = bus.CopperHdf.DeviceNodeAddress;
			Assert.Equal(16, bus.ReadByte(bootNode + 0x08));
			Assert.Equal(deviceNode, bus.ReadLong(bootNode + 0x10));
			Assert.Equal(0u, bus.ReadLong(deviceNode + 0x04));
			Assert.NotEqual(0u, bus.ReadLong(deviceNode + 0x1C));
			Assert.Equal(0xFFFF_FFFFu, bus.ReadLong(deviceNode + 0x0C));
			Assert.NotEqual(0u, bus.ReadLong(deviceNode + 0x28));

			var ioAddress = 0x5000u;
			var openState = new M68kCpuState();
			openState.D[0] = 0;
			openState.A[1] = ioAddress;
			openState.A[6] = bus.CopperHdf.DeviceBase;
			var openTrap = bus.CopperHdf.DeviceBase - 6u;
			Assert.True(bus.HasHostGateway(openTrap));
			Assert.True(bus.TryInvokeHostGateway(openTrap, bus.ReadLong(openTrap + 2), openState));
			Assert.Equal(0u, openState.D[0]);
			Assert.Equal(bus.CopperHdf.DeviceBase, bus.ReadLong(ioAddress + 0x14));
			Assert.NotEqual(0u, bus.ReadLong(ioAddress + 0x18));

			bus.WriteWord(ioAddress + 0x1C, 15);
			bus.WriteByte(ioAddress + 0x1E, 1); // This test requests synchronous completion.
			var beginIoTrap = bus.CopperHdf.DeviceBase - 30u;
			openState.A[1] = ioAddress;
			Assert.True(bus.HasHostGateway(beginIoTrap));
			Assert.True(bus.TryInvokeHostGateway(beginIoTrap, bus.ReadLong(beginIoTrap + 2), openState));
			Assert.Equal(0, bus.ReadByte(ioAddress + 0x1F));
			Assert.Equal(0u, bus.ReadLong(ioAddress + 0x20));
		}
		finally
		{
			DeleteTemp(path);
		}
	}

	[Fact]
	public void DeviceIoRequestReadsAndWritesHardfileBlocks()
	{
		var path = CreateTempPath();
		try
		{
			AmigaHardfile.CreateBlank(path, 2 * AmigaHardfile.SectorSize);
			using var machine = new LightweightA500Machine(new() { Hardfiles = [new AmigaHardfileConfiguration(0, path)] });
			var bus = machine.HardfileBus!;
			var unitAddress = 0x1000u;
			var ioAddress = 0x1100u;
			var dataAddress = 0x1200u;
			bus.WriteLong(unitAddress, 0);
			bus.WriteLong(ioAddress + 0x18, unitAddress);
			var source = Enumerable.Range(0, AmigaHardfile.SectorSize).Select(value => (byte)(255 - value)).ToArray();
			bus.CopyToMemory(dataAddress, source);

			bus.WriteWord(ioAddress + 0x1C, 3);
			bus.WriteLong(ioAddress + 0x24, AmigaHardfile.SectorSize);
			bus.WriteLong(ioAddress + 0x28, dataAddress);
			bus.WriteLong(ioAddress + 0x2C, AmigaHardfile.SectorSize);
			Assert.True(bus.CopperHdf.TryExecuteIoRequest(bus, ioAddress));
			Assert.Equal(0, bus.ReadByte(ioAddress + 0x1F));
			Assert.Equal((uint)AmigaHardfile.SectorSize, bus.ReadLong(ioAddress + 0x20));

			bus.ClearMemory(dataAddress, AmigaHardfile.SectorSize);
			bus.WriteWord(ioAddress + 0x1C, 2);
			Assert.True(bus.CopperHdf.TryExecuteIoRequest(bus, ioAddress));

			var destination = new byte[AmigaHardfile.SectorSize];
			bus.CopyFromMemory(dataAddress, destination);
			Assert.Equal(source, destination);
		}
		finally
		{
			DeleteTemp(path);
		}
	}

	[Fact]
	public void Td64ReadsAndWritesBeyondFourGigabytes()
	{
		var path = CreateTempPath();
		try
		{
			AmigaHardfile.CreateBlank(path, 4L * 1024L * 1024L * 1024L + AmigaHardfile.SectorSize);
			using var machine = new LightweightA500Machine(new() { Hardfiles = [new AmigaHardfileConfiguration(0, path)] });
			var bus = machine.HardfileBus!;
			var unitAddress = 0x1000u;
			var ioAddress = 0x1100u;
			var dataAddress = 0x1200u;
			bus.WriteLong(unitAddress, 0);
			bus.WriteLong(ioAddress + 0x18, unitAddress);
			bus.WriteLong(ioAddress + 0x24, AmigaHardfile.SectorSize);
			bus.WriteLong(ioAddress + 0x28, dataAddress);
			bus.WriteLong(ioAddress + 0x20, 1);
			bus.WriteLong(ioAddress + 0x2C, 0);

			var source = Enumerable.Range(0, AmigaHardfile.SectorSize).Select(value => (byte)(value ^ 0xA5)).ToArray();
			bus.CopyToMemory(dataAddress, source);
			bus.WriteWord(ioAddress + 0x1C, 25);
			Assert.True(bus.CopperHdf.TryExecuteIoRequest(bus, ioAddress));
			Assert.Equal(0, bus.ReadByte(ioAddress + 0x1F));
			Assert.Equal((uint)AmigaHardfile.SectorSize, bus.ReadLong(ioAddress + 0x20));

			bus.ClearMemory(dataAddress, AmigaHardfile.SectorSize);
			bus.WriteLong(ioAddress + 0x20, 1);
			bus.WriteLong(ioAddress + 0x2C, 0);
			bus.WriteWord(ioAddress + 0x1C, 24);
			Assert.True(bus.CopperHdf.TryExecuteIoRequest(bus, ioAddress));
			Assert.Equal(0, bus.ReadByte(ioAddress + 0x1F));

			var destination = new byte[AmigaHardfile.SectorSize];
			bus.CopyFromMemory(dataAddress, destination);
			Assert.Equal(source, destination);

			bus.WriteLong(ioAddress + 0x20, 1);
			bus.WriteLong(ioAddress + 0x2C, 0);
			bus.WriteWord(ioAddress + 0x1C, 26);
			Assert.True(bus.CopperHdf.TryExecuteIoRequest(bus, ioAddress));
			Assert.Equal(0, bus.ReadByte(ioAddress + 0x1F));
		}
		finally
		{
			DeleteTemp(path);
		}
	}

	private static (uint CopyBase, uint ExecBase, uint ExpansionBase, uint ConfigDev) InvokeBootstrap(LightweightHdfBus bus)
	{
        if (!bus.CopperHdf.IsConfigured)
        {
            bus.WriteByte(CopperHdfController.AutoConfigBase + 0x48, 0xE0, 0);
            bus.WriteByte(CopperHdfController.AutoConfigBase + 0x4A, 0xA0, 0);
        }
		var boardBase = bus.CopperHdf.ConfiguredBase;
		var diagBase = boardBase + CopperHdfController.DiagAreaOffset;
		var copyBase = 0x3000u;
		for (var i = 0; i < CopperHdfController.DiagAreaCopySize; i++)
		{
			bus.WriteByte(copyBase + (uint)i, bus.ReadByte(diagBase + (uint)i), 0);
		}

		var state = new M68kCpuState();
		var execBase = 0x1000u;
		var configDev = 0x1800u;
		var expansionBase = 0x2000u;
		bus.WriteLong(4, execBase);
		bus.WriteByte(configDev + 0x0E, 0x02, 0);
		state.A[0] = boardBase;
		state.A[2] = copyBase;
		state.A[3] = configDev;
		state.A[5] = expansionBase;
		state.A[6] = execBase;
        state.A[7] = 0x7000;

		var copiedDiagPoint = copyBase + CopperHdfController.DiagPointOffset;
		Assert.True(bus.HasHostGateway(copiedDiagPoint));
		Assert.True(bus.TryInvokeHostGateway(copiedDiagPoint, bus.ReadLong(copiedDiagPoint + 2), state));
		Assert.Equal(1u, state.D[0]);

		var copiedBootPoint = copyBase + CopperHdfController.BootPointOffset;
		Assert.True(bus.HasHostGateway(copiedBootPoint));
		Assert.True(bus.TryInvokeHostGateway(copiedBootPoint, bus.ReadLong(copiedBootPoint + 2), state));
		Assert.Equal(1u, state.D[0]);

		var resident = copyBase + CopperHdfController.ResidentOffset;
		var residentInit = bus.ReadLong(resident + 0x16);
		Assert.True(bus.HasHostGateway(residentInit));
		Assert.True(bus.TryInvokeHostGateway(residentInit, bus.ReadLong(residentInit + 2), state));
		return (copyBase, execBase, expansionBase, configDev);
	}

	private static byte[] CreateRdb(uint partitionListBlock, uint fileSystemListBlock = NullBlock, int blockBytes = AmigaHardfile.SectorSize)
	{
		var block = CreateCheckedBlock(0x5244_534B, blockBytes);
		WriteUInt32(block, 0x10, (uint)blockBytes);
		WriteUInt32(block, 0x18, NullBlock);
		WriteUInt32(block, 0x1C, partitionListBlock);
		WriteUInt32(block, 0x20, fileSystemListBlock);
		WriteUInt32(block, 0x24, NullBlock);
		SetChecksum(block);
		return block;
	}

	private static byte[] CreatePart(uint next, uint flags, string name, IReadOnlyList<uint> environment, int blockBytes = AmigaHardfile.SectorSize)
	{
		var block = CreateCheckedBlock(0x5041_5254, blockBytes);
		WriteUInt32(block, 0x10, next);
		WriteUInt32(block, 0x14, flags);
		WriteBstr(block, 0x24, 32, name);
		for (var i = 0; i < AmigaDosEnvec.LongCount; i++)
		{
			WriteUInt32(block, 0x80 + i * 4, environment[i]);
		}

		SetChecksum(block);
		return block;
	}

	private static byte[] CreateFshd(uint next, uint dosType, uint version, uint lsegBlock)
	{
		var block = CreateCheckedBlock(0x4653_4844, AmigaHardfile.SectorSize);
		WriteUInt32(block, 0x10, next);
		WriteUInt32(block, 0x20, dosType);
		WriteUInt32(block, 0x24, version);
		WriteUInt32(block, 0x28, 0x0000_0180);
		WriteUInt32(block, 0x3C, 4096);
		WriteUInt32(block, 0x40, unchecked((uint)-5));
		WriteUInt32(block, 0x48, lsegBlock);
		WriteUInt32(block, 0x4C, 0xFFFF_FFFF);
		SetChecksum(block);
		return block;
	}

	private static byte[] CreateLseg(uint next, byte[] loadData)
	{
		var summedLongs = 5 + ((loadData.Length + 3) / 4);
		var block = CreateCheckedBlock(0x4C53_4547, AmigaHardfile.SectorSize, (uint)summedLongs);
		WriteUInt32(block, 0x10, next);
		Array.Copy(loadData, 0, block, 0x14, loadData.Length);
		SetChecksum(block);
		return block;
	}

	private static byte[] CreateMinimalHunk()
	{
		var data = new byte[13 * 4];
		var offset = 0;
		WriteUInt32(data, offset, 0x0000_03F3); offset += 4;
		WriteUInt32(data, offset, 0); offset += 4;
		WriteUInt32(data, offset, 1); offset += 4;
		WriteUInt32(data, offset, 0); offset += 4;
		WriteUInt32(data, offset, 0); offset += 4;
		WriteUInt32(data, offset, 1); offset += 4;
		WriteUInt32(data, offset, 0x0000_03E9); offset += 4;
		WriteUInt32(data, offset, 1); offset += 4;
		WriteUInt32(data, offset, 0x4E75_0000); offset += 4;
		WriteUInt32(data, offset, 0x0000_03F2);
		return data[..40];
	}

	private static uint[] CreateEnvironment(uint dosType, int bootPri, uint lowCylinder = 0, uint highCylinder = 9, uint buffers = 30)
		=>
		[
			16,
			128,
			0,
			1,
			1,
			32,
			2,
			0,
			0,
			lowCylinder,
			highCylinder,
			buffers,
			1,
			0x0020_0000,
			0x7FFF_FFFE,
			unchecked((uint)bootPri),
			dosType
		];

	private static byte[] CreateCheckedBlock(uint id, int blockBytes, uint summedLongs = 64)
	{
		var block = new byte[blockBytes];
		WriteUInt32(block, 0x00, id);
		WriteUInt32(block, 0x04, summedLongs);
		WriteUInt32(block, 0x08, 0);
		WriteUInt32(block, 0x0C, 0);
		return block;
	}

	private static void SetChecksum(byte[] block)
	{
		WriteUInt32(block, 0x08, 0);
		var summedLongs = ReadUInt32(block, 0x04);
		var sum = 0u;
		for (var i = 0; i < summedLongs; i++)
		{
			unchecked
			{
				sum += ReadUInt32(block, i * 4);
			}
		}

		WriteUInt32(block, 0x08, unchecked(0u - sum));
	}

	private static void WriteBlock(byte[] image, int block, byte[] data)
		=> Array.Copy(data, 0, image, block * AmigaHardfile.SectorSize, data.Length);

	private static void WriteRdbBlock(byte[] image, int block, int blockBytes, byte[] data)
		=> Array.Copy(data, 0, image, block * blockBytes, data.Length);

	private static void WriteBstr(byte[] data, int offset, int maximumBytes, string value)
	{
		var length = Math.Min(value.Length, maximumBytes - 1);
		data[offset] = (byte)length;
		for (var i = 0; i < length; i++)
		{
			data[offset + 1 + i] = (byte)value[i];
		}
	}

	private static string ReadBstr(LightweightHdfBus bus, uint address)
	{
		var length = bus.ReadByte(address);
		var chars = new char[length];
		for (var i = 0; i < chars.Length; i++)
		{
			chars[i] = (char)bus.ReadByte(address + 1u + (uint)i);
		}

		return new string(chars);
	}

	private static string ReadCString(LightweightHdfBus bus, uint address)
	{
		var chars = new List<char>();
		for (var i = 0u; i < 256; i++)
		{
			var value = bus.ReadByte(address + i);
			if (value == 0)
			{
				break;
			}

			chars.Add((char)value);
		}

		return new string(chars.ToArray());
	}

	private static uint ReadUInt32(byte[] data, int offset)
		=> ((uint)data[offset] << 24) |
			((uint)data[offset + 1] << 16) |
			((uint)data[offset + 2] << 8) |
			data[offset + 3];

	private static void WriteUInt32(byte[] data, int offset, uint value)
	{
		data[offset] = (byte)(value >> 24);
		data[offset + 1] = (byte)(value >> 16);
		data[offset + 2] = (byte)(value >> 8);
		data[offset + 3] = (byte)value;
	}

	private static string CreateTempPath()
		=> Path.Combine(Path.GetTempPath(), "copperhdf-" + Guid.NewGuid().ToString("N") + ".hdf");

	private static void DeleteTemp(string path)
	{
		try
		{
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}
		catch (IOException)
		{
		}
	}
}
