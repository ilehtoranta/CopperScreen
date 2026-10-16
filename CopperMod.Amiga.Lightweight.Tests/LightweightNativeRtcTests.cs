using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Copper68k;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

public sealed class LightweightNativeRtcTests
{
    // Supplied A500 KS3.1 (40.63). No ROM/media bytes are stored in the repo.
    // NDK 3.1 BATTCLOCK_LIB.FD: Reset=-6, Read=-12, Write=-18 (D0).
    // EXEC_LIB.FD: OpenResource=-498 (A1). All services execute native ROM code.
    [NativeRtcTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void NativeBattClockReadsWritesAndRetainsTimeThroughReset(bool batch)
    {
        var rom = File.ReadAllBytes(Environment.GetEnvironmentVariable("COPPERSCREEN_KICKSTART31_ROM")!);
        Assert.Equal("8C8A0CF04F91B88EAF0C4F1126041987067E2286A8EE590BDBAE447A8000C5EE",
            Convert.ToHexString(SHA256.HashData(rom)));
        var now = new DateTimeOffset(2026, 6, 14, 21, 45, 37, TimeSpan.FromHours(3));
        using var machine = new LightweightA500Machine(new() { FramebufferWidth = 908, RtcEnabled = true, RtcNowProvider = () => now }, batch);
        machine.LoadKickstart(rom);
        Boot(machine);
        var resource = OpenClock(machine);
        var expected = Seconds(now.DateTime);
        Assert.Equal(expected, Invoke(machine, resource, 12));
        var written = Seconds(new DateTime(2028, 2, 29, 13, 24, 56));
        _ = Invoke(machine, resource, 18, written);
        Assert.Equal(written, Invoke(machine, resource, 12));
        now = now.AddSeconds(5);
        Assert.Equal(written + 5, Invoke(machine, resource, 12));
        machine.Reset();
        Boot(machine);
        resource = OpenClock(machine);
        Assert.Equal(written + 5, Invoke(machine, resource, 12));
    }

    private static uint Seconds(DateTime date)
        => checked((uint)(date - new DateTime(1978, 1, 1)).TotalSeconds);

    private static void Boot(LightweightA500Machine machine)
    {
        for (var i = 0; i < 1500; i++) machine.ExecuteFrame();
        Assert.False(machine.RomOverlayEnabled);
        Assert.Null(machine.UnsupportedActiveFeature);
        var exec = BinaryPrimitives.ReadUInt32BigEndian(machine.ChipRam.Span[4..]);
        AssertGuestRam(exec);
    }

    private static uint OpenClock(LightweightA500Machine machine)
    {
        // Original diagnostic guest code in scratch Chip RAM, on the stopped
        // owner with interrupts masked. No native instructions are patched and
        // no OS service is replaced. This machine has no mounted guest media.
        var name = Encoding.ASCII.GetBytes("battclock.resource\0");
        var cycle = machine.Cycle;
        for (var i = 0; i < name.Length; i++)
            machine.WriteByte(0x70100u + (uint)i, name[i], ref cycle, M68kBusAccessKind.CpuDataWrite);
        var resource = Run(machine, [0x2C78, 4, 0x43F9, 7, 0x0100, 0x4EAE, unchecked((ushort)-498)]);
        AssertGuestRam(resource);
        return resource;
    }

    private static void AssertGuestRam(uint address)
        => Assert.True(address is >= 0x400 and < 0x80000 or >= 0xC00000 and < 0xC80000,
            $"Expected guest Chip/slow RAM, got ${address:X8}");

    private static uint Invoke(LightweightA500Machine machine, uint resource, int vector, uint value = 0)
        => Run(machine, [0x2C7C, (ushort)(resource >> 16), (ushort)resource,
            0x203C, (ushort)(value >> 16), (ushort)value, 0x4EAE, unchecked((ushort)-vector)]);

    private static uint Run(LightweightA500Machine machine, ushort[] body)
    {
        var cycle = machine.Cycle;
        ushort[] code = [.. body, 0x4E72, 0x2700];
        for (var i = 0; i < code.Length; i++)
            machine.WriteWord(0x70000u + (uint)i * 2, code[i], ref cycle, M68kBusAccessKind.CpuDataWrite);
        machine.Cpu.Cycles = cycle;
        machine.Cpu.ProgramCounter = 0x70000;
        machine.Cpu.A[7] = 0x7F000;
        machine.Cpu.StatusRegister = 0x2700;
        machine.Cpu.Stopped = false;
        for (var i = 0; i < 10 && !machine.Cpu.Stopped; i++) machine.ExecuteFrame();
        Assert.True(machine.Cpu.Stopped, $"Native RTC call did not return; PC=${machine.Cpu.ProgramCounter:X8}");
        Assert.Equal(0x70000u + (uint)code.Length * 2, machine.Cpu.ProgramCounter);
        Assert.Null(machine.UnsupportedActiveFeature);
        return machine.Cpu.D[0];
    }
}

public sealed class NativeRtcTheoryAttribute : TheoryAttribute
{
    public NativeRtcTheoryAttribute()
    {
        var path = Environment.GetEnvironmentVariable("COPPERSCREEN_KICKSTART31_ROM");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            Skip = "Native RTC coverage unavailable: supply COPPERSCREEN_KICKSTART31_ROM (A500 40.63).";
    }
}
