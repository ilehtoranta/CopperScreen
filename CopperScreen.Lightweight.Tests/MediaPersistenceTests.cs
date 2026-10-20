using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json.Nodes;
using CopperScreen;

namespace CopperScreen.Tests;

// Synthetic DOS1 metadata volumes. Native Shell/DOS replay is a separate check.
public sealed class MediaPersistenceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "copperscreen-media-" + Guid.NewGuid().ToString("N"));
    public MediaPersistenceTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);
    private string PathFor(string name) => Path.Combine(_root, name);
    private static byte[] Image(byte marker)
    {
        var bytes = new byte[901120]; "DOS\x01"u8.CopyTo(bytes); bytes[100 * 512] = marker;
        void U(int at, uint value) => BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(880 * 512 + at), value);
        U(0, 2); U(12, 72); U(312, uint.MaxValue); U(316, 881); U(508, 1);
        uint sum = 0; for (var i = 0; i < 512; i += 4) sum = unchecked(sum + BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(880 * 512 + i)));
        U(20, unchecked(0u - sum)); return bytes;
    }
    private string Workspace(string user = "user", string id = "personal")
    {
        var seed = PathFor("fixture.adf"); var bytes = Image(1); File.WriteAllBytes(seed, bytes);
        var definition = new CopperOsWorkspace.Seed("synthetic", Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant());
        var image = CopperOsWorkspace.Prepare(PathFor(user), id, seed, definition);
        // Model a user-mutated image with an admitted identity, not a native boot proof.
        var metadata = Path.Combine(Path.GetDirectoryName(image)!, "workspace.json");
        var json = JsonNode.Parse(File.ReadAllText(metadata))!;
        json["SeedPairId"] = CopperOsWorkspace.BundledSeed.PairId;
        json["SeedSha256"] = CopperOsWorkspace.BundledSeed.Sha256;
        File.WriteAllText(metadata, json.ToJsonString()); return image;
    }

    [Theory]
    [InlineData("--hdf", true)]
    [InlineData("--hdf-readonly", true)]
    [InlineData("--hdf-writable", false)]
    [InlineData("--hardfile-writable", false)]
    public void HardfileImportIsProtectedUnlessWritesAreExplicit(string option, bool readOnly)
    {
        var options = CopperScreenStartupOptions.Parse([option, PathFor("user-owned.hdf")], AppContext.BaseDirectory);
        Assert.Equal(readOnly, Assert.Single(options.HardDrives).ReadOnly);
    }

    [Fact]
    public void SnapshotRefusesImplicitReplacementAndPublishesOnlyCompleteBytes()
    {
        var path = PathFor("original.adf"); File.WriteAllBytes(path, Image(1));
        Assert.Throws<IOException>(() => CopperScreenAtomicFile.Write(path, Image(2)));
        Assert.Equal(Image(1), File.ReadAllBytes(path));
        CopperScreenAtomicFile.Write(path, Image(2), overwrite: true, backup: PathFor("backup.adf"));
        Assert.Equal(Image(2), File.ReadAllBytes(path)); Assert.Equal(Image(1), File.ReadAllBytes(PathFor("backup.adf")));
        Assert.Empty(Directory.GetFiles(_root, ".snapshot-*"));
    }

    [Fact]
    public void PermissionLossAndMissingStoragePreserveTheCheckpointAndCleanTemporaryFiles()
    {
        var image = Workspace(); File.SetAttributes(image, FileAttributes.ReadOnly);
        try { Assert.True(Record.Exception(() => CopperScreenAtomicFile.Write(image, Image(2), true)) is IOException or UnauthorizedAccessException); }
        finally { File.SetAttributes(image, FileAttributes.Normal); }
        Assert.Equal(Image(1), File.ReadAllBytes(image));
        Assert.Throws<DirectoryNotFoundException>(() => CopperScreenAtomicFile.Write(PathFor("removed/saved.adf"), Image(2)));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(image)!, ".snapshot-*"));
        Assert.Equal(Image(1), File.ReadAllBytes(PathFor("fixture.adf")));
    }

    [Fact]
    public void BackupRoundTripsAcrossUserRootsWithoutChangingTheOriginalOrExistingWorkspace()
    {
        var image = Workspace(); using (var owner = CopperOsWorkspace.Open(image)) owner.Save(Image(37));
        var backup = PathFor("portable.copperos"); CopperOsWorkspace.ExportSaved(image, backup);
        var backupBytes = File.ReadAllBytes(backup);
        var imported = CopperOsWorkspace.ImportBackup(PathFor("other-account"), "imported", backup);
        CopperOsWorkspace.ValidateOwnership(imported);
        using (var reopened = CopperOsWorkspace.Open(imported)) Assert.Equal(CopperOsWorkspace.BundledSeed.PairId, reopened.PairId);
        Assert.Equal(Image(37), File.ReadAllBytes(imported));
        Assert.Throws<IOException>(() => CopperOsWorkspace.ImportBackup(PathFor("other-account"), "imported", backup));
        Assert.Throws<IOException>(() => CopperOsWorkspace.ExportSaved(image, backup));
        Assert.Throws<IOException>(() => CopperOsWorkspace.ExportSaved(image, Path.Combine(Path.GetDirectoryName(image)!, "backup.copperos")));
        Assert.Equal(backupBytes, File.ReadAllBytes(backup)); Assert.Equal(Image(37), File.ReadAllBytes(image));
        Assert.Equal(Image(1), File.ReadAllBytes(PathFor("fixture.adf")));
    }

    [Theory]
    [InlineData("hash")]
    [InlineData("pair")]
    [InlineData("image")]
    [InlineData("extra")]
    public void InvalidBackupCannotCreateOrReplaceUserStorage(string damage)
    {
        var image = Workspace(); var path = PathFor("damaged.copperos"); CopperOsWorkspace.ExportSaved(image, path);
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Update))
        {
            if (damage == "extra") zip.CreateEntry("../escaped");
            else if (damage == "image")
            { var entry = zip.GetEntry("System.adf")!; entry.Delete(); using var output = zip.CreateEntry("System.adf").Open(); output.Write(Image(99)); }
            else
            {
                var entry = zip.GetEntry("export.json")!; JsonNode json;
                using (var input = entry.Open()) json = JsonNode.Parse(input)!;
                entry.Delete(); json[damage == "hash" ? "ImageSha256" : "SeedPairId"] = "invalid";
                using var output = zip.CreateEntry("export.json").Open(); System.Text.Json.JsonSerializer.Serialize(output, json);
            }
        }
        var failure = Record.Exception(() => CopperOsWorkspace.ImportBackup(PathFor("other"), "restored", path));
        Assert.True(failure is InvalidDataException or NotSupportedException);
        Assert.False(Directory.Exists(PathFor("other/CopperOS/Workspaces/restored")));
        Assert.Equal(Image(1), File.ReadAllBytes(image));
    }

    [Fact]
    public void RecoveryRequiresAnIdleExclusiveOwnerAndPreservesCurrentAndPreviousImages()
    {
        var image = Workspace(); using (var owner = CopperOsWorkspace.Open(image))
        { owner.Save(Image(2)); Assert.Throws<IOException>(() => CopperOsWorkspace.RecoverPrevious(image)); }
        var damaged = Image(99); damaged[880 * 512 + 20] ^= 1; File.WriteAllBytes(image, damaged);
        var previous = Path.Combine(Path.GetDirectoryName(image)!, "Previous.adf");
        var backup = PathFor("last-good.copperos"); CopperOsWorkspace.ExportSaved(image, backup, previous: true);
        var preserved = CopperOsWorkspace.RecoverPrevious(image);
        Assert.Equal(damaged, File.ReadAllBytes(preserved)); Assert.Equal(Image(1), File.ReadAllBytes(image));
        Assert.Equal(Image(1), File.ReadAllBytes(previous)); using var reopened = CopperOsWorkspace.Open(image);
    }

    [Fact]
    public void BackupMetadataCannotBypassTheReadLimitByLyingAboutItsZipLength()
    {
        var image = Workspace(); var backup = PathFor("oversized-metadata.copperos"); CopperOsWorkspace.ExportSaved(image, backup);
        using (var zip = ZipFile.Open(backup, ZipArchiveMode.Update))
        {
            var entry = zip.GetEntry("export.json")!; string json;
            using (var reader = new StreamReader(entry.Open())) json = reader.ReadToEnd();
            entry.Delete(); using var writer = new StreamWriter(zip.CreateEntry("export.json").Open());
            writer.Write(new string(' ', 8192) + json);
        }
        var bytes = File.ReadAllBytes(backup); var patched = false;
        for (var i = 0; i <= bytes.Length - 46 - "export.json".Length; i++)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(i)) != 0x02014b50 ||
                !bytes.AsSpan(i + 46, "export.json".Length).SequenceEqual("export.json"u8)) continue;
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i + 24), 4096); patched = true; break;
        }
        Assert.True(patched); File.WriteAllBytes(backup, bytes);
        Assert.Throws<InvalidDataException>(() => CopperOsWorkspace.ImportBackup(PathFor("other"), "oversized", backup));
        Assert.False(Directory.Exists(PathFor("other/CopperOS/Workspaces/oversized")));
        Assert.Equal(Image(1), File.ReadAllBytes(image));
    }

    [Fact]
    public void UnsupportedPairCanBePreservedButCannotBeImportedOrRecoveredByThisApp()
    {
        var image = Workspace(); using (var owner = CopperOsWorkspace.Open(image)) owner.Save(Image(2));
        var metadata = Path.Combine(Path.GetDirectoryName(image)!, "workspace.json");
        var json = JsonNode.Parse(File.ReadAllText(metadata))!; json["SeedPairId"] = "older-pair"; File.WriteAllText(metadata, json.ToJsonString());
        var backup = PathFor("preserved.copperos"); CopperOsWorkspace.ExportSaved(image, backup);
        Assert.True(File.Exists(backup));
        Assert.Throws<NotSupportedException>(() => CopperOsWorkspace.ImportBackup(PathFor("other"), "older", backup));
        Assert.Throws<NotSupportedException>(() => CopperOsWorkspace.RecoverPrevious(image));
        Assert.Equal(Image(2), File.ReadAllBytes(image));
    }

    [Fact]
    public void MissingPrimaryRecoversButInvalidPreviousNeverResetsAWorkspace()
    {
        var image = Workspace(); using (var owner = CopperOsWorkspace.Open(image)) owner.Save(Image(2));
        File.Delete(image); CopperOsWorkspace.RecoverPrevious(image); Assert.Equal(Image(1), File.ReadAllBytes(image));
        var previous = Path.Combine(Path.GetDirectoryName(image)!, "Previous.adf"); File.WriteAllBytes(previous, [1, 2, 3]);
        Assert.Throws<InvalidDataException>(() => CopperOsWorkspace.RecoverPrevious(image));
        Assert.Equal(Image(1), File.ReadAllBytes(image)); Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(previous));
    }

    [Theory]
    [InlineData("Partial", 2, 1)]
    [InlineData("Flushed", 2, 1)]
    [InlineData("Published", 37, 2)]
    [InlineData("FailPartial", 2, 1)]
    public async Task AbruptProcessTerminationLeavesACompleteReopenableCheckpoint(string stage, byte expected, byte previousExpected)
    {
        var image = Workspace(); using (var owner = CopperOsWorkspace.Open(image)) owner.Save(Image(2));
        var incoming = PathFor("incoming.adf"); File.WriteAllBytes(incoming, Image(37)); var signal = PathFor("barrier.txt");
        var probe = Path.Combine(AppContext.BaseDirectory, "CopperScreen.Storage.Probe.dll");
        Assert.True(File.Exists(probe), "Build the test project with its diagnostic process probe first.");
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var argument in new[] { probe, image, incoming, stage, signal }) start.ArgumentList.Add(argument);
        using var child = Process.Start(start)!;
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            if (stage == "FailPartial")
            {
                await child.WaitForExitAsync(deadline.Token); Assert.Equal(3, child.ExitCode);
                Assert.Contains("disk-full", await child.StandardError.ReadToEndAsync());
                Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(image)!, ".snapshot-*"));
            }
            else
            {
                while (!File.Exists(signal))
                {
                    if (child.HasExited) Assert.Fail(await child.StandardError.ReadToEndAsync());
                    await Task.Delay(25, deadline.Token);
                }
                child.Kill(entireProcessTree: true); await child.WaitForExitAsync();
            }
        }
        finally { if (!child.HasExited) { child.Kill(entireProcessTree: true); await child.WaitForExitAsync(); } }
        // Windows can signal process exit just before its file handles finish closing.
        // Keep the live-owner lock checks strict; wait only after our writer is dead.
        var release = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                using var lease = new FileStream(Path.Combine(Path.GetDirectoryName(image)!, "workspace.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                break;
            }
            catch (IOException ex) when (((ex.HResult & 0xffff) is 32 or 33) && release.Elapsed < TimeSpan.FromSeconds(5))
            { await Task.Delay(25); }
        }
        Assert.Equal(Image(expected), File.ReadAllBytes(image));
        Assert.Equal(Image(previousExpected), File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(image)!, "Previous.adf")));
        using var reopened = CopperOsWorkspace.Open(image);
        Assert.Equal(Image(1), File.ReadAllBytes(PathFor("fixture.adf")));
    }
}
