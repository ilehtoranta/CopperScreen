using CopperDisk.FileSystem;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed partial class DiskImageOperationsTests
{
    [Theory]
    [InlineData("CON")] [InlineData("NUL.txt")] [InlineData("COM¹")] [InlineData("x?y.mod")]
    [InlineData("tail.")] [InlineData("tail ")] [InlineData(".copperimage-v1-434F4E")]
    [InlineData("back\\slash.mod")]
    public void HostAliasesAreCanonicalAndReversible(string name)
    {
        var alias = HostNameCodec.MapSiblings([name])[name];
        Assert.StartsWith(HostNameCodec.Prefix, alias); Assert.Equal(name, HostNameCodec.Decode(alias));
        Assert.Equal(alias, HostNameCodec.Decode(alias, HostNameInterpretation.Literal));
        Assert.True(alias.Length <= 255); DiskTransfer.ValidateName(alias);
    }

    [Fact]
    public void SiblingMappingIsIndependentOfSelectionAndEnumerationOrder()
    {
        var map = HostNameCodec.MapSiblings(["normal.txt", "Foo", "FOO", "CON.txt"]);
        var reversed = HostNameCodec.MapSiblings(["CON.txt", "FOO", "Foo", "normal.txt"]);
        Assert.Equal("normal.txt", map["normal.txt"]);
        Assert.Equal(HostNameCodec.Encode("Foo"), map["Foo"]); Assert.Equal(HostNameCodec.Encode("FOO"), map["FOO"]);
        Assert.EndsWith(".txt", map["CON.txt"]);
        foreach (var pair in map) Assert.Equal(pair.Value, reversed[pair.Key]);
    }

    [Theory]
    [InlineData(".copperimage-v1-ZZ")] [InlineData(".copperimage-v1-2E2E")]
    [InlineData(".copperimage-v1-612F62")] [InlineData(".copperimage-v1-434F4E.txt")]
    public void MalformedOrUnsafeAliasesRequireExplicitLiteralImport(string name)
    {
        Assert.Throws<InvalidDataException>(() => HostNameCodec.Decode(name));
        Assert.Equal(name, HostNameCodec.Decode(name, HostNameInterpretation.Literal));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    public void EncodedTreeExportsAndImportsRoundTripThroughGuestExecution(int format)
    {
        var sourcePath = Blank("Names", format); Seed(sourcePath, format); var targetPath = Blank("Restored", format);
        var host = Path.Combine(_root, "Export"); Directory.CreateDirectory(host);
        using var source = DiskImageSession.Open(sourcePath); var sv = source.Volumes[0];
        DiskTransfer.Extract(sv, sv.ReadDirectory(sv.Root), host);
        Assert.True(File.Exists(Path.Combine(host, "Tree", HostNameCodec.Encode("CON"))));
        Assert.Single(HostNameCodec.Preview(Directory.GetFileSystemEntries(host)));
        using var target = DiskImageSession.Open(targetPath); var tv = target.Volumes[0];
        target.Import(tv, tv.Root, Directory.GetFileSystemEntries(host)); tv = target.Volumes[0];
        var tree = Assert.Single(tv.ReadDirectory(tv.Root)); var file = tv.ReadDirectory(tree).Single(e => e.Name == "CON");
        Assert.Equal(file.Name, target.ResolveEntry(file.Id).Name);
        Assert.True(tv.Capabilities.Operations.HasFlag(DiskVolumeOperations.Import));
        using var bytes = new MemoryStream(); tv.CopyFileTo(file, bytes); Assert.Equal(Payload, bytes.ToArray());
        using var read = tv.OpenRead(file); var before = File.ReadAllBytes(targetPath);
        Assert.Throws<IOException>(() => target.CreateDirectory(tv, tv.Root, "Blocked")); Assert.Equal(before, File.ReadAllBytes(targetPath));
        var oldId = file.Id; read.Dispose(); target.Refresh();
        Assert.Throws<InvalidOperationException>(() => target.ResolveEntry(oldId));
    }
}
