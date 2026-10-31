using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Copper68k;
using CopperDisk;
using CopperMod.Amiga.Lightweight;

// External diagnostic only. Preserve the production scalar interrupt sampling
// and hardware advancement. Low stacks/PCs and null-base vector calls are
// diagnostic stopping conditions, not additional emulated hardware faults.
if (args.Length is not (5 or 6 or 7)) throw new ArgumentException("ROM media output start-field end-field [a500-ocs-1m-v1|a500-ocs-2m-v1|a500-ocs-3m-v1] [adf|hdf-ro|hdf-ffs]");
var profile = args.Length >= 6 ? args[5] : "a500-ocs-1m-v1";
var mediaMode = args.Length == 7 ? args[6] : "adf";
var hardfiles = mediaMode switch
{
    "adf" => Array.Empty<AmigaHardfileConfiguration>(),
    "hdf-ro" => new[] { new AmigaHardfileConfiguration(0, args[1], true) },
    "hdf-ffs" => new[] { new AmigaHardfileConfiguration(0, args[1], true,
        mountMode: AmigaHardfileMountMode.Partition, partition: AmigaFfsPartition.ParseGeometry("DH0:1:32:0:1023")) },
    _ => throw new ArgumentException("Unknown diagnostic media mode")
};
var fastBytes = profile switch { "a500-ocs-1m-v1" => 0, "a500-ocs-2m-v1" => 1048576, "a500-ocs-3m-v1" => 2097152,
    _ => throw new ArgumentException("Unknown diagnostic machine profile") };
var output = Path.GetFullPath(args[2]);
if (Directory.Exists(output)) throw new ArgumentException("Use a new output directory");
Directory.CreateDirectory(output);
using var m = new LightweightA500Machine(new LightweightA500Configuration
{
    ChipRamBytes=524288, SlowRamBytes=524288, FastRamBytes=fastBytes,
    Hardfiles=hardfiles,
    CpuModel=M68kCpuModel.M68000, AgnusModel=LightweightAgnusModel.Mos8371,
    DeniseModel=LightweightDeniseModel.Mos8362
});
var rom = File.ReadAllBytes(args[0]);
m.LoadKickstart(rom);
if (mediaMode == "adf") { m.MountAdf(0, File.ReadAllBytes(args[1])); m.SetDriveWriteProtected(0,true); }
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
    if (m.FastRamBase is uint fast && a >= fast && a - fast <= m.FastRam.Length - 4)
        return BinaryPrimitives.ReadUInt32BigEndian(m.FastRam.Span.Slice((int)(a-fast),4));
    return 0;
}
bool Stack(uint sp) => sp >= 0x400 && sp <= 0x80000 || sp >= 0xc00004 && sp <= 0xc80000 ||
    m.FastRamBase is uint fast && sp >= fast+4 && sp-fast <= m.FastRam.Length;
byte ReadByte(uint a) => a<m.ChipRam.Length ? m.ChipRam.Span[(int)a] :
    a>=0xc00000 && a<0xc80000 ? slow[a-0xc00000] :
    m.FastRamBase is uint fast && a>=fast && a-fast<m.FastRam.Length ? m.FastRam.Span[(int)(a-fast)] :
    a>=0x1000000-rom.Length && a<0x1000000 ? rom[a-(0x1000000-rom.Length)] : (byte)0;
string Name(uint a)
{
    var bytes=new List<byte>();
    for(var i=0;i<128 && ReadByte(a+(uint)i)!=0;i++) bytes.Add(ReadByte(a+(uint)i));
    return Encoding.ASCII.GetString(bytes.ToArray());
}
var ringSize = int.Parse(Environment.GetEnvironmentVariable("COPPERSTART_PROBE_RING_SIZE") ?? "512");
if (ringSize < 1 || ringSize > 1048576) throw new ArgumentOutOfRangeException(nameof(ringSize));
uint? recordPcMin = Environment.GetEnvironmentVariable("COPPERSTART_PROBE_PC_MIN") is { Length: > 0 } minPc ? Convert.ToUInt32(minPc, 16) : null;
uint? recordPcMax = Environment.GetEnvironmentVariable("COPPERSTART_PROBE_PC_MAX") is { Length: > 0 } maxPc ? Convert.ToUInt32(maxPc, 16) : null;
uint? stopPc = Environment.GetEnvironmentVariable("COPPERSTART_PROBE_STOP_PC") is { Length: > 0 } targetPc ? Convert.ToUInt32(targetPc, 16) : null;
uint? stopPcD0 = Environment.GetEnvironmentVariable("COPPERSTART_PROBE_STOP_PC_D0") is { Length: > 0 } targetD0 ? Convert.ToUInt32(targetD0, 16) : null;
// Optional task-specific stack-floor capture; task identity excludes other
// runnable tasks and the supervisor stack from this diagnostic condition.
// Limit capture to the 4 KiB immediately below the selected stack: dispatch
// publishes ThisTask before leaving the outgoing task's distant stack.
uint? stopStackTask = Environment.GetEnvironmentVariable("COPPERSTART_PROBE_STACK_TASK") is { Length: > 0 } taskText ? Convert.ToUInt32(taskText,16) : null;
uint? stopStackFloor = Environment.GetEnvironmentVariable("COPPERSTART_PROBE_STACK_FLOOR") is { Length: > 0 } floorText ? Convert.ToUInt32(floorText,16) : null;
long stackCaptureChecks=0; uint minimumCapturedStack=uint.MaxValue;
long executedInstructions = 0;
var ring = new Entry[ringSize]; long count = 0; bool stop = false; string reason = "END_FIELD";
var pending=new List<(string Api,string Library,uint Pc,uint Sp,uint A6,uint Version,long Cycle,uint Request,string? Text)>();
var vectorPending = new List<(string Api,uint ReturnPc,uint ReturnSp,uint Record,uint Buffer,uint Length)>();
var calls=new List<object>(); uint intuitionBase=uint.MaxValue, dosBase=uint.MaxValue, layersBase=uint.MaxValue, graphicsBase=uint.MaxValue;
var stopFailedDevice = Environment.GetEnvironmentVariable("COPPERSTART_PROBE_STOP_FAILED_DEVICE") == "1";
var stopIllegal = Environment.GetEnvironmentVariable("COPPERSTART_PROBE_STOP_ILLEGAL") == "1";
var stopSetFunction = Environment.GetEnvironmentVariable("COPPERSTART_PROBE_STOP_SET_FUNCTION") == "1";
var stopFailedLayer = Environment.GetEnvironmentVariable("COPPERSTART_PROBE_STOP_FAILED_LAYER") == "1";
var stopFailedReadArgs = Environment.GetEnvironmentVariable("COPPERSTART_PROBE_STOP_FAILED_READARGS") == "1";
void ObserveCall()
{
    var c=m.Cpu;
    if (stopStackTask is uint task && stopStackFloor is uint floor && (c.StatusRegister & 0x2000)==0 &&
        Read(Read(4)+276)==task && Read(task+58)==floor && c.A[7]<=Read(task+62) &&
        (c.A[7]>=floor || floor-c.A[7]<=4096))
    {
        stackCaptureChecks++; minimumCapturedStack=Math.Min(minimumCapturedStack,c.A[7]);
        if(c.A[7]<floor)
        { Keep("TASK_STACK_BELOW_FLOOR"); stop=true; reason="TASK_STACK_BELOW_FLOOR"; return; }
    }
    if (stopPc == c.ProgramCounter && (stopPcD0 is null || stopPcD0 == c.D[0]))
    { Keep("TARGET_PC"); stop=true; reason="TARGET_PC"; return; }
    peek(c.ProgramCounter,out var op); peek(c.ProgramCounter+2,out var ext);
    if(stopIllegal && op==0x4AFC)
    { Keep("ILLEGAL_INSTRUCTION"); stop=true; reason="ILLEGAL_INSTRUCTION"; return; }
    // Public DOS vector entries also expose callers using a different base
    // register or an indirect JSR. Keep their actual return address explicit.
    if(op==0x4ef9 && c.ProgramCounter<dosBase && dosBase-c.ProgramCounter<=1000 && (dosBase-c.ProgramCounter)%6==0)
    {
        var vector = unchecked((int)c.ProgramCounter-(int)dosBase);
        var api = vector switch { -30=>"DOS.Open", -42=>"DOS.Read", -48=>"DOS.Write", -84=>"DOS.Lock", -102=>"DOS.Examine", -108=>"DOS.ExNext",
            -390=>"DOS.ExamineFH", -822=>"DOS.MatchFirst", -828=>"DOS.MatchNext", -834=>"DOS.MatchEnd", _=>null };
        if(api is not null)
        {
            calls.Add(new { Api=api+"VectorInput", Library="dos.library", frame=m.CompletedFrames, entryCycle=c.Cycles,
                vectorPc=c.ProgramCounter, vector, callerReturnPc=Read(c.A[7]), callerSp=c.A[7], statusRegister=c.StatusRegister,
                task=Read(Read(4)+276), stackLower=Read(Read(Read(4)+276)+58), stackUpper=Read(Read(Read(4)+276)+62),
                d1=c.D[1], d2=c.D[2], d3=c.D[3],
                name=vector is -30 or -84 or -822 ? Name(c.D[1]) : null,
                inputDataPrefix=vector is not (-42 or -48) ? null : Convert.ToHexString(Enumerable.Range(0,(int)Math.Min(c.D[3],32u)).Select(n=>ReadByte(c.D[2]+(uint)n)).ToArray()),
                inputHashedBytes=vector != -42 ? 0 : Math.Min(c.D[3],262144u),
                inputDataSha256=vector != -42 ? null : Convert.ToHexString(SHA256.HashData(Enumerable.Range(0,(int)Math.Min(c.D[3],262144u)).Select(n=>ReadByte(c.D[2]+(uint)n)).ToArray())) });
            var record=vector is -102 or -108 or -390 or -822 ? c.D[2] : vector is -828 or -834 ? c.D[1] : 0;
            vectorPending.Add((api,Read(c.A[7]),c.A[7]+4,record,vector == -42 ? c.D[2] : 0,vector == -42 ? c.D[3] : 0));
        }
    }
    if(op!=0x4eae) return;
    var displacement=unchecked((short)ext);
    if(c.A[6]==Read(4) && displacement == -420)
    {
        calls.Add(new {Api="SetFunctionInput",Library="exec.library",frame=m.CompletedFrames,
            entryCycle=c.Cycles,callPc=c.ProgramCounter,libraryBase=c.A[1],offset=c.A[0],newFunction=c.D[0],
            addressRegisters=c.A.ToArray(),dataRegisters=c.D.ToArray()});
        if(stopSetFunction) { Keep("SET_FUNCTION_INPUT"); stop=true; reason="SET_FUNCTION_INPUT"; return; }
    }
    if(c.A[6]==Read(4) && displacement is -552 or -408)
        pending.Add(("OpenLibrary",Name(c.A[1]),c.ProgramCounter+4,c.A[7],c.A[6],displacement==-408?0:c.D[0],c.Cycles,0,null));
    else if(c.A[6]==Read(4) && displacement == -498)
        pending.Add(("OpenResource",Name(c.A[1]),c.ProgramCounter+4,c.A[7],c.A[6],0,c.Cycles,0,null));
    else if(c.A[6]==Read(4) && displacement is -198 or -684)
        pending.Add((displacement == -198 ? "AllocMem" : "AllocVec",$"D0={c.D[0]:X8},D1={c.D[1]:X8}",
            c.ProgramCounter+4,c.A[7],c.A[6],c.D[1],c.Cycles,0,null));
    else if(c.A[6]==Read(4) && displacement==-372)
        pending.Add(("GetMsg",$"A0={c.A[0]:X8}",c.ProgramCounter+4,c.A[7],c.A[6],0,c.Cycles,0,null));
    else if(c.A[6]==Read(4) && displacement is -444 or -456 or -318)
        pending.Add((displacement == -444 ? "OpenDevice" : displacement == -456 ? "DoIO" : "Wait",
            displacement == -444 ? Name(c.A[0]) : $"A1={c.A[1]:X8}",c.ProgramCounter+4,c.A[7],c.A[6],c.D[0],c.Cycles,displacement == -318 ? 0 : c.A[1],null));
    else if(c.A[6]==graphicsBase && displacement is -210 or -216 or -222)
    {
        var api = displacement == -210 ? "Graphics.MrgCop" : displacement == -216 ? "Graphics.MakeVPort" : "Graphics.LoadView";
        var view = displacement == -216 ? c.A[0] : c.A[1];
        calls.Add(new { Api=api+"Input", Library="graphics.library", callPc=c.ProgramCounter,
            entryCycle=c.Cycles, frame=m.CompletedFrames, view, activeView=Read(graphicsBase+34),
            cpr=Read(view+4), cprStart=Read(Read(view+4)+4),
            viewport=Read(view), viewportHeight=ReadByte(Read(view)+26)*256+ReadByte(Read(view)+27),
            addressRegisters=c.A.ToArray() });
        pending.Add((api,"graphics.library",c.ProgramCounter+4,c.A[7],c.A[6],0,c.Cycles,view,null));
    }
    else if(displacement is -198 or -612 && c.A[6]==intuitionBase)
    {
        calls.Add(new {Api="OpenScreenInput",Library="intuition.library",callPc=c.ProgramCounter,
            entryCycle=c.Cycles,frame=m.CompletedFrames,address=c.A[0],tags=c.A[1],lvo=displacement,
            bytes=Convert.ToHexString(Enumerable.Range(0,36).Select(i=>ReadByte(c.A[0]+(uint)i)).ToArray())});
        pending.Add(("OpenScreen","intuition.library",c.ProgramCounter+4,c.A[7],c.A[6],0,c.Cycles,0,null));
    }
    else if(c.A[6]==layersBase && displacement is -36 or -42 or -186 or -192)
    {
        calls.Add(new {Api="CreateLayerInput",Library="layers.library",callPc=c.ProgramCounter,
            entryCycle=c.Cycles,frame=m.CompletedFrames,lvo=displacement,
            addressRegisters=c.A.ToArray(),dataRegisters=c.D.ToArray()});
        pending.Add(("CreateLayer","layers.library",c.ProgramCounter+4,c.A[7],c.A[6],0,c.Cycles,0,null));
    }
    else if(displacement==-204 && c.A[6]==intuitionBase)
    {
        calls.Add(new {Api="OpenWindowInput",Library="intuition.library",callPc=c.ProgramCounter,
            entryCycle=c.Cycles,frame=m.CompletedFrames,address=c.A[0],
            bytes=Convert.ToHexString(Enumerable.Range(0,48).Select(i=>ReadByte(c.A[0]+(uint)i)).ToArray())});
        pending.Add(("OpenWindow","intuition.library",c.ProgramCounter+4,c.A[7],c.A[6],0,c.Cycles,0,null));
    }
    else if(displacement==-72 && c.A[6]==intuitionBase)
        pending.Add(("CloseWindow",$"A0={c.A[0]:X8}",c.ProgramCounter+4,c.A[7],c.A[6],0,c.Cycles,0,null));
    else if(c.A[6]==intuitionBase && displacement is -66 or -246 or -252 or -450)
    {
        var api=displacement switch {-66=>"CloseScreen",-246=>"ScreenToBack",-252=>"ScreenToFront",_=>"ActivateWindow"};
        calls.Add(new {Api=api+"Input",Library="intuition.library",callPc=c.ProgramCounter,
            entryCycle=c.Cycles,frame=m.CompletedFrames,address=c.A[0],front=Read(intuitionBase+60),active=Read(intuitionBase+52)});
        pending.Add((api,"intuition.library",c.ProgramCounter+4,c.A[7],c.A[6],0,c.Cycles,0,null));
    }
    else if(c.A[6]==dosBase && displacement == -798)
    {
        calls.Add(new {Api="DOS.ReadArgsInput",Library="dos.library",frame=m.CompletedFrames,
            callPc=c.ProgramCounter,entryCycle=c.Cycles,template=Name(c.D[1]),
            templateAddress=c.D[1],array=c.D[2],rdArgs=c.D[3],
            addressRegisters=c.A.ToArray(),dataRegisters=c.D.ToArray()});
        pending.Add(("DOS.ReadArgs",Name(c.D[1]),c.ProgramCounter+4,c.A[7],c.A[6],c.D[2],c.Cycles,0,null));
    }
    else if(c.A[6]==dosBase && displacement is -84 or -102 or -108 or -228 or -390 or -402 or -822 or -828 or -834)
    {
        var api = displacement switch { -84=>"DOS.Lock", -102=>"DOS.Examine", -108=>"DOS.ExNext", -228=>"DOS.AllocDosObject",
            -390=>"DOS.ExamineFH", -402=>"DOS.NameFromLock", -822=>"DOS.MatchFirst", -828=>"DOS.MatchNext", _=>"DOS.MatchEnd" };
        calls.Add(new { Api=api+"Input", Library="dos.library", frame=m.CompletedFrames, callPc=c.ProgramCounter,
            entryCycle=c.Cycles, d1=c.D[1], d2=c.D[2], d3=c.D[3],
            name=displacement is -84 or -822 ? Name(c.D[1]) : null });
        var record = displacement is -102 or -108 or -390 or -822 ? c.D[2] : displacement is -828 or -834 ? c.D[1] : 0;
        pending.Add((api,"dos.library",c.ProgramCounter+4,c.A[7],c.A[6],0,c.Cycles,record,null));
    }
    else if(c.A[6]==dosBase && displacement is -30 or -42 or -48)
        pending.Add((displacement == -30 ? "DOS.Open" : displacement == -42 ? "DOS.Read" : "DOS.Write",
            displacement == -30 ? Name(c.D[1]) : $"D1={c.D[1]:X8},D2={c.D[2]:X8},D3={c.D[3]:X8}",
            c.ProgramCounter+4,c.A[7],c.A[6],c.D[2],c.Cycles,0,
            displacement == -48 ? Encoding.Latin1.GetString(Enumerable.Range(0,(int)Math.Min(c.D[3],128u)).Select(i=>ReadByte(c.D[2]+(uint)i)).ToArray()) : null));
    if(c.A[6]==0 && displacement<0)
    { Keep("NULL_BASE_VECTOR_CALL"); stop=true; reason="NULL_BASE_VECTOR_CALL"; }
}
void ObserveReturn()
{
    var c=m.Cpu;
    for(var i=vectorPending.Count-1;i>=0;i--)
    {
        var p=vectorPending[i]; if(c.ProgramCounter!=p.ReturnPc || c.A[7]!=p.ReturnSp) continue;
        calls.Add(new { Api=p.Api+"VectorReturn", Library="dos.library", frame=m.CompletedFrames, returnCycle=c.Cycles,
            callerReturnPc=p.ReturnPc, result=c.D[0], record=p.Record,
            bytes=p.Record==0 ? null : Convert.ToHexString(Enumerable.Range(0,384).Select(n=>ReadByte(p.Record+(uint)n)).ToArray()),
            dataPrefix=p.Buffer==0 ? null : Convert.ToHexString(Enumerable.Range(0,(int)Math.Min(p.Length,32u)).Select(n=>ReadByte(p.Buffer+(uint)n)).ToArray()),
            outputHashedBytes=p.Buffer==0 || c.D[0]>p.Length ? 0 : Math.Min(c.D[0],262144u),
            outputDataSha256=p.Buffer==0 || c.D[0]>p.Length ? null : Convert.ToHexString(SHA256.HashData(Enumerable.Range(0,(int)Math.Min(c.D[0],262144u)).Select(n=>ReadByte(p.Buffer+(uint)n)).ToArray())) });
        vectorPending.RemoveAt(i); break;
    }
    for(var i=pending.Count-1;i>=0;i--)
    {
        var p=pending[i]; if(c.ProgramCounter!=p.Pc || c.A[7]!=p.Sp) continue;
        if(p.Api=="GetMsg" && c.D[0]==0) { pending.RemoveAt(i); break; }
        calls.Add(new {p.Api,p.Library,version=p.Version,callPc=p.Pc-4,entryCycle=p.Cycle,returnCycle=c.Cycles,result=c.D[0],baseAddress=p.A6,frame=m.CompletedFrames,
            ioRequest=p.Request,ioError=p.Request==0 ? 0 : ReadByte(p.Request+31),writePrefix=p.Text,
            messagePrefix=p.Api=="GetMsg" ? Convert.ToHexString(Enumerable.Range(0,52).Select(n=>ReadByte(c.D[0]+(uint)n)).ToArray()) : null,
            intuitionFront=p.A6==intuitionBase ? Read(intuitionBase+60) : 0, intuitionActive=p.A6==intuitionBase ? Read(intuitionBase+52) : 0});
        if(p.Api is "DOS.Examine" or "DOS.ExNext" or "DOS.ExamineFH" or "DOS.MatchFirst" or "DOS.MatchNext")
            calls.Add(new { Api=p.Api+"Record", Library="dos.library", frame=m.CompletedFrames, returnCycle=c.Cycles,
                result=c.D[0], address=p.Request,
                bytes=Convert.ToHexString(Enumerable.Range(0,384).Select(n=>ReadByte(p.Request+(uint)n)).ToArray()) });
        if(p.Api is "Graphics.MrgCop" or "Graphics.MakeVPort" or "Graphics.LoadView")
            calls.Add(new { Api=p.Api+"DisplayReturn", Library="graphics.library", frame=m.CompletedFrames,
                returnCycle=c.Cycles, result=c.D[0], view=p.Request, activeView=Read(p.A6+34),
                cpr=Read(p.Request+4), cprStart=Read(Read(p.Request+4)+4) });
        if(p.Library=="graphics.library" && p.Api=="OpenLibrary") graphicsBase=c.D[0];
        if(p.Library=="intuition.library" && p.Api=="OpenLibrary") intuitionBase=c.D[0];
        if(p.Library=="dos.library" && p.Api=="OpenLibrary") dosBase=c.D[0];
        if(p.Library=="layers.library" && p.Api=="OpenLibrary") layersBase=c.D[0];
        if(stopFailedLayer && p.Api=="CreateLayer" && c.D[0]==0)
        { Keep("CREATE_LAYER_FAILED"); stop=true; reason="CREATE_LAYER_FAILED"; }
        if(stopFailedReadArgs && p.Api=="DOS.ReadArgs" && c.D[0]==0)
        { Keep("READARGS_FAILED"); stop=true; reason="READARGS_FAILED"; }
        if(stopFailedDevice && p.Api=="OpenDevice" && c.D[0]!=0)
        { Keep("OPEN_DEVICE_FAILED"); stop=true; reason="OPEN_DEVICE_FAILED"; }
        pending.RemoveAt(i); break;
    }
}
Entry Snapshot(string kind)
{
    var c = m.Cpu; peek(c.ProgramCounter, out var op);
    return new(kind,m.CompletedFrames,c.Cycles,c.ProgramCounter,op,c.StatusRegister,c.A[7],c.UserStackPointer,c.SupervisorStackPointer,
        c.D[0],c.D[1],c.D[2],c.D[3],c.D[4],c.A[0],c.A[1],c.A[3],c.A[6], Read(c.A[7]));
}
void Keep(string kind)
{
    if (kind == "instruction" && (recordPcMin.HasValue && m.Cpu.ProgramCounter < recordPcMin.Value ||
        recordPcMax.HasValue && m.Cpu.ProgramCounter >= recordPcMax.Value)) return;
    ring[count++ % ring.Length] = Snapshot(kind);
}
void Check(string kind)
{
    if (watchAddress is uint watched && ReadByte(watched) != watchExpected)
    { Keep("WATCH_BYTE_CHANGED"); stop = true; reason = "WATCH_BYTE_CHANGED"; }
    if (!Stack(m.Cpu.A[7]) || !Stack(m.Cpu.SupervisorStackPointer)) { Keep(kind); stop = true; reason = kind; }
    var pc = m.Cpu.ProgramCounter;
    if (pc < 0x1000 || !peek(pc, out _)) { Keep("LOW_OR_UNPEEKABLE_PC"); stop = true; reason = "LOW_OR_UNPEEKABLE_PC"; }
}
for (var frame = 0; frame < start; frame++) m.ExecuteFrame();
// A late trace may start after these libraries were opened. Discover their
// public bases without issuing guest calls or advancing emulated time.
var seenLibraries = new HashSet<uint>();
for (var node = Read(Read(4) + 378); node != 0 && Read(node) != 0 && seenLibraries.Add(node); node = Read(node))
{
    var libraryName = Name(Read(node + 10));
    if (libraryName == "dos.library") dosBase = node;
    if (libraryName == "intuition.library") intuitionBase = node;
    if (libraryName == "layers.library") layersBase = node;
    if (libraryName == "graphics.library") graphicsBase = node;
}
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
            core.ExecuteInstruction(); executedInstructions++; advance(c.Cycles);
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
    profile, mediaMode, configuration=$"PAL OCS 68000 interpreter, 512 KiB Chip + 512 KiB slow + {fastBytes/1024} KiB Fast, media={mediaMode} read-only",
    fastRamBase=m.FastRamBase,fastRamBytes=m.FastRam.Length,startField=start,endField=end,
    watchAddress, watchExpected, stopStackTask, stopStackFloor, stackCaptureWindowBytes=4096, stackCaptureChecks, minimumCapturedStack=stackCaptureChecks==0?(uint?)null:minimumCapturedStack, stopFailedDevice, stopIllegal, stopSetFunction, stopFailedLayer, stopFailedReadArgs, ringSize, stopPc, stopPcD0,
    recordPcMin, recordPcMax, executedInstructions,
    calls,pendingCalls=pending.Select(p=>new {p.Api,p.Library,callPc=p.Pc-4,p.Sp,p.A6,p.Version,p.Cycle,p.Request}),entries },new JsonSerializerOptions { WriteIndented=true }));
File.WriteAllBytes(Path.Combine(output,"chipram.bin"),m.ChipRam.ToArray());
File.WriteAllBytes(Path.Combine(output,"slowram.bin"),slow);
if (!m.FastRam.IsEmpty) File.WriteAllBytes(Path.Combine(output,"fastram.bin"),m.FastRam.ToArray());
Console.WriteLine($"{reason} field={m.CompletedFrames} cycle={m.Cycle} PC={m.Cpu.ProgramCounter:X8} SP={m.Cpu.A[7]:X8}; {output}");

delegate bool PeekWord(uint address,out ushort word);
readonly record struct Entry(string Kind,long Frame,long Cycle,uint Pc,ushort Op,ushort Sr,uint Sp,uint Usp,uint Ssp,
    uint D0,uint D1,uint D2,uint D3,uint D4,uint A0,uint A1,uint A3,uint A6,uint StackTop);
