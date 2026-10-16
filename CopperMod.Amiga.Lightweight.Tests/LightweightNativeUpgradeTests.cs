using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using Copper68k;
using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

public sealed class NativeUpgradeTheoryAttribute : TheoryAttribute
{
    public NativeUpgradeTheoryAttribute()
    {
        if (!File.Exists(Environment.GetEnvironmentVariable("COPPERSCREEN_UPGRADE_A1200_ROM")) ||
            !File.Exists(Environment.GetEnvironmentVariable("COPPERSCREEN_UPGRADE_A500_ROM")))
            Skip = "Set COPPERSCREEN_UPGRADE_A1200_ROM (native ROM or ZIP) and COPPERSCREEN_UPGRADE_A500_ROM (native 3.1).";
    }
}

public sealed class LightweightNativeUpgradeTests
{
    [NativeUpgradeTheory]
    [InlineData(false, M68kCpuModel.M68020, 16, false)]
    [InlineData(false, M68kCpuModel.M68030, 32, false)]
    [InlineData(false, M68kCpuModel.M68040, 64, false)]
    [InlineData(false, M68kCpuModel.M68040, 16, true)]
    [InlineData(false, M68kCpuModel.M68020, 1024, false)]
    [InlineData(true, M68kCpuModel.M68EC020, 8, false)]
    [InlineData(true, M68kCpuModel.M68020, 16, false)]
    [InlineData(true, M68kCpuModel.M68030, 32, false)]
    [InlineData(true, M68kCpuModel.M68040, 64, false)]
    [InlineData(true, M68kCpuModel.M68040, 16, true)]
    [InlineData(true, M68kCpuModel.M68020, 128, false)]
    [InlineData(true, M68kCpuModel.M68020, 256, false)]
    [InlineData(true, M68kCpuModel.M68020, 512, false)]
    [InlineData(true, M68kCpuModel.M68020, 1024, false)]
    public void NativeKickstartDiscoversRamAllocatesGuestBufferAndExecutesBootDisk(bool aga, M68kCpuModel cpu, int mib, bool jit)
    {
        var path = Environment.GetEnvironmentVariable(aga ? "COPPERSCREEN_UPGRADE_A1200_ROM" : "COPPERSCREEN_UPGRADE_A500_ROM")!;
        byte[] rom;
        if (Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using var zip = ZipFile.OpenRead(path);
            using var stream = Assert.Single(zip.Entries, e => Path.GetExtension(e.FullName).Equals(".rom", StringComparison.OrdinalIgnoreCase)).Open();
            using var memory = new MemoryStream(); stream.CopyTo(memory); rom = memory.ToArray();
        }
        else rom = File.ReadAllBytes(path);
        Assert.Equal(524288, rom.Length);
        Assert.Equal(aga ? "293BEEA59D7DE4CAAB3E07223D66E844AD5268EB06A04F405B523A0A1543D64C"
            : "8C8A0CF04F91B88EAF0C4F1126041987067E2286A8EE590BDBAE447A8000C5EE", Convert.ToHexString(SHA256.HashData(rom)));
        Assert.Equal(aga ? (ushort)39 : (ushort)40, BinaryPrimitives.ReadUInt16BigEndian(rom.AsSpan(12)));
        Assert.Equal(aga ? (ushort)106 : (ushort)63, BinaryPrimitives.ReadUInt16BigEndian(rom.AsSpan(14)));
        var disk = new byte[901120]; "DOS"u8.CopyTo(disk);
        // Original guest bootblock: AvailMem(FAST|TOTAL), AllocMem(4,FAST),
        // a write through the returned pointer, then a proof marker and bounded loop.
        ushort[] code = [0x223C, 0x0008, 0x0004, 0x2C78, 0x0004, 0x4EAE, 0xFF28,
            0x23C0, 0x0007, 0x0004, 0x7004, 0x7204, 0x4EAE, 0xFF3A,
            0x23C0, 0x0007, 0x0008, 0x2040, 0x20BC, 0x1234, 0x5678];
        // Mask interrupts before the steady loop. Posted exception-stack writes
        // can finish after instruction retirement; equality is checked only
        // once this controlled guest has drained those transfers.
        code = [..code, 0x4EAE, 0xFF6A, 0x007C, 0x0700]; // Exec.SuperState(); ORI.W #$0700,SR
        if (jit) code = [..code,
            0x4E7A, 0x2003, 0x4E7A, 0x3004, 0x4E7A, 0x4005, 0x4E7A, 0x5006, 0x4E7A, 0x6007,
            // Native 3.x leaves transparent translation enabled. That context
            // deliberately uses interpreter fallback. This takeover guest
            // selects flat memory before checking compiled loop execution.
            0x7000, 0x4E7B, 0x0003, 0x4E7B, 0x0004, 0x4E7B, 0x0005, 0x4E7B, 0x0006, 0x4E7B, 0x0007,
            0x203C, 0, 0x8000, 0x4E7B, 0x0002];
        code = [..code, 0x23FC, 0x5550, 0x4752, 0x0007, 0x0000];
        if (jit) code = [..code, 0x7000, 0x5280, 0x60FC];
        else code = [..code, 0x60FE];
        for (var i = 0; i < code.Length; i++) BinaryPrimitives.WriteUInt16BigEndian(disk.AsSpan(12 + i * 2), code[i]);
        uint sum = 0;
        for (var i = 0; i < 1024; i += 4)
        { var previous = sum; sum = unchecked(sum + BinaryPrimitives.ReadUInt32BigEndian(disk.AsSpan(i))); if (sum < previous) sum++; }
        BinaryPrimitives.WriteUInt32BigEndian(disk.AsSpan(4), ~sum);
        using var m = new LightweightA500Machine(new() { CpuModel = cpu, CpuExecutionMode = jit ? M68kExecutionMode.Jit : M68kExecutionMode.Interpreter,
            AgnusModel = aga ? LightweightAgnusModel.Mos8374Alice : LightweightAgnusModel.Mos8371,
            DeniseModel = aga ? LightweightDeniseModel.Lisa4203 : LightweightDeniseModel.Mos8362,
            ChipRamBytes = aga ? 2097152 : 524288, SlowRamBytes = aga ? 0 : 524288, FastRamBytes = mib * 1048576 });
        m.LoadKickstart(rom); m.MountAdf(disk); m.Reset();
        for (var field = 0; field < 1200 && BinaryPrimitives.ReadUInt32BigEndian(m.ChipRam.Span[0x70000..]) != 0x55504752; field++)
        {
            var previousCpuCycle = m.Cpu.Cycles;
            var previousHardwareCycle = m.Cycle;
            m.ExecuteFrame();
            Assert.True(m.Cpu.Cycles > previousCpuCycle);
            Assert.True(m.Cycle > previousHardwareCycle);
        }
        Assert.Equal(0x55504752u, BinaryPrimitives.ReadUInt32BigEndian(m.ChipRam.Span[0x70000..]));
        // Exec also classifies the A500's trapdoor slow bank as MEMF_FAST.
        Assert.Equal((uint)mib * 1048576 + (aga ? 0u : 524288u), BinaryPrimitives.ReadUInt32BigEndian(m.ChipRam.Span[0x70004..]));
        var buffer = BinaryPrimitives.ReadUInt32BigEndian(m.ChipRam.Span[0x70008..]);
        Assert.NotNull(m.FastRamBase); Assert.InRange(buffer, m.FastRamBase!.Value, m.FastRamBase.Value + (uint)m.FastRam.Length - 4);
        Assert.Equal(0x12345678u, BinaryPrimitives.ReadUInt32BigEndian(m.FastRam.Span[(int)(buffer - m.FastRamBase.Value)..]));
        var compiledBeforeLoop = m.JitStatistics?.CompiledInstructions ?? 0;
        for (var field = 0; field < 4 || jit && field < 120 && m.JitStatistics!.Value.CompiledInstructions <= compiledBeforeLoop; field++) m.ExecuteFrame();
        Assert.False(m.Cpu.Halted); Assert.Null(m.UnsupportedActiveFeature);
        Assert.Equal(0x2700, m.Cpu.StatusRegister & 0x2700);
        if (jit)
        {
            Assert.True(m.JitStatistics!.Value.CompiledInstructions > compiledBeforeLoop,
                $"PC ${m.Cpu.ProgramCounter:X8}, CACR ${m.Cpu.CacheControlRegister:X8}, TC ${m.Cpu.D[2]:X8}, ITT ${m.Cpu.D[3]:X8}/{m.Cpu.D[4]:X8}, DTT ${m.Cpu.D[5]:X8}/{m.Cpu.D[6]:X8}: compiled before {compiledBeforeLoop}, after {m.JitStatistics}");
            Assert.True(m.Cpu.D[0] > 0);
        }
        Assert.True(m.Cpu.Cycles == m.Cycle, $"PC ${m.Cpu.ProgramCounter:X8}, SR ${m.Cpu.StatusRegister:X4}: CPU {m.Cpu.Cycles}, hardware {m.Cycle}");
    }
}
