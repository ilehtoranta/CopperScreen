#if COPPERSCREEN_INSTALLED_TESTS
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace CopperScreen;

// Installed diagnostics only. Normal MainWindow, runtime, audio and presentation
// own execution. The observer only issues host commands and inspects snapshots.
internal static class CopperScreenInstalledRdbProof
{
    internal sealed record Request(string RunId, string Phase, string RomPath, string FixturePath, string OutputDirectory, string? Scenario = null, string? FailureInputsDirectory = null);
    internal static CopperScreenRecentSession? ActiveSession { get; private set; }
    private static Request _request = null!;
    private static Dictionary<string, object?> _report = null!;
    private static string _image = "", _profile = "";
    private static byte[] _pristine = [];
    internal static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    internal static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    internal static string HashFile(string path) => Hash(File.ReadAllBytes(path));
    internal static bool FailurePhase => _request.Scenario == "backup-failures";
    internal static bool IoPhase => _request.Scenario is "backup-io" or "backup-cleanup";
    internal static bool CleanupPhase => _request.Scenario == "backup-cleanup";
    internal static string IoCase => _request.Phase;
    internal static string FailureInputs => _request.FailureInputsDirectory ?? throw new InvalidDataException("Failure inputs required.");
    internal static bool Priority => _request.Phase.StartsWith("priority-", StringComparison.Ordinal);
    internal static bool RestorePhase => _request.Phase.EndsWith("restore", StringComparison.Ordinal) || _request.Phase.EndsWith("restore-write", StringComparison.Ordinal);
    internal static bool RestoredWrites => _request.Scenario == "restored-writes";
    internal static bool AppBackup => _request.Scenario is "app-backup" or "restored-writes";
    internal static string BackupPath => Path.Combine(_request.OutputDirectory, (Priority ? "priority" : "default") + ".copperhdf");
    internal static bool WritePhase => _request.Phase.EndsWith("write", StringComparison.Ordinal);
    internal static bool Protected => _request.Phase == "protected";
    internal static uint Identity => Priority ? 1u : 2u;
    internal static string Image => _image;
    internal static byte[] Pristine => _pristine;
    internal static string Output(string suffix) => Path.Combine(_request.OutputDirectory, _request.Phase + suffix);
    internal static void SaveReport() => CopperScreenUserData.WriteAtomically(Output(".json"), JsonSerializer.Serialize(_report, new JsonSerializerOptions { WriteIndented = true }));

    internal static void Run(string[] args, Action<CopperScreenRecentSession, string[]> show)
    {
        if (args.Length != 2 || !Path.IsPathFullyQualified(args[1])) { Environment.ExitCode = 2; return; }
        _request = JsonSerializer.Deserialize<Request>(File.ReadAllText(args[1]))!;
        _report = new() { ["protocol"] = "copperscreen-installed-rdb-writes-v1", ["passed"] = false, ["phase"] = _request.Phase };
        try
        {
            if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Installed RDB proof requires Windows.");
            Require(Guid.TryParseExact(_request.RunId, "N", out _) &&
                _request.Phase is "write" or "restart" or "fresh" or "priority-write" or "priority-restart" or "priority-fresh" or "protected" or "restore" or "priority-restore" or "restore-write" or "priority-restore-write" or "fresh-restore" or "priority-fresh-restore" or "failures" or "io-failures" or "io-export-partial" or "io-export-flushed" or "io-restore-partial" or "io-restore-flushed" or "io-recovery" &&
                _request.Scenario is null or "post-update" or "post-restore" or "app-backup" or "restored-writes" or "backup-failures" or "backup-io" or "backup-cleanup" &&
                Path.IsPathFullyQualified(_request.OutputDirectory), "Invalid installed RDB request.");
            Require(!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator), "Installed app must run non-elevated.");
            uint length = 0; var status = GetCurrentPackageFullName(ref length, null);
            if (status != 122) throw new Win32Exception(status, "Activate the installed package identity.");
            var fullName = new StringBuilder((int)length); status = GetCurrentPackageFullName(ref length, fullName);
            if (status != 0) throw new Win32Exception(status);
            var identity = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "AppxManifest.xml")).Root!.Elements().Single(e => e.Name.LocalName == "Identity");
            Require((string?)identity.Attribute("Name") == "CopperScreen.Test." + _request.RunId, "Installed identity does not match this run.");
            _ = CopperScreenCopperStartRom.Read(_request.RomPath);
            _pristine = RestoredWrites ? CopperScreenRdbImageProof.ForRestoredWrites(_request.FixturePath) : CopperScreenRdbImageProof.Pristine(_request.FixturePath);
            if (Priority) _pristine = CopperScreenRdbImageProof.BootFromDh0(_pristine);
            var fresh = _request.Phase.EndsWith("fresh", StringComparison.Ordinal);
            var freshRestore = _request.Phase.Contains("fresh-restore", StringComparison.Ordinal);
            var group = Protected ? "protected" : Priority ? "priority" : "default";
            if (fresh || freshRestore) group += "-fresh";
            if (_request.Scenario != null) group = _request.Scenario + "-" + group;
            var directory = Path.Combine(CopperScreenUserData.DefaultDirectory, "RdbProof", _request.RunId, group);
            _image = Path.Combine(directory, "disk.hdf");
            _profile = Path.Combine(directory, "profile.json");
            if ((WritePhase && !RestorePhase) || fresh || Protected || FailurePhase || (IoPhase && IoCase == "io-failures") || (CleanupPhase && IoCase == "io-export-partial"))
            {
                Require(!Directory.Exists(directory), "Use fresh package-private proof data.");
                Directory.CreateDirectory(directory);
                var bytes = FailurePhase || IoPhase ? File.ReadAllBytes(_request.FixturePath) : fresh ? File.ReadAllBytes(Path.Combine(_request.OutputDirectory, (Priority ? "priority-write" : "write") + "-live.hdf")) : _pristine;
                if (fresh) CopperScreenRdbImageProof.AssertDisk(_pristine, bytes);
                File.WriteAllBytes(_image, bytes);
                var options = CopperScreenStartupOptions.Parse(["--copperstart", "--rom", _request.RomPath,
                    fresh || Protected ? "--hdf-rdb" : "--hdf-rdb-writable", _image], AppContext.BaseDirectory, userDataDirectory: directory);
                Require(options.Error == null, options.Error ?? "Invalid startup options.");
                var draft = CopperScreenSettingsDraft.FromStartupOptions(options);
                // Match the settings form's explicit empty-drive protection defaults
                // before taking the failure-preservation baseline.
                if (FailurePhase || IoPhase) Array.Fill(draft.DriveWriteProtected, true);
                CopperScreenProfileStore.SaveToPath(draft, _profile);
                new CopperScreenRecentSession(directory).Save(draft, 20, false, AppContext.BaseDirectory);
            }
            ActiveSession = new CopperScreenRecentSession(directory);
            if (!RestorePhase)
            {
            var restored = ActiveSession.LoadMachine(AppContext.BaseDirectory) ?? throw new InvalidDataException("Missing last-machine settings.");
            var drive = restored.HardDrives.Single();
            if (AppBackup && !WritePhase && !File.Exists(_profile))
            {
                _image = drive.Path; _profile = Path.Combine(directory, "last-machine.json");
                _report["restoredImagePath"] = _image;
            }
            Require(drive.Mode == AmigaHardfileMountMode.RigidDiskBlock && drive.Partition == null && drive.Path == _image && drive.ReadOnly == (fresh || Protected), "RDB settings did not persist.");
            }
            _report["packageFullName"] = fullName.ToString(); _report["packageFamily"] = CopperScreenUserData.CurrentPackageFamily();
            _report["installDirectory"] = AppContext.BaseDirectory; _report["userDataDirectory"] = directory;
            _report["elevated"] = false; _report["engine"] = "Lightweight"; _report["hostDosCalls"] = false;
            _report["scenario"] = _request.Scenario;
            _report["romSha256"] = HashFile(_request.RomPath); _report["fixtureSha256"] = HashFile(_request.FixturePath);
            _report["pristineSha256"] = Hash(_pristine);
            if (!RestorePhase) _report["profileSha256Before"] = HashFile(_profile);
            _report["binaries"] = new[] { typeof(Program).Assembly.Location, typeof(CopperMod.Amiga.Lightweight.LightweightA500Machine).Assembly.Location,
                typeof(CopperDisk.AmigaHardfileConfiguration).Assembly.Location, typeof(Copper68k.M68kCpuModel).Assembly.Location }
                .Select(path => new { path, sha256 = HashFile(path) }).ToArray();
            Directory.CreateDirectory(_request.OutputDirectory);
            // Restart deliberately uses the normal remembered-machine path.
            show(ActiveSession, RestorePhase || _request.Phase.EndsWith("restart", StringComparison.Ordinal) ? [] : ["--profile", _profile]);
            Require(_report.ContainsKey("completedWindowSession"), "Installed desktop observation did not complete.");
            Require(HashFile(_profile) == (string)_report["profileSha256Before"]!, "Saved profile changed.");
            _report["savedSha256AfterWindowClose"] = HashFile(_image);
            Require(HashFile(_image) == (string)_report["liveSha256"]!, "Closing changed the observed backing image.");
            _report["passed"] = true;
        }
        catch (Exception ex) { _report["error"] = ex.ToString(); Environment.ExitCode = 1; }
        finally { SaveReport(); }
    }

    internal static void Restored(CopperScreenSettingsDraft draft)
    {
        var drive = draft.HardDrives.Single();
        Require(drive.Mode == AmigaHardfileMountMode.RigidDiskBlock && drive.Partition == null && !drive.ReadOnly, "Restored RDB configuration changed.");
        _image = drive.Path; _profile = draft.SourcePath!;
        var bytes = File.ReadAllBytes(_image);
        if (RestoredWrites && WritePhase) Require(bytes.SequenceEqual(_pristine), "Restore did not preserve the complete prepared input image before native writes.");
        else CopperScreenRdbImageProof.AssertDisk(_pristine, bytes, RestoredWrites ? "After" : "Saved");
        File.WriteAllBytes(Output("-restored-before-boot.hdf"), bytes);
        _report["restoredSha256BeforeBoot"] = Hash(bytes);
        _report["profileSha256Before"] = HashFile(_profile);
        _report["restoredImagePath"] = _image; _report["restoredProfilePath"] = _profile;
    }

    internal static void Observe(MainWindow window)
    {
        if (ActiveSession == null) return;
        window.Opened += (_, _) => DispatcherTimer.RunOnce(async () =>
        {
            try { await window.RunInstalledRdbProofAsync(_report); _report["completedWindowSession"] = true; }
            catch (Exception ex) { _report["sessionError"] = ex.ToString(); Environment.ExitCode = 1; }
            finally { SaveReport(); window.Close(); }
        }, TimeSpan.FromMilliseconds(500));
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern int GetCurrentPackageFullName(ref uint length, StringBuilder? name);
}

internal sealed partial class MainWindow
{
    internal async Task RunInstalledRdbProofAsync(Dictionary<string, object?> report)
    {
        report["startupBackupCleanup"] = StartupHardDiskBackupCleanup;
        if (CopperScreenInstalledRdbProof.RestorePhase)
        {
            ShowSettingsWindow(); SelectSettingsPage(3);
            await Dispatcher.UIThread.InvokeAsync(() => { _settingsWindow.UpdateLayout(); }, DispatcherPriority.Background);
            report["uiStage"] = "restore"; CopperScreenInstalledRdbProof.SaveReport();
            var timeout = Stopwatch.StartNew();
            while ((_settingsDraft.SourcePath == null || !_settingsDraft.SourcePath.Contains("HardDiskRestores")) && timeout.Elapsed < TimeSpan.FromMinutes(10)) await Task.Delay(200);
            CopperScreenInstalledRdbProof.Require(_settingsDraft.SourcePath?.Contains("HardDiskRestores") == true && !_applyingSettings, "Settings restore did not complete.");
            CopperScreenInstalledRdbProof.Restored(_settingsDraft);
            report["settingsRestoreCompleted"] = true;
            HideSettings();
        }
        if (_runtime == null) await StartRuntimeFromSettingsAsync();
        var runtime = _runtime ?? throw new InvalidOperationException("Normal native runtime did not start: " + _settingsStartupError);
        var observations = new List<object>(); report["observations"] = observations;
        report["windowOpened"] = IsVisible; report["audio"] = runtime.DiagnosticAudio;
        CopperScreenInstalledRdbProof.Require(IsVisible, "Installed window is not visible.");
        async Task<(byte[] Chip, byte[] Slow, long Cycle)> Snapshot()
        {
            byte[] chip = [], slow = []; long cycle = 0;
            var result = await runtime.DiagnosticObserveAsync(e =>
            {
                var session = (CopperScreenLightweightSession)e;
                CopperScreenInstalledRdbProof.Require(session.FaultMessage == null && session.Machine.UnsupportedActiveFeature == null && !session.Machine.IsDriveMounted(0), "Native fault, unsupported feature or unexpected floppy.");
                chip = session.Machine.ChipRam.ToArray();
                slow = ((byte[])session.Machine.GetType().GetField("_slowRam", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session.Machine)!).ToArray();
                cycle = session.Machine.Cycle;
            }).WaitAsync(TimeSpan.FromSeconds(15));
            CopperScreenInstalledRdbProof.Require(result.Success, result.Message);
            return (chip, slow, cycle);
        }
        async Task Wait(uint phase)
        {
            var timer = Stopwatch.StartNew();
            while (timer.Elapsed < TimeSpan.FromMinutes(5))
            {
                var s = await Snapshot(); var marker = CopperScreenRdbImageProof.Long(s.Chip, 0xB80);
                if (marker == CopperScreenRdbImageProof.Failed)
                { CopperScreenInstalledRdbProof.Require(CopperScreenInstalledRdbProof.Protected, "Guest RDB probe failed: " + CopperScreenRdbImageProof.Long(s.Chip, 0xB84)); break; }
                if (marker == CopperScreenRdbImageProof.Complete && CopperScreenRdbImageProof.Long(s.Chip, 0xB98) == phase &&
                    CopperScreenRdbImageProof.Long(s.Chip, 0xB9C) == phase && CopperScreenRdbImageProof.HasCompletedFlush(s.Chip, s.Slow)) break;
                await Task.Delay(250);
            }
            var paused = await runtime.TogglePausedAsync();
            CopperScreenInstalledRdbProof.Require(paused.Success && paused.State.IsPaused, "Could not pause installed runtime.");
        }
        async Task<byte[]> Observe(uint phase, string suffix)
        {
            var s = await Snapshot();
            if (CopperScreenInstalledRdbProof.Protected)
            {
                CopperScreenInstalledRdbProof.Require(CopperScreenRdbImageProof.Long(s.Chip, 0xB80) == CopperScreenRdbImageProof.Failed &&
                    CopperScreenRdbImageProof.Long(s.Chip, 0xB84) == 214, "Protected write did not return error 214.");
            }
            else CopperScreenRdbImageProof.AssertReceipt(s.Chip, s.Slow, phase, CopperScreenInstalledRdbProof.Identity);
            var image = CopperScreenRdbImageProof.Backing(CopperScreenInstalledRdbProof.Image);
            if (CopperScreenInstalledRdbProof.Protected) CopperScreenInstalledRdbProof.Require(image.SequenceEqual(CopperScreenInstalledRdbProof.Pristine), "Protected backing changed.");
            else CopperScreenRdbImageProof.AssertDisk(CopperScreenInstalledRdbProof.Pristine, image, CopperScreenInstalledRdbProof.RestoredWrites ? "After" : "Saved");
            File.WriteAllBytes(CopperScreenInstalledRdbProof.Output(suffix + ".chipram"), s.Chip);
            File.WriteAllBytes(CopperScreenInstalledRdbProof.Output(suffix + ".slowram"), s.Slow);
            File.WriteAllBytes(CopperScreenInstalledRdbProof.Output(suffix + ".hdf"), image);
            using (var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(ClientSize.Width * RenderScaling), (int)Math.Ceiling(ClientSize.Height * RenderScaling)), new Vector(96 * RenderScaling, 96 * RenderScaling)))
            { bitmap.Render(this); bitmap.Save(CopperScreenInstalledRdbProof.Output(suffix + ".png")); }
            observations.Add(new { phase, sysIdentity = CopperScreenRdbImageProof.Long(s.Chip, 0xB88), cycle = s.Cycle, sha256 = CopperScreenInstalledRdbProof.Hash(image), snapshot = suffix });
            return image;
        }
        var phase = CopperScreenInstalledRdbProof.WritePhase ? 1u : 2u;
        await Wait(phase);
        var live = await Observe(phase, "-live"); // Before any host save or disposal.
        report["liveSha256"] = CopperScreenInstalledRdbProof.Hash(live);
        report["guestFlushBeforeHostSave"] = !CopperScreenInstalledRdbProof.Protected;
        var before = await Snapshot();
        var saved = await runtime.SavePersistentStorageAsync();
        CopperScreenInstalledRdbProof.Require(saved.Success, saved.Message);
        var after = await Snapshot();
        CopperScreenInstalledRdbProof.Require(before.Cycle == after.Cycle && live.SequenceEqual(CopperScreenRdbImageProof.Backing(CopperScreenInstalledRdbProof.Image)), "Host save advanced guest cycles or changed backing.");
        report["hostSaveAdvancedCycles"] = false;
        if (CopperScreenInstalledRdbProof.WritePhase && !CopperScreenInstalledRdbProof.Priority)
        {
            var reset = await runtime.ResetAsync();
            CopperScreenInstalledRdbProof.Require(reset.Success && !reset.State.IsPaused, reset.Message);
            await Wait(2);
            var resetImage = await Observe(2, "-reset");
            CopperScreenInstalledRdbProof.Require(live.SequenceEqual(resetImage), "Reset readback changed backing.");
            report["resetReadback"] = true;
        }
        report["state"] = runtime.CurrentState;
        report["audioAfter"] = runtime.DiagnosticAudio;
        if (CopperScreenInstalledRdbProof.AppBackup && CopperScreenInstalledRdbProof.WritePhase)
        {
            await StopToLauncherAsync();
            CopperScreenInstalledRdbProof.Require(_runtime == null, "Normal Stop did not release the writable session.");
            ShowSettingsWindow(); SelectSettingsPage(3);
            report["uiStage"] = "export"; report["backupPath"] = CopperScreenInstalledRdbProof.BackupPath;
            CopperScreenInstalledRdbProof.SaveReport();
            var timeout = Stopwatch.StartNew();
            while ((!File.Exists(CopperScreenInstalledRdbProof.BackupPath) || _applyingSettings) && timeout.Elapsed < TimeSpan.FromMinutes(10)) await Task.Delay(200);
            CopperScreenInstalledRdbProof.Require(File.Exists(CopperScreenInstalledRdbProof.BackupPath) && !_applyingSettings, "Settings export did not complete.");
            report["settingsExportCompleted"] = true;
            report["backupSha256"] = CopperScreenInstalledRdbProof.HashFile(CopperScreenInstalledRdbProof.BackupPath);
            HideSettings();
        }
        if (CopperScreenInstalledRdbProof.FailurePhase)
        {
            var assertUnchanged = await RunInstalledBackupFailuresAsync(report, runtime);
            await StartRuntimeFromSettingsAsync();
            runtime = _runtime ?? throw new InvalidOperationException("Could not restart the preserved machine after failed backups.");
            await Wait(2);
            var readback = await Observe(2, "-after-failures");
            CopperScreenInstalledRdbProof.Require(live.SequenceEqual(readback), "Failure handling changed the bootable disk.");
            assertUnchanged();
            report["postFailureFullRomReadback"] = true;
            report["audioAfterFailures"] = runtime.DiagnosticAudio;
        }
        if (CopperScreenInstalledRdbProof.IoPhase && CopperScreenInstalledRdbProof.IoCase != "io-recovery")
        {
            var unchanged = await RunInstalledBackupIoAsync(report);
            await StartRuntimeFromSettingsAsync();
            runtime = _runtime ?? throw new InvalidOperationException("Could not restart after storage failures.");
            await Wait(2);
            var readback = await Observe(2, "-after-io");
            CopperScreenInstalledRdbProof.Require(live.SequenceEqual(readback), "Storage failures changed the bootable disk.");
            unchanged(); report["postIoFullRomReadback"] = true;
        }
        CopperScreenInstalledRdbProof.SaveReport();
        // Leave the real window available briefly for external native UI capture.
        await Task.Delay(5000);
    }
}
#endif
