using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.InteropServices;
using Copper68k;
using CopperMod.Amiga.Lightweight;
using CopperScreen;

namespace CopperScreen.Tests;

// Execute the bundled guest graphics vectors against real Lightweight DMA.
// Synthetic library storage isolates raster semantics; full boot tests cover
// installed library ownership, Layer locking, scheduling and input together.
public sealed partial class CopperOsGraphicsDmaTests
{
    [Theory]
    [InlineData(M68kCpuModel.M68000)]
    [InlineData(M68kCpuModel.M68EC020)]
    public void NativeRectangleDmaMatchesPixelOracleAndPreservesRegisters(M68kCpuModel model)
    {
        using var f = new Fixture(model);
        foreach (var mode in Enumerable.Range(0,8))
        foreach (var mask in new[]{0,1,3})
        foreach (var x in new[]{0,5,15})
        foreach (var width in new[]{1,17,600})
        {
            f.Raster(mode,mask);
            var expected=f.Planes();
            for(var plane=0;plane<2;plane++)
            for(var y=1;y<=65;y++) for(var px=x;px<x+width;px++)
            {
                if((mask & (1<<plane))==0 || mode is 4 or 6) continue;
                var at=4+y*80+px/8; var bit=(byte)(0x80>>(px&7));
                if((mode&2)!=0) expected[plane][at]^=bit;
                else if((((mode&4)!=0?1:2)&(1<<plane))!=0) expected[plane][at]|=bit;
                else expected[plane][at]&=(byte)~bit;
            }
            f.Call(-306, (uint)x,1,(uint)(x+width-1),65);
            Assert.Equal(expected[0],f.Planes()[0]); Assert.Equal(expected[1],f.Planes()[1]);
            Assert.Equal(mask!=0 && mode is not (4 or 6), f.SawBlitter);
        }
    }

    [Theory]
    [InlineData(M68kCpuModel.M68000)]
    [InlineData(M68kCpuModel.M68EC020)]
    public void NativeSetRastUsesDmaAndRetainsUnselectedPlanes(M68kCpuModel model)
    {
        using var f=new Fixture(model);
        foreach(var pen in new[]{0,1,2,3}) foreach(var mask in new[]{1,3})
        {
            f.Raster(7,mask); var expected=f.Planes();
            for(var plane=0;plane<2;plane++) if((mask&(1<<plane))!=0)
                expected[plane].AsSpan(4,80*70).Fill((pen&(1<<plane))==0?(byte)0:(byte)255);
            f.Call(-234,(uint)pen);
            Assert.Equal(expected[0],f.Planes()[0]); Assert.Equal(expected[1],f.Planes()[1]);
            Assert.True(f.SawBlitter);
        }
    }

    [Theory]
    [InlineData(M68kCpuModel.M68000)]
    [InlineData(M68kCpuModel.M68EC020)]
    public void NativeBitmapCopyUsesDmaForBothShiftDirectionsAndPreservesEdges(M68kCpuModel model)
    {
        using var f=new Fixture(model);
        foreach(var sx in Enumerable.Range(0,16)) foreach(var dx in Enumerable.Range(0,16))
        foreach(var width in new[]{1,17,601}) foreach(var mask in new[]{1,3})
        {
            f.Raster(0,mask); f.CopySource(); var expected=f.Planes();
            for(var plane=0;plane<2;plane++) if((mask&(1<<plane))!=0)
            for(var y=0;y<3;y++) for(var x=0;x<width;x++)
            {
                var ink=f.SourcePixel(plane,sx+x,y+12); var at=4+(y+1)*78+(dx+x)/8;
                var bit=(byte)(0x80>>((dx+x)&7));
                expected[plane][at]=ink?(byte)(expected[plane][at]|bit):(byte)(expected[plane][at]&~bit);
            }
            f.Call(-30,(uint)sx,12,(uint)dx,1,a0:0x60400,a1:0x60200,
                d4:(uint)width,d5:3,d6:0xC0,d7:(uint)mask);
            var actual=f.Planes();
            for(var plane=0;plane<2;plane++)
            {
                var mismatch=Enumerable.Range(0,expected[plane].Length).FirstOrDefault(i=>expected[plane][i]!=actual[plane][i],-1);
                Assert.True(mismatch<0,$"sx={sx} dx={dx} width={width} mask={mask} plane={plane} at={mismatch}");
            }
            Assert.True(f.SawBlitter,$"sx={sx} dx={dx} width={width} mask={mask}");
        }
    }

    private sealed partial class Fixture : IDisposable
    {
        private const uint Gfx=0x50000, Exec=0x40000, Rp=0x60000, Bitmap=0x60200,
            Plane0=0x90000, Plane1=0xA0000, Stack=0xE0000, Return=0x70000;
        private readonly LightweightA500Machine machine;
        private readonly IM68kCore core;
        private readonly Action<long> advance;
        private readonly Func<bool> busy;
        internal bool SawBlitter;
        internal uint RomFontAddress;
        private Span<byte> Ram=>MemoryMarshal.AsMemory(machine.ChipRam).Span;
        internal Fixture(M68kCpuModel model)
        {
            var aga=model!=M68kCpuModel.M68000;
            machine=new(new(){CpuModel=model,AgnusModel=aga?LightweightAgnusModel.Mos8374Alice:LightweightAgnusModel.Mos8372A,
                DeniseModel=aga?LightweightDeniseModel.Lisa4203:LightweightDeniseModel.Mos8362,
                ChipRamBytes=aga?2097152:1048576,SlowRamBytes=0});
            using var stream=typeof(CopperScreenBundledCopperOs).Assembly.GetManifestResourceStream("CopperScreen.Bundled.CopperStart-A1200.rom")!;
            using var buffer=new MemoryStream(); stream.CopyTo(buffer);
            var path=Environment.GetEnvironmentVariable("COPPERSCREEN_GRAPHICS_DMA_ROM");
            var rom=path==null?buffer.ToArray():File.ReadAllBytes(path);
            var combined=rom.Length==1048576;
            Assert.True(combined || rom.Length==524288);
            // Test-only ROM font outside emitted code. DMA must stage this
            // source into Chip RAM rather than use a ROM address on channel B.
            RomFontAddress=combined?0xE7F000u:0xF81000u;
            var fontOffset=combined?0x7F000:0x1000;
            for(var slot=0;slot<4;slot++)for(var row=0;row<8;row++)
                rom[fontOffset+row*8+slot]=TextInk(slot==3?(byte)1:(byte)(slot+32),row);
            machine.LoadKickstart(rom.AsSpan(combined?524288:0));
            if(combined) machine.LoadExtensionRom(rom.AsSpan(0,524288));
            var clock=machine.Cycle;
            machine.WriteByte(0xBFE201,1,ref clock,default);
            machine.WriteByte(0xBFE001,0,ref clock,default);
            Assert.False(machine.RomOverlayEnabled);
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            core=(IM68kCore)machine.GetType().GetField("_cpu",flags)!.GetValue(machine)!;
            advance=machine.GetType().GetMethod("AdvanceHardwareTo",flags)!.CreateDelegate<Action<long>>(machine);
            busy=machine.GetType().GetProperty("BlitterBusy",flags)!.GetMethod!.CreateDelegate<Func<bool>>(machine);
            Long(4,Exec); Long(Exec+62,(uint)machine.ChipRam.Length); Long(Gfx+0x1A4,Exec);
            Word(Gfx+0x10,1200); Word(Gfx+0x12,512); // ReadExecBase supported; synthetic gate has no private owner
            int RomOffset(uint address)=>address is >=0xF80000 and <0x1000000
                ?checked((int)(address-0xF80000))+(combined?524288:0)
                :combined && address is >=0xE00000 and <0xE80000?checked((int)(address-0xE00000)):-1;
            uint RomLong(int offset)=>BinaryPrimitives.ReadUInt32BigEndian(rom.AsSpan(offset,4));
            var residents=Enumerable.Range(0,(rom.Length-26)/2).Select(n=>n*2).Where(offset=>
            {
                if(BinaryPrimitives.ReadUInt16BigEndian(rom.AsSpan(offset,2))!=0x4AFC ||
                    RomOffset(RomLong(offset+2))!=offset) return false;
                var name=RomOffset(RomLong(offset+14));
                return name>=0 && name<=rom.Length-17 && rom.AsSpan(name,17).SequenceEqual("graphics.library\0"u8);
            }).ToArray();
            var resident=Assert.Single(residents);
            var table=RomLong(RomOffset(RomLong(resident+22))+4);
            var tableOffset=RomOffset(table);
            Assert.Equal(0xFFFF,BinaryPrimitives.ReadUInt16BigEndian(rom.AsSpan(tableOffset,2)));
            for(var slot=0;slot<200;slot++)
            {
                var relative=BinaryPrimitives.ReadInt16BigEndian(rom.AsSpan(tableOffset+2+slot*2,2));
                if(relative==-1) break;
                var vector=Gfx-(uint)((slot+1)*6);
                Word(vector,0x4EF9); Long(vector+2,unchecked(table+(uint)relative));
            }
            machine.WriteWord(0xDFF096,0x8240,ref clock,default);
        }
        private void Long(uint at,uint value)=>BinaryPrimitives.WriteUInt32BigEndian(Ram.Slice((int)at,4),value);
        private void Word(uint at,ushort value)=>BinaryPrimitives.WriteUInt16BigEndian(Ram.Slice((int)at,2),value);
        internal void Raster(int mode,int mask)
        {
            Ram.Slice((int)Rp,100).Clear(); Long(Rp+4,Bitmap);
            Ram[(int)Rp+24]=(byte)mask;
            // The RKM requires setters to keep the cached drawing minterms in sync.
            Call(-894,2,1,(uint)mode);
            Word(Bitmap,80); Word(Bitmap+2,70); Ram[(int)Bitmap+5]=2;
            Long(Bitmap+8,Plane0); Long(Bitmap+12,Plane1);
            Ram.Slice((int)Plane0-4,80*70+8).Fill(0xA5); Ram.Slice((int)Plane1-4,80*70+8).Fill(0xA5);
        }
        internal byte[][] Planes()=>[Ram.Slice((int)Plane0-4,80*70+8).ToArray(),Ram.Slice((int)Plane1-4,80*70+8).ToArray()];
        internal void CopySource()
        {
            Word(Bitmap,78); const uint source=0x60400;
            Word(source,80); Word(source+2,70); Ram[(int)source+5]=2;
            Long(source+8,0xB0000); Long(source+12,0xC0000);
            for(var plane=0;plane<2;plane++) for(var i=0;i<80*70;i++)
                Ram[0xB0000+plane*0x10000+i]=(byte)(i*41+19+plane*73);
        }
        internal bool SourcePixel(int plane,int x,int y)=>(Ram[0xB0000+plane*0x10000+y*80+x/8]&(0x80>>(x&7)))!=0;
        internal void Call(int vector,uint d0,uint d1=0,uint d2=0,uint d3=0,uint a0=0,uint a1=Rp,
            uint d4=0x12340004,uint d5=0x12340005,uint d6=0x12340006,uint d7=0x12340007)
        {
            Long(Stack,Return); core.Reset(unchecked(Gfx+(uint)vector),Stack);
            machine.Cpu.Cycles=machine.Cycle; machine.Cpu.StatusRegister=0x2700;
            for(var i=2;i<=7;i++) machine.Cpu.D[i]=0x12340000u+(uint)i;
            for(var i=2;i<=5;i++) machine.Cpu.A[i]=0x23450000u+(uint)i;
            machine.Cpu.A[0]=a0; machine.Cpu.A[1]=a1; machine.Cpu.A[6]=Gfx;
            machine.Cpu.D[0]=d0; machine.Cpu.D[1]=d1; machine.Cpu.D[2]=d2; machine.Cpu.D[3]=d3;
            machine.Cpu.D[4]=d4; machine.Cpu.D[5]=d5; machine.Cpu.D[6]=d6; machine.Cpu.D[7]=d7;
            SawBlitter=false;
            for(var i=0;i<2_000_000 && machine.Cpu.ProgramCounter!=Return;i++)
            {
                core.ExecuteInstruction(); advance(machine.Cpu.Cycles); SawBlitter|=busy();
                Assert.False(machine.Cpu.Halted);
            }
            Assert.Equal(Return,machine.Cpu.ProgramCounter); Assert.Equal(Stack+4,machine.Cpu.A[7]);
            // Classic drawing calls may return while their final DMA runs.
            // Observe pixels only after the public WaitBlit synchronization.
            if(busy())
            {
                Long(Stack,Return); machine.Cpu.A[7]=Stack; machine.Cpu.ProgramCounter=Gfx-228;
                for(var i=0;i<2_000_000 && machine.Cpu.ProgramCounter!=Return;i++)
                { core.ExecuteInstruction(); advance(machine.Cpu.Cycles); Assert.False(machine.Cpu.Halted); }
                Assert.Equal(Return,machine.Cpu.ProgramCounter); Assert.Equal(Stack+4,machine.Cpu.A[7]);
            }
            Assert.False(busy()); Assert.Equal(d2,machine.Cpu.D[2]); Assert.Equal(d3,machine.Cpu.D[3]);
            Assert.Equal(d4,machine.Cpu.D[4]); Assert.Equal(d5,machine.Cpu.D[5]);
            Assert.Equal(d6,machine.Cpu.D[6]); Assert.Equal(d7,machine.Cpu.D[7]);
            for(var i=2;i<=5;i++) Assert.Equal(0x23450000u+(uint)i,machine.Cpu.A[i]);
            Assert.Equal(Gfx,machine.Cpu.A[6]);
        }
        public void Dispose()=>machine.Dispose();
    }
}
