using System.Buffers.Binary;
using System.Reflection;
using System.Text.Json;
using CopperMod.Amiga.Lightweight;

if (args.Length != 1) throw new ArgumentException("Provide a new output directory under artifacts.");
var output = Path.GetFullPath(args[0]);
if (Directory.Exists(output)) throw new IOException("Use a new output directory to preserve prior evidence.");
Directory.CreateDirectory(output);
var rom = new byte[512 * 1024];
Put(0, [0, 0x1400, 0xF8, 0x0100]);
// Disable overlay/devices and copy independently authored code into chip RAM.
Put(0x100, [0x13FC,3,0xBF,0xE201,0x13FC,2,0xBF,0xE001,
    0x4DF9,0xDF,0xF000,0x3D7C,0x7FFF,0x96,0x3D7C,0x7FFF,0x9A,
    0x3D7C,0x7FFF,0x9C,0x3D7C,0,0x100,
    0x41F9,0xF8,0x0400,0x43F9,0,0x1000,0x303C,0x0203,
    0x22D8,0x51C8,0xFFFC,
    0x47F9,0xC0,0x8000,0x426B,0x073A,0x426B,0x0742,
    0x377C,0xFFF5,0x073E,0x23FC,0,0x1200,0,0x006C,
    0x23FC,0xC0,0x9100,0xC0,0x9004,0x42B9,0xC0,0x9008,
    0x2D7C,0,0x1800,0x0080,0x3D7C,0x8280,0x0096,
    0x3D7C,0xC010,0x009A,0x46FC,0x2000,0x4EF9,0,0x1000]);
// CLR.B flag; JSR poll; TST.W completion; BEQ loop.
Ram(0x1000, [0x422B,0x081A,0x4EB9,0,0x1100,0x4A6B,0x0742,0x67F0]);
// Timer comparison, BTST new-press flag and a counted success path.
Ram(0x1100, [0x302B,0x073A,0xB06B,0x073E,0x640E,
    0x082B,4,0x081A,0x6706,0x6108,0x70FF,0x4E75,0x7000,0x4E75,
    0x52B9,0xC0,0x9008,0x4E75]);
// IRQ saves D0/D1/A0, logs the interrupted PC, sets flag and acknowledges COPER.
Ram(0x1200, [0x48E7,0xC080,0x202F,0x000E,0x2079,0xC0,0x9004,
    0x20C0,0xB1FC,0xC0,0x9500,0x6506,0x41F9,0xC0,0x9100,
    0x23C8,0xC0,0x9004,0x177C,0x0010,0x081A,
    0x3D7C,0x0010,0x009C,0x4CDF,0x0103,0x4E73]);
Ram(0x1800, [0xF801,0xFFFE,0x009C,0x8010,0xFFFF,0xFFFE,0,0]);
// Amiga interrupt-acknowledge ROM vector bytes, required by WinUAE.
Put(0x7FFF0, [24,25,26,27,28,29,30,31]);
File.WriteAllBytes(Path.Combine(output, "polling.rom"), rom);

byte[] priorChip = null, priorSlow = null;
foreach (var batch in new[] { false, true })
{
    using var machine = new LightweightA500Machine(new(), batch);
    machine.LoadKickstart(rom);
    for (var i = 0; i < 100; i++) machine.ExecuteFrame();
    if (machine.Cpu.Halted || machine.UnsupportedActiveFeature != null)
        throw new Exception($"Probe stopped: {machine.UnsupportedActiveFeature}");
    var chip = machine.ChipRam.ToArray();
    // External diagnostic only: keep RAM capture out of production APIs.
    var slow = ((byte[])typeof(LightweightA500Machine)
        .GetField("_slowRam", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(machine)!).ToArray();
    var name = batch ? "normal" : "scalar";
    File.WriteAllBytes(Path.Combine(output, name + ".chipram"), chip);
    File.WriteAllBytes(Path.Combine(output, name + ".slowram"), slow);
    var pcs = Enumerable.Range(0, 100)
        .Select(i => BinaryPrimitives.ReadUInt32BigEndian(slow.AsSpan(0x9100 + i * 4)))
        .GroupBy(pc => pc).ToDictionary(g => g.Key.ToString("X6"), g => g.Count());
    var result = new { mode = name, machine.CompletedFrames, machine.Cycle,
        pc = machine.Cpu.ProgramCounter.ToString("X6"), interruptedPcs = pcs,
        consumedEdges = BinaryPrimitives.ReadUInt32BigEndian(slow.AsSpan(0x9008)) };
    var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(Path.Combine(output, name + ".json"), json);
    Console.WriteLine(json);
    if (priorChip != null && (!priorChip.AsSpan().SequenceEqual(chip) || !priorSlow.AsSpan().SequenceEqual(slow)))
        throw new Exception("Scalar/normal memory mismatch.");
    priorChip = chip; priorSlow = slow;
}

void Ram(int address, ushort[] words) => Put(0x400 + address - 0x1000, words);
void Put(int offset, ushort[] words)
{
    for (var i = 0; i < words.Length; i++)
        BinaryPrimitives.WriteUInt16BigEndian(rom.AsSpan(offset + i * 2), words[i]);
}
