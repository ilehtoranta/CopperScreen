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

// Diagnostic package only. The native window, runtime, device and machine retain their normal owners.
internal static class CopperScreenInstalledSoak
{
    internal sealed record Request(string RunId, string Case, string Phase, int DurationSeconds, string OutputDirectory, string? Media, string? InputScript, bool Recovery = false, bool CaptureProbe = false);
    internal static CopperScreenRecentSession? ActiveSession { get; set; }
    private static Request _request = null!;
    private static Dictionary<string, object?> _report = null!;
    private static string _directory = "", _profile = "";
    private static long _dashboardWriteFailures;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    internal static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    internal static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    internal static void Run(string[] args, Action<CopperScreenRecentSession, string[]> show)
    {
        if (args.Length != 2 || !Path.IsPathFullyQualified(args[1])) { Environment.ExitCode = 2; return; }
        _request = JsonSerializer.Deserialize<Request>(File.ReadAllText(args[1]))!;
        var r = _request;
        _report = new() { ["protocol"] = r.Recovery ? "copperscreen-installed-recovery-v4" : "copperscreen-installed-soak-v1", ["passed"] = false, ["case"] = r.Case, ["phase"] = r.Phase,
            ["durationRequestedSeconds"] = r.DurationSeconds, ["releaseBudgets"] = "OPEN", ["engine"] = "Lightweight", ["legacy"] = false };
        try
        {
            if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Installed soak requires Windows.");
            Require(Guid.TryParseExact(r.RunId, "N", out _) && r.Case is "copperstart" or "copperos" &&
                r.Phase is "soak" or "reopen" && r.DurationSeconds is >= 10 and <= 3600 && Path.IsPathFullyQualified(r.OutputDirectory), "Invalid bounded soak request.");
            Require(!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator), "App must be non-elevated.");
            uint length = 0; var status = GetCurrentPackageFullName(ref length, null);
            if (status != 122) throw new Win32Exception(status, "Activate the installed package.");
            var fullName = new StringBuilder((int)length); status = GetCurrentPackageFullName(ref length, fullName);
            if (status != 0) throw new Win32Exception(status);
            var identity = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "AppxManifest.xml")).Root!.Elements().Single(e => e.Name.LocalName == "Identity");
            Require((string?)identity.Attribute("Name") == "CopperScreen.Test." + r.RunId, "Package identity must match this run.");
            _report["packageFullName"] = fullName.ToString(); _report["packageVersion"] = (string?)identity.Attribute("Version");
            _report["packageFamily"] = CopperScreenUserData.CurrentPackageFamily(); _report["installDirectory"] = AppContext.BaseDirectory;
            _report["elevated"] = false; _report["startedUtc"] = DateTime.UtcNow;
            _report["jitConfiguration"] = new { quickJit = AppContext.GetData("System.Runtime.TieredCompilation.QuickJit"),
                tieredCompilation = AppContext.GetData("System.Runtime.TieredCompilation") };
            _directory = Path.Combine(CopperScreenUserData.DefaultDirectory, "Soaks", r.RunId, r.Case);
            _report["userDataDirectory"] = _directory;
            Directory.CreateDirectory(r.OutputDirectory);
            var guest = Path.Combine(AppContext.BaseDirectory, "Diagnostics", "GuestProof");
            var rom = Path.Combine(guest, "CopperStart-DiskBoot.rom");
            _ = CopperScreenCopperStartRom.Read(rom);
            if (r.Phase == "soak")
            {
                Require(!Directory.Exists(_directory), "Use fresh owned soak data.");
                var draft = CopperScreenLaunchWorkflow.CopperStart(new()); draft.KickstartRomPath = rom;
                if (r.Case == "copperos") draft = CopperScreenLaunchWorkflow.Workspace(draft, _directory, "soak", Path.Combine(guest, "System.adf"));
                else
                {
                    Require(r.Media != null && Hash(r.Media) == "82dc1faf56823653cd3b0b299e179932c1fae117b03cae220e121d96ae8b3a06", "Exact Lotus II raw ADF required.");
                    Require(r.InputScript != null && Hash(r.InputScript) == "b54d47135be66b7949a3c5fc10daad342801789570ce27799c6ed7cb15768fd3", "Exact retained Lotus input required.");
                    draft.DriveDiskPaths[0] = r.Media; draft.DriveWriteProtected[0] = true;
                    draft.Id = "copperstart-soak";
                }
                _profile = CopperScreenProfileStore.Save(draft, AppContext.BaseDirectory, _directory);
                new CopperScreenRecentSession(_directory).Save(draft, 20, false, AppContext.BaseDirectory);
                File.WriteAllText(Path.Combine(r.OutputDirectory, r.Case + "-profile-path.txt"), _profile);
            }
            else _profile = File.ReadAllText(Path.Combine(r.OutputDirectory, r.Case + "-profile-path.txt"));
            Require(Path.GetFullPath(_profile).StartsWith(_directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "Profile outside owned data.");
            _report["profileSha256Before"] = Hash(_profile);
            _report["romSha256"] = Hash(rom);
            _report["binaries"] = new[] { typeof(Program).Assembly.Location, typeof(CopperMod.Amiga.Lightweight.LightweightA500Machine).Assembly.Location,
                typeof(Copper68k.M68kCpuModel).Assembly.Location }.Select(p => new { path = p, sha256 = Hash(p) }).ToArray();
            show(new CopperScreenRecentSession(_directory), ["--profile", _profile]);
            Require(_report.ContainsKey("completedSession"), "Desktop session did not complete its observation.");
            Require(Hash(_profile) == (string)_report["profileSha256Before"]!, "Personal profile changed.");
            _report["files"] = Directory.GetFiles(_directory, "*.json").Append(_profile).ToDictionary(p => p, Hash);
            _report["passed"] = true;
        }
        catch (Exception ex) { _report["error"] = ex.ToString(); Environment.ExitCode = 1; }
        finally { _report["completedUtc"] = DateTime.UtcNow; _report["dashboardWriteFailures"] = Interlocked.Read(ref _dashboardWriteFailures); Write(Path.Combine(r.OutputDirectory, r.Case + "-" + r.Phase + ".json"), _report); }
    }
    internal static void Observe(MainWindow window)
    {
        var closes = new List<object>(); _report["closeRequests"] = closes;
        window.Closing += (_, _) => closes.Add(new { utc = DateTime.UtcNow, stack = Environment.StackTrace });
        window.Opened += (_, _) => DispatcherTimer.RunOnce(async () =>
        {
            try { await window.RunInstalledSoakAsync(_request, _report); _report["completedSession"] = true; }
            catch (Exception ex)
            {
                _report["sessionError"] = ex.ToString(); Environment.ExitCode = 1;
                Write(Path.Combine(_request.OutputDirectory, _request.Case + "-" + _request.Phase + "-failure.json"), _report);
                // A failed diagnostic retains its private image and exports; do not hang on an unattended discard dialog.
                await window.AbortInstalledSoakAsync();
            }
            finally { _report["observerRequestedCloseUtc"] = DateTime.UtcNow; window.Close(); }
        }, TimeSpan.FromMilliseconds(500));
    }
    internal static void Write(string path, object value) => CopperScreenUserData.WriteAtomically(path, JsonSerializer.Serialize(value, Json));
    internal static bool TryWriteDashboard(string path, object value)
    {
        try { Write(path, value); return true; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { Interlocked.Increment(ref _dashboardWriteFailures); return false; }
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern int GetCurrentPackageFullName(ref uint length, StringBuilder? name);

    internal static string Console(CopperScreenLightweightSession session)
    {
        var machine = session.Machine;
        var slow = (byte[])machine.GetType().GetField("_slowRam", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(machine)!;
        byte Read(uint a) => a < machine.ChipRam.Length ? machine.ChipRam.Span[(int)a] : a >= 0xc00000 && a - 0xc00000 < slow.Length ? slow[a - 0xc00000] : (byte)0;
        uint Long(uint a) => ((uint)Read(a) << 24) | ((uint)Read(a + 1) << 16) | ((uint)Read(a + 2) << 8) | Read(a + 3);
        var control = Long(0x880); var console = Long(control + 32); var bitmap = Long(console + 128);
        if (control == 0 || console == 0 || bitmap == 0 || bitmap + 16000 > machine.ChipRam.Length) return "";
        var glyphs = (Dictionary<ulong, char>)typeof(CopperOsNativeStorageProof).GetField("Glyphs", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var text = new StringBuilder();
        for (var row = 0; row < 25; row++)
        {
            for (var col = 0; col < 80; col++) { ulong glyph = 0; for (var y = 0; y < 8; y++) glyph = (glyph << 8) | Read(bitmap + (uint)((row * 8 + y) * 80 + col)); text.Append(glyphs.GetValueOrDefault(glyph, '?')); }
            text.Append('\n');
        }
        return text.ToString();
    }
}

internal sealed partial class MainWindow
{
    internal async Task AbortInstalledSoakAsync()
    {
        if (_runtime != null) await _runtime.DiscardPersistentStorageAsync();
        _allowDiscardOnClose = true;
    }
    internal async Task RunInstalledSoakAsync(CopperScreenInstalledSoak.Request request, Dictionary<string, object?> report)
    {
        var runtime = _runtime ?? throw new InvalidOperationException("Normal window did not start its native runtime: " + _settingsStartupError);
        var output = request.OutputDirectory; var stem = request.Case + "-" + request.Phase;
        var warmup = Stopwatch.StartNew(); var lastWarmupReport = -5d; var measuring = false;
        void Check()
        {
            var state = runtime.CurrentState;
            if (!IsVisible || state.IsPaused || state.FaultMessage != null)
            {
                report["failureState"] = state; report["failureWindow"] = new { IsVisible, WindowState, _isClosed, _checkingDiskChanges, _allowDiscardOnClose, uiThread = Dispatcher.UIThread.CheckAccess() };
                throw new InvalidOperationException($"Window visible={IsVisible}, closed={_isClosed}, paused={state.IsPaused}, fault={state.FaultMessage ?? "<none>"}.");
            }
            if (!measuring && warmup.Elapsed.TotalSeconds - lastWarmupReport >= 5)
            {
                lastWarmupReport = warmup.Elapsed.TotalSeconds;
                CopperScreenInstalledSoak.TryWriteDashboard(Path.Combine(output, "progress.json"), new { phase = stem + "-warmup", elapsedSeconds = lastWarmupReport,
                    frame = runtime.CurrentState.FrameNumber, audio = runtime.DiagnosticAudio });
            }
        }
        async Task Observe(Action<CopperScreenLightweightSession> action)
        { var result = await runtime.DiagnosticObserveAsync(e => action((CopperScreenLightweightSession)e)).WaitAsync(TimeSpan.FromSeconds(15)); CopperScreenInstalledSoak.Require(result.Success, result.Message); }
        async Task<string> Screen() { var text = ""; await Observe(e => text = CopperScreenInstalledSoak.Console(e)); return text; }
        async Task Wait(string phrase)
        {
            var timer = Stopwatch.StartNew();
            while (true) { Check(); var text = await Screen(); if (text.Contains(phrase, StringComparison.Ordinal)) return;
                CopperScreenInstalledSoak.Require(!text.Contains("FAILED", StringComparison.Ordinal) && timer.Elapsed.TotalMinutes < 10, "Native CLI failed/timed out: " + text); await Task.Delay(500); }
        }
        async Task Storage(string phrase)
        {
            foreach (var key in "storage".Select(c => Enum.Parse<AmigaRawKey>(char.ToUpperInvariant(c).ToString())).Append(AmigaRawKey.Return))
            { runtime.KeyDown(key); await Task.Delay(100); runtime.KeyUp(key); await Task.Delay(100); }
            await Wait(phrase); await Task.Delay(1000);
            File.WriteAllText(Path.Combine(output, stem + "-storage.txt"), await Screen());
        }
        void Capture(string suffix)
        {
            CopperScreenInstalledCapture.Save(this, _presenter, ClientSize, RenderScaling,
                Path.Combine(output, stem + "-" + suffix + ".png"), request.Case == "copperos", _presentedFrames);
        }
        Check(); report["windowOpened"] = true; report["renderScaling"] = RenderScaling;
        if (request.Case == "copperos")
        {
            await Wait("2.>");
            await Storage(request.Phase == "reopen" ? "COPPEROS STORAGE REOPEN VERIFIED" : "COPPEROS STORAGE CREATED AND VERIFIED");
            var saved = await runtime.SavePersistentStorageAsync(); CopperScreenInstalledSoak.Require(saved.Success, saved.Message);
        }
        else
        {
            using var script = JsonDocument.Parse(File.ReadAllText(request.InputScript!));
            foreach (var input in script.RootElement.EnumerateArray())
            {
                var frame = input.GetProperty("frame").GetInt64(); var timeout = Stopwatch.StartNew();
                while (runtime.CurrentState.FrameNumber < frame) { Check(); CopperScreenInstalledSoak.Require(timeout.Elapsed.TotalMinutes < 5, "CopperStart boot/input did not advance."); await Task.Delay(10); }
                var joy = input.TryGetProperty("joystickPort1", out var j) ? j.GetInt32() : 0;
                runtime.SetJoystickPort(1, (joy & 1) != 0, (joy & 2) != 0, (joy & 4) != 0, (joy & 8) != 0, (joy & 16) != 0, (joy & 32) != 0);
            }
            while (runtime.CurrentState.FrameNumber < 14000) { Check(); await Task.Delay(100); }
        }
        Capture("start");
        if (request.CaptureProbe) await RunInstalledCaptureProbeAsync(runtime, output, stem);
        measuring = true;
        report["audioBefore"] = runtime.DiagnosticAudio; report["stateBefore"] = runtime.CurrentState;
        var clock = Stopwatch.StartNew(); var previous = 0d; var previousFrame = runtime.CurrentState.FrameNumber;
        var samples = 0; var midpoint = false;
        using (var stream = new StreamWriter(Path.Combine(output, stem + "-samples.jsonl"), false, Encoding.UTF8) { AutoFlush = true })
        {
            if (request.Recovery)
                report["recovery"] = await RunInstalledRecoveryAsync(runtime, request, stream, Capture);
            else while (clock.Elapsed.TotalSeconds < request.DurationSeconds)
            {
                Check(); using var process = Process.GetCurrentProcess(); process.Refresh();
                var elapsed = clock.Elapsed.TotalSeconds; var state = runtime.CurrentState;
                CopperScreenInstalledSoak.Require(elapsed - previous < 30 && (samples == 0 || state.FrameNumber > previousFrame), "Sampling gap or guest execution stopped; hour is not qualified.");
                previous = elapsed; previousFrame = state.FrameNumber;
                var sample = new { elapsedSeconds = elapsed, utc = DateTime.UtcNow, state, audio = runtime.DiagnosticAudio,
                    privateBytes = process.PrivateMemorySize64, workingSetBytes = process.WorkingSet64, managedBytes = GC.GetTotalMemory(false),
                    committedManagedBytes = GC.GetGCMemoryInfo().TotalCommittedBytes, gc0 = GC.CollectionCount(0), gc1 = GC.CollectionCount(1), gc2 = GC.CollectionCount(2),
                    handles = process.HandleCount, threads = process.Threads.Count, cpuSeconds = process.TotalProcessorTime.TotalSeconds,
                    logBytes = Directory.Exists(Path.Combine(_recentSession.DataDirectory, "Logs")) ? Directory.GetFiles(Path.Combine(_recentSession.DataDirectory, "Logs")).Sum(p => new FileInfo(p).Length) : 0 };
                stream.WriteLine(JsonSerializer.Serialize(sample)); samples++;
                CopperScreenInstalledSoak.TryWriteDashboard(Path.Combine(output, "progress.json"), new { phase = stem, elapsedSeconds = elapsed, targetSeconds = request.DurationSeconds, samples, state.FrameNumber, state.FaultMessage, sample.privateBytes, audio = sample.audio });
                if (!midpoint && elapsed >= request.DurationSeconds / 2d)
                {
                    midpoint = true;
                    var saved = await runtime.SavePersistentStorageAsync(); CopperScreenInstalledSoak.Require(saved.Success, saved.Message);
                    Capture("middle");
                }
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(5, Math.Max(.01, request.DurationSeconds - clock.Elapsed.TotalSeconds))));
            }
        }
        Check(); report["elapsedSeconds"] = clock.Elapsed.TotalSeconds; report["samples"] = samples;
        report["stateAfter"] = runtime.CurrentState; report["audioAfter"] = runtime.DiagnosticAudio; Capture("end");
        if (request.Case == "copperos" && request.Phase == "soak") await Storage("COPPEROS STORAGE REOPEN VERIFIED");
        var checkpoint = await runtime.SavePersistentStorageAsync(); CopperScreenInstalledSoak.Require(checkpoint.Success, checkpoint.Message);
        var disk = runtime.CurrentState.DiskPath!;
        report["diskSha256"] = CopperScreenInstalledSoak.Hash(disk);
        if (request.Case == "copperos")
        {
            CopperScreenInstalledSoak.Require((string)report["diskSha256"]! == "e58ea8d751039e5ddfc6ee77f72e705ba48ed4b75a8df96de87a926dc8f84bb0", "Native persisted files differ from the qualified storage result.");
            var backup = await runtime.ExportWorkspaceAsync(Path.Combine(output, stem + ".copperos")); CopperScreenInstalledSoak.Require(backup.Success, backup.Message);
        }
        else
        {
            var export = Path.Combine(output, stem + ".adf");
            var saved = await runtime.SaveAdfAsync(0, export); CopperScreenInstalledSoak.Require(saved.Success && CopperScreenInstalledSoak.Hash(export) == CopperScreenInstalledSoak.Hash(request.Media!), "Read-only game image/export changed.");
        }
        await runtime.TogglePausedAsync(); RememberCurrentSession();
        await runtime.DiagnosticWriteAudioTimelineAsync(Path.Combine(output, stem + "-audio-timeline.json"));
        report["crashLog"] = CopperScreenCrashLog.CurrentLogPath;
    }
}
#endif
