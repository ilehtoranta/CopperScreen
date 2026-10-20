#if COPPERSCREEN_INSTALLED_TESTS
using System.Text.Json;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed class AudioTimelineTests
{
    [Fact]
    public void IncidentRetentionPreservesPrecedingAndFollowingRecordsWithoutGrowingStorage()
    {
        var ring = new AudioTimelineRing<int>(2, 3);
        for (var i = 1; i <= 10; i++) ring.Add(i);
        ring.RetainIncidentWindow(2);
        for (var i = 11; i <= 14; i++) { ring.RetainIncidentWindow(2); ring.Add(i); }
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(ring.Export()));
        var root = json.RootElement;
        Assert.Equal(12, root.GetProperty("total").GetInt64());
        Assert.Equal(14, root.GetProperty("observedTotal").GetInt64());
        Assert.Equal(2, root.GetProperty("notRecordedAfterIncident").GetInt64());
        Assert.Equal([1, 2, 10, 11, 12], root.GetProperty("records").EnumerateArray().Select(r => r.GetProperty("value").GetInt32()));
    }

    [Fact]
    public void FirstUnderrunIsRetainedAndLaterMissingValuesRemainInFinalCounter()
    {
        var trace = new CopperScreenAudioTimeline();
        for (var i = 1; i <= 30000; i++) trace.RecordCallback(new(i, i + 1, 8, 7, 2048, 0, 0, true));
        trace.RecordCallback(new(30001, 30002, 0, 0, 2048, 12, 12, true));
        for (var i = 30002; i <= 35000; i++) trace.RecordCallback(new(i, i + 1, 8, 7, 2048, 0, 12, true));
        trace.RecordCallback(new(35001, 35002, 0, 0, 2048, 7, 19, true));
        trace.Freeze();
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(trace.Export()));
        Assert.Equal(30001, json.RootElement.GetProperty("firstIncidentQpc").GetInt64());
        Assert.Equal(19, json.RootElement.GetProperty("finalCumulativeMissingValues").GetInt64());
        var callbacks = json.RootElement.GetProperty("callbacks");
        Assert.True(callbacks.GetProperty("notRecordedAfterIncident").GetInt64() > 0);
        Assert.Contains(callbacks.GetProperty("records").EnumerateArray(), r => r.GetProperty("value").GetProperty("MissingValues").GetInt64() == 12);
        Assert.Equal(12, callbacks.GetProperty("records").EnumerateArray().Last().GetProperty("value").GetProperty("CumulativeMissingValues").GetInt64());
    }
    [Fact]
    public void BoundedTraceRetainsStartupAndLatestRecordsWithExplicitLoss()
    {
        var ring = new AudioTimelineRing<int>(2, 3);
        for (var i = 1; i <= 10; i++) ring.Add(i);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(ring.Export()));
        var root = json.RootElement;
        Assert.Equal(10, root.GetProperty("total").GetInt64());
        Assert.Equal(5, root.GetProperty("overwritten").GetInt64());
        Assert.Equal(0, root.GetProperty("incomplete").GetInt32());
        Assert.Equal([1, 2, 8, 9, 10], root.GetProperty("records").EnumerateArray()
            .Select(r => r.GetProperty("value").GetInt32()));
    }

    [Fact]
    public void RecordingAndWraparoundAllocateNoManagedMemory()
    {
        var ring = new AudioTimelineRing<long>(2, 3);
        for (var i = 0; i < 1000; i++) ring.Add(i);
        ring.RetainIncidentWindow(128);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10000; i++) ring.Add(i);
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }

    [Fact]
    public async Task SnapshotDoesNotExposeTornRecordsDuringConcurrentWraparound()
    {
        var ring = new AudioTimelineRing<Pair>(2, 3);
        var writer = Task.Run(() => { for (long i = 1; i <= 100000; i++) ring.Add(new(i, -i)); });
        for (var i = 0; i < 100; i++)
        {
            using var json = JsonDocument.Parse(JsonSerializer.Serialize(ring.Export()));
            foreach (var record in json.RootElement.GetProperty("records").EnumerateArray())
            {
                var value = record.GetProperty("value");
                Assert.Equal(value.GetProperty("A").GetInt64(), -value.GetProperty("B").GetInt64());
            }
        }
        await writer;
    }

    private readonly record struct Pair(long A, long B);
}
#endif
