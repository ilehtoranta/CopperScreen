#if COPPERSCREEN_INSTALLED_TESTS
using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;
using Avalonia;
using Avalonia.Media.Imaging;

namespace CopperScreen;

internal sealed partial class MainWindow
{
    private Exception? _installedBackupError;
    internal async Task<Action> RunInstalledBackupIoAsync(Dictionary<string, object?> report)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        void Require(bool value, string message) => CopperScreenInstalledRdbProof.Require(value, message);
        string Hash(string path) => CopperScreenInstalledRdbProof.Hash(CopperScreenRdbImageProof.Backing(path));
        await StopToLauncherAsync(); Require(_runtime == null, "Normal Stop failed.");
        var data = _recentSession.DataDirectory;
        var inputs = CopperScreenInstalledRdbProof.FailureInputs;
        var backup = Path.Combine(inputs, "existing-backup.copperhdf");
        var backupHash = Hash(backup);
        var protectedFiles = new[] { "disk.hdf", "profile.json", "last-machine.json", "recent-session.json" }
            .ToDictionary(n => n, n => Hash(Path.Combine(data, n)));
        report["protectedFilesBefore"] = protectedFiles; report["backupHashBefore"] = backupHash;
        void Unchanged()
        {
            Require(protectedFiles.All(kv => Hash(Path.Combine(data, kv.Key)) == kv.Value), "Existing disk/settings changed.");
            Require(Hash(backup) == backupHash, "Existing backup changed.");
        }
        ShowSettingsWindow(); SelectSettingsPage(3);
        const string edit = "Keep this unsaved edit after failed storage operations";
        _profileDescriptionBox.Text = edit;
        var cases = new List<object>(); report["ioCases"] = cases;
        async Task Case(string name, string operation, string stage, string failure)
        {
            ClearSettingsStartupError(); UpdateSettingsStatus();
            _installedBackupError = null;
            var before = CopperScreenProfileStore.Serialize(ReadSettingsDraft(), AppContext.BaseDirectory);
            var committed = CopperScreenProfileStore.Serialize(_committedSettings, AppContext.BaseDirectory);
            var exportRoot = Path.Combine(inputs, name); Directory.CreateDirectory(exportRoot);
            var path = operation == "export" ? Path.Combine(exportRoot, "new.copperhdf") : backup;
            var restoreRoot = Path.Combine(data, "HardDiskRestores"); Directory.CreateDirectory(restoreRoot);
            var filesBefore = Directory.GetFiles(data, "*", SearchOption.AllDirectories).Where(p => !p.Contains(Path.DirectorySeparatorChar + "Logs" + Path.DirectorySeparatorChar)).ToDictionary(p => p, Hash);
            string? denied = null; string? originalAcl = null;
            FileStream? settingsLease = null;
            long hitBytes = -1; string? hitPath = null;
            CopperScreenHardDiskBackup.TestObserver = (point, staged, bytes) =>
            {
                if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
                if (point != stage || (point.EndsWith("-chunk") && bytes < 256 * 1024)) return;
                hitBytes = bytes; hitPath = staged;
                if (failure == "full") throw new IOException("There is not enough space on the disk. (diagnostic ERROR_DISK_FULL)", unchecked((int)0x80070070));
                if (failure == "deny")
                {
                    denied = operation == "export" ? exportRoot : restoreRoot;
                    var info = new DirectoryInfo(denied); originalAcl = info.GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.Access);
                    var acl = info.GetAccessControl();
                    acl.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.CreateFiles | FileSystemRights.CreateDirectories, AccessControlType.Deny));
                    info.SetAccessControl(acl);
                }
                if (failure == "settings") settingsLease = new FileStream(Path.Combine(data, "last-machine.json"), FileMode.Open, FileAccess.Read, FileShare.Read);
                if (failure == "kill")
                {
                    CopperScreenUserData.WriteAtomically(CopperScreenInstalledRdbProof.Output("-barrier.json"), System.Text.Json.JsonSerializer.Serialize(new { name, operation, stage = point, staged, bytes, protectedFiles, backupHash }));
                    Thread.Sleep(Timeout.Infinite); // Controller terminates this exact installed PID.
                }
            };
            report["ioCaseReady"] = new { name, operation, path, stage, failure };
            CopperScreenInstalledRdbProof.SaveReport();
            try
            {
                var timer = Stopwatch.StartNew();
                while ((_settingsStartupError == null || _applyingSettings) && timer.Elapsed < TimeSpan.FromMinutes(10)) await Task.Delay(100);
            }
            finally
            {
                CopperScreenHardDiskBackup.TestObserver = null;
                settingsLease?.Dispose();
                if (denied != null)
                {
                    // Fresh GetAccessControl objects have no modified sections;
                    // importing SDDL explicitly marks the DACL for restoration.
                    var restored = new DirectorySecurity(); restored.SetSecurityDescriptorSddlForm(originalAcl!, AccessControlSections.Access);
                    new DirectoryInfo(denied).SetAccessControl(restored);
                    Require(new DirectoryInfo(denied).GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.Access) == originalAcl, "Test destination permissions were not restored.");
                }
            }
            report["ioFailureObservation"] = new { name, hitBytes, hitPath, error = _settingsStartupError, exception = _installedBackupError?.ToString() };
            CopperScreenInstalledRdbProof.SaveReport();
            Require(hitBytes >= 0 && hitPath != null, "The real operation did not reach its diagnostic barrier.");
            Require(_settingsStartupError != null && _settingsWindow.IsVisible && _settingsStatus.Text == _settingsStartupError && !_applyingSettings, "No visible completed failure.");
            var hresult = unchecked((uint)(_installedBackupError?.HResult ?? 0));
            Require(failure == "full" ? hresult == 0x80070070 : failure == "deny" ? hresult == 0x80070005 : hresult is 0x80070020 or 0x80070005, "Unexpected storage failure: " + _installedBackupError);
            Require(_runtime == null && _profileDescriptionBox.Text == edit && CopperScreenProfileStore.Serialize(ReadSettingsDraft(), AppContext.BaseDirectory) == before, "Failure changed unsaved settings or started the machine.");
            Require(CopperScreenProfileStore.Serialize(_committedSettings, AppContext.BaseDirectory) == committed, "Failure committed replacement settings.");
            Unchanged();
            var filesAfter = Directory.GetFiles(data, "*", SearchOption.AllDirectories).Where(p => !p.Contains(Path.DirectorySeparatorChar + "Logs" + Path.DirectorySeparatorChar)).ToDictionary(p => p, Hash);
            Require(filesBefore.Count == filesAfter.Count && filesBefore.All(kv => filesAfter.TryGetValue(kv.Key, out var hash) && hash == kv.Value), "Failure published files or retained staging.");
            Require(!Directory.EnumerateFileSystemEntries(exportRoot).Any(), "Failure retained an export or temporary file.");
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(_settingsWindow.ClientSize.Width * _settingsWindow.RenderScaling), (int)Math.Ceiling(_settingsWindow.ClientSize.Height * _settingsWindow.RenderScaling)), new Vector(96 * _settingsWindow.RenderScaling, 96 * _settingsWindow.RenderScaling));
            bitmap.Render(_settingsWindow); bitmap.Save(CopperScreenInstalledRdbProof.Output("-" + name + ".png"));
            cases.Add(new { name, operation, stage, failure, error = _settingsStartupError, hresult = hresult.ToString("X8"), hitBytes, hitPath, savedFilesUnchanged = true, draftUnchanged = true, committedUnchanged = true, backupUnchanged = true, noPublishedOrPartialFiles = true });
            report["ioCaseReady"] = null; CopperScreenInstalledRdbProof.SaveReport();
        }
        var phase = CopperScreenInstalledRdbProof.IoCase;
        if (phase == "io-failures")
        {
            await Case("export-disk-full", "export", "export-chunk", "full");
            await Case("restore-disk-full", "restore", "restore-chunk", "full");
            await Case("export-access-denied", "export", "export-opening", "deny");
            await Case("restore-access-denied", "restore", "restore-opening", "deny");
            await Case("restore-settings-unwritable", "restore", "restore-flushed", "settings");
            report["allIoFailuresPassed"] = true;
        }
        else
        {
            var operation = phase.Contains("export") ? "export" : "restore";
            var stage = operation + (phase.EndsWith("partial") ? "-chunk" : "-flushed");
            await Case(phase, operation, stage, "kill");
            throw new InvalidOperationException("The interruption controller did not terminate this process.");
        }
        HideSettings(); Unchanged(); report["protectedFilesAfter"] = protectedFiles;
        return Unchanged;
    }
}
#endif
