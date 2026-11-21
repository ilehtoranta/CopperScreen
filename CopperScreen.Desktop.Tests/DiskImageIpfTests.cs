using CopperDisk.FileSystem;
using CopperDisk.Tests;

namespace CopperScreen.Tests;

public sealed class DiskImageIpfTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "CopperIpf-" + Guid.NewGuid().ToString("N"));
    public DiskImageIpfTests() => Directory.CreateDirectory(root);
    public void Dispose() => Directory.Delete(root, true);

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public void RecognizableIpfBrowsesAndExtractsWithoutReadingUnusedMissingSectors(int dos)
    {
        var fixture = new AmigaFileSystemFixture(dos); var ipf = FileSystemIpfFixture.Create(fixture.Bytes, missingTrack:159);
        var path = Path.Combine(root,"Disk.ipf"); File.WriteAllBytes(path,ipf);
        using (var image = DiskImageSession.Open(path))
        {
            Assert.True(image.IsReadOnly); var v = image.Volumes[0]; var entries = v.ReadDirectory(v.Root);
            using var output = new MemoryStream(); v.CopyFileTo(entries.Single(e=>e.Name=="ReadMe"),output);
            Assert.Equal(fixture.Content,output.ToArray());
            Assert.Throws<NotSupportedException>(()=>image.CreateDirectory(v,v.Root,"Refused"));
        }
        Assert.Equal(ipf,File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData("metadata")] [InlineData("data")] [InlineData("ambiguous")] [InlineData("weak")]
    public void RequiredUnreadableSectorsFailExplicitlyAndNeverPublishFabricatedBytes(string failure)
    {
        var f = new AmigaFileSystemFixture();
        var ipf=FileSystemIpfFixture.Create(f.Bytes, missingTrack:failure=="metadata"?80:failure=="data"?9:-1,
            conflictingTrack:failure=="ambiguous"?9:-1,weakTrack:failure=="weak"?9:-1);
        var path=Path.Combine(root,"Disk.ipf"); File.WriteAllBytes(path,ipf);
        using var image=DiskImageSession.Open(path); var v=image.Volumes[0];
        if(failure=="metadata") Assert.ThrowsAny<IOException>(()=>v.Mount());
        else
        {
            var host=Path.Combine(root,"out"); Directory.CreateDirectory(host);
            Assert.ThrowsAny<IOException>(()=>DiskTransfer.Extract(v,v.ReadDirectory(v.Root),host));
            Assert.Empty(Directory.GetFileSystemEntries(host));
        }
        Assert.Equal(ipf,File.ReadAllBytes(path));
    }
}
