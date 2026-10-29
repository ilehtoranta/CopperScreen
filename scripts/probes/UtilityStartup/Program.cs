using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Copper68k;
using CopperMod.Amiga.Lightweight;

// External diagnostic only. Preserve the production scalar interrupt sampling
// and hardware advancement. Low stacks/PCs and null-base vector calls are
// diagnostic stopping conditions, not additional emulated hardware faults.
if (args.Length != 5) throw new ArgumentException("ROM ADF output start-field end-field");
var output = Path.GetFullPath(args[2]);
if (Directory.Exists(output)) throw new ArgumentException("Use a new output directory");
Directory.CreateDirectory(output);
using var m = new LightweightA500Machine(new LightweightA500Configuration
{
    ChipRamBytes=524288, SlowRamBytes=524288, FastRamBytes=0,
    CpuModel=M68kCpuModel.M68000, AgnusModel=LightweightAgnusModel.Mos8371,
    DeniseModel=LightweightDeniseModel.Mos8362
});
var rom = File.ReadAllBytes(args[0]);
m.LoadKickstart(rom); m.MountAdf(0, File.ReadAllBytes(args[1]));
var start = int.Parse(args[3]); var end = int.Parse(args[4]);
var watchText = Environment.GetEnvironmentVariable("COPPERSTART_PROBE_WATCH_ADDRESS");
uint? watchAddress = string.IsNullOrWhiteSpace(watchText) ? null : Convert.ToUInt32(watchText, 16);
var watchExpected = watchAddress.HasValue
    ? Convert.ToByte(Environment.GetEnvironmentVariable("COPPERSTART_PROBE_WATCH_BYTE") ?? throw new ArgumentException("WATCH_BYTE is required"), 16)
    : (byte)0;
var flags = BindingFlags.Instance | BindingFlags.NonPublic;
var type = m.GetType();
var core = (IM68kCore)type.GetField("_cpu", flags)!.GetValue(m)!;
var advance = type.GetMethod("AdvanceHardwareTo", flags)!.CreateDelegate<Action<long>>(m);
var clamp = type.GetMethod("ClampCpuBatchTarget", flags)!.CreateDelegate<Func<long, long>>(m);
var dispatch = type.GetMethod("DispatchPendingCpuInterrupt", flags)!.CreateDelegate<Func<bool>>(m);
var boundary = type.GetProperty("NextFrameCycle", flags)!.GetMethod!.CreateDelegate<Func<long>>(m);
var peek = type.GetMethod("TryPeekInstructionWord", flags)!.CreateDelegate<PeekWord>(m);
var slow = (byte[])type.GetField("_slowRam", flags)!.GetValue(m)!;
uint Read(uint a)
{
    a &= 0xffffff;
    if (a <= m.ChipRam.Length - 4) return BinaryPrimitives.ReadUInt32BigEndian(m.ChipRam.Span.Slice((int)a, 4));
    if (a >= 0xc00000 && a - 0xc00000 <= slow.Length - 4) return BinaryPrimitives.ReadUInt32BigEndian(slow.AsSpan((int)(a-0xc00000),4));
    return 0;
}
bool Stack(uint sp) => sp >= 0x400 && sp <= 0x80000 || sp >= 0xc00004 && sp <= 0xc80000;
byte ReadByte(uint a) => a<m.ChipRam.Length ? m.ChipRam.Span[(int)a] :
    a>=0xc00000 && a<0xc80000 ? slow[a-0xc00000] :
    a>=0x1000000-rom.Length && a<0x1000000 ? rom[a-(0x1000000-rom.Length)] : (byte)0;
string Name(uint a)
{
    var bytes=new List<byte>();
    for(var i=0;i<128 && ReadByte(a+(uint)i)!=0;i++) bytes.Add(ReadByte(a+(uint)i));
    return Encoding.ASCII.GetString(bytes.ToArray());
}
var ring = new Entry[512]; long count = 0; bool stop = false; string reason = "END_FIELD";
var pending=new List<(string Api,string Library,uint Pc,uint Sp,uint A6,uint Version,long Cycle)>();
var calls=new List<object>(); uint intuitionBase=uint.MaxValue;
void ObserveCall()
{
    var c=m.Cpu;
    peek(c.ProgramCounter,out var op); peek(c.ProgramCounter+2,out var ext);
    if(op!=0x4eae) return;
    var displacement=unchecked((short)ext);
    if(c.A[6]==Read(4) && displacement is -552 or -408)
        pending.Add(("OpenLibrary",Name(c.A[1]),c.ProgramCounter+4,c.A[7],c.A[6],displacement==-408?0:c.D[0],c.Cycles));
    else if(c.A[6]==Read(4) && displacement == -498)
        pending.Add(("OpenResource",Name(c.A[1]),c.ProgramCounter+4,c.A[7],c.A[6],0,c.Cycles));
    else if(c.A[6]==Read(4) && displacement is -444 or -456 or -318)
        pending.Add((displacement == -444 ? "OpenDevice" : displacement == -456 ? "DoIO" : "Wait",
            displacement == -444 ? Name(c.A[0]) : $"A1={c.A[1]:X8}",c.ProgramCounter+4,c.A[7],c.A[6],c.D[0],c.Cycles));
    else if(displacement==-198 && c.A[6]==intuitionBase)
        pending.Add(("OpenScreen","intuition.library",c.ProgramCounter+4,c.A[7],c.A[6],0,c.Cycles));
    if(c.A[6]==0 && displacement<0)
    { Keep("NULL_BASE_VECTOR_CALL"); stop=true; reason="NULL_BASE_VECTOR_CALL"; }
}
void ObserveReturn()
{
    var c=m.Cpu;
    for(var i=pending.Count-1;i>=0;i--)
    {
        var p=pending[i]; if(c.ProgramCounter!=p.Pc || c.A[7]!=p.Sp) continue;
        calls.Add(new {p.Api,p.Library,version=p.Version,callPc=p.Pc-4,entryCycle=p.Cycle,returnCycle=c.Cycles,result=c.D[0],baseAddress=p.A6,frame=m.CompletedFrames});
        if(p.Library=="intuition.library" && p.Api=="OpenLibrary") intuitionBase=c.D[0];
        pending.RemoveAt(i); break;
    }
}
Entry Snapshot(string kind)
{
    var c = m.Cpu; peek(c.ProgramCounter, out var op);
    return new(kind,m.CompletedFrames,c.Cycles,c.ProgramCounter,op,c.StatusRegister,c.A[7],c.UserStackPointer,c.SupervisorStackPointer,
        c.D[0],c.D[1],c.D[2],c.D[3],c.D[4],c.A[0],c.A[1],c.A[3],c.A[6], Read(c.A[7]));
}
void Keep(string kind) { ring[count++ % ring.Length] = Snapshot(kind); }
void Check(string kind)
{
    if (watchAddress is uint watched && ReadByte(watched) != watchExpected)
    { Keep("WATCH_BYTE_CHANGED"); stop = true; reason = "WATCH_BYTE_CHANGED"; }
    if (!Stack(m.Cpu.A[7]) || !Stack(m.Cpu.SupervisorStackPointer)) { Keep(kind); stop = true; reason = kind; }
    var pc = m.Cpu.ProgramCounter;
    if (pc < 0x1000 || !peek(pc, out _)) { Keep("LOW_OR_UNPEEKABLE_PC"); stop = true; reason = "LOW_OR_UNPEEKABLE_PC"; }
}
for (var frame = 0; frame < start; frame++) m.ExecuteFrame();
if (watchAddress is uint initialWatch && ReadByte(initialWatch) != watchExpected)
{ Keep("WATCH_ALREADY_CHANGED_AT_START"); stop = true; reason = "WATCH_ALREADY_CHANGED_AT_START"; }
Console.WriteLine($"Trace starts field={m.CompletedFrames} PC={m.Cpu.ProgramCounter:X8}");
while (m.CompletedFrames < end && !stop)
{
    var target = m.CompletedFrames + 1;
    while (m.CompletedFrames < target && !stop)
    {
        var limit = boundary(); var c = m.Cpu;
        if (c.Halted) { advance(limit); c.Cycles = Math.Max(c.Cycles,limit); continue; }
        if (c.Stopped)
        {
            var wake = ((c.StatusRegister >> 8) & 7) >= 6 ? limit : clamp(limit);
            advance(wake); c.Cycles = Math.Max(c.Cycles,wake);
            if (dispatch()) { Keep("interrupt-wake"); Check("INVALID_STACK_AFTER_WAKE"); }
            continue;
        }
        if (dispatch()) { Keep("interrupt-outer"); Check("INVALID_STACK_AFTER_INTERRUPT"); continue; }
        while (c.Cycles < limit && !stop)
        {
            if (dispatch()) { Keep("interrupt-inner"); Check("INVALID_STACK_AFTER_INTERRUPT"); continue; }
            if (c.Halted || c.Stopped) break;
            Keep("instruction"); ObserveCall(); if(stop) break; var before = c.Cycles;
            core.ExecuteInstruction(); advance(c.Cycles);
            ObserveReturn();
            if (c.Cycles <= before) throw new Exception("No CPU progress");
            Check("INVALID_STACK_AFTER_INSTRUCTION");
        }
    }
    if (m.CompletedFrames % 100 == 0) Console.WriteLine($"field={m.CompletedFrames} PC={m.Cpu.ProgramCounter:X8} SP={m.Cpu.A[7]:X8}");
}
var entries = Enumerable.Range(0,(int)Math.Min(count,ring.Length)).Select(i => ring[(Math.Max(0,count-ring.Length)+i)%ring.Length]).ToArray();
File.WriteAllText(Path.Combine(output,"trace.json"),JsonSerializer.Serialize(new { reason, m.CompletedFrames, m.Cycle, instructions=count,
    romSha256=Convert.ToHexString(SHA256.HashData(rom)), diskSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[1]))),
    configuration="PAL OCS 68000 interpreter, 512 KiB Chip + 512 KiB slow, one protected DF0", startField=start,endField=end,
    watchAddress, watchExpected,
    calls,pendingCalls=pending.Select(p=>new {p.Api,p.Library,callPc=p.Pc-4,p.Sp,p.A6,p.Version,p.Cycle}),entries },new JsonSerializerOptions { WriteIndented=true }));
File.WriteAllBytes(Path.Combine(output,"chipram.bin"),m.ChipRam.ToArray());
File.WriteAllBytes(Path.Combine(output,"slowram.bin"),slow);
Console.WriteLine($"{reason} field={m.CompletedFrames} cycle={m.Cycle} PC={m.Cpu.ProgramCounter:X8} SP={m.Cpu.A[7]:X8}; {output}");

delegate bool PeekWord(uint address,out ushort word);
readonly record struct Entry(string Kind,long Frame,long Cycle,uint Pc,ushort Op,ushort Sr,uint Sp,uint Usp,uint Ssp,
    uint D0,uint D1,uint D2,uint D3,uint D4,uint A0,uint A1,uint A3,uint A6,uint StackTop);
