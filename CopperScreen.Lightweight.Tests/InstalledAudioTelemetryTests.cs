using System.Reflection;
using System.Text.Json;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed class InstalledAudioTelemetryTests
{
    [InstalledAudioTelemetryFact]
    public void JoinedResetReportsUnreadValuesSeparatelyFromCallbackUnderruns()
    {
        var queue = new MiniaudioSampleQueue(4, 3);
        queue.TryEnqueue([1f, 2f, 3f, 4f]);
        queue.Dequeue(new float[1]);
        queue.ResetAfterConsumerStopped();
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(typeof(MiniaudioSampleQueue)
            .GetProperty("DiagnosticSnapshot", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(queue)));
        var root = json.RootElement;
        Assert.Equal(3, root.GetProperty("discardedSampleValues").GetInt64());
        Assert.Equal(1, root.GetProperty("consumedSamples").GetInt64());
        Assert.Equal(0, root.GetProperty("missingSamples").GetInt64());
    }

    [InstalledAudioTelemetryFact]
    public void DeliberatePausedSilenceDoesNotHideAnActivePlaybackUnderrun()
    {
        var queue = new MiniaudioSampleQueue(4, 3);
        queue.Dequeue(new float[8], expectAudio: false);
        queue.TryEnqueue([1f, -1f]);
        queue.Dequeue(new float[4]);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(typeof(MiniaudioSampleQueue)
            .GetProperty("DiagnosticSnapshot", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(queue)));
        var root = json.RootElement;
        Assert.Equal(8, root.GetProperty("pausedSilenceSamples").GetInt64());
        Assert.Equal(2, root.GetProperty("missingSamples").GetInt64());
        Assert.Equal(1, root.GetProperty("starvedCallbacks").GetInt64());
        Assert.Equal(12, root.GetProperty("requestedSamples").GetInt64());
    }

    [InstalledAudioTelemetryFact]
    public void LockedDiagnosticDashboardCannotAbortTheNativeSession()
    {
        var type = typeof(MiniaudioSampleQueue).Assembly.GetType("CopperScreen.CopperScreenInstalledSoak")!;
        // The fallback lets the unchanged control expose the old fatal writer in a retained package.
        var write = type.GetMethod("TryWriteDashboard", BindingFlags.Static | BindingFlags.NonPublic)
            ?? type.GetMethod("Write", BindingFlags.Static | BindingFlags.NonPublic)!;
        var directory = Path.Combine(Path.GetTempPath(), "copperscreen-dashboard-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "progress.json");
        try
        {
            File.WriteAllText(path, "retained");
            using (var reader = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Assert.False((bool)write.Invoke(null, [path, new { phase = "locked" }])!);
                Assert.Equal("retained", File.ReadAllText(path));
            }
            Assert.True((bool)write.Invoke(null, [path, new { phase = "recovered" }])!);
            Assert.Contains("recovered", File.ReadAllText(path));
        }
        finally { File.Delete(path); Directory.Delete(directory); }
    }

    [InstalledAudioTelemetryFact]
    public void CallbackCountersDistinguishQueuedSilenceFromMissingSamples()
    {
        var queue = new MiniaudioSampleQueue(4, 3);
        Assert.True(queue.TryEnqueue([0f, 0f]));
        queue.Dequeue(new float[4]); // Two supplied silent samples and two missing samples.
        Assert.True(queue.TryEnqueue([.5f, -.5f, 0f, 0f]));
        queue.Dequeue(new float[4]);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(typeof(MiniaudioSampleQueue)
            .GetProperty("DiagnosticSnapshot", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(queue)));
        var root = json.RootElement;
        Assert.Equal(2, root.GetProperty("callbacks").GetInt64());
        Assert.Equal(8, root.GetProperty("requestedSamples").GetInt64());
        Assert.Equal(6, root.GetProperty("consumedSamples").GetInt64());
        Assert.Equal(1, root.GetProperty("starvedCallbacks").GetInt64());
        Assert.Equal(2, root.GetProperty("missingSamples").GetInt64());
        Assert.Equal(1, root.GetProperty("nonzeroCallbacks").GetInt64());
    }
}

public sealed class InstalledAudioTelemetryFactAttribute : FactAttribute
{
    public InstalledAudioTelemetryFactAttribute()
    {
        if (typeof(MiniaudioSampleQueue).GetProperty("DiagnosticSnapshot", BindingFlags.NonPublic | BindingFlags.Instance) == null)
            Skip = "Diagnostic-only callback instrumentation is excluded from this production assembly.";
    }
}
