using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed class CopperOsSystemUpdateTests
{
    [Fact]
    public void CurrentThreeComponentBundleIsIdempotentAndMismatchedGuiDoesNotPublishPartialUpdates()
    {
        using var stream=typeof(CopperScreenBundledCopperOs).Assembly.GetManifestResourceStream("CopperScreen.Bundled.CopperOS.hdf")!;
        using var buffer=new MemoryStream();stream.CopyTo(buffer);var image=buffer.ToArray();var before=(byte[])image.Clone();
        var home=CopperScreenCopperOsSystemUpdate.ReadHome(image);var command=CopperScreenCopperOsSystemUpdate.ReadCommand(image);var gui=CopperScreenCopperOsSystemUpdate.ReadGui(image);
        Assert.True(CopperScreenCopperOsSystemUpdate.TryCreateSystemImage(image,home,command,gui,out var same));Assert.Null(same);
        gui[20]^=1;
        Assert.False(CopperScreenCopperOsSystemUpdate.TryCreateSystemImage(image,home,command,gui,out var mismatch));Assert.Null(mismatch);
        Assert.Equal(before,image);
    }

    [Fact]
    public void UpdateRetainsEveryByteOutsideTheRecognizedPayloadIncludingFileTables()
    {
        var (image, original, blocks) = Fixture();
        image[^10] = 0x5A;
        var replacement = original.Select(b => (byte)(b ^ 0x73)).ToArray();
        var expected = (byte[])image.Clone(); var at = 0;
        foreach (var block in blocks)
        {
            var count = Math.Min(512, replacement.Length - at);
            replacement.AsSpan(at, count).CopyTo(expected.AsSpan(block * 512, count)); at += count;
        }
        Assert.True(CopperScreenCopperOsSystemUpdate.TryCreateImage(image, replacement, Hash(original), out var updated));
        Assert.Equal(expected, updated); Assert.Equal(replacement, CopperScreenCopperOsSystemUpdate.ReadHome(updated!));
        Assert.Equal(original, CopperScreenCopperOsSystemUpdate.ReadHome(image));
    }

    [Fact]
    public void CurrentOrCustomizedComponentsAndDifferentLengthsAreNeverOverwritten()
    {
        var (image, original, _) = Fixture(); var before = (byte[])image.Clone();
        Assert.True(CopperScreenCopperOsSystemUpdate.TryCreateImage(image, original, Hash(original), out var current));
        Assert.Null(current);
        var replacement = original.Select(b => (byte)(b ^ 1)).ToArray();
        Assert.False(CopperScreenCopperOsSystemUpdate.TryCreateImage(image, replacement, Hash([1,2,3]), out var customized));
        Assert.Null(customized);
        Assert.False(CopperScreenCopperOsSystemUpdate.TryCreateImage(image, [1,2,3], Hash(original), out var resized));
        Assert.Null(resized); Assert.Equal(before, image);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void InvalidChecksumsAndAliasedDataCannotProduceAnUpdatedImage(bool alias)
    {
        var (image, original, blocks) = Fixture();
        if (alias)
        {
            BinaryPrimitives.WriteUInt32BigEndian(image.AsSpan(3*512+76*4,4),(uint)blocks[0]);
            Checksum(image,3);
        }
        else image[3*512+20] ^= 1;
        var before = (byte[])image.Clone();
        Assert.Throws<InvalidDataException>(() => CopperScreenCopperOsSystemUpdate.TryCreateImage(image,
            original.Select(b=>(byte)(b^1)).ToArray(),Hash(original),out _));
        Assert.Equal(before,image);
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static void Checksum(byte[] image,int block)
    {
        var span=image.AsSpan(block*512,512); BinaryPrimitives.WriteUInt32BigEndian(span[20..],0);
        uint sum=0;for(var i=0;i<128;i++)sum=unchecked(sum+BinaryPrimitives.ReadUInt32BigEndian(span[(i*4)..]));
        BinaryPrimitives.WriteUInt32BigEndian(span[20..],unchecked(0u-sum));
    }
    private static (byte[] Image,byte[] Payload,List<int> Blocks) Fixture()
    {
        var image=new byte[400*512]; const int root=200, directory=2, header=3;
        void Word(int block,int index,uint value)=>BinaryPrimitives.WriteUInt32BigEndian(image.AsSpan(block*512+index*4,4),value);
        void Header(int block,int parent,string name,uint type)
        {
            Word(block,0,2);Word(block,125,(uint)parent);Word(block,127,type);
            image[block*512+432]=(byte)name.Length;Encoding.ASCII.GetBytes(name).CopyTo(image,block*512+433);
        }
        int Bucket(string name){var hash=name.Length;foreach(var c in name.ToUpperInvariant())hash=(hash*13+c)&2047;return 6+hash%72;}
        BinaryPrimitives.WriteUInt32BigEndian(image,0x444F5301);BinaryPrimitives.WriteUInt32BigEndian(image.AsSpan(8),root);
        Header(root,0,"CopperOS",1);Header(directory,root,"L",2);Header(header,directory,"CopperHome",0xFFFFFFFD);
        Word(root,Bucket("L"),directory);Word(directory,Bucket("CopperHome"),header);
        var payload=Enumerable.Range(0,41500).Select(i=>(byte)(i*41+19)).ToArray();Word(header,81,(uint)payload.Length);
        var next=4;var table=header;var blocks=new List<int>();var count=(payload.Length+511)/512;
        for(var i=0;i<count;i++)
        {
            if(i!=0 && i%72==0)
            {
                var extension=next++;Word(table,126,(uint)extension);Checksum(image,table);table=extension;
                Word(table,0,16);Word(table,125,header);Word(table,127,0xFFFFFFFD);
            }
            var block=next++;blocks.Add(block);Word(table,2,(uint)(i%72+1));Word(table,77-i%72,(uint)block);
            var take=Math.Min(512,payload.Length-i*512);payload.AsSpan(i*512,take).CopyTo(image.AsSpan(block*512,take));
        }
        Checksum(image,table);Checksum(image,root);Checksum(image,directory);
        return(image,payload,blocks);
    }
}
