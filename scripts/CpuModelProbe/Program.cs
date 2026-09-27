using Copper68k;
using CopperMod.Amiga.Lightweight;
using System.Security.Cryptography;
using System.Text.Json;

// ROM-free, opt-in dependency readiness checks. Exit 1 means an observed CPU
// blocker remains; these are not assertions of complete CPU conformance.
var results = new List<ProbeResult>();
foreach (var model in new[] { M68kCpuModel.M68000, M68kCpuModel.M68010 })
{
    Run("Agnus bus spacing", model, () =>
    {
        using var machine = new LightweightA500Machine();
        long setup = 0;
        ushort[] code = [0x3028, 0x0020, 0x4228, 0x0022, 0x60F6];
        for (var i = 0; i < code.Length; i++)
            machine.WriteWord(0x1000u + (uint)i * 2, code[i], ref setup, M68kBusAccessKind.CpuDataWrite);
        machine.Reset();
        var bus = new ObservedBus(machine);
        using var cpu = M68kCoreFactory.Default.Create(model, bus);
        cpu.Reset(0x1000, 0x70000);
        cpu.State.A[0] = 0xC02000;
        for (var i = 0; i < 30; i++) cpu.ExecuteInstruction();
        var minimum = bus.Transfers.Zip(bus.Transfers.Skip(1), (a, b) => b - a).Min();
        return (minimum >= 4, $"Minimum strobe separation: {minimum} clocks; expected >=4. Timing callbacks: {bus.TimingCallbacks}.");
    });
    Run("Address-error frame size", model, () =>
    {
        var bus = new RamBus();
        bus.SetWord(0x1000, 0x3211); // MOVE.W (A1),D1; deliberately odd operand.
        bus.SetLong(12, 0x2000);
        using var cpu = M68kCoreFactory.Default.Create(model, bus);
        cpu.Reset(0x1000, 0x3000);
        cpu.State.A[1] = 1;
        cpu.ExecuteInstruction();
        var actual = 0x3000 - cpu.State.A[7];
        var expected = model == M68kCpuModel.M68010 ? 58 : 14;
        return (actual == expected && cpu.State.ProgramCounter == 0x2000,
            $"Frame: {actual} bytes; expected {expected}. Handler PC: {cpu.State.ProgramCounter:X8}.");
    });
}
foreach (var model in Enum.GetValues<M68kCpuModel>())
{
    Run("MOVE.B absolute word to address displacement", model, () =>
    {
        var bus = new RamBus();
        bus.SetWord(0x1000, 0x1378); // MOVE.B ($3000).W,16(A1)
        bus.SetWord(0x1002, 0x3000);
        bus.SetWord(0x1004, 0x0010);
        bus.SetWord(0x3000, 0x5A00);
        using var cpu = M68kCoreFactory.Default.Create(model, bus);
        cpu.Reset(0x1000, 0x7000);
        cpu.State.A[1] = 0x4000;
        cpu.ExecuteInstruction();
        long cycle = 0;
        var value = bus.ReadByte(0x4010, ref cycle, M68kBusAccessKind.CpuDataRead);
        return (value == 0x5A && cpu.State.ProgramCounter == 0x1006,
            $"Destination: {value:X2}; expected 5A. PC: {cpu.State.ProgramCounter:X8}; expected 00001006.");
    });
}
foreach (var model in new[] { M68kCpuModel.M68020, M68kCpuModel.M68EC020 })
{
    Run("Enable instruction cache with IM68kBus only", model, () =>
    {
        var bus = new RamBus();
        bus.SetWord(0xC01000, 0x7001); // MOVEQ #1,D0
        bus.SetWord(0xC01002, 0x4E7B); // MOVEC D0,CACR
        bus.SetWord(0xC01004, 0x0002);
        bus.SetWord(0xC01006, 0x4E71); // NOP in cacheable slow RAM
        bus.SetWord(0xC01008, 0x4E71);
        using var cpu = M68kCoreFactory.Default.Create(model, bus);
        cpu.Reset(0xC01000, 0x7000);
        for (var i = 0; i < 4; i++) cpu.ExecuteInstruction();
        return (cpu.State.ProgramCounter == 0xC0100A,
            $"PC: {cpu.State.ProgramCounter:X8}; expected 00C0100A. CACR: {cpu.State.CacheControlRegister:X8}.");
    });
}
var assembly = typeof(M68kCoreFactory).Assembly;
var report = new
{
    copper68kAssemblyVersion = assembly.GetName().Version?.ToString(),
    copper68kSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))),
    results
};
Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
return results.All(r => r.Passed) ? 0 : 1;

void Run(string check, M68kCpuModel model, Func<(bool Passed, string Detail)> probe)
{
    try
    {
        var result = probe();
        results.Add(new(check, model.ToString(), result.Passed, result.Detail));
    }
    catch (Exception e)
    {
        results.Add(new(check, model.ToString(), false, $"{e.GetType().FullName}: {e.Message}"));
    }
}
record ProbeResult(string Check, string Model, bool Passed, string Detail);

sealed class ObservedBus(LightweightA500Machine machine) : IM68kBus, IM68000BusCycleTiming
{
    readonly IM68000BusCycleTiming timing = machine;
    public List<long> Transfers { get; } = [];
    public int TimingCallbacks;
    public int M68000BusCycleStartDelay => timing.M68000BusCycleStartDelay;
    public bool RequiresExactM68000PipelineFallback => timing.RequiresExactM68000PipelineFallback;
    public M68000BusAccessTiming GetM68000BusAccessTiming(uint a, M68kOperandSize s, M68kBusAccessKind k, bool write, long requested, long completed)
    { TimingCallbacks++; return timing.GetM68000BusAccessTiming(a, s, k, write, requested, completed); }
    public byte ReadByte(uint a, ref long c, M68kBusAccessKind k) { var v = machine.ReadByte(a, ref c, k); Transfers.Add(c - 2); return v; }
    public ushort ReadWord(uint a, ref long c, M68kBusAccessKind k) { var v = machine.ReadWord(a, ref c, k); Transfers.Add(c - 2); return v; }
    public uint ReadLong(uint a, ref long c, M68kBusAccessKind k) => machine.ReadLong(a, ref c, k);
    public void WriteByte(uint a, byte v, ref long c, M68kBusAccessKind k) { machine.WriteByte(a, v, ref c, k); Transfers.Add(c - 2); }
    public void WriteWord(uint a, ushort v, ref long c, M68kBusAccessKind k) { machine.WriteWord(a, v, ref c, k); Transfers.Add(c - 2); }
    public void WriteLong(uint a, uint v, ref long c, M68kBusAccessKind k) => machine.WriteLong(a, v, ref c, k);
    public void ResetExternalDevices(long c) => machine.ResetExternalDevices(c);
}

// Deliberately implements only the documented IM68kBus requirement. Mirrored
// storage lets the same fixture execute at a cacheable motherboard address.
sealed class RamBus : IM68kBus
{
    readonly byte[] memory = new byte[65536];
    public void SetWord(uint a, ushort v)
    { memory[a & 0xFFFF] = (byte)(v >> 8); memory[(a + 1) & 0xFFFF] = (byte)v; }
    public void SetLong(uint a, uint v) { SetWord(a, (ushort)(v >> 16)); SetWord(a + 2, (ushort)v); }
    public byte ReadByte(uint a, ref long c, M68kBusAccessKind k) => memory[a & 0xFFFF];
    public ushort ReadWord(uint a, ref long c, M68kBusAccessKind k) => (ushort)((ReadByte(a, ref c, k) << 8) | ReadByte(a + 1, ref c, k));
    public uint ReadLong(uint a, ref long c, M68kBusAccessKind k) => ((uint)ReadWord(a, ref c, k) << 16) | ReadWord(a + 2, ref c, k);
    public void WriteByte(uint a, byte v, ref long c, M68kBusAccessKind k) => memory[a & 0xFFFF] = v;
    public void WriteWord(uint a, ushort v, ref long c, M68kBusAccessKind k) => SetWord(a, v);
    public void WriteLong(uint a, uint v, ref long c, M68kBusAccessKind k) => SetLong(a, v);
    public void ResetExternalDevices(long c) { }
}
