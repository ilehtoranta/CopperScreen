using Copper68k;

namespace CopperScreen.Tests;

public sealed partial class CopperOsGraphicsDmaTests
{
    private static readonly byte[] DmaText = [32,33,1,34,255,32,34,33,1,32];
    private static byte TextInk(byte c,int row)
    {
        var slot=c is >=32 and <=34?c-32:3;
        return slot switch {0=>(byte)(0x81^(row*17)),1=>(byte)(0xA5+row*3),2=>(byte)(0x80>>row),_=>(byte)(0x3C^(row*9))};
    }

    [Theory]
    [InlineData(M68kCpuModel.M68000)]
    [InlineData(M68kCpuModel.M68EC020)]
    public void NativeTextStagesRomFontsAndFallsBackForLongStrings(M68kCpuModel model)
    {
        using var f=new Fixture(model);
        foreach(var rom in new[]{false,true})foreach(var count in new[]{0,1,79,80,81,128})
        {
            f.Raster(1,3);f.TextSetup(3,9,0);f.TextRepeat(count,rom);
            var expected=f.Planes();
            for(var ch=0;ch<count;ch++)for(var row=0;row<8;row++)for(var bit=0;bit<8;bit++)
            {
                var x=3+ch*8+bit;if(x>=640)continue;
                var active=(TextInk(32,row)&(0x80>>bit))!=0;
                for(var plane=0;plane<2;plane++)
                {
                    var at=4+(3+row)*80+x/8;var pixel=(byte)(0x80>>(x&7));
                    if(((active?2:1)&(1<<plane))!=0)expected[plane][at]|=pixel;else expected[plane][at]&=(byte)~pixel;
                }
            }
            f.Call(-60,(uint)count,a0:0xB0800);
            var actual=f.Planes();Assert.Equal(expected[0],actual[0]);Assert.Equal(expected[1],actual[1]);
            Assert.Equal((ushort)(3+count*8),f.TextCursor);
            Assert.Equal(count is >0 and <=80,f.SawBlitter);
        }
    }

    [Theory]
    [InlineData(M68kCpuModel.M68000)]
    [InlineData(M68kCpuModel.M68EC020)]
    public void NativeTextDmaMatchesAllModesAlignmentsAndSimpleSmartClipping(M68kCpuModel model)
    {
        using var f=new Fixture(model);
        foreach(var layout in new[]{0,1,2}) foreach(var mode in Enumerable.Range(0,8))
        foreach(var mask in new[]{0,1,3}) foreach(var x in Enumerable.Range(-8,16)) foreach(var baseline in new[]{-2,5,69,80})
        {
            f.Raster(mode,mask);f.TextSetup(x,baseline,layout);
            var expected=f.TextPlanes();var visible=false;
            for(var ch=0;ch<DmaText.Length;ch++)for(var row=0;row<8;row++)for(var bit=0;bit<8;bit++)
            {
                var px=x+ch*8+bit+(layout==0?0:3);var py=baseline-6+row+(layout==0?0:3);
                if(px<0||px>=640||py<0||py>=70||layout!=0&&px>200)continue;
                var hidden=layout!=0&&px is >=31 and <=94;if(hidden&&layout==1)continue;
                visible=true;var active=(TextInk(DmaText[ch],row)&(0x80>>bit))!=0;
                if((mode&3)==3)active=true;else if((mode&4)!=0)active=!active;
                if(!active&&(mode&1)==0)continue;
                for(var plane=0;plane<2;plane++)
                {
                    if((mask&(1<<plane))==0)continue;
                    var dx=px-(hidden?16:0);var at=4+py*80+dx/8;var pixel=(byte)(0x80>>(dx&7));
                    var data=expected[plane+(hidden?2:0)];
                    if((mode&2)!=0){if(active)data[at]^=pixel;}
                    else if(((active?2:1)&(1<<plane))!=0)data[at]|=pixel;else data[at]&=(byte)~pixel;
                }
            }
            f.Call(-60,0xCAFE0000u+(uint)DmaText.Length,a0:0xB0800);
            var actual=f.TextPlanes();
            for(var plane=0;plane<4;plane++)
            {
                var mismatch=Enumerable.Range(0,expected[plane].Length).FirstOrDefault(i=>expected[plane][i]!=actual[plane][i],-1);
                Assert.True(mismatch<0,$"layout={layout} mode={mode} mask={mask} x={x} y={baseline} plane={plane} at={mismatch}");
            }
            Assert.Equal(unchecked((ushort)(x+DmaText.Length*8)),f.TextCursor);
            Assert.Equal(visible&&mask!=0,f.SawBlitter);
            Assert.Equal(layout==0?0u:1u,f.TextLocks);Assert.Equal(0,f.TextHeld);
        }
    }

    private sealed partial class Fixture
    {
        private const uint TextFont=0xB0000,TextData=0xB0100,TextLoc=0xB0400,
            TextLayer=0xB1000,TextClips=0xB1100,TextBacking=0xB1200,TextPlane0=0xB2000,TextPlane1=0xB4000;
        internal ushort TextCursor=>System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(Ram.Slice((int)Rp+36,2));
        internal ushort TextHeld=>System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(Ram.Slice((int)TextLayer+72,2));
        internal uint TextLocks=>System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(Ram.Slice(0x45000,4));
        internal void TextRepeat(int count,bool rom)
        {
            Ram.Slice(0xB0800,count).Fill(32);
            if(rom)Long(TextFont+34,0xE7F000);
        }
        internal void TextSetup(int x,int baseline,int layout)
        {
            Ram.Slice((int)TextFont,52).Clear();Long(Rp+52,TextFont);Word(Rp+36,unchecked((ushort)x));Word(Rp+38,unchecked((ushort)baseline));
            Word(Rp+62,6);Word(TextFont+20,8);Word(TextFont+24,8);Word(TextFont+26,6);
            Ram[(int)TextFont+32]=32;Ram[(int)TextFont+33]=34;Long(TextFont+34,TextData);Word(TextFont+38,8);Long(TextFont+40,TextLoc);
            for(var i=0;i<4;i++){Long(TextLoc+(uint)i*4,(uint)(i*8<<16|8));for(var row=0;row<8;row++)Ram[(int)TextData+row*8+i]=TextInk(i==3?(byte)1:(byte)(i+32),row);}
            DmaText.CopyTo(Ram.Slice(0xB0800));
            Ram.Slice((int)TextPlane0-4,80*70+8).Fill(0xA5);Ram.Slice((int)TextPlane1-4,80*70+8).Fill(0xA5);
            Long(0x45000,0);Ram.Slice((int)TextLayer,200).Clear();
            if(layout==0)return;
            Long(Rp,TextLayer);Word(TextLayer+16,5);Word(TextLayer+18,4);Word(TextLayer+44,2);Word(TextLayer+46,1);Word(TextLayer+30,(ushort)layout);
            Long(TextLayer+8,TextClips);Word(TextBacking,80);Word(TextBacking+2,70);Ram[(int)TextBacking+5]=2;
            Long(TextBacking+8,TextPlane0);Long(TextBacking+12,TextPlane1);
            for(var i=0;i<3;i++)
            {
                var clip=TextClips+(uint)i*36;Ram.Slice((int)clip,36).Clear();Long(clip,i==2?0:clip+36);
                Long(clip+8,i==1?1u:0u);Long(clip+12,TextBacking);
                Word(clip+16,(ushort)(i==0?0:i==1?31:95));Word(clip+18,0);Word(clip+20,(ushort)(i==0?30:i==1?94:200));Word(clip+22,69);
            }
            Word(Exec-552,0x7000);Word(Exec-550,0x4E75);
            Word(Exec-564,0x4EF9);Long(Exec-562,0x45010);Word(Exec-570,0x5351);Word(Exec-568,0x4E75);
            ushort[] obtain=[0x5251,0x52B9,0x0004,0x5000,0x4E75];for(var i=0;i<obtain.Length;i++)Word(0x45010+(uint)i*2,obtain[i]);
        }
        internal byte[][] TextPlanes()=>[..Planes(),Ram.Slice((int)TextPlane0-4,80*70+8).ToArray(),Ram.Slice((int)TextPlane1-4,80*70+8).ToArray()];
    }
}
