using System.Buffers.Binary;
using Copper68k;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

public sealed class LightweightCd32Tests
{
    private const M68kBusAccessKind Read = M68kBusAccessKind.CpuDataRead;
    private const M68kBusAccessKind Write = M68kBusAccessKind.CpuDataWrite;

    [Theory]
    [InlineData(LightweightCd32Region.Pal, 7_093_790, 142102)]
    [InlineData(LightweightCd32Region.Ntsc, 7_159_090, 119664)]
    public void RegionFitsTheBoardOscillatorAndCadence(LightweightCd32Region region, int frequency, long fieldCycles)
    {
        using var machine = new LightweightA500Machine(LightweightA500Configuration.CreateCd32(region));
        Assert.Equal(region, machine.Cd32Region);
        Assert.Equal(M68kCpuModel.M68EC020, machine.CpuModel);
        Assert.Equal(2 * 1024 * 1024, machine.ChipRam.Length);
        Assert.Equal(0, machine.FloppyDriveCount);
        Assert.False(machine.IsAdfMounted);
        Assert.Equal(frequency, machine.MotherboardClockHz);
        machine.AdvanceHardwareTo(fieldCycles - 1);
        Assert.Equal(0, machine.CompletedFrames);
        machine.AdvanceHardwareTo(fieldCycles);
        Assert.Equal(1, machine.CompletedFrames);
        Assert.Equal(fieldCycles * 48_000 / frequency * 2, machine.AudioSamples.Length);

        var cycle = machine.Cycle;
        machine.WriteWord(0xDFF1DC, 0x0020, ref cycle, Write);
        Assert.Equal(frequency, machine.MotherboardClockHz);
    }

    [Fact]
    public void BootstrapBanksMirrorAndFirstCiaAWriteReleasesTheOverlay()
    {
        using var machine = Create();
        var kickstart = new byte[512 * 1024];
        var extension = new byte[kickstart.Length];
        BinaryPrimitives.WriteUInt32BigEndian(kickstart, 0x0007FFF0);
        BinaryPrimitives.WriteUInt32BigEndian(kickstart.AsSpan(4), 0x00F80100);
        kickstart[0x100] = 0x12;
        extension[0x100] = 0x34;
        Assert.Throws<ArgumentException>(() => machine.LoadCd32Rom(kickstart, extension));
        machine.LoadCd32RomBanks(kickstart, extension);

        long cycle = 0;
        Assert.Equal(0x00F80100u, machine.Cpu.ProgramCounter);
        Assert.Equal(0x1200, machine.ReadWord(0x080100, ref cycle, Read));
        Assert.Equal(0x1200, machine.ReadWord(0xA80100, ref cycle, Read));
        Assert.Equal(0x3400, machine.ReadWord(0xB00100, ref cycle, Read));
        Assert.Equal(0x3400, machine.ReadWord(0xE00100, ref cycle, Read));
        machine.WriteByte(0xBFE401, 0xFF, ref cycle, Write);
        Assert.False(machine.RomOverlayEnabled);
        machine.WriteByte(0xBFE001, 0xFF, ref cycle, Write);
        Assert.False(machine.RomOverlayEnabled);
        machine.WriteWord(0x080100, 0xABCD, ref cycle, Write);
        Assert.Equal(0xABCD, machine.ReadWord(0x080100, ref cycle, Read));
    }

    [Fact]
    public void AkikoTodSelectsItsLineDividerIndependentlyOfTheDisplayField()
    {
        using var machine = Create();
        long cycle = 0;
        machine.WriteLong(0xB80024, 0x00800000, ref cycle, Write);
        machine.WriteByte(0xBFE801, 0, ref cycle, Write);
        machine.AdvanceHardwareTo(262 * 454);
        cycle = machine.Cycle;
        Assert.Equal(1, machine.ReadByte(0xBFE801, ref cycle, Read));
        Assert.Equal(0, machine.CompletedFrames);
        machine.AdvanceHardwareTo(263 * 454);
        cycle = machine.Cycle;
        Assert.Equal(2, machine.ReadByte(0xBFE801, ref cycle, Read));
        Assert.Equal(0, machine.CompletedFrames);
    }

    [Theory]
    [InlineData(0xBF0200u, 0xBFD200u, 0x56FF56FFu)]
    [InlineData(0xBFF200u, 0xBFE200u, 0xFF78FF78u)]
    [InlineData(0xBED200u, 0xBFD200u, 0x56FF56FFu)]
    public void CiaMirrorsSelectTheSameDataDirectionRegister(uint mirror, uint canonical, uint expected)
    {
        using var m = Create();
        long cycle = 0;
        m.WriteLong(mirror, 0x12345678, ref cycle, Write);
        Assert.Equal(expected, m.ReadLong(canonical, ref cycle, Read));
        Assert.Equal(expected, m.ReadLong(mirror, ref cycle, Read));
    }

    [Fact]
    public void OutputDeadlineInsideAnExtendedRasterlineAdvancesToItsBoundary()
    {
        using var machine = new LightweightA500Machine(
            LightweightA500Configuration.CreateCd32(LightweightCd32Region.Ntsc));
        machine.WriteCustomRegisterFromCopper(0x1C0, 227, machine.Cycle);
        machine.WriteCustomRegisterFromCopper(0x1C8, 399, machine.Cycle);
        machine.WriteCustomRegisterFromCopper(0x1DC, 0x0080, machine.Cycle);
        machine.AdvanceHardwareTo(LightweightA500Machine.MaximumOutputIntervalCycles);
        Assert.Equal(0, machine.CompletedFrames);
        var nextLine = 328L * 456;
        Assert.Equal(nextLine, machine.NextFrameCycle);
        machine.AdvanceHardwareTo(nextLine);
        Assert.Equal(1, machine.CompletedFrames);
    }

    [Fact]
    public void AkikoConvertsBigEndianPixelLanesAndAdvancesOncePerReadStrobe()
    {
        using var machine = Create();
        var bus = new LightweightAcceleratorBus(machine, M68kCpuModel.M68EC020);
        long cycle = 0;
        Assert.Equal(0xC0CACAFEu, bus.ReadLong(0xB80000, ref cycle, Read));
        bus.WriteLong(0xB80038, 0x01020408, ref cycle, Write);
        bus.WriteLong(0xB80038, 0x10204080, ref cycle, Write);
        for (var i = 0; i < 6; i++)
            bus.WriteLong(0xB80038, 0, ref cycle, Write);
        Assert.Equal(0x80, bus.ReadByte(0xB80038, ref cycle, Read));
        Assert.Equal(0x4000, bus.ReadWord(0xB80038, ref cycle, Read));
        for (var plane = 2; plane < 8; plane++)
            Assert.Equal(0x80000000u >> plane, bus.ReadLong(0xB80038, ref cycle, Read));
        Assert.Equal(0x80000000u, bus.ReadLong(0xB80038, ref cycle, Read));

        using var a500 = new LightweightA500Machine();
        cycle = 0;
        // Gary selects both CIAs here. A1 does not participate in their register decode.
        var portB = a500.ReadByte(0xBFD000, ref cycle, Read);
        var portA = a500.ReadByte(0xBFE001, ref cycle, Read);
        Assert.Equal((uint)((portB << 8) | portA) * 0x10001u, a500.ReadLong(0xB80000, ref cycle, Read));
    }

    [Theory]
    [InlineData(0x2000u, 4)]
    [InlineData(0x2001u, 8)]
    [InlineData(0x2002u, 8)]
    [InlineData(0x2003u, 8)]
    public void DirectAndGuestChipLongwordsUseTheSameBusCycles(uint address, int clocks)
    {
        using var direct = Create();
        using var guest = Create();
        var bus = new LightweightAcceleratorBus(guest, M68kCpuModel.M68EC020);
        long directCycle = 20, guestCycle = 20;
        direct.WriteLong(address, 0x12345678, ref directCycle, Write);
        bus.WriteLong(address, 0x12345678, ref guestCycle, Write);
        Assert.Equal(20 + clocks, directCycle);
        Assert.Equal(guestCycle, directCycle);
        Assert.Equal(0x12345678u, direct.ReadLong(address, ref directCycle, Read));
        Assert.Equal(0x12345678u, bus.ReadLong(address, ref guestCycle, Read));
        Assert.Equal(20 + 2 * clocks, directCycle);
        Assert.Equal(guestCycle, directCycle);
    }

    [Theory]
    [InlineData(0x37, 4, 0x00FFFFFFu)]
    [InlineData(0x39, 4, 0xFFFFFF00u)]
    [InlineData(0x39, 2, 0xFFFFu)]
    [InlineData(0x3B, 2, 0xFF00u)]
    public void OddAkikoReadAdvancesOnceForTheSelectedPixelLanes(int offset, int size, uint expected)
    {
        using var machine = Create();
        long cycle = 20;
        for (var i = 0; i < 8; i++)
            machine.WriteLong(0xB80038, 0x01030103, ref cycle, Write);
        var address = 0xB80000u + (uint)offset;
        var actual = size == 2 ? machine.ReadWord(address, ref cycle, Read) : machine.ReadLong(address, ref cycle, Read);
        Assert.Equal(expected, actual);
        Assert.Equal(0x55555555u, machine.ReadLong(0xB80038, ref cycle, Read));
    }

    [Theory]
    [InlineData(0x001FFFFDu, 0x1FFFFC, 0xFF112233u, 0x112233FFu, 8)]
    [InlineData(0x001FFFFFu, 0x1FFFFC, 0xFFFFFF11u, 0x11FFFFFFu, 12)]
    [InlineData(0x00FFFFFFu, 0, 0x223344FFu, 0xFF223344u, 8)]
    public void Ec020LongAccessDecodesEachSideOfChipRamBoundary(uint address, int inspectAt, uint expected, uint readback, int cycles)
    {
        using var machine = Create();
        var bus = new LightweightAcceleratorBus(machine, M68kCpuModel.M68EC020);
        long cycle = 16;
        bus.WriteLong((uint)inspectAt, uint.MaxValue, ref cycle, Write);
        var before = cycle;
        bus.WriteLong(address, 0x11223344, ref cycle, Write);
        Assert.Equal(cycles, cycle - before);
        Assert.Equal(expected, BinaryPrimitives.ReadUInt32BigEndian(machine.ChipRam.Span[inspectAt..]));
        before = cycle;
        Assert.Equal(readback, bus.ReadLong(address, ref cycle, Read));
        Assert.Equal(cycles, cycle - before);
    }

    [Theory]
    [InlineData(0x37, 0x00010204u, 0x80000000u)]
    [InlineData(0x39, 0x01020400u, 0x40000000u)]
    public void Ec020OddLongWritesThreePixelLanesInOneStrobe(int offset, uint pixels, uint firstPlane)
    {
        using var machine = Create();
        var rom = new byte[512 * 1024];
        BinaryPrimitives.WriteUInt32BigEndian(rom, 0x0007FFF0);
        BinaryPrimitives.WriteUInt32BigEndian(rom.AsSpan(4), 0x00F80100);
        ushort[] program =
        [
            0x23FC, (ushort)(pixels >> 16), (ushort)pixels, 0x00B8, (ushort)offset, // MOVE.L #pixels,Akiko
            0x2039, 0x00B8, 0x0038, // Read successive planes into D0-D2.
            0x2239, 0x00B8, 0x0038,
            0x2439, 0x00B8, 0x0038,
            0x4E72, 0x2700
        ];
        for (var i = 0; i < program.Length; i++)
            BinaryPrimitives.WriteUInt16BigEndian(rom.AsSpan(0x100 + i * 2), program[i]);
        machine.LoadCd32RomBanks(rom, new byte[rom.Length]);
        machine.ExecuteFrame();
        Assert.True(machine.Cpu.Stopped);
        for (var plane = 0; plane < 3; plane++)
            Assert.Equal(firstPlane >> plane, machine.Cpu.D[plane]);
    }

    [Theory]
    [InlineData(LightweightCd32Region.Pal)]
    [InlineData(LightweightCd32Region.Ntsc)]
    public void Ec020BootCodeCanIdentifyAkikoAndPollACommand(LightweightCd32Region region)
    {
        using var machine = new LightweightA500Machine(LightweightA500Configuration.CreateCd32(region));
        var kickstart = new byte[512 * 1024];
        BinaryPrimitives.WriteUInt32BigEndian(kickstart, 0x0007FFF0);
        BinaryPrimitives.WriteUInt32BigEndian(kickstart.AsSpan(4), 0x00F80100);
        ushort[] program =
        [
            0x13FC, 0x00FF, 0x00BF, 0xE401, // Release boot overlay with a CIA-A write.
            0x2039, 0x00B8, 0x0000,         // Read Akiko ID into D0.
            0x23C0, 0x0000, 0x2000,         // Copy D0 to chip RAM.
            0x13FC, 0x0007, 0x00B8, 0x0028, // Drive information command.
            0x13FC, 0x00F8, 0x00B8, 0x0028, // Checksum.
            0x0839, 0x0005, 0x00B8, 0x0004, // Test receive-ready flag.
            0x67F6,                         // Keep polling while the command is pending.
            0x1239, 0x00B8, 0x0028,         // Read the response tag into D1.
            0x4E72, 0x2700
        ];
        for (var i = 0; i < program.Length; i++)
            BinaryPrimitives.WriteUInt16BigEndian(kickstart.AsSpan(0x100 + i * 2), program[i]);
        machine.LoadCd32RomBanks(kickstart, new byte[512 * 1024]);
        machine.ExecuteFrame();
        Assert.True(machine.Cpu.Stopped);
        Assert.False(machine.RomOverlayEnabled);
        Assert.Equal(0xC0CACAFEu, BinaryPrimitives.ReadUInt32BigEndian(machine.ChipRam.Span[0x2000..]));
        Assert.Equal(7u, machine.Cpu.D[1]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Cd32PadReadsTwoButtonsOrSevenSerialBitsOnEitherPort(int port)
    {
        using var machine = Create();
        long cycle = 0;
        var fire = (byte)(0x40 << port);
        var returnBit = 0x0400 << (4 * port);
        machine.SetCd32PadButtons(port, LightweightCd32Buttons.Red | LightweightCd32Buttons.Blue | LightweightCd32Buttons.Play);
        Assert.Equal(0, machine.ReadByte(0xBFE001, ref cycle, Read) & fire);
        Assert.Equal(0, machine.ReadWord(0xDFF016, ref cycle, Read) & returnBit);

        machine.WriteWord(0xDFF034, (ushort)(0x0200 << (4 * port)), ref cycle, Write);
        machine.WriteByte(0xBFE201, (byte)(fire | 3), ref cycle, Write);
        var expectedHigh = new[] { false, false, true, true, true, true, false, true, false };
        foreach (var high in expectedHigh)
        {
            Assert.Equal(high, (machine.ReadWord(0xDFF016, ref cycle, Read) & returnBit) != 0);
            machine.WriteByte(0xBFE001, fire, ref cycle, Write);
            machine.WriteByte(0xBFE001, 0, ref cycle, Write);
        }
        machine.WriteWord(0xDFF034, 0, ref cycle, Write);
        machine.WriteWord(0xDFF034, (ushort)(0x0200 << (4 * port)), ref cycle, Write);
        machine.SetCd32PadButtons(port, LightweightCd32Buttons.None);
        Assert.NotEqual(0, machine.ReadWord(0xDFF016, ref cycle, Read) & returnBit);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EepromAcknowledgesWhenOneLongWriteLowersClockAndReleasesData(bool threeLanes)
    {
        using var machine = Create();
        long cycle = 0;
        const uint pins = 0xB80030;
        void Drive(uint value)
        {
            if (threeLanes) machine.WriteLong(pins - 1, value >> 8, ref cycle, Write);
            else machine.WriteLong(pins, value, ref cycle, Write);
        }

        Drive(0xC000C000);
        Drive(0x8000C000); // START
        for (var bit = 7; bit >= 0; bit--)
        {
            var data = (uint)((0xA0 >> bit) & 1) << 30;
            Drive(data | 0xC000);
            Drive(data | 0x8000C000);
        }

        Drive(0x00008000); // Falling SCL and SDA direction change on the same strobe.
        Drive(0x80008000);
        Assert.Equal(0x80, machine.ReadByte(pins, ref cycle, Read) & 0xC0);
    }

    [Fact]
    public void EepromBanksWrapPageWritesAndFinishOnTheBoardClock()
    {
        using var machine = Create();
        long cycle = 0;
        const uint pins = 0xB80030;
        const uint direction = 0xB80032;
        void Drive(int value) => machine.WriteByte(pins, (byte)value, ref cycle, Write);
        void SetDirection(int value) => machine.WriteByte(direction, (byte)value, ref cycle, Write);
        void Start() { SetDirection(0xC0); Drive(0xC0); Drive(0x80); Drive(0); }
        void Stop() { SetDirection(0xC0); Drive(0); Drive(0x80); Drive(0xC0); }
        bool Send(int value)
        {
            for (var shift = 7; shift >= 0; shift--)
            {
                var data = ((value >> shift) & 1) << 6;
                Drive(data); Drive(data | 0x80); Drive(data);
            }
            SetDirection(0x80);
            Drive(0xC0);
            var ack = (machine.ReadByte(pins, ref cycle, Read) & 0x40) == 0;
            Drive(0x40);
            SetDirection(0xC0);
            return ack;
        }
        int Receive(bool ack)
        {
            SetDirection(0x80);
            var result = 0;
            for (var bit = 0; bit < 8; bit++)
            {
                Drive(0x80);
                result = (result << 1) | ((machine.ReadByte(pins, ref cycle, Read) >> 6) & 1);
                Drive(0);
            }
            if (ack) SetDirection(0xC0);
            Drive(0x80);
            Drive(0);
            return result;
        }

        Start();
        Assert.True(Send(0xA6));
        Assert.True(Send(0xFF));
        Assert.True(Send(0xAB));
        Assert.True(Send(0xCD));
        Stop();
        var completed = cycle + machine.MotherboardClockHz / 200;
        Assert.False(machine.IsCd32NvramDirty);
        Start();
        Assert.False(Send(0xA6));
        Stop();
        machine.AdvanceHardwareTo(completed);
        cycle = machine.Cycle;
        Assert.True(machine.IsCd32NvramDirty);
        Assert.Equal(0xAB, machine.Cd32Nvram.Span[0x3FF]);
        Assert.Equal(0xCD, machine.Cd32Nvram.Span[0x3F0]);
        Assert.Equal(0, machine.Cd32Nvram.Span[0x0FF]);
        Start();
        Assert.True(Send(0xA6));
        Assert.True(Send(0xFF));
        Start();
        Assert.True(Send(0xA7));
        Assert.Equal(0xAB, Receive(ack: true));
        Assert.Equal(0, Receive(ack: false));
        Stop();
        machine.Reset();
        Assert.Equal(0xCD, machine.Cd32Nvram.Span[0x3F0]);
        machine.MarkCd32NvramSaved();
        Assert.False(machine.IsCd32NvramDirty);
    }

    [Theory]
    [InlineData(0x10000u, 0)]
    [InlineData(0x9FFC00u, 8 * 1024 * 1024)]
    public void CommandAndResponseDmaRetireAtTheRingComparators(uint rings, int fastRamBytes)
    {
        using var machine = Create(fastRamBytes);
        long cycle = machine.Cycle;
        machine.WriteWord(rings + 0x200, 0x00FF, ref cycle, Write); // Checksum-only status command.
        var cd = new AkikoRegisters(machine);
        cd.Long(0x14, rings);
        cd.Long(0x08, 0x18000000);
        cd.Long(0x24, 0x60000000);
        cd.Byte(0x1D, 2);
        cd.Byte(0x1F, 2);
        cd.Advance(3 * 454 - 1);
        Assert.Equal(0, cd.Byte(0x19));
        Assert.Equal(0, cd.Byte(0x1A));
        cd.Advance(700);
        Assert.Equal(2, cd.Byte(0x19));
        Assert.Equal(2, cd.Byte(0x1A));
        cycle = machine.Cycle;
        Assert.Equal(0x00FF, machine.ReadWord(rings, ref cycle, Read));
        if (fastRamBytes != 0) Assert.Equal(0, machine.ReadChipWordDma(rings));
        Assert.Equal(0x18000000u, cd.Long(0x04) & 0x18000000);
        Assert.Equal(8, cd.PaulaInterruptRequests & 8);

        cd.Byte(0x1D, 2);
        Assert.Equal(0x10000000u, cd.Long(0x04) & 0x18000000);
        cd.Byte(0x1F, 2);
        Assert.Equal(0u, cd.Long(0x04) & 0x18000000);
    }

    [Theory]
    [InlineData(0u, 0)]
    [InlineData(0x200000u, 8 * 1024 * 1024)]
    public void IsoReadDeliversEachCdBlockInPriorityOrderAndReportsMediaChanges(uint ramBase, int fastRamBytes)
    {
        var path = Path.Combine(Path.GetTempPath(), $"copper-cd32-{Guid.NewGuid():N}.iso");
        var image = new byte[20 * 2048];
        image[0] = 0xA5;
        image[2048] = 0x5A;
        File.WriteAllBytes(path, image);
        try
        {
            using var machine = Create(fastRamBytes);
            machine.MountCd32Image(path);
            var cd = new AkikoRegisters(machine);
            cd.Advance(454);
            Assert.Equal(new byte[] { 0x0A, 1, 0xF4 }, cd.ReadPacket(3));
            Assert.Throws<ArgumentException>(() => machine.MountCd32Image(Path.ChangeExtension(path, ".nrg")));
            Assert.True(machine.IsCd32DiscMounted);

            cd.Long(0x10, ramBase + 0x20000);
            cd.Long(0x14, ramBase + 0x10000);
            cd.Long(0x24, 0x9C000000);
            cd.Word(0x20, 0x8001);
            cd.SendPacket([0x34, 0, 0x02, 0, 0, 0x02, 2, 0x80, 0, 0, 0, 0]);
            Assert.Equal(new byte[] { 0x34, 2, 0xC9 }, cd.ReadPacket(3));

            cd.Advance(90_000);
            Assert.Equal(0x8001, cd.Word(0x20));
            Assert.Equal(0, DmaWord(0x2F010));
            cd.Advance(10_000);
            Assert.Equal(0x0001, cd.Word(0x20));
            Assert.Equal(0x0002, DmaWord(0x2F00C));
            Assert.Equal(0x0001, DmaWord(0x2F00E));
            Assert.Equal(0xA500, DmaWord(0x2F010));
            Assert.Equal(0xFFFF, DmaWord(0x10160));
            Assert.Equal(0x84000000u, cd.Long(0x04) & 0x84000000);
            Assert.Equal(0, DmaWord(0x20010));
            cd.Advance(100_000);
            Assert.Equal(0, cd.Word(0x20));
            Assert.Equal(0x0001, DmaWord(0x20002));
            Assert.Equal(0x5A00, DmaWord(0x20010));
            if (fastRamBytes != 0) Assert.Equal(0, machine.ReadChipWordDma(0x20010));

            ushort DmaWord(int offset) => BinaryPrimitives.ReadUInt16BigEndian(
                (fastRamBytes == 0 ? machine.ChipRam : machine.FastRam).Span[offset..]);

            machine.EjectCd32Disc();
            cd.Advance(454);
            Assert.False(machine.IsCd32DiscMounted);
            Assert.Equal(new byte[] { 0x0A, 0, 0xF5 }, cd.ReadPacket(3));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void CueTracksReadFromSeparateFilesWithRecordedAndSilentPregaps()
    {
        const int raw = LightweightCd32DiscImage.RawSectorBytes;
        const int waveTail = 4 * 17;
        var directory = Directory.CreateTempSubdirectory("copper-cue-");
        var cue = Path.Combine(directory.FullName, "mixed.cue");
        var iso = Path.Combine(directory.FullName, "disc data.iso");
        var wav = Path.Combine(directory.FullName, "audio.wav");
        var bin = Path.Combine(directory.FullName, "audio.bin");
        try
        {
            var data = new byte[2 * 2048];
            data[2048] = 0x67;
            File.WriteAllBytes(iso, data);
            using (var writer = new BinaryWriter(File.Create(wav)))
            {
                writer.Write("RIFF"u8);
                writer.Write(36 + 12 + 2 * raw + waveTail);
                writer.Write("WAVEJUNK"u8);
                writer.Write(3);
                writer.Write(0);
                writer.Write("fmt "u8);
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)2);
                writer.Write(44100);
                writer.Write(176400);
                writer.Write((short)4);
                writer.Write((short)16);
                writer.Write("data"u8);
                writer.Write(2 * raw + waveTail);
                writer.Write(Enumerable.Repeat((byte)0x38, raw).ToArray());
                writer.Write(Enumerable.Repeat((byte)0x49, raw).ToArray());
                writer.Write(Enumerable.Repeat((byte)0x5A, waveTail).ToArray());
            }
            var binary = new byte[3 * raw];
            binary[0] = 0x72;
            binary[raw] = 0x83;
            binary[2 * raw] = 0x94;
            File.WriteAllBytes(bin, binary);
            File.WriteAllText(cue, """
                FILE "disc data.iso" BINARY
                  TRACK 01 MODE1/2048
                    INDEX 01 00:00:00
                FILE "audio.wav" WAVE
                  TRACK 02 AUDIO
                    PREGAP 00:00:02
                    INDEX 01 00:00:00
                FILE "audio.bin" BINARY
                  TRACK 03 AUDIO
                    INDEX 00 00:00:00
                    INDEX 01 00:00:01
                """);

            using var disc = LightweightCd32DiscImage.Open(cue);
            Assert.Equal(9, disc.SectorCount);
            Assert.Equal(new LightweightCd32DiscImage.Track(1, 0, 0, false), disc.GetTrack(1));
            Assert.Equal(new LightweightCd32DiscImage.Track(2, 4, 2, true), disc.GetTrack(2));
            Assert.Equal(new LightweightCd32DiscImage.Track(3, 7, 6, true), disc.GetTrack(6));

            var sector = new byte[raw];
            ReadSector(1);
            Assert.Equal(new byte[] { 0, 2, 1, 1, 0x67 }, sector[12..17]);
            ReadSector(5);
            Assert.All(sector, value => Assert.Equal(0x49, value));
            ReadSector(2);
            Assert.All(sector, value => Assert.Equal(0, value));
            ReadSector(3);
            Assert.All(sector, value => Assert.Equal(0, value));
            ReadSector(4);
            Assert.All(sector, value => Assert.Equal(0x38, value));
            ReadSector(6);
            Assert.Equal(0x72, sector[0]);
            ReadSector(7);
            Assert.Equal(0x83, sector[0]);
            ReadSector(8);
            Assert.Equal(0x94, sector[0]);
            Assert.False(disc.Request(9));

            void ReadSector(int lba)
            {
                Assert.True(disc.Request(lba));
                Assert.True(disc.CopyRequestedSector(sector));
            }
        }
        finally
        {
            File.Delete(cue);
            File.Delete(iso);
            File.Delete(wav);
            File.Delete(bin);
            directory.Delete();
        }
    }

    [Theory]
    [InlineData(417, 0x90, 10)] // 44.1 kHz rounds the last 468 frames up to one CD sector.
    [InlineData(384, 0x94, 9)]  // 48 kHz converts 640 source frames per CD sector.
    public void CueMp3SectorsUseDecodedDurationAndSupportRandomReads(int mpegFrameBytes, byte rateHeader, int audioSectors)
    {
        var directory = Directory.CreateTempSubdirectory("copper-mp3-cue-");
        var iso = Path.Combine(directory.FullName, "disc.iso");
        var mp3 = Path.Combine(directory.FullName, "audio.mp3");
        var cue = Path.Combine(directory.FullName, "disc.cue");
        try
        {
            File.WriteAllBytes(iso, new byte[2048]);
            // MPEG-1 Layer III stereo, five silent frames with 1152 samples each.
            var encoded = new byte[5 * mpegFrameBytes];
            for (var offset = 0; offset < encoded.Length; offset += mpegFrameBytes)
            {
                encoded[offset] = 0xFF;
                encoded[offset + 1] = 0xFB;
                encoded[offset + 2] = rateHeader;
            }
            File.WriteAllBytes(mp3, encoded);
            File.WriteAllText(cue, """
                FILE "disc.iso" BINARY
                  TRACK 01 MODE1/2048
                    INDEX 01 00:00:00
                FILE "audio.MP3" MP3
                  TRACK 02 AUDIO
                    PREGAP 00:00:02
                    INDEX 01 00:00:00
                """);

            using var disc = LightweightCd32DiscImage.Open(cue);
            const int start = 3;
            Assert.Equal(start + audioSectors, disc.SectorCount);
            Assert.Equal(new LightweightCd32DiscImage.Track(2, start, 1, true), disc.GetTrack(start));
            var sector = new byte[LightweightCd32DiscImage.RawSectorBytes];
            foreach (var lba in new[] { start + audioSectors - 1, start + 3, start + 4, start, 1 })
            {
                Array.Fill(sector, (byte)0xFF);
                Assert.True(disc.Request(lba));
                Assert.True(disc.CopyRequestedSector(sector));
                Assert.All(sector, value => Assert.Equal(0, value));
            }
            Assert.False(disc.Request(start + audioSectors));
        }
        finally
        {
            File.Delete(cue);
            File.Delete(iso);
            File.Delete(mp3);
            directory.Delete();
        }
    }

    [Fact]
    public void CueAudioPlaysAndPausesInTheMachinePcmStream()
    {
        var stem = Path.Combine(Path.GetTempPath(), $"copper disc {Guid.NewGuid():N}");
        var bin = stem + ".bin";
        var cue = stem + ".cue";
        var image = new byte[20 * 2352];
        for (var lba = 0; lba < 4; lba++)
        {
            image.AsSpan(lba * 2352 + 1, 10).Fill(0xFF);
            image[lba * 2352 + 15] = 1;
        }
        for (var sample = 4 * 2352; sample < image.Length; sample += 4)
        {
            BinaryPrimitives.WriteInt16LittleEndian(image.AsSpan(sample), 1200);
            BinaryPrimitives.WriteInt16LittleEndian(image.AsSpan(sample + 2), -2200);
        }
        File.WriteAllBytes(bin, image);
        File.WriteAllText(cue, $"""
            FILE "{Path.GetFileName(bin)}" BINARY
              TRACK 01 MODE1/2352
                INDEX 01 00:00:00
              TRACK 02 AUDIO
                INDEX 01 00:00:04
            """);
        try
        {
            using var machine = Create();
            machine.MountCd32Image(cue);
            var cd = new AkikoRegisters(machine);
            cd.Advance(454);
            Assert.Equal(new byte[] { 0x0A, 1, 0xF4 }, cd.ReadPacket(3));
            cd.SendPacket([4, 0, 0x02, 0x04, 0, 0x02, 0x20, 0, 0, 0, 0, 0]);
            Assert.Equal(new byte[] { 4, 0x42, 0xB9 }, cd.ReadPacket(3));
            machine.ExecuteFrame();
            Assert.Equal(1200, machine.AudioSamples.Span[^2]);
            Assert.Equal(-2200, machine.AudioSamples.Span[^1]);
            cd.Advance(0);
            Assert.Equal(new byte[] { 4, 0x0B, 0xF0 }, cd.ReadPacket(3));

            cd.SendPacket([0x12]);
            Assert.Equal(new byte[] { 0x12, 9, 0xE4 }, cd.ReadPacket(3));
            machine.ExecuteFrame();
            Assert.Equal(0, machine.AudioSamples.Span[^2]);
            Assert.Equal(0, machine.AudioSamples.Span[^1]);
            machine.ExecuteFrame();
            Assert.All(machine.AudioSamples.ToArray(), sample => Assert.Equal(0, sample));

            cd.Advance(0);
            cd.SendPacket([0x23]);
            Assert.Equal(new byte[] { 0x23, 9, 0xD3 }, cd.ReadPacket(3));
            machine.ExecuteFrame();
            Assert.Equal(1200, machine.AudioSamples.Span[^2]);
            Assert.Equal(-2200, machine.AudioSamples.Span[^1]);
        }
        finally
        {
            File.Delete(cue);
            File.Delete(bin);
        }
    }

    [Theory]
    [InlineData(0xB90000u)]
    [InlineData(0xB9FE00u)]
    [InlineData(0xCFFE00u)]
    public void CustomMirrorsStrobeBothWordsOfAnInterruptRegisterAccess(uint mirror)
    {
        using var machine = Create();
        long cycle = 20;
        machine.WriteLong(0xDFF09A, 0x7FFF7FFF, ref cycle, Write);
        machine.WriteLong(mirror + 0x9A, 0x81018002, ref cycle, Write);
        Assert.Equal(0x01010002u, machine.ReadLong(0xDFF01C, ref cycle, Read));
        Assert.Equal(0x01010002u, machine.ReadLong(mirror + 0x1C, ref cycle, Read));
        machine.WriteLong(mirror + 0x9A, 0x00010002, ref cycle, Write);
        Assert.Equal(0x01000000u, machine.ReadLong(0xDFF01C, ref cycle, Read));
    }

    [Fact]
    public void C2pMirrorsReadTheCurrentPlaneAndOnlyThePrimaryPortAdvancesIt()
    {
        using var machine = Create();
        var cd = new AkikoRegisters(machine);
        for (var i = 0; i < 8; i++) cd.Long(0x38, 0x01030103);
        Assert.Equal(uint.MaxValue, cd.Long(0x78));
        Assert.Equal(0xFFFF, cd.Word(0x7FFA));
        Assert.Equal(uint.MaxValue, cd.Long(0x38));
        Assert.Equal(0x55555555u, cd.Long(0x78));
        Assert.Equal(0x55, cd.Byte(0x3B));
        Assert.Equal(0u, cd.Long(0x78));
    }

    [Fact]
    public void PioReadDuringReceiveDmaAcknowledgesTheInterruptAndKeepsThePacket()
    {
        using var machine = Create();
        var cd = new AkikoRegisters(machine);
        cd.SendPacket([0x10]);
        const uint driveRx = 0x20000000;
        cd.Long(0x24, 0x20000000); // RX index equals its comparator, so no DMA byte can retire.
        Assert.Equal(driveRx, cd.Long(0x04) & driveRx);
        Assert.Equal(0u, cd.Long(0x28));
        Assert.Equal(0u, cd.Long(0x04) & driveRx);
        cd.Long(0x24, 0);
        Assert.Equal(new byte[] { 0x10, 0xEF }, cd.ReadPacket(2));
    }

    private sealed class AkikoRegisters(LightweightA500Machine machine)
    {
        private long _cycle = machine.Cycle;
        internal ushort PaulaInterruptRequests => machine.ReadWord(0xDFF01E, ref _cycle, Read);
        internal void Advance(long cycles)
        {
            _cycle = machine.Cycle + cycles;
            machine.AdvanceHardwareTo(_cycle);
        }
        internal byte Byte(uint offset) => machine.ReadByte(0xB80000 + offset, ref _cycle, Read);
        internal void Byte(uint offset, byte value) => machine.WriteByte(0xB80000 + offset, value, ref _cycle, Write);
        internal ushort Word(uint offset) => machine.ReadWord(0xB80000 + offset, ref _cycle, Read);
        internal void Word(uint offset, ushort value) => machine.WriteWord(0xB80000 + offset, value, ref _cycle, Write);
        internal uint Long(uint offset) => machine.ReadLong(0xB80000 + offset, ref _cycle, Read);
        internal void Long(uint offset, uint value) => machine.WriteLong(0xB80000 + offset, value, ref _cycle, Write);
        internal byte[] ReadPacket(int length)
        {
            var bytes = new byte[length];
            for (var i = 0; i < length; i++) bytes[i] = Byte(0x28);
            return bytes;
        }
        internal void SendPacket(ReadOnlySpan<byte> packet)
        {
            byte checksum = 0xFF;
            foreach (var data in packet)
            {
                Byte(0x28, data);
                checksum = unchecked((byte)(checksum - data));
            }
            Byte(0x28, checksum);
            Advance(454);
        }
    }

    private static LightweightA500Machine Create(int fastRamBytes = 0)
    {
        var machine = new LightweightA500Machine(LightweightA500Configuration.CreateCd32() with { FastRamBytes = fastRamBytes });
        if (fastRamBytes != 0)
        {
            long cycle = 0;
            machine.WriteByte(0xE8004A, 0, ref cycle, Write);
            machine.WriteByte(0xE80048, 0x20, ref cycle, Write);
            Assert.Equal(0x200000u, machine.FastRamBase);
        }
        return machine;
    }
}
