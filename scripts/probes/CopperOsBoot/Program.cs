using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Copper68k;
using CopperDisk;
using CopperMod.Amiga.Lightweight;

// Diagnostic-only guest-cycle profiler. The scalar loop preserves the machine's
// outer/inner interrupt sampling points; a control run checks execution parity.
if (args.Length is < 9 or > 11) throw new ArgumentException("ROM HDF ROM-map GUI GUI-map Home Home-map output profile|control [graphics-map] [tab]");
Directory.CreateDirectory(args[7]);
var profile = args[8] == "profile";
using var machine = new LightweightA500Machine(new LightweightA500Configuration
{
    CpuModel = M68kCpuModel.M68EC020, AgnusModel = LightweightAgnusModel.Mos8374Alice,
    DeniseModel = LightweightDeniseModel.Lisa4203, ChipRamBytes = 2097152, SlowRamBytes = 0,
    FramebufferWidth = 1816,
    Hardfiles = [new AmigaHardfileConfiguration(0, args[1], true, 0, AmigaHardfileMountMode.Partition,
        AmigaFfsPartition.ParseGeometry("DH0:2:32:0:255"))]
});
var rom = File.ReadAllBytes(args[0]);
machine.LoadKickstart(rom.AsSpan(524288)); machine.LoadExtensionRom(rom.AsSpan(0, 524288));
machine.SubmitInput(new(0,0,0,0,0,0));
const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
var type = machine.GetType();
var core = (IM68kCore)type.GetField("_cpu",flags)!.GetValue(machine)!;
var advance = type.GetMethod("AdvanceHardwareTo",flags)!.CreateDelegate<Action<long>>(machine);
var clamp = type.GetMethod("ClampCpuBatchTarget",flags)!.CreateDelegate<Func<long,long>>(machine);
var dispatch = type.GetMethod("DispatchPendingCpuInterrupt",flags)!.CreateDelegate<Func<bool>>(machine);
var boundary = type.GetProperty("NextFrameCycle",flags)!.GetMethod!.CreateDelegate<Func<long>>(machine);
var custom = type.GetMethod("GetCustomRegister",flags)!.CreateDelegate<Func<ushort,ushort>>(machine);
uint Read(uint p) => p <= machine.ChipRam.Length-4 ? BinaryPrimitives.ReadUInt32BigEndian(machine.ChipRam.Span.Slice((int)p,4)) : 0;
string Name(uint p)
{
    if(p==0 || p>=machine.ChipRam.Length) return "";
    var n=(int)p; while(n<machine.ChipRam.Length && n-p<100 && machine.ChipRam.Span[n]!=0) n++;
    return Encoding.ASCII.GetString(machine.ChipRam.Span[(int)p..n]);
}
uint Find(int list,string name)
{
    var p=Read(Read(4)+(uint)list);
    for(var n=0;n<256 && p!=0 && Read(p)!=0;n++,p=Read(p)) if(Name(Read(p+10))==name) return p;
    return 0;
}
var symbols=new List<(uint Address,int Size,string Name)> { (0,0,"unmapped") };
var indices=new int[0x800000]; var cycles=new List<long>{0}; var instructions=new List<long>{0};
var entryCalls=new List<long>{0};
var callEdges=new Dictionary<(int Caller,int Callee),long>();
void Map(string path,uint offset)
{
    foreach(Match m in Regex.Matches(File.ReadAllText(path),@"(?m)^([0-9A-F]{8}) +(\d+) (Copper\S+)\r?$"))
    {
        var address=Convert.ToUInt32(m.Groups[1].Value,16)+offset; var size=int.Parse(m.Groups[2].Value);
        if(size==0 || address+(uint)size>0x1000000) continue;
        var id=symbols.Count; symbols.Add((address,size,m.Groups[3].Value)); cycles.Add(0); instructions.Add(0);entryCalls.Add(0);
        Array.Fill(indices,id,(int)(address>>1),(size+1)/2);
    }
}
Map(args[2],0);
if (args.Length >= 10) Map(args[9],0);
var modules=new[]{new Module(args[3],args[4]),new Module(args[5],args[6])};
var milestones=new Dictionary<string,long>();
void Mark(string name) {milestones.TryAdd(name,machine.Cycle);}
void Discover()
{
    foreach(var module in modules.Where(m=>m.Address==0))
    {
        var span=machine.ChipRam.Span; var start=4096;
        while(start<span.Length-32)
        {
            var found=span[start..].IndexOf(module.Signature); if(found<0) break;
            var at=(uint)(start+found);
            if(Read(at+6)==at+module.Relative)
            {module.Address=at; Map(module.Map,at); Mark(Path.GetFileName(module.Path)+"-loaded"); break;}
            start=(int)at+2;
        }
    }
}
long samples=0;
var bitmapCopies = new Dictionary<string,int>();
var bitmapEntry = symbols.FirstOrDefault(s => s.Name == "CopperGraphics.Vector.BltBitMap").Address;
var blitterStarts = new HashSet<uint>();
// Diagnostic disassembly of MOVE.W ...,absolute-long BLTSIZE instructions.
// This observes real guest register writes without instrumenting the engine.
for (var at=0;at<rom.Length-8;at+=2)
{
    var op=BinaryPrimitives.ReadUInt16BigEndian(rom.AsSpan(at,2));
    if ((op & 0xFFC0)!=0x33C0) continue;
    var ea=op & 63; var mode=ea>>3;
    var extension=mode is 0 or 1 or 2 or 3 or 4 ? 0 : mode is 5 or 6 || ea is 56 or 58 or 59 or 60 ? 2 : 4;
    if (BinaryPrimitives.ReadUInt32BigEndian(rom.AsSpan(at+2+extension,4))==0xDFF058)
        blitterStarts.Add((uint)(at<524288?0xE00000+at:0xF80000+at-524288));
}
var blitterLaunches = new Dictionary<string,int>();
var unmappedPages = new long[65536];
void ScalarFrame()
{
    var target=machine.CompletedFrames+1; var cpu=machine.Cpu;
    while(machine.CompletedFrames<target)
    {
        var end=boundary();
        if(cpu.Halted) {advance(end); cpu.Cycles=Math.Max(cpu.Cycles,end);continue;}
        if(cpu.Stopped)
        {var wake=((cpu.StatusRegister>>8)&7)>=6?end:clamp(end);advance(wake);cpu.Cycles=Math.Max(cpu.Cycles,wake);dispatch();continue;}
        if(dispatch()) continue;
        while(cpu.Cycles<end)
        {
            if(dispatch()) continue;
            if(cpu.Halted||cpu.Stopped) break;
            var id=indices[(cpu.ProgramCounter&0xFFFFFF)>>1]; var before=cpu.Cycles;
            if(id!=0 && cpu.ProgramCounter==symbols[id].Address)
            {
                entryCalls[id]++;
                // Read-only attribution of the return address at a native
                // method entry. Unmapped/tail-call cases stay unattributed.
                var returned=Read(cpu.A[7]);
                var caller=returned is >=2 and <0x1000000 ? indices[(returned-2)>>1] : 0;
                var edge=(caller,id);callEdges[edge]=callEdges.GetValueOrDefault(edge)+1;
            }
            if (bitmapEntry != 0 && cpu.ProgramCounter == bitmapEntry)
            {
                var shape = string.Join(",", cpu.D.Select(d => d & 0xFFFF)) +
                    $" strides={Read(cpu.A[0]) >> 16}/{Read(cpu.A[1]) >> 16}";
                bitmapCopies[shape] = bitmapCopies.GetValueOrDefault(shape) + 1;
            }
            var startsBlitter=blitterStarts.Contains(cpu.ProgramCounter);
            core.ExecuteInstruction(); advance(cpu.Cycles);
            if (startsBlitter)
            {
                var shape=$"{symbols[id].Name} con0={custom(0x40):X4} con1={custom(0x42):X4} size={custom(0x58):X4}";
                blitterLaunches[shape]=blitterLaunches.GetValueOrDefault(shape)+1;
            }
            if(cpu.Cycles<=before) throw new InvalidOperationException("No CPU progress");
            cycles[id]+=cpu.Cycles-before; instructions[id]++; samples++;
            if (id==0) unmappedPages[(cpu.ProgramCounter & 0xFFFFFF)>>8]+=cpu.Cycles-before;
        }
    }
}
var ready=false;
for(var field=0;field<6000;field++)
{
    if(profile) ScalarFrame();else machine.ExecuteFrame();
    if(machine.UnsupportedActiveFeature is {} unsupported) throw new InvalidOperationException(unsupported);
    if(field%20==0) Discover();
    if(Read(0x880)!=0) Mark("dos-control");
    var port=Find(392,"CopperOS.Desktop"); var intuition=Find(378,"intuition.library");
    if(port!=0) Mark("desktop-port");
    if(intuition!=0) Mark("intuition-installed");
    var screen=intuition==0?0:Read(intuition+60);
    if(screen!=0) Mark("desktop-screen");
    if(screen!=0 && Read(screen+4)!=0) Mark("desktop-window");
    var task=Read(port+16); var worker=Read(intuition+244);
    if(port!=0 && worker!=0 && task+15<machine.ChipRam.Length && machine.ChipRam.Span[(int)task+15]==4 &&
        Read(worker+108)==0xFFFF && Read(worker+112)==0 && Read(port+20)==port+24)
    {Mark("desktop-ready");ready=true;break;}
    if(field%500==499) Console.WriteLine($"field={field+1} guest-seconds={machine.Cycle/7093790.0:F3} PC={machine.Cpu.ProgramCounter:X8}");
}
var bootCycle=machine.Cycle;
var bootCosts=cycles.ToArray();
var bootCalls=entryCalls.ToArray();var bootEdges=new Dictionary<(int Caller,int Callee),long>(callEdges);
object? interaction=null;
if (ready && args.Length==11)
{
    if (args[10]!="tab") throw new ArgumentException("Only the tab redraw scenario is supported");
    void Frame() {if(profile) ScalarFrame(); else machine.ExecuteFrame();}
    machine.SetKeyState(0x42,true); for(var i=0;i<4;i++) Frame();
    machine.SetKeyState(0x42,false); for(var i=0;i<4;i++) Frame();
    var redrawn=false;
    for(var i=0;i<1500;i++)
    {
        var port=Find(392,"CopperOS.Desktop"); var intuition=Find(378,"intuition.library");
        var task=Read(port+16); var worker=Read(intuition+244); var state=Read(task+88);
        if(Read(state+28)==1 && Read(state+156)==1 && machine.ChipRam.Span[(int)task+15]==4 &&
            Read(worker+108)==0xFFFF && Read(worker+112)==0 && Read(port+20)==port+24)
        {redrawn=true;break;}
        Frame();
    }
    interaction=new{scenario="tab",redrawn,seconds=(machine.Cycle-bootCycle)/7093790.0,
        hot=symbols.Select((s,i)=>new{s.Name,entries=entryCalls[i]-bootCalls[i],cycles=cycles[i]-bootCosts[i],seconds=(cycles[i]-bootCosts[i])/7093790.0}).OrderByDescending(p=>p.cycles).Take(160),
        calls=callEdges.Select(e=>new{caller=symbols[e.Key.Caller].Name,callee=symbols[e.Key.Callee].Name,count=e.Value-bootEdges.GetValueOrDefault(e.Key)}).Where(e=>e.count>0).OrderByDescending(e=>e.count).Take(200)};
    if(!redrawn) throw new InvalidOperationException("Tab redraw did not finish");
}
string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
var result=new
{
    protocol=args.Length == 11 ? "copperos-a1200-guest-boot-profile-v4" : args.Length == 10 ? "copperos-a1200-guest-boot-profile-v2" : "copperos-a1200-guest-boot-profile-v1", profile, ready, machine.CompletedFrames, machine.Cycle, samples,
    seconds=bootCycle/7093790.0,
    milestones=milestones.Select(p=>new{name=p.Key,cycle=p.Value,seconds=p.Value/7093790.0}),
    modules=modules.Select(m=>new{m.Path,m.Address}),
    bitmapCopies,
    blitterLaunches,
    interaction,
    unmappedPages=unmappedPages.Select((cost,i)=>new{address=i*256,cycles=cost}).OrderByDescending(p=>p.cycles).Take(20),
    hot=symbols.Select((s,i)=>new{s.Address,s.Size,s.Name,cycles=cycles[i],instructions=instructions[i],seconds=cycles[i]/7093790.0}).OrderByDescending(p=>p.cycles).Take(60),
    inputs=args.Take(7).Select(p=>new{path=p,sha256=Hash(File.ReadAllBytes(p))}),
    graphicsMap=args.Length >= 10 ? new{path=args[9],sha256=Hash(File.ReadAllBytes(args[9]))} : null,
    state=new{pc=machine.Cpu.ProgramCounter,sr=machine.Cpu.StatusRegister,d=machine.Cpu.D,a=machine.Cpu.A,
        chip=Hash(machine.ChipRam.Span), pixels=Hash(System.Runtime.InteropServices.MemoryMarshal.AsBytes(machine.Framebuffer.Span)),
        audio=Hash(System.Runtime.InteropServices.MemoryMarshal.AsBytes(machine.AudioSamples.Span))}
};
File.WriteAllText(Path.Combine(args[7],"profile.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"ready={ready} fields={machine.CompletedFrames} seconds={machine.Cycle/7093790.0:F3}");
if(!ready) throw new InvalidOperationException("Desktop did not become ready");

sealed class Module(string path,string map)
{
    public readonly string Path=path,Map=map;
    public readonly byte[] Signature=File.ReadAllBytes(path).AsSpan(32,6).ToArray();
    public readonly uint Relative=BinaryPrimitives.ReadUInt32BigEndian(File.ReadAllBytes(path).AsSpan(38,4));
    public uint Address;
}
