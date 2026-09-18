using System.Buffers.Binary;
using CopperMod.Amiga.Lightweight;

// ROM-free architectural probe for the development pin's vector-9 dispatch.
// The published 1.4.1-boundary.1 fails this check; scalar and batch must both pass.
var failed = false;
foreach (var batch in new[] { false, true })
{
    using var machine = new LightweightA500Machine(new(), batch);
    var rom = new byte[256 * 1024];
    BinaryPrimitives.WriteUInt32BigEndian(rom, 0x70000);
    BinaryPrimitives.WriteUInt32BigEndian(rom.AsSpan(4), 0xFC0100);
    // Disable overlay; vector 9 -> FC0140; set T; NOP; failure marker; STOP.
    ushort[] code = [0x13FC, 2, 0x00BF, 0xE001, 0x21FC, 0x00FC, 0x0140, 0x0024,
        0x007C, 0x8000, 0x4E71, 0x7001, 0x4E72, 0x2700];
    for (var i = 0; i < code.Length; i++) BinaryPrimitives.WriteUInt16BigEndian(rom.AsSpan(0x100 + i * 2), code[i]);
    ushort[] handler = [0x702A, 0x4E72, 0x2700];
    for (var i = 0; i < handler.Length; i++) BinaryPrimitives.WriteUInt16BigEndian(rom.AsSpan(0x140 + i * 2), handler[i]);
    machine.LoadKickstart(rom); machine.ExecuteFrame();
    var passed = machine.Cpu.D[0] == 42 && machine.Cpu.A[7] == 0x6FFFA;
    Console.WriteLine($"trace-vector-9 batch={batch} result={(passed ? "PASS" : "BLOCKED")} D0={machine.Cpu.D[0]:X8} A7={machine.Cpu.A[7]:X8} PC={machine.Cpu.ProgramCounter:X8} SR={machine.Cpu.StatusRegister:X4}; expected D0=0000002A A7=0006FFFA");
    failed |= !passed;
}
return failed ? 1 : 0;
