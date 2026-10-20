using CopperScreen;
using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CopperScreen.Tests;

public sealed class InstalledMediaProofTests
{
    [Fact]
    public void WorkspacePhaseControlRejectsRetainedStateAndRestoresExternalBackupAfterRemoval()
    {
        // Synthetic DOS1 metadata and a stub replay receipt test phase bookkeeping only.
        // Actual signed package/native acceptance requires the installed replay.
        var root = Path.Combine(Path.GetTempPath(), "copperscreen-media-phases-" + Guid.NewGuid().ToString("N"));
        var directory = Path.Combine(root, "private"); var output = Path.Combine(root, "external"); Directory.CreateDirectory(output);
        try
        {
            var bytes = new byte[901120]; "DOS\x01"u8.CopyTo(bytes);
            void U(int offset, uint value) => BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(880 * 512 + offset), value);
            U(0, 2); U(12, 72); U(312, uint.MaxValue); U(316, 881); U(508, 1);
            uint sum = 0; for (var i = 0; i < 512; i += 4) sum = unchecked(sum + BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(880 * 512 + i)));
            U(20, unchecked(0u - sum));
            var seed = Path.Combine(root, "synthetic.adf"); File.WriteAllBytes(seed, bytes);
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
            var image = CopperOsWorkspace.Prepare(directory, "persistence", seed, new("fixture", hash));
            var identity = Path.Combine(Path.GetDirectoryName(image)!, "workspace.json"); var json = JsonNode.Parse(File.ReadAllText(identity))!;
            json["SeedPairId"] = CopperOsWorkspace.BundledSeed.PairId; json["SeedSha256"] = CopperOsWorkspace.BundledSeed.Sha256;
            File.WriteAllText(identity, json.ToJsonString());
            var placeholder = Path.Combine(directory, "synthetic-empty.adf"); File.WriteAllBytes(placeholder, []);
            var profile = new CopperScreenSettingsDraft { DisplayName = "Separate profile fixture" }; profile.DriveDiskPaths[0] = placeholder;
            var profilePath = CopperScreenProfileStore.Save(profile, AppContext.BaseDirectory, directory);
            var profileBytes = File.ReadAllBytes(profilePath);
            var receipt = new { passed = true, engine = "Lightweight", legacy = false, hostDosCalls = false };
            CopperScreenInstalledMediaProof.Complete("seed", directory, output, receipt);
            Assert.Equal(profileBytes, File.ReadAllBytes(profilePath)); Assert.Empty(File.ReadAllBytes(placeholder));
            CopperScreenInstalledMediaProof.Complete("verify", directory, output, receipt);
            Assert.Throws<InvalidOperationException>(() => CopperScreenInstalledMediaProof.Prepare("fresh", directory, output));
            Assert.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, Path.GetFullPath(directory));
            Directory.Delete(directory, true); // Only this test's owned synthetic package-data root.
            var restored = CopperScreenInstalledMediaProof.Prepare("fresh", directory, output)!;
            Assert.Equal(bytes, File.ReadAllBytes(restored));
            var result = JsonSerializer.SerializeToElement(CopperScreenInstalledMediaProof.Complete("fresh", directory, output, receipt));
            Assert.True(result.GetProperty("PackagePrivateStateWasRemoved").GetBoolean());
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void SyntheticInstalledAdfControlWritesSavesAndReopensWithoutTouchingItsOriginals()
    {
        var root = Path.Combine(Path.GetTempPath(), "copperscreen-installed-media-" + Guid.NewGuid().ToString("N"));
        try
        {
            var proof = CopperScreenInstalledMediaProof.RunAdfProtection(Path.Combine(root, "private"), Path.Combine(root, "external"));
            Assert.Equal(new byte[901120], File.ReadAllBytes(proof.Original));
            var saved = File.ReadAllBytes(proof.Saved); Assert.Equal(37, saved[128]);
            Assert.NotEqual(File.ReadAllBytes(proof.Original), saved);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
