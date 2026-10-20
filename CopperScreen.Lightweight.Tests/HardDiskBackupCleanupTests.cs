using System.Text.Json;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed class HardDiskBackupCleanupTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "copper-backup-cleanup-" + Guid.NewGuid().ToString("N"));
    public HardDiskBackupCleanupTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);
    private string? _data;
    private string Data => _data ?? Path.Combine(_root, "data");
    private string Journals => Path.Combine(Data, "HardDiskBackupOperations");
    private static void Windows() { Assert.True(OperatingSystem.IsWindows(), "Windows file-identity cleanup requires its Windows qualification host."); }

    private (string Path, string Journal, CopperScreenHardDiskBackupCleanup.Operation Operation) Export(string? parent = null)
    {
        Windows(); var id = Guid.NewGuid().ToString("N");
        var path = Path.Combine(parent ?? _root, ".hdf-backup-" + id + ".tmp");
        var operation = CopperScreenHardDiskBackupCleanup.Begin(Data, "export", path, id)!;
        using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
        { operation.TrackRoot(file.SafeFileHandle); file.Write(new byte[4096]); }
        return (path, Path.Combine(Journals, id + ".journal"), operation);
    }
    private (string Path, string Journal, CopperScreenHardDiskBackupCleanup.Operation Operation) Restore()
    {
        Windows(); var id = Guid.NewGuid().ToString("N");
        var path = Path.Combine(Data, "HardDiskRestores", ".restore-" + id);
        var operation = CopperScreenHardDiskBackupCleanup.Begin(Data, "restore", path, id)!;
        Directory.CreateDirectory(path); operation.TrackDirectory();
        foreach (var name in new[] { "profile.json", "unit-0.hdf", "unit-1.hdf" })
        {
            using var file = new FileStream(Path.Combine(path, name), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            operation.TrackFile(name, file.SafeFileHandle); file.Write(new byte[4096]);
        }
        return (path, Path.Combine(Journals, id + ".journal"), operation);
    }
    [WindowsTheory]
    [InlineData(false)] [InlineData(true)]
    public void ActiveOperationIsSkippedThenOnlyItsAbandonedObjectsAreRemoved(bool restore)
    {
        var item = restore ? Restore() : Export();
        var keep = Path.Combine(_root, "existing.copperhdf"); File.WriteAllText(keep, "original backup");
        var before = File.ReadAllBytes(keep);
        Assert.Equal(0, CopperScreenHardDiskBackupCleanup.Cleanup(Data).Cleaned);
        Assert.True(Path.Exists(item.Path)); Assert.True(File.Exists(item.Journal));
        item.Operation.Dispose();
        Assert.Equal(1, CopperScreenHardDiskBackupCleanup.Cleanup(Data).Cleaned);
        Assert.False(Path.Exists(item.Path)); Assert.False(File.Exists(item.Journal));
        Assert.Equal(before, File.ReadAllBytes(keep));
    }
    [WindowsTheory]
    [InlineData(false)] [InlineData(true)]
    public void ReplacedFileOrDirectoryIsNeverDeleted(bool restore)
    {
        var item = restore ? Restore() : Export(); item.Operation.Dispose();
        // Retain the original node elsewhere so its identity cannot be recycled.
        if (restore) { Directory.Move(item.Path, item.Path + ".original"); Directory.CreateDirectory(item.Path); File.WriteAllText(Path.Combine(item.Path, "keep"), "user data"); }
        else { File.Move(item.Path, item.Path + ".original"); File.WriteAllText(item.Path, "user data"); }
        Assert.Equal(0, CopperScreenHardDiskBackupCleanup.Cleanup(Data).Cleaned);
        Assert.True(Path.Exists(item.Path)); Assert.True(File.Exists(item.Journal));
    }
    [WindowsFact]
    public void ReplacedChildOrUnknownChildDefersTheWholeRestore()
    {
        var item = Restore(); item.Operation.Dispose();
        var child = Path.Combine(item.Path, "unit-0.hdf"); File.Move(child, Path.Combine(_root, "original-child"));
        File.WriteAllText(child, "replacement");
        Assert.Equal(0, CopperScreenHardDiskBackupCleanup.Cleanup(Data).Cleaned);
        Assert.Equal("replacement", File.ReadAllText(child)); Assert.True(File.Exists(Path.Combine(item.Path, "unit-1.hdf")));
        var other = Restore(); other.Operation.Dispose(); File.WriteAllText(Path.Combine(other.Path, "unknown.txt"), "keep");
        Assert.Equal(0, CopperScreenHardDiskBackupCleanup.Cleanup(Data).Cleaned);
        Assert.True(File.Exists(Path.Combine(other.Path, "profile.json"))); Assert.True(File.Exists(Path.Combine(other.Path, "unknown.txt")));
    }
    [WindowsTheory]
    [InlineData("truncated")] [InlineData("null-identity")] [InlineData("published-path")] [InlineData("missing-identity")]
    public void InvalidOwnershipRecordsAndUntrackedLookalikesArePreserved(string corruption)
    {
        var item = Export(); item.Operation.Dispose();
        if (corruption == "truncated") File.AppendAllText(item.Journal, "{broken");
        if (corruption == "missing-identity") File.WriteAllText(item.Journal, File.ReadAllLines(item.Journal)[0] + "\n");
        if (corruption == "null-identity") File.WriteAllText(item.Journal, File.ReadAllLines(item.Journal)[0] + "\n{\"Name\":\"\",\"Identity\":null}\n");
        if (corruption == "published-path")
        {
            var lines = File.ReadAllLines(item.Journal); var header = System.Text.Json.Nodes.JsonNode.Parse(lines[0])!;
            header["Path"] = Path.Combine(_root, "existing.copperhdf"); File.WriteAllText(header["Path"]!.GetValue<string>(), "keep");
            File.WriteAllText(item.Journal, header.ToJsonString() + "\n" + lines[1] + "\n");
        }
        var lookalike = Path.Combine(_root, ".hdf-backup-" + Guid.NewGuid().ToString("N") + ".tmp"); File.WriteAllText(lookalike, "not tracked");
        Assert.Equal(0, CopperScreenHardDiskBackupCleanup.Cleanup(Data).Cleaned);
        Assert.True(File.Exists(item.Path)); Assert.Equal("not tracked", File.ReadAllText(lookalike));
    }
    [WindowsTheory]
    [InlineData(false)] [InlineData(true)]
    public void OwnedObjectsWithInstalledAppLengthPathsAreCleaned(bool restore)
    {
        _data = Path.Combine(_root, new string('a', 100), new string('b', 100));
        Directory.CreateDirectory(Data);
        var item = restore ? Restore() : Export(Data);
        Assert.True(item.Path.Length > 260);
        item.Operation.Dispose();
        Assert.Equal(1, CopperScreenHardDiskBackupCleanup.Cleanup(Data).Cleaned);
        Assert.False(Path.Exists(item.Path)); Assert.False(File.Exists(item.Journal));
    }
    [WindowsFact]
    public void PublishedObjectsSurviveAStaleJournalAndOtherDataDirectoryCannotCleanThem()
    {
        var item = Restore(); item.Operation.Dispose();
        Assert.Equal(0, CopperScreenHardDiskBackupCleanup.Cleanup(Path.Combine(_root, "other-data")).Cleaned);
        var published = Path.Combine(Path.GetDirectoryName(item.Path)!, Guid.NewGuid().ToString("N")); Directory.Move(item.Path, published);
        Assert.Equal(1, CopperScreenHardDiskBackupCleanup.Cleanup(Data).Cleaned);
        Assert.True(File.Exists(Path.Combine(published, "unit-0.hdf"))); Assert.False(File.Exists(item.Journal));
    }
    [WindowsFact]
    public void AnOpenFileDefersCleanupWithoutRemovingOtherRestoreFiles()
    {
        var item = Restore(); item.Operation.Dispose();
        using (var lease = new FileStream(Path.Combine(item.Path, "unit-1.hdf"), FileMode.Open, FileAccess.Read, FileShare.Read))
        { Assert.Equal(0, CopperScreenHardDiskBackupCleanup.Cleanup(Data).Cleaned); Assert.True(File.Exists(Path.Combine(item.Path, "profile.json"))); }
        Assert.Equal(1, CopperScreenHardDiskBackupCleanup.Cleanup(Data).Cleaned);
    }
    [WindowsFact]
    public void RecordedIdentityCannotAuthorizeALinkedRestoreDirectory()
    {
        var item = Restore(); item.Operation.Dispose();
        var original = item.Path + ".original"; Directory.Move(item.Path, original);
        // A junction can be created without enabling symlink privileges.
        var start = new System.Diagnostics.ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("/c"); start.ArgumentList.Add("mklink"); start.ArgumentList.Add("/J"); start.ArgumentList.Add(item.Path); start.ArgumentList.Add(original);
        using var process = System.Diagnostics.Process.Start(start)!;
        process.WaitForExit(); Assert.Equal(0, process.ExitCode);
        try
        {
            Assert.Equal(0, CopperScreenHardDiskBackupCleanup.Cleanup(Data).Cleaned);
            Assert.True(File.Exists(Path.Combine(original, "unit-0.hdf"))); Assert.True(Directory.Exists(item.Path));
        }
        finally { Directory.Delete(item.Path); } // Remove only the junction itself.
    }
    private sealed class WindowsFactAttribute : FactAttribute
    { public WindowsFactAttribute() { if (!OperatingSystem.IsWindows()) Skip = "Windows file-identity cleanup is unavailable on this host."; } }
    private sealed class WindowsTheoryAttribute : TheoryAttribute
    { public WindowsTheoryAttribute() { if (!OperatingSystem.IsWindows()) Skip = "Windows file-identity cleanup is unavailable on this host."; } }
}
