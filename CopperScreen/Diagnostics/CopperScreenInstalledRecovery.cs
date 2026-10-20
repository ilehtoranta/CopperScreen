#if COPPERSCREEN_INSTALLED_TESTS
using System.Diagnostics;
using System.Text.Json;

namespace CopperScreen;

// The controller may request only normal runtime operations. OS power/device
// changes occur in a separate bounded Windows test helper, never in the app.
internal sealed partial class MainWindow
{
    private sealed record RecoveryCommand(int Id, string Action);

    private async Task<object> RunInstalledRecoveryAsync(CopperScreenRuntime runtime,
        CopperScreenInstalledSoak.Request request, StreamWriter samples, Action<string> capture)
    {
        var prefix = Path.Combine(request.OutputDirectory, request.Case + "-recovery");
        var timer = Stopwatch.StartNew();
        var commands = new List<object>();
        var lastId = 0;
        var finished = false;
        object Snapshot() => new { utc = DateTime.UtcNow, qpc = Stopwatch.GetTimestamp(),
            qpcFrequency = Stopwatch.Frequency, elapsedSeconds = timer.Elapsed.TotalSeconds,
            state = runtime.CurrentState, audio = runtime.DiagnosticAudio, visible = IsVisible };
        CopperScreenInstalledSoak.Write(prefix + "-ready.json", Snapshot());
        while (timer.Elapsed.TotalSeconds < request.DurationSeconds)
        {
            var state = runtime.CurrentState;
            CopperScreenInstalledSoak.Require(IsVisible && state.FaultMessage == null, "Recovery session closed or faulted.");
            var path = prefix + "-command.json";
            if (File.Exists(path))
            {
                var command = JsonSerializer.Deserialize<RecoveryCommand>(File.ReadAllText(path))!;
                if (command.Id > lastId)
                {
                    CopperScreenInstalledSoak.Require(command.Id == lastId + 1 &&
                        command.Action is "snapshot" or "pause" or "resume" or "checkpoint" or "finish" or "audio-stop" or "audio-close" or "audio-scheduling-off" or "audio-scheduling-on", "Invalid recovery command.");
                    if (command.Action is "audio-scheduling-off" or "audio-scheduling-on")
                    {
                        var changed = await runtime.DiagnosticSetAudioSchedulingAsync(command.Action == "audio-scheduling-on").WaitAsync(TimeSpan.FromSeconds(15));
                        CopperScreenInstalledSoak.Require(changed.Success, changed.Message);
                    }
                    if (command.Action == "audio-stop")
                    {
                        var stopped = await runtime.DiagnosticStopAudioAsync().WaitAsync(TimeSpan.FromSeconds(15));
                        CopperScreenInstalledSoak.Require(stopped.Success, stopped.Message);
                    }
                    if (command.Action == "audio-close")
                    {
                        var closed = await runtime.DiagnosticCloseAudioAsync().WaitAsync(TimeSpan.FromSeconds(15));
                        CopperScreenInstalledSoak.Require(closed.Success, closed.Message);
                    }
                    if (command.Action is "pause" or "resume")
                    {
                        var pause = command.Action == "pause";
                        if (runtime.CurrentState.IsPaused != pause)
                        {
                            // Same command and toolbar update used by the normal pause control.
                            await ToggleRuntimePauseAsync().WaitAsync(TimeSpan.FromSeconds(15));
                        }
                        CopperScreenInstalledSoak.Require(runtime.CurrentState.IsPaused == pause, "Pause/resume command failed.");
                    }
                    if (command.Action == "checkpoint")
                    {
                        var saved = await runtime.SavePersistentStorageAsync().WaitAsync(TimeSpan.FromSeconds(15));
                        CopperScreenInstalledSoak.Require(saved.Success, saved.Message);
                    }
                    if (command.Action == "finish")
                    {
                        CopperScreenInstalledSoak.Require(!runtime.CurrentState.IsPaused, "Resume before ending recovery qualification.");
                        finished = true;
                    }
                    capture("recovery-" + command.Id);
                    var reply = new { id = command.Id, action = command.Action, passed = true, snapshot = Snapshot() };
                    commands.Add(reply);
                    CopperScreenInstalledSoak.Write(prefix + "-reply-" + command.Id + ".json", reply);
                    lastId = command.Id;
                }
            }
            samples.WriteLine(JsonSerializer.Serialize(Snapshot()));
            CopperScreenInstalledSoak.TryWriteDashboard(prefix + "-progress.json", Snapshot());
            if (finished) break;
            await Task.Delay(250);
        }
        CopperScreenInstalledSoak.Require(finished, "Recovery controller did not finish within its bounded test interval.");
        return new { protocol = "copperscreen-installed-recovery-v4", passed = true, commands,
            elapsedSeconds = timer.Elapsed.TotalSeconds, assertions = "Controller must verify actual OS transitions, paused stability, audio delivery and saves independently." };
    }
}
#endif
