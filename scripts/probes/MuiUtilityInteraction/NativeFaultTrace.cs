using System.Reflection;
using System.Text.Json;
using Copper68k;
using CopperMod.Amiga.Lightweight;

// Diagnostic scalar execution follows the engine's interrupt/device order.
// It observes instruction boundaries without patching guest memory or registers.
// This optional trace is distinct from the normal native replay verdict.
sealed class NativeFaultTrace
{
    private readonly LightweightA500Machine machine;
    private readonly string output;
    private readonly IM68kCore core;
    private readonly Action<long> advance;
    private readonly Func<long, long> clamp;
    private readonly Func<bool> dispatch;
    private readonly Func<long> boundary;
    private readonly PeekWord peek;
    private readonly uint? watchAddress;
    private readonly uint? expected;
    private readonly Entry[] ring = new Entry[2048];
    private long count;
    private uint libraryClose;
    private uint libraryCloseTarget;
    private uint alertVector;
    private uint alertTarget;
    private uint programBase;
    private readonly List<object> programEvents = new();
    public bool StoppedAtInvalidPc { get; private set; }
    public string Reason { get; private set; } = "DIAGNOSTIC_TRACE_INVALID_PC";

    public void WatchLibraryClose(uint library)
    {
        if (libraryClose != 0 || library < 12) return;
        libraryClose = library - 12;
        libraryCloseTarget = ReadOptionalLong(libraryClose + 2);
        alertVector = ReadOptionalLong(4) - 108; // SDK ExecLvo.Alert
        alertTarget = ReadOptionalLong(alertVector + 2);
        // Original MUIJoin 1.0 main prologue, containing only relative operands.
        var signature = Convert.FromHexString("200f90bc00000020b0ac04b065002196594f48e721367e01");
        var index = machine.FastRam.Span.IndexOf(signature);
        if (index < 0 || machine.FastRamBase is not uint b) throw new InvalidDataException("Original MUIJoin main signature unavailable");
        if (machine.FastRam.Span[(index+1)..].IndexOf(signature) >= 0) throw new InvalidDataException("Ambiguous original MUIJoin main signature");
        programBase = b + (uint)index - 0x102c;
    }

    public NativeFaultTrace(LightweightA500Machine machine, string output,
        uint? watchAddress = null, uint? expected = null)
    {
        this.machine = machine; this.output = output;
        this.watchAddress = watchAddress; this.expected = expected;
        var t = machine.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        core = (IM68kCore)t.GetField("_cpu", flags)!.GetValue(machine)!;
        advance = t.GetMethod("AdvanceHardwareTo", flags)!.CreateDelegate<Action<long>>(machine);
        clamp = t.GetMethod("ClampCpuBatchTarget", flags)!.CreateDelegate<Func<long,long>>(machine);
        dispatch = t.GetMethod("DispatchPendingCpuInterrupt", flags)!.CreateDelegate<Func<bool>>(machine);
        boundary = t.GetProperty("NextFrameCycle", flags)!.GetMethod!.CreateDelegate<Func<long>>(machine);
        peek = t.GetMethod("TryPeekInstructionWord", flags)!.CreateDelegate<PeekWord>(machine);
    }

    public void ExecuteFrame()
    {
        var target = machine.CompletedFrames + 1;
        var c = machine.Cpu;
        while (machine.CompletedFrames < target && !StoppedAtInvalidPc)
        {
            var limit = boundary();
            if (c.Halted) { advance(limit); c.Cycles = Math.Max(c.Cycles, limit); continue; }
            if (c.Stopped)
            {
                var wake = ((c.StatusRegister >> 8) & 7) >= 6 ? limit : clamp(limit);
                advance(wake); c.Cycles = Math.Max(c.Cycles, wake); dispatch(); continue;
            }
            if (dispatch()) continue;
            while (c.Cycles < limit && !StoppedAtInvalidPc)
            {
                if (dispatch()) continue;
                if (c.Halted || c.Stopped) break;
                var mapped = peek(c.ProgramCounter, out var op);
                ring[count++ % ring.Length] = new(machine.CompletedFrames, c.Cycles,
                    c.ProgramCounter, op, c.StatusRegister, c.A[7], c.D[0], c.D[1],
                    c.D[2], c.D[3], c.D[4], c.D[5], c.D[6], c.D[7],
                    c.A[0], c.A[1], c.A[2], c.A[3], c.A[4], c.A[5], c.A[6]);
                var offset = c.ProgramCounter - programBase;
                if (programBase != 0 && programEvents.Count < 128 &&
                    offset is 0x10f0 or 0x115c or 0x117e or 0x118a or 0x11a0 or 0x11c2 or 0x11ce or 0x11ec or 0x1352 or 0x31d0)
                {
                    programEvents.Add(new { field = machine.CompletedFrames, pc = c.ProgramCounter,
                        fileOffset = offset, d0 = c.D[0], d7 = c.D[7], a4 = c.A[4], sp = c.A[7],
                        stringPointer = ReadOptionalLong(c.A[4] + 0x492),
                        length = ReadOptionalLong(c.A[4] + 0x496), buffer = ReadOptionalLong(c.A[4] + 0x49a) });
                    File.WriteAllText(Path.Combine(output, "original-program-events.json"),
                        JsonSerializer.Serialize(new { programBase, programEvents }, new JsonSerializerOptions { WriteIndented = true }));
                }
                var watchedValue = watchAddress.HasValue ? ReadLong(watchAddress.Value) : 0;
                var unexpectedWrite = watchAddress.HasValue && watchedValue != 0 && watchedValue != expected;
                var closingLibrary = libraryClose != 0 &&
                    (c.ProgramCounter == libraryClose || c.ProgramCounter == libraryCloseTarget);
                var changedLibraryHeader = libraryClose != 0 &&
                    (ReadOptionalLong(libraryClose + 28) != 0x02f40030 ||
                     ReadOptionalLong(libraryClose + 32) != 0x000c0001);
                var raisingAlert = alertTarget != 0 &&
                    (c.ProgramCounter == alertVector || c.ProgramCounter == alertTarget);
                if (raisingAlert || changedLibraryHeader || closingLibrary || unexpectedWrite || !mapped || c.ProgramCounter < 0x400 || c.ProgramCounter >= 0x1000000)
                {
                    if (unexpectedWrite) Reason = "DIAGNOSTIC_TRACE_UNEXPECTED_GLOBAL_VALUE";
                    if (closingLibrary) Reason = "DIAGNOSTIC_TRACE_MASTER_LIBRARY_CLOSE";
                    if (changedLibraryHeader) Reason = "DIAGNOSTIC_TRACE_MASTER_HEADER_CHANGED";
                    if (raisingAlert) Reason = "DIAGNOSTIC_TRACE_EXEC_ALERT";
                    StoppedAtInvalidPc = true;
                    var entries = Enumerable.Range(0, (int)Math.Min(count, ring.Length))
                        .Select(i => ring[(Math.Max(0, count - ring.Length) + i) % ring.Length]).ToArray();
                    File.WriteAllText(Path.Combine(output, "native-trace.json"),
                        JsonSerializer.Serialize(new { protocol = "mui-utility-scalar-trace-v2",
                            reason = Reason, watchAddress, expected, watchedValue,
                            libraryClose, libraryCloseTarget, programBase, programEvents,
                            alertVector, alertTarget, alertNumber = c.D[7],
                            instructionCount = count, entries }, new JsonSerializerOptions { WriteIndented = true }));
                    break;
                }
                var before = c.Cycles;
                core.ExecuteInstruction(); advance(c.Cycles);
                if (c.Cycles <= before) throw new InvalidOperationException("No trace CPU progress");
            }
        }
    }

    private uint ReadOptionalLong(uint address)
    {
        if (address <= machine.ChipRam.Length - 4)
            return System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(machine.ChipRam.Span.Slice((int)address, 4));
        if (machine.FastRamBase is uint b && address >= b && address-b <= machine.FastRam.Length-4)
            return System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(machine.FastRam.Span.Slice((int)(address-b), 4));
        return 0;
    }

    private uint ReadLong(uint address)
    {
        if (address + 4 <= machine.ChipRam.Length)
            return System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(machine.ChipRam.Span.Slice((int)address, 4));
        if (machine.FastRamBase is uint b && address >= b && address - b + 4 <= machine.FastRam.Length)
            return System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(machine.FastRam.Span.Slice((int)(address-b), 4));
        throw new ArgumentException("Trace watch must refer to captured physical Chip/Fast RAM");
    }

    private delegate bool PeekWord(uint address, out ushort word);
    private readonly record struct Entry(long Field, long Cycle, uint Pc, ushort Op,
        ushort Sr, uint Sp, uint D0, uint D1, uint D2, uint D3,
        uint D4, uint D5, uint D6, uint D7,
        uint A0, uint A1, uint A2, uint A3, uint A4, uint A5, uint A6);
}
