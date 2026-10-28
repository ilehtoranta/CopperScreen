using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CopperMod.Amiga.Lightweight;

sealed class GraphicsObserver
{
    private readonly LightweightA500Machine machine;
    private readonly byte[] rom;
    private readonly Func<ushort,ushort> custom;
    private readonly Dictionary<uint,string> entries=new();
    private readonly Dictionary<(uint Pc,uint Sp),List<Frame>> pending=new();
    private readonly List<Frame> active=new();
    private readonly Dictionary<string,Counter> totals=new();
    private readonly List<TextSample> texts=new();
    private readonly Dictionary<string,int> launches=new();
    private long droppedText;
    public string Phase="boot";
    public GraphicsObserver(LightweightA500Machine machine,byte[] rom,string map,Func<ushort,ushort> custom)
    {
        this.machine=machine;this.rom=rom;this.custom=custom;
        foreach(Match m in Regex.Matches(File.ReadAllText(map),@"(?m)^([0-9A-F]{8}) +2 CopperGraphics.Vector.(\w+)\r?$"))
        {
            var name=m.Groups[2].Value;
            if(name is "VBeamPos" or "WaitTOF" or "WaitBOVP") continue;
            entries[Convert.ToUInt32(m.Groups[1].Value,16)]=name;
        }
    }
    private byte B(uint p)=>p<machine.ChipRam.Length?machine.ChipRam.Span[(int)p]:
        p is >=0xE00000 and <0xE80000?rom[p-0xE00000]:
        p is >=0xF80000 and <0x1000000?rom[524288+p-0xF80000]:(byte)0;
    private ushort W(uint p)=>(ushort)(B(p)<<8|B(p+1));
    private uint L(uint p)=>(uint)W(p)<<16|W(p+2);
    private string Name(uint p)
    {
        var data=new List<byte>();
        for(var i=0;i<80&&p!=0&&B(p+(uint)i)!=0;i++) data.Add(B(p+(uint)i));
        return Encoding.Latin1.GetString(data.ToArray());
    }
    public void BeforeInstruction()
    {
        var cpu=machine.Cpu;
        if(pending.Remove((cpu.ProgramCounter,cpu.A[7]),out var completed))
            foreach(var f in completed)
            {
                var cycles=cpu.Cycles-f.Start;
                f.Counter.Completed++;f.Counter.InclusiveCycles+=cycles;f.Counter.MaxInclusiveCycles=Math.Max(f.Counter.MaxInclusiveCycles,cycles);
                if(f.Text is not null) f.Text.InclusiveCycles=cycles;
                active.Remove(f);
            }
        if(!entries.TryGetValue(cpu.ProgramCounter,out var name)) return;
        var key=Phase+":"+name;
        if(!totals.TryGetValue(key,out var counter)) totals[key]=counter=new Counter();
        counter.Entries++;
        TextSample? sample=null;
        if(name=="Text")
        {
            if(texts.Count<512){sample=new TextSample{Phase=Phase,Cycle=cpu.Cycles,Arguments=DescribeText()};texts.Add(sample);}
            else droppedText++;
        }
        var frame=new Frame(counter,cpu.Cycles,name,L(L(4)+0x114),cpu.A[7],sample);
        active.Add(frame);var returned=(L(cpu.A[7]),cpu.A[7]+4);
        if(!pending.TryGetValue(returned,out var list)) pending[returned]=list=new();
        list.Add(frame);
    }
    private object DescribeText()
    {
        var cpu=machine.Cpu;var rp=cpu.A[1];var font=L(rp+52);var bitmap=L(rp+4);
        var count=(short)cpu.D[0];var lo=B(font+32);var hi=B(font+33);var metrics=new HashSet<string>();
        var reasons=new HashSet<string>();var text=new byte[Math.Clamp((int)count,0,160)];
        for(var i=0;i<text.Length;i++) text[i]=B(cpu.A[0]+(uint)i);
        if(count is <=0 or >80) reasons.Add("count");
        if(font==0) reasons.Add("font-null");
        if(B(rp+56)!=0) reasons.Add("algorithmic-style");
        if((B(font+22)&64)!=0) reasons.Add("color-font");
        if((B(font+23)&4)!=0) reasons.Add("font-flag-bit2");
        if(W(font+24)!=8) reasons.Add("cell-width");
        if(W(font+20) is 0 or >32) reasons.Add("height");
        if(L(font+44)!=0) reasons.Add("char-space");
        if(L(font+48)!=0) reasons.Add("char-kern");
        if(W(rp+64)!=0) reasons.Add("tx-spacing");
        for(var i=0;i<Math.Min(text.Length,80);i++)
        {
            var c=text[i];var slot=c>=lo&&c<=hi?c-lo:hi-lo+1;
            var loc=L(L(font+40)+(uint)slot*4);var width=(ushort)loc;var bit=(ushort)(loc>>16);
            metrics.Add($"{width}:{bit&7}");
            if(width!=8) reasons.Add("glyph-width");
            if((bit&7)!=0) reasons.Add("glyph-bit-alignment");
        }
        return new{count,text=Encoding.Latin1.GetString(text),rp,font,fontName=Name(L(font+10)),
            x=(short)W(rp+36),y=(short)W(rp+38),cellWidth=W(font+24),height=W(font+20),baseline=W(rp+62),
            charSpace=L(font+44),charKern=L(font+48),txSpacing=(short)W(rp+64),algoStyle=B(rp+56),mode=B(rp+28),
            layer=L(rp),bitmap,stride=W(bitmap),rows=W(bitmap+2),depth=B(bitmap+5),mask=B(rp+24),
            stack=cpu.A[7],glyphMetrics=metrics.Order().ToArray(),headerAndCellRefusals=reasons.Order().ToArray()};
    }
    public bool IsSizeWrite(uint pc)
    {
        var op=W(pc);if((op&0xF000)!=0x3000) return false;
        var src=op&63;var srcMode=src>>3;
        var extension=srcMode<=4?0:srcMode is 5 or 6?2:src==57?4:2;
        var mode=(op>>6)&7;var reg=(op>>9)&7;
        var addr=mode switch{2 or 3=>machine.Cpu.A[reg],4=>machine.Cpu.A[reg]-2,
            5=>unchecked(machine.Cpu.A[reg]+(uint)(short)W(pc+2+(uint)extension)),
            7 when reg==0=>unchecked((uint)(int)(short)W(pc+2+(uint)extension)),
            7 when reg==1=>L(pc+2+(uint)extension),_=>uint.MaxValue};
        return (addr&0xFFFFFF)==0xDFF058;
    }
    public void AfterSizeWrite()
    {
        var cpu=machine.Cpu;var task=L(L(4)+0x114);
        var owner=active.LastOrDefault(f=>f.Task==task&&cpu.A[7]<=f.Stack)?.Name??"unattributed";
        var key=$"{Phase}:{owner}:con0={custom(0x40):X4},con1={custom(0x42):X4},size={custom(0x58):X4}";
        launches[key]=launches.GetValueOrDefault(key)+1;
    }
    public object Snapshot()=>new{coverage="Entry-only map; latencies include callees, hardware stalls and preemption. MOVE.W BLTSIZE observation includes address-register destinations.",
        totals,texts,droppedText,launches,pending=active.Select(f=>new{f.Name,f.Task,f.Stack,f.Start}).ToArray()};
    private sealed record Frame(Counter Counter,long Start,string Name,uint Task,uint Stack,TextSample? Text);
    private sealed class Counter{public long Entries{get;set;}public long Completed{get;set;}public long InclusiveCycles{get;set;}public long MaxInclusiveCycles{get;set;}}
    private sealed class TextSample{public string Phase{get;set;}="";public long Cycle{get;set;}public object Arguments{get;set;}=null!;public long? InclusiveCycles{get;set;}}
}
