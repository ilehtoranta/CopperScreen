using System.Buffers.Binary;
using System.Security.Cryptography;
using Copper68k;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

public sealed class LightweightNativeCpuTests
{
    [NativeDiskFormatTheory]
    [InlineData(M68kCpuModel.M68010, false, true)]
    [InlineData(M68kCpuModel.M68010, false, false)]
    [InlineData(M68kCpuModel.M68040, false, true)]
    [InlineData(M68kCpuModel.M68040, true, true)]
    public void KickstartLoadsOriginalBootCodeWithSelectedCpu(M68kCpuModel model, bool jit, bool batch)
    {
        var rom = File.ReadAllBytes(Environment.GetEnvironmentVariable("COPPERSCREEN_KICKSTART13_ROM")!);
        Assert.Equal("EE05862D8102A08436AC4056DA7D549DB31625C7D47B24DFB7B3C9A5C113CA53", Convert.ToHexString(SHA256.HashData(rom)));
        var disk = new byte[901120]; "DOS"u8.CopyTo(disk);
        // Original guest code, loaded and called by unmodified Kickstart.
        ushort[] code = jit
            ? [0x23FC, 0x4350, 0x5542, 0x0007, 0x0000, 0x203C, 0, 0x8000, 0x4E7B, 0x0002, 0x7000, 0x5280, 0x60FC]
            : [0x23FC, 0x4350, 0x5542, 0x0007, 0x0000, 0x7000, 0x5280, 0x60FC];
        for (var i = 0; i < code.Length; i++) BinaryPrimitives.WriteUInt16BigEndian(disk.AsSpan(12 + i * 2), code[i]);
        uint sum = 0;
        for (var i = 0; i < 1024; i += 4)
        {
            var previous = sum; sum = unchecked(sum + BinaryPrimitives.ReadUInt32BigEndian(disk.AsSpan(i)));
            if (sum < previous) sum = unchecked(sum + 1);
        }
        BinaryPrimitives.WriteUInt32BigEndian(disk.AsSpan(4), ~sum);
        using var machine = new LightweightA500Machine(new() { CpuModel = model,
            CpuExecutionMode = jit ? M68kExecutionMode.Jit : M68kExecutionMode.Interpreter }, batch);
        machine.LoadKickstart(rom); machine.MountAdf(disk); machine.Reset();
        for (var frame = 0; frame < 1000 && BinaryPrimitives.ReadUInt32BigEndian(machine.ChipRam.Span[0x70000..]) != 0x43505542; frame++)
            machine.ExecuteFrame();
        Assert.Equal(0x43505542u, BinaryPrimitives.ReadUInt32BigEndian(machine.ChipRam.Span[0x70000..]));
        for (var frame = 0; frame < 4; frame++) machine.ExecuteFrame();
        Assert.True(machine.Cpu.D[0] > 0); Assert.False(machine.Cpu.Halted);
        Assert.Null(machine.UnsupportedActiveFeature); Assert.Equal(machine.Cycle, machine.Cpu.Cycles);
        if (jit) Assert.True(machine.JitStatistics!.Value.CompiledInstructions > 0, machine.JitStatistics.ToString());
    }
}
