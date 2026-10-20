#if COPPERSCREEN_INSTALLED_TESTS
using System.Diagnostics;
using Avalonia;
using Avalonia.Media.Imaging;

namespace CopperScreen;

internal sealed partial class MainWindow
{
    // The observer waits for actual settings-button and native picker input.
    // It never calls the backup or restore implementation on the user's behalf.
    internal async Task<Action> RunInstalledBackupFailuresAsync(Dictionary<string, object?> report, CopperScreenRuntime runtime)
    {
        void Require(bool value, string message) => CopperScreenInstalledRdbProof.Require(value, message);
        string Hash(string path) => CopperScreenInstalledRdbProof.Hash(CopperScreenRdbImageProof.Backing(path));
        var data = _recentSession.DataDirectory;
        var inputs = CopperScreenInstalledRdbProof.FailureInputs;
        Require(Path.IsPathFullyQualified(inputs) && Directory.Exists(inputs), "Missing failure inputs.");
        // Running sessions append diagnostic logs. Compare every other file,
        // including unexpected new files; log activity is not a settings change.
        Dictionary<string, string> SavedFiles() => Directory.GetFiles(data, "*", SearchOption.AllDirectories)
            .Where(p => !Path.GetRelativePath(data, p).StartsWith("Logs" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(p => Path.GetRelativePath(data, p), Hash);
        var originalFiles = SavedFiles();
        var originalInputs = Directory.GetFiles(inputs).ToDictionary(p => Path.GetFileName(p)!, Hash);
        report["filesBeforeFailures"] = originalFiles;
        report["inputHashesBeforeFailures"] = originalInputs;
        void SnapshotFiles(string suffix)
        {
            foreach (var file in originalFiles.Keys)
            {
                var destination = CopperScreenInstalledRdbProof.Output(suffix + "/" + file);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.WriteAllBytes(destination, CopperScreenRdbImageProof.Backing(Path.Combine(data, file)));
            }
        }
        SnapshotFiles("-before-data");
        void AssertUnchanged()
        {
            var current = SavedFiles();
            Require(current.Count == originalFiles.Count && originalFiles.All(kv => current.TryGetValue(kv.Key, out var hash) && hash == kv.Value), "A failed operation changed existing disks or saved settings, or published new files.");
            Require(originalInputs.All(kv => Hash(Path.Combine(inputs, kv.Key!)) == kv.Value), "A failure input or existing backup changed.");
            var restores = Path.Combine(data, "HardDiskRestores");
            Require(!Directory.Exists(restores) || !Directory.EnumerateFileSystemEntries(restores).Any(), "Failed restore retained staging or published a restore.");
        }
        var cases = new List<object>(); report["failureCases"] = cases;
        const string edit = "Uncommitted description: preserve during failed backup operations";
        ShowSettingsWindow(); SelectSettingsPage(3);
        _profileDescriptionBox.Text = edit;
        async Task Case(string name, string operation, string? input, string expected, bool active, bool paused)
        {
            ClearSettingsStartupError(); UpdateSettingsStatus();
            var draftBefore = CopperScreenProfileStore.Serialize(ReadSettingsDraft(), AppContext.BaseDirectory);
            var committedBefore = CopperScreenProfileStore.Serialize(_committedSettings, AppContext.BaseDirectory);
            var frameBefore = active ? runtime.CurrentState.FrameNumber : 0;
            Require((active && ReferenceEquals(_runtime, runtime) && runtime.CurrentState.IsPaused == paused) || (!active && _runtime == null), "Unexpected active-session state before failure attempt.");
            report["failureCaseReady"] = new { name, operation, path = input == null ? null : Path.Combine(inputs, input), active, paused };
            CopperScreenInstalledRdbProof.SaveReport();
            var timer = Stopwatch.StartNew();
            while ((_settingsStartupError == null || _applyingSettings) && timer.Elapsed < TimeSpan.FromMinutes(10)) await Task.Delay(100);
            Require(_settingsStartupError?.Contains(expected, StringComparison.OrdinalIgnoreCase) == true, "Expected settings failure was not reported for " + name + ": " + _settingsStartupError);
            Require(_settingsWindow.IsVisible && _settingsStatus.Text == _settingsStartupError, "Settings error is not visible.");
            Require(_profileDescriptionBox.Text == edit && CopperScreenProfileStore.Serialize(ReadSettingsDraft(), AppContext.BaseDirectory) == draftBefore, "Failure discarded or changed uncommitted settings.");
            Require(CopperScreenProfileStore.Serialize(_committedSettings, AppContext.BaseDirectory) == committedBefore, "Failure changed committed machine settings.");
            Require((active && ReferenceEquals(_runtime, runtime) && runtime.CurrentState.IsPaused == paused && runtime.CurrentState.FrameNumber >= frameBefore) || (!active && _runtime == null), "Failure replaced, stopped or changed pause state of the active session.");
            AssertUnchanged();
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(_settingsWindow.ClientSize.Width * _settingsWindow.RenderScaling), (int)Math.Ceiling(_settingsWindow.ClientSize.Height * _settingsWindow.RenderScaling)), new Vector(96 * _settingsWindow.RenderScaling, 96 * _settingsWindow.RenderScaling));
            bitmap.Render(_settingsWindow); bitmap.Save(CopperScreenInstalledRdbProof.Output("-" + name + ".png"));
            cases.Add(new { name, operation, path = input, active, paused, error = _settingsStartupError,
                savedFilesUnchanged = true, inputsUnchanged = true, draftUnchanged = true, committedUnchanged = true,
                runtimeUnchanged = true, noPublishedRestoreOrStaging = true });
            report["failureCaseReady"] = null;
            CopperScreenInstalledRdbProof.SaveReport();
        }
        await Case("paused-export", "export", null, "Stop the Amiga", true, true);
        await Case("paused-restore", "restore", null, "Stop the Amiga", true, true);
        var resumed = await runtime.TogglePausedAsync();
        Require(resumed.Success && !resumed.State.IsPaused, "Could not resume the existing session.");
        // State publication follows the command; use a fresh owner observation.
        while (runtime.CurrentState.IsPaused) await Task.Delay(50);
        await Case("running-export", "export", null, "Stop the Amiga", true, false);
        await Case("running-restore", "restore", null, "Stop the Amiga", true, false);
        HideSettings(); await StopToLauncherAsync(); Require(_runtime == null, "Normal Stop failed after active-session rejection.");
        AssertUnchanged();
        ShowSettingsWindow(); SelectSettingsPage(3); _profileDescriptionBox.Text = edit;
        await Case("corrupt-image", "restore", "corrupt-image.copperhdf", "disk image failed its integrity check", false, false);
        await Case("corrupt-profile", "restore", "corrupt-profile.copperhdf", "configuration failed its integrity check", false, false);
        await Case("truncated-archive", "restore", "truncated-archive.copperhdf", "Central Directory", false, false);
        await Case("truncated-image", "restore", "truncated-image.copperhdf", "length does not match", false, false);
        await Case("existing-backup", "export", "existing-backup.copperhdf", "Existing backups are preserved", false, false);
        Require(cases.Count == 9, "All nine failure attempts are required.");
        HideSettings(); AssertUnchanged(); SnapshotFiles("-after-data");
        report["filesAfterFailures"] = originalFiles;
        report["inputHashesAfterFailures"] = originalInputs;
        report["allFailureCasesPassed"] = true;
        return AssertUnchanged;
    }
}
#endif
