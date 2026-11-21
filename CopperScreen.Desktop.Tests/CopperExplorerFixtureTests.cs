using System.Security.Cryptography;
using System.Text.Json;

namespace CopperScreen.Tests;

public sealed partial class DiskImageOperationsTests
{
    [Fact]
    public void GeneratesIndependentConsumerFixturesForEverySupportedFormat()
    {
        var output = Environment.GetEnvironmentVariable("COPPEREXPLORER_FIXTURE_OUTPUT");
        var receipts = new List<object>();
        for (var format = 0; format < 8; format++)
        {
            var path = Blank("Format" + format, format); Seed(path, format);
            using (var image = CopperScreenDiskImage.Open(path))
            {
                var volume = image.Volumes[0];
                var tree = Assert.Single(volume.ReadDirectory(volume.Root));
                var entries = volume.ReadDirectory(tree);
                Assert.Equal(3, entries.Count);
                using var content = new MemoryStream(); volume.CopyFileTo(entries.Single(e => e.Name == "CON"), content);
                Assert.Equal(Payload, content.ToArray());
            }
            receipts.Add(new { format, file = Path.GetFileName(path), sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) });
            if (!string.IsNullOrEmpty(output)) { Directory.CreateDirectory(output); File.Copy(path, Path.Combine(output, Path.GetFileName(path)), true); }
        }
        if (!string.IsNullOrEmpty(output)) File.WriteAllText(Path.Combine(output, "manifest.json"), JsonSerializer.Serialize(receipts));
        if (!string.IsNullOrEmpty(output))
        {
            var dmsReceipts = new List<object>();
            for (var dos = 0; dos < 8; dos++)
            {
                var dms = DmsImage(dos);
                File.Copy(dms, Path.Combine(output, Path.GetFileName(dms)), true);
                dmsReceipts.Add(new { format = dos, file = Path.GetFileName(dms), sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(dms))) });
            }
            File.WriteAllText(Path.Combine(output, "dms-manifest.json"), JsonSerializer.Serialize(dmsReceipts));
            for (var dos = 0; dos < 2; dos++)
                File.WriteAllBytes(Path.Combine(output, $"ReadOnly{dos}.ipf"), CopperDisk.Tests.FileSystemIpfFixture.Create(new CopperDisk.Tests.AmigaFileSystemFixture(dos).Bytes, missingTrack:159));
            var wide = ServiceImageFixture.Smart(true, wide:true);
            File.WriteAllText(Path.Combine(output,"SparseSfs2.json"),JsonSerializer.Serialize(new { length=wide.Length, blocks=wide.Blocks }));
        }
    }
}
