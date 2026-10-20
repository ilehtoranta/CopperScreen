using CopperScreen;

namespace CopperScreen.Tests;

public sealed class MeasuredWasapiBurstTests
{
    // Signed 41 CopperOS owner/callback trace, retained in the release evidence:
    // callbacks 23810..23821 request 12 * 2048 values in three groups within
    // 47.44 ms; four intervening complete fields submit 1924,1922,1924,1922.
    // The initial available PCM is reconstructed by conservation, not inferred
    // from slot count: 24576 requested - 2926 missing - 7692 produced = 13958.
    // Seven conservative 1922-value blocks plus one 504-value partial represent
    // that same availability. No ROM, timing sleeps or PCM fixture is needed.
    [Theory]
    [InlineData(8, 2926)]
    [InlineData(9, 1004)]
    [InlineData(10, 0)]
    public void RetainedDemandDistinguishesInsufficientAndCompleteReserves(int blocks, int expectedMissing)
        => Assert.Equal(expectedMissing, Replay(blocks));

    [Fact]
    public void ProductionReserveCoversRetainedWasapiCatchupDemandWithoutPcmLoss()
    {
        var blocks = (int)typeof(CopperScreenRuntime).GetField("AudioOutputBufferCount",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.GetRawConstantValue()!;
        Assert.Equal(0, Replay(blocks));
    }

    private static int Replay(int blocks)
    {
        var queue = new MiniaudioSampleQueue(1924, blocks);
        var next = 1;
        var output = new List<float>();
        void Submit(int count)
        {
            var values = Enumerable.Range(next, count).Select(n => (float)n).ToArray();
            Assert.True(queue.TryEnqueue(values));
            next += count;
        }
        void CallbackGroup()
        {
            for (var i = 0; i < 4; i++)
            {
                var values = new float[2048];
                queue.Dequeue(values);
                output.AddRange(values);
            }
        }
        Submit(504);
        for (var i = 1; i < blocks; i++) Submit(1922);
        CallbackGroup();
        Submit(1924); Submit(1922);
        CallbackGroup();
        Submit(1924); Submit(1922);
        CallbackGroup();
        var missing = output.Count(n => n == 0);
        Assert.Equal(Enumerable.Range(1, output.Count - missing).Select(n => (float)n), output.Where(n => n != 0));
        return missing;
    }
}
