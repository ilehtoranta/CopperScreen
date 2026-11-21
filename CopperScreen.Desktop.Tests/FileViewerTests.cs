using System.Text;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed class FileViewerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "copper-viewer-" + Guid.NewGuid().ToString("N"));
    public FileViewerTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void TextPagingPreservesUtf8AndCrLfAcrossBoundaries(bool latin1)
    {
        var expected = new string('a', 4095) + "\r\n" + new string('b', 4093) + (latin1 ? "éü\r\nEnd" : "😀é\r\nEnd");
        var encoding = latin1 ? Encoding.Latin1 : Encoding.UTF8;
        var bytes = encoding.GetBytes(expected); var path = Path.Combine(_root, "Text"); File.WriteAllBytes(path, bytes);
        var source = CopperScreenFileViewSource.Host(path); var text = new StringBuilder();
        for (long offset = 0; offset < bytes.Length; offset += CopperScreenFileViewSource.PageSize)
        {
            var page = source.ReadPage(offset, default); Assert.True(page.Data.Length <= CopperScreenFileViewSource.PageSize + 5);
            text.Append(page.Text(!latin1));
        }
        Assert.Equal(expected.Replace("\r\n", "\n"), text.ToString()); Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData(4093)] [InlineData(4094)] [InlineData(4095)]
    public void FourByteUtf8CharactersAreShownOnceAtPageBoundaries(int padding)
    {
        var expected = new string('x', padding) + "😀tail";
        var path = Path.Combine(_root, "Unicode"); File.WriteAllBytes(path, Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(expected)).ToArray());
        var source = CopperScreenFileViewSource.Host(path); var text = new StringBuilder();
        for (long offset = 0; offset < source.Length; offset += CopperScreenFileViewSource.PageSize)
        {
            var page = source.ReadPage(offset, default); Assert.True(page.IsUtf8()); text.Append(page.Text(true));
        }
        Assert.Equal(expected, text.ToString());
    }

    [Fact]
    public void HexShowsExactBytesOffsetsAndAsciiAndTextDoesNotInterpretControls()
    {
        var page = new FileViewPage(0, [0, 9, 10, 13, 27, 32, 65, 126, 127, 255]);
        var hex = page.Hex(false);
        Assert.StartsWith("00000000  00 09 0A 0D 1B 20 41 7E  7F FF", hex); Assert.Contains(" | ..... A~..", hex);
        Assert.False(page.IsUtf8()); Assert.Equal("·\t\n\n· A~·ÿ", page.Text(false));
        Assert.Equal("", new FileViewPage(0, []).Hex(false)); Assert.Equal("", new FileViewPage(0, []).Text(true));
    }

    [Fact]
    public void LargeOffsetsAreBoundedAndEachPageClosesItsReadStream()
    {
        long requested = -1; var disposed = 0;
        var source = new CopperScreenFileViewSource("Large", "Sparse", 0x1_0000_0005,
            (offset, _) => { requested = offset; return new ClosedStream([0, 65, 66, 67, 68, 69], () => disposed++); });
        var page = source.ReadPage(0x1_0000_0000, default);
        Assert.Equal(0xFFFF_FFFFL, requested); Assert.Equal(5, page.Count); Assert.Equal(1, disposed);
        Assert.StartsWith("0000000100000000  41 42 43 44 45", page.Hex(true)); Assert.Equal("ABCDE", page.Text(true));
        Assert.Throws<ArgumentOutOfRangeException>(() => source.ReadPage(-1, default));
        Assert.Throws<ArgumentOutOfRangeException>(() => source.ReadPage(source.Length, default));
        Assert.ThrowsAny<OperationCanceledException>(() => source.ReadPage(0, new(true))); Assert.Equal(1, disposed);
    }

    [Fact]
    public void ChangedHostFilesAndShortReadsFailRatherThanDisplayStaleOrInventedBytes()
    {
        var path = Path.Combine(_root, "Changed"); File.WriteAllBytes(path, [1, 2]); var source = CopperScreenFileViewSource.Host(path);
        File.WriteAllBytes(path, [3]); Assert.Throws<IOException>(() => source.ReadPage(0, default));
        var shortSource = new CopperScreenFileViewSource("Short", "Short", 40, (_, _) => new MemoryStream([1]));
        Assert.Throws<EndOfStreamException>(() => shortSource.ReadPage(0, default));
    }

    private sealed class ClosedStream(byte[] bytes, Action closed) : MemoryStream(bytes)
    { protected override void Dispose(bool disposing) { base.Dispose(disposing); if (disposing) closed(); } }
}

public sealed partial class DiskImageOperationsTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    public void FileViewerPagesReadActualGuestFilesInEveryFormatWithoutChangingTheImage(int format)
    {
        var path = Blank("View", format); Seed(path, format); var before = File.ReadAllBytes(path);
        using var image = CopperScreenDiskImage.Open(path); var volume = image.Volumes[0];
        var tree = Assert.Single(volume.ReadDirectory(volume.Root)); var children = volume.ReadDirectory(tree);
        var source = CopperScreenFileViewSource.Image(children.Single(e => e.Name == "CON"));
        using var result = new MemoryStream();
        for (long offset = 0; offset < source.Length; offset += CopperScreenFileViewSource.PageSize)
        {
            var page = source.ReadPage(offset, default); result.Write(page.Data.AsSpan(offset > 0 ? 1 : 0, page.Count));
        }
        Assert.Equal(Payload, result.ToArray());
        Assert.Equal(0, CopperScreenFileViewSource.Image(children.Single(e => e.Name == "Zero")).ReadPage(0, default).Count);
        Assert.Throws<NotSupportedException>(() => CopperScreenFileViewSource.Image(tree)); Assert.Equal(before, File.ReadAllBytes(path));
    }
}
