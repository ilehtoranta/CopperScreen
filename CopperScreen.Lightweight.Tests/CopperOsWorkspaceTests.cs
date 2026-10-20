using System.Buffers.Binary;
using System.Security.Cryptography;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed class CopperOsWorkspaceTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"copperos-workspace-"+Guid.NewGuid().ToString("N"));
    public CopperOsWorkspaceTests()=>Directory.CreateDirectory(root);
    private (string,CopperOsWorkspace.Seed) Seed(string name,byte marker)
    {
        var data=Image(marker);var path=Path.Combine(root,name+".adf");File.WriteAllBytes(path,data);
        return(path,new(name,Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant()));
    }
    private static byte[] Image(byte marker)
    {
        var b=new byte[901120];"DOS\x01"u8.CopyTo(b);b[100*512]=marker;
        void U(int at,uint value)=>BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(880*512+at),value);
        U(0,2);U(12,72);U(312,uint.MaxValue);U(316,881);U(508,1);
        uint sum=0;for(var at=0;at<512;at+=4)sum=unchecked(sum+BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(880*512+at)));
        U(20,unchecked(0u-sum));return b;
    }
    [Fact]
    public void WorkspaceAndSettingsSurviveRestartAndNewPackageSeedWithoutReplacement()
    {
        var(seed,definition)=Seed("package-v1",1);var user=Path.Combine(root,"user");
        var image=CopperOsWorkspace.Prepare(user,"personal",seed,definition);
        using(var workspace=CopperOsWorkspace.Open(image))workspace.Save(Image(37));
        var changed=File.ReadAllBytes(image);Assert.Equal(37,changed[100*512]);Assert.Equal(1,File.ReadAllBytes(seed)[100*512]);
        var(updated,v2)=Seed("package-v2",2);
        Assert.Equal(image,CopperOsWorkspace.Prepare(user,"personal",updated,v2));
        Assert.Equal(changed,File.ReadAllBytes(image));
        using(var reopened=CopperOsWorkspace.Open(image))Assert.Equal("package-v1",reopened.PairId);
        Assert.Equal(Image(1),File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(image)!,"Previous.adf")));
        var draft=CopperScreenSettingsDraft.FromStartupOptions(CopperScreenStartupOptions.Parse(["--copperstart","--rom","selected.rom",image],AppContext.BaseDirectory,userDataDirectory:user));
        draft.DriveWriteProtected[0]=false;new CopperScreenRecentSession(user).Save(draft,37,true,AppContext.BaseDirectory);
        var package2=Path.Combine(root,"installed-v2");Directory.CreateDirectory(package2);
        var settings=new CopperScreenRecentSession(user);var restored=settings.LoadMachine(package2)!;
        Assert.Null(settings.LastError);Assert.Equal(image,restored.DriveDiskPaths[0]);Assert.Equal(false,restored.DriveWriteProtected[0]);Assert.True(settings.Muted);Assert.Equal(37,settings.Volume);
    }
    [Fact]
    public void ExclusiveOwnershipAndFailedCheckpointKeepLastCompleteImage()
    {
        var(seed,definition)=Seed("source",1);var path=CopperOsWorkspace.Prepare(Path.Combine(root,"user"),"owned",seed,definition);
        using(var workspace=CopperOsWorkspace.Open(path))
        {
            Assert.Throws<IOException>(()=>CopperOsWorkspace.Open(path));
            var damaged=Image(2);damaged[880*512+312]=0;
            Assert.Throws<InvalidDataException>(()=>workspace.Save(damaged));Assert.Equal(Image(1),File.ReadAllBytes(path));
            File.SetAttributes(path,FileAttributes.ReadOnly);
            try { var error=Record.Exception(()=>workspace.Save(Image(3))); Assert.True(error is IOException or UnauthorizedAccessException); }
            finally {File.SetAttributes(path,FileAttributes.Normal);}
            Assert.Equal(Image(1),File.ReadAllBytes(path));
        }
        using var reopened=CopperOsWorkspace.Open(path);
    }
    [Fact]
    public void SeparateUsersNeverShareImagesAndDamagedMetadataDoesNotResetThem()
    {
        var(seed,definition)=Seed("source",1);
        var a=CopperOsWorkspace.Prepare(Path.Combine(root,"alice"),"default",seed,definition);
        var b=CopperOsWorkspace.Prepare(Path.Combine(root,"bob"),"default",seed,definition);
        using(var w=CopperOsWorkspace.Open(a))w.Save(Image(2));
        Assert.Equal(Image(1),File.ReadAllBytes(b));
        var broken=File.ReadAllBytes(a);broken[880*512+20]^=1;File.WriteAllBytes(a,broken);
        Assert.Throws<InvalidDataException>(()=>CopperOsWorkspace.Open(a));
        CopperOsWorkspace.Prepare(Path.Combine(root,"alice"),"default",seed,definition);
        Assert.Equal(broken,File.ReadAllBytes(a));
    }
    [Fact]
    public void AlteredSeedCannotCreateAnOwnedWorkspace()
    {
        var (seed, definition) = Seed("source", 1);
        var changed = File.ReadAllBytes(seed); changed[100 * 512] ^= 1; File.WriteAllBytes(seed, changed);
        var user = Path.Combine(root, "user");
        Assert.Throws<InvalidDataException>(() => CopperOsWorkspace.Prepare(user, "personal", seed, definition));
        Assert.False(Directory.Exists(Path.Combine(user, "CopperOS", "Workspaces", "personal")));
    }
    [Fact]
    public void NewRuntimeMustExplicitlyAdmitOriginalWorkspaceIdentity()
    {
        var (seed, original) = Seed("original-pair", 1);
        var image = CopperOsWorkspace.Prepare(Path.Combine(root, "user"), "personal", seed, original);
        var (_, updated) = Seed("updated-pair", 2);
        Assert.Throws<NotSupportedException>(() => CopperOsWorkspace.ValidateCompatibility(image, [updated]));
        Assert.Throws<NotSupportedException>(() => CopperOsWorkspace.ValidateCompatibility(image, [original with { Sha256 = updated.Sha256 }]));
        CopperOsWorkspace.ValidateCompatibility(image, [updated, original]);
        Assert.Equal(Image(1), File.ReadAllBytes(image));
    }
    [Theory]
    [InlineData("../outside")][InlineData("CON")][InlineData("a/b")][InlineData("")][InlineData("LPT1")]
    public void InvalidWorkspaceNamesNeverCreateFiles(string name)
        =>Assert.Throws<ArgumentException>(()=>CopperOsWorkspace.Prepare(root,name,null));
    public void Dispose()=>Directory.Delete(root,true);
}
public sealed class CopperOsStorageTests
{
    [CopperOsStorageFact]
    public void NativeStorageSurvivesAppRestartAndChangedPackageDirectory()
    {
        var proof=Environment.GetEnvironmentVariable("COPPERSCREEN_COPPEROS_STORAGE")!;
        var rom=Environment.GetEnvironmentVariable("COPPERSCREEN_COPPERSTART_ROM")!;
        var output=Path.Combine(proof,"storage-replays",DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ"));Directory.CreateDirectory(output);
        var user=Path.Combine(output,"user-data");
        var seed=Path.Combine(proof,"System.adf");
        CopperOsNativeStorageProof.Run(seed,rom,user,Path.Combine(output,"first"),false,AppContext.BaseDirectory);
        CopperOsNativeStorageProof.Run(seed,rom,user,Path.Combine(output,"restart"),true,AppContext.BaseDirectory);
        var package2=Path.Combine(output,"package-v2");Directory.CreateDirectory(package2);
        Directory.CreateDirectory(Path.Combine(package2,"Presets"));
        foreach(var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory,"Presets")))File.Copy(file,Path.Combine(package2,"Presets",Path.GetFileName(file)));
        CopperOsNativeStorageProof.Run(seed,rom,user,Path.Combine(output,"package-update-simulation"),true,package2);
    }
    [CopperOsStorageFact]
    public void NativeFullAndReadOnlyWorkspacesRecoverWithoutChangingData()
    {
        var proof=Environment.GetEnvironmentVariable("COPPERSCREEN_COPPEROS_STORAGE")!;
        var rom=Environment.GetEnvironmentVariable("COPPERSCREEN_COPPERSTART_ROM")!;
        var output=Path.Combine(proof,"negative-replays",DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ"));Directory.CreateDirectory(output);
        var seed=Path.Combine(proof,"System.adf");
        CopperOsNativeStorageProof.Run(seed,rom,Path.Combine(output,"readonly-user"),Path.Combine(output,"readonly"),false,AppContext.BaseDirectory,
            "COPPEROS STORAGE WRITE PROTECTED",true);
        var fullUser=Path.Combine(output,"full-user");var path=CopperOsWorkspace.Prepare(fullUser,"persistence",seed);
        var full=File.ReadAllBytes(path);var bitmap=BinaryPrimitives.ReadUInt32BigEndian(full.AsSpan(880*512+316));
        full.AsSpan((int)bitmap*512,512).Clear();
        using(var workspace=CopperOsWorkspace.Open(path))workspace.Save(full);
        CopperOsNativeStorageProof.Run(seed,rom,fullUser,Path.Combine(output,"full"),false,AppContext.BaseDirectory,"COPPEROS STORAGE DISK FULL");
    }

    [CopperOsStorageFact]
    public void NativeWorkspaceCanExportAfterHostCheckpointFailure()
    {
        var proof=Environment.GetEnvironmentVariable("COPPERSCREEN_COPPEROS_STORAGE")!;
        var rom=Environment.GetEnvironmentVariable("COPPERSCREEN_COPPERSTART_ROM")!;
        var output=Path.Combine(proof,"recovery-replays",DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ"));Directory.CreateDirectory(output);
        var seed=Path.Combine(proof,"System.adf");var exported=Path.Combine(output,"denied","recovered.adf");
        CopperOsNativeStorageProof.Run(seed,rom,Path.Combine(output,"user"),Path.Combine(output,"denied"),false,AppContext.BaseDirectory,denyHostSaves:true);
        var imported = Path.Combine(output, "denied", "imported-user", "CopperOS", "Workspaces", "imported", "System.adf");
        CopperOsNativeStorageProof.Run(seed,rom,Path.Combine(output,"reopen-user"),Path.Combine(output,"reopened-export"),true,AppContext.BaseDirectory,directImage:imported);
    }

}
public sealed class CopperOsStorageFactAttribute:FactAttribute
{
    public CopperOsStorageFactAttribute()
    {
        if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COPPERSCREEN_COPPEROS_STORAGE"))||string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COPPERSCREEN_COPPERSTART_ROM")))Skip="Native CopperOS storage pair unavailable; no native persistence coverage.";
    }
}
