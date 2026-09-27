using System.Buffers.Binary;
using System.Reflection;
using System.Text.Json;
using Copper68k;
using CopperMod.Amiga.Lightweight;

if (args.Length != 2) throw new ArgumentException("Provide a new output directory and 68020 or 68ec020.");
var model = args[1].ToLowerInvariant() switch
{
    "68020" => M68kCpuModel.M68020,
    "68ec020" => M68kCpuModel.M68EC020,
    _ => throw new ArgumentException("CPU must be 68020 or 68ec020.")
};
var output = Path.GetFullPath(args[0]);
if (Directory.Exists(output)) throw new IOException("Use a new output directory.");
Directory.CreateDirectory(output);
var rom = new byte[512 * 1024];
Put(0, [0, 0x1400, 0xF8, 0x0100]);
// Disable overlay, DMA and interrupts; copy the independently authored program.
Put(0x100, [0x13FC, 3, 0xBF, 0xE201, 0x13FC, 2, 0xBF, 0xE001,
    0x4DF9, 0xDF, 0xF000, 0x3D7C, 0x7FFF, 0x96, 0x3D7C, 0x7FFF, 0x9A,
    0x41F9, 0xF8, 0x0400, 0x43F9, 0, 0x1000, 0x303C, 0x007F,
    0x22D8, 0x51C8, 0xFFFC, 0x4EF9, 0, 0x1000]);
// Enable I-cache, call a chip-RAM function, replace its MOVEQ instruction,
// call again, then clear the cache and call once more. Expect D1 = 1, 1, 7.
Put(0x400, [0x7001, 0x4E7B, 0x0002,
    0x4EB9, 0, 0x1180, 0x23C1, 0xC0, 0x9000,
    0x33FC, 0x7207, 0, 0x1180,
    0x4EB9, 0, 0x1180, 0x23C1, 0xC0, 0x9004,
    0x7009, 0x4E7B, 0x0002,
    0x4EB9, 0, 0x1180, 0x23C1, 0xC0, 0x9008,
    0x4E72, 0x2700]);
Put(0x580, [0x7201, 0x4E75]);
Put(0x7FFF0, [24, 25, 26, 27, 28, 29, 30, 31]);
File.WriteAllBytes(Path.Combine(output, "chip-cache.rom"), rom);

byte[] previous = null;
foreach (var batch in new[] { false, true })
{
    using var machine = new LightweightA500Machine(new() { CpuModel = model }, batch);
    machine.LoadKickstart(rom);
    machine.ExecuteFrame();
    var slow = (byte[])typeof(LightweightA500Machine)
        .GetField("_slowRam", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(machine)!;
    var words = Enumerable.Range(0, 3)
        .Select(i => BinaryPrimitives.ReadUInt32BigEndian(slow.AsSpan(0x9000 + 4 * i))).ToArray();
    var mode = batch ? "normal" : "scalar";
    var result = new { mode, cpuModel = model.ToString(), machine.Cycle, machine.Cpu.Stopped,
        pc = machine.Cpu.ProgramCounter.ToString("X8"), values = words,
        cacheCoherenceMatches = words.SequenceEqual(new uint[] { 1, 1, 7 }) };
    var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(Path.Combine(output, mode + ".json"), json);
    Console.WriteLine(json);
    if (!machine.Cpu.Stopped || machine.UnsupportedActiveFeature != null)
        throw new Exception("Probe did not reach its final STOP.");
    var captured = slow.AsSpan(0x9000, 12).ToArray();
    if (previous != null && !previous.AsSpan().SequenceEqual(captured))
        throw new Exception("Normal/scalar mismatch.");
    previous = captured;
}

void Put(int offset, ushort[] words)
{
    for (var i = 0; i < words.Length; i++)
        BinaryPrimitives.WriteUInt16BigEndian(rom.AsSpan(offset + i * 2), words[i]);
}
