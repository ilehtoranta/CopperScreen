using System.Diagnostics;
using CopperScreen;

namespace CopperScreen.Lightweight.Tests;

public sealed class CrtPhosphorComposerTests
{
    [Fact]
    public void CrtPhosphorStopsAtNextFieldBoundaryAndFreshFieldRestartsDecay()
    {
        var phosphor = new CrtPhosphorComposer();
        var active = unchecked((int)0xFF204080);
        var fieldHistory = Enumerable.Repeat(active, 16).ToArray();
        var timestamp = Stopwatch.Frequency;
        var fieldTicks = Stopwatch.Frequency / 50;

        phosphor.SubmitField(
            fieldHistory,
            width: 4,
            height: 4,
            interlaceField: 1,
            fieldDurationSeconds: 1.0 / 50.0,
            timestamp);
        phosphor.Advance(timestamp + fieldTicks);
        var heldOutput = phosphor.Output.ToArray();

        phosphor.Advance(timestamp + Stopwatch.Frequency);

        Assert.Equal(heldOutput, phosphor.Output);
        Assert.False(phosphor.HasPendingDecay);

        var nextFieldTimestamp = timestamp + Stopwatch.Frequency;
        phosphor.SubmitField(
            fieldHistory,
            width: 4,
            height: 4,
            interlaceField: 0,
            fieldDurationSeconds: 1.0 / 50.0,
            nextFieldTimestamp);
        var beforeResumedDecay = phosphor.Output[0];

        phosphor.Advance(nextFieldTimestamp + (fieldTicks / 2));

        Assert.True((uint)phosphor.Output[0] < (uint)beforeResumedDecay);
        Assert.True(phosphor.HasPendingDecay);
    }
}
