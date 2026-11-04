using System.Diagnostics;
using System.Reflection;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed class InstalledBackupFactAttribute : FactAttribute
{
    public InstalledBackupFactAttribute()
    {
        if (HardDiskSparseRestoreFailureTests.Observer is null)
            Skip = "Requires -p:CopperScreenInstalledTests=true for the existing backup fault observer.";
    }
}

public sealed class RestoreChildFactAttribute : FactAttribute
{
    public RestoreChildFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("COPPERSCREEN_RESTORE_CHILD_ROOT") is null)
            Skip = "Executed only by the abrupt-restore parent test.";
    }
}

[CollectionDefinition("Backup restore failures", DisableParallelization = true)]
public sealed class BackupRestoreFailureCollection;

[Collection("Backup restore failures")]
public sealed class HardDiskSparseRestoreFailureTests : IDisposable
{
    internal static FieldInfo? Observer => typeof(CopperScreenHardDiskBackup).GetField("TestObserver", BindingFlags.Static | BindingFlags.NonPublic);
    private readonly string _root = Path.Combine(Path.GetTempPath(), "CopperRestoreFailure-" + Guid.NewGuid().ToString("N"));
    public HardDiskSparseRestoreFailureTests() => Directory.CreateDirectory(_root);
    public void Dispose() { Observer?.SetValue(null, null); Directory.Delete(_root, true); }

    private string Backup()
    {
        var image = Path.Combine(_root, "original.hdf");
        var bytes = new byte[2 * 1024 * 1024]; bytes[0] = 71; bytes[300000] = 9;
        File.WriteAllBytes(image, bytes);
        var draft = new CopperScreenSettingsDraft { Id = "sparse", DisplayName = "Sparse restore" };
        draft.HardDrives.Add(new(0, image, false, 0));
        var backup = Path.Combine(_root, "backup.copperhdf");
        CopperScreenHardDiskBackup.Export(draft, backup, AppContext.BaseDirectory, Path.Combine(_root, "data"));
        return backup;
    }

    [InstalledBackupFact]
    public void DiskFullDuringSparseRestoreRemovesStagingAndPreservesPreviousRestore()
    {
        var backup = Backup(); var data = Path.Combine(_root, "data");
        var existing = CopperScreenHardDiskBackup.Restore(backup, data, AppContext.BaseDirectory);
        var before = File.ReadAllBytes(existing.HardDrives[0].Path);
        Observer!.SetValue(null, (Action<string, string, long>)((stage, _, _) =>
        {
            if (stage == "restore-chunk") throw new IOException("Injected host disk-full after partial restore.", unchecked((int)0x80070070));
        }));
        Assert.Throws<IOException>(() => CopperScreenHardDiskBackup.Restore(backup, data, AppContext.BaseDirectory));
        Observer.SetValue(null, null);
        Assert.Equal(before, File.ReadAllBytes(existing.HardDrives[0].Path));
        Assert.Single(Directory.GetDirectories(Path.Combine(data, "HardDiskRestores")));
    }

    [InstalledBackupFact]
    public async Task AbruptSparseRestoreIsCleanedOnNextRestoreWithoutPublishingPartialImage()
    {
        var backup = Backup(); var data = Path.Combine(_root, "data");
        var existing = CopperScreenHardDiskBackup.Restore(backup, data, AppContext.BaseDirectory);
        var before = File.ReadAllBytes(existing.HardDrives[0].Path);
        var signal = Path.Combine(_root, "restore-barrier");
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("vstest"); start.ArgumentList.Add(typeof(HardDiskSparseRestoreFailureTests).Assembly.Location);
        start.ArgumentList.Add("--TestCaseFilter:FullyQualifiedName~AbruptRestoreChild");
        start.Environment["COPPERSCREEN_RESTORE_CHILD_ROOT"] = _root;
        using var child = Process.Start(start)!;
        var stdout = child.StandardOutput.ReadToEndAsync(); var stderr = child.StandardError.ReadToEndAsync();
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(40));
            while (!File.Exists(signal))
            {
                if (child.HasExited) Assert.Fail(await stdout + await stderr);
                await Task.Delay(25, deadline.Token);
            }
            child.Kill(entireProcessTree: true); await child.WaitForExitAsync(deadline.Token);
        }
        finally { if (!child.HasExited) { child.Kill(entireProcessTree: true); await child.WaitForExitAsync(); } }
        // The vstest launcher can exit before its testhost has released handles.
        // Cleanup correctly defers an operation while its lease remains held.
        var journal = Assert.Single(Directory.GetFiles(Path.Combine(data, "HardDiskBackupOperations"), "*.journal"));
        var leaseDeadline = Stopwatch.StartNew();
        while (true)
        {
            try { using var lease = new FileStream(journal, FileMode.Open, FileAccess.ReadWrite, FileShare.None); break; }
            catch (IOException) when (leaseDeadline.Elapsed < TimeSpan.FromSeconds(5)) { await Task.Delay(25); }
        }
        // Only the already published copy may appear before recovery cleanup.
        Assert.Single(Directory.GetDirectories(Path.Combine(data, "HardDiskRestores")), p => !Path.GetFileName(p).StartsWith('.'));
        var restored = CopperScreenHardDiskBackup.Restore(backup, data, AppContext.BaseDirectory);
        Assert.Equal(before, File.ReadAllBytes(existing.HardDrives[0].Path));
        Assert.Equal(before, File.ReadAllBytes(restored.HardDrives[0].Path));
        Assert.Equal(2, Directory.GetDirectories(Path.Combine(data, "HardDiskRestores")).Length);
    }

    [RestoreChildFact]
    public void AbruptRestoreChild()
    {
        var root = Environment.GetEnvironmentVariable("COPPERSCREEN_RESTORE_CHILD_ROOT")!;
        Observer!.SetValue(null, (Action<string, string, long>)((stage, _, _) =>
        {
            if (stage != "restore-chunk") return;
            File.WriteAllText(Path.Combine(root, "restore-barrier"), "partial");
            Thread.Sleep(Timeout.Infinite);
        }));
        CopperScreenHardDiskBackup.Restore(Path.Combine(root, "backup.copperhdf"), Path.Combine(root, "data"), AppContext.BaseDirectory);
        Assert.Fail("The parent must terminate the restore while it is staged.");
    }
}
