using System.Diagnostics;
using CopperMod.Amiga.Lightweight;

// Active synchronous gateway-service throughput, not emulated FPS or retention.
// Use a NEW disposable path. Mount/startup, JIT warmup and final flush are excluded.
if (args.Length != 1) throw new ArgumentException("Supply a new disposable HDF path.");
using (var file = new FileStream(args[0], FileMode.CreateNew, FileAccess.Write, FileShare.None)) file.SetLength(4 * 1024 * 1024);
using var machine = new LightweightA500Machine(new() { Hardfiles = [new(0, args[0])] });
var bus = machine.HardfileBus!;
bus.WriteLong(0x1000, 0); bus.WriteLong(0x1118, 0x1000);
bus.WriteLong(0x1124, 512); bus.WriteLong(0x1128, 0x2000);
for (uint i = 0; i < 512; i++) bus.WriteByte(0x2000 + i, (byte)(i * 13 + 37));
void Transfer(ushort command, int count)
{
    bus.WriteWord(0x111C, command);
    for (var i = 0; i < count; i++)
    {
        bus.WriteLong(0x112C, (uint)(i % 8192) * 512);
        if (!bus.CopperHdf.TryExecuteIoRequest(bus, 0x1100) || bus.ReadByte(0x111F) != 0 || bus.ReadLong(0x1120) != 512)
            throw new InvalidOperationException("Incomplete hardfile transfer");
    }
}
Transfer(3, 8192); Transfer(2, 8192);
foreach (var command in new ushort[] { 3, 2 })
{
    const int count = 131072;
    var watch = new Stopwatch();
    var allocated = GC.GetAllocatedBytesForCurrentThread(); watch.Start();
    Transfer(command, count);
    watch.Stop(); allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
    Console.WriteLine($"hdf-service command={command} requests={count} bytes={count * 512L} seconds={watch.Elapsed.TotalSeconds:F6} MiB/s={count / 2048.0 / watch.Elapsed.TotalSeconds:F3} allocated={allocated}");
    if (allocated != 0) throw new InvalidOperationException("Steady-state gateway allocation");
}
for (uint i = 0; i < 512; i++) if (bus.ReadByte(0x2000 + i) != (byte)(i * 13 + 37)) throw new InvalidOperationException("Readback mismatch");
bus.WriteWord(0x111C, 4);
if (!bus.CopperHdf.TryExecuteIoRequest(bus, 0x1100) || bus.ReadByte(0x111F) != 0) throw new InvalidOperationException("Flush failed");
Console.WriteLine("readback=PASS update=PASS; file cache applies; no full guest or presentation throughput claim");
