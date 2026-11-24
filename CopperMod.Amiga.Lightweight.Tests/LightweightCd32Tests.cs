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
        Assert.Equal(uint.MaxValue, a500.ReadLong(0xB80000, ref cycle, Read));
    }

    [Theory]
    [InlineData(0x001FFFFDu, 0x1FFFFC, 0xFF112233u, 8)]
    [InlineData(0x001FFFFFu, 0x1FFFFC, 0xFFFFFF11u, 12)]
    [InlineData(0x00FFFFFFu, 0, 0x223344FFu, 8)]
    public void Ec020LongWriteDecodesEachSideOfChipRamBoundary(uint address, int inspectAt, uint expected, int cycles)
    {
        using var machine = Create();
        var bus = new LightweightAcceleratorBus(machine, M68kCpuModel.M68EC020);
        long cycle = 16;
        bus.WriteLong((uint)inspectAt, uint.MaxValue, ref cycle, Write);
        var before = cycle;
        bus.WriteLong(address, 0x11223344, ref cycle, Write);
        Assert.Equal(cycles, cycle - before);
        Assert.Equal(expected, BinaryPrimitives.ReadUInt32BigEndian(machine.ChipRam.Span[inspectAt..]));
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

    [Fact]
    public void CommandAndResponseDmaRetireAtTheRingComparators()
    {
        using var machine = Create();
        var cd = new AkikoRegisters(machine);
        const uint rings = 0x10000;
        machine.WriteChipWordDma(rings + 0x200, 0x00FF); // Checksum-only status command.
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
        Assert.Equal(0x00FF, machine.ReadChipWordDma(rings));
        Assert.Equal(0x18000000u, cd.Long(0x04) & 0x18000000);
        Assert.Equal(8, cd.PaulaInterruptRequests & 8);

        cd.Byte(0x1D, 2);
        Assert.Equal(0x10000000u, cd.Long(0x04) & 0x18000000);
        cd.Byte(0x1F, 2);
        Assert.Equal(0u, cd.Long(0x04) & 0x18000000);
    }

    [Fact]
    public void IsoReadDeliversEachCdBlockInPriorityOrderAndReportsMediaChanges()
    {
        var path = Path.Combine(Path.GetTempPath(), $"copper-cd32-{Guid.NewGuid():N}.iso");
        var image = new byte[20 * 2048];
        image[0] = 0xA5;
        image[2048] = 0x5A;
        File.WriteAllBytes(path, image);
        try
        {
            using var machine = Create();
            machine.MountCd32Image(path);
            var cd = new AkikoRegisters(machine);
            cd.Advance(454);
            Assert.Equal(new byte[] { 0x0A, 1, 0xF4 }, cd.ReadPacket(3));
            Assert.Throws<ArgumentException>(() => machine.MountCd32Image(Path.ChangeExtension(path, ".nrg")));
            Assert.True(machine.IsCd32DiscMounted);

            cd.Long(0x10, 0x20000);
            cd.Long(0x14, 0x10000);
            cd.Long(0x24, 0x9C000000);
            cd.Word(0x20, 0x8001);
            cd.SendPacket([0x34, 0, 0x02, 0, 0, 0x02, 2, 0x80, 0, 0, 0, 0]);
            Assert.Equal(new byte[] { 0x34, 2, 0xC9 }, cd.ReadPacket(3));

            cd.Advance(90_000);
            Assert.Equal(0x8001, cd.Word(0x20));
            Assert.Equal(0, machine.ReadChipWordDma(0x2F010));
            cd.Advance(10_000);
            Assert.Equal(0x0001, cd.Word(0x20));
            Assert.Equal(0x0002, machine.ReadChipWordDma(0x2F00C));
            Assert.Equal(0x0001, machine.ReadChipWordDma(0x2F00E));
            Assert.Equal(0xA500, machine.ReadChipWordDma(0x2F010));
            Assert.Equal(0xFFFF, machine.ReadChipWordDma(0x10160));
            Assert.Equal(0x84000000u, cd.Long(0x04) & 0x84000000);
            Assert.Equal(0, machine.ReadChipWordDma(0x20010));
            cd.Advance(100_000);
            Assert.Equal(0, cd.Word(0x20));
            Assert.Equal(0x0001, machine.ReadChipWordDma(0x20002));
            Assert.Equal(0x5A00, machine.ReadChipWordDma(0x20010));

            machine.EjectCd32Disc();
            cd.Advance(454);
            Assert.False(machine.IsCd32DiscMounted);
            Assert.Equal(new byte[] { 0x0A, 0, 0xF5 }, cd.ReadPacket(3));
        }
        finally { File.Delete(path); }
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

    private static LightweightA500Machine Create() => new(LightweightA500Configuration.CreateCd32());
}
