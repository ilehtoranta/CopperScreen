using Xunit;

namespace CopperMod.Amiga.Lightweight.Tests;

public sealed class LightweightStorageReceiverTests
{
    [Fact]
    public void AlarmSelectedTodReadDoesNotLeaveStaleReadLatch()
    {
        var cia = new LightweightCia(); cia.Reset();
        cia.WriteRegister(8, 215, 0, out _);
        cia.WriteRegister(15, 0x80, 0, out _);
        Assert.Equal(0, cia.ReadRegister(10, 255, 0, out _));
        cia.WriteRegister(15, 0, 0, out _);
        cia.WriteRegister(10, 0, 0, out _); cia.WriteRegister(9, 0, 0, out _); cia.WriteRegister(8, 0, 0, out _);
        for (var i = 0; i < 313; i++) cia.PulseTod(i);
        Assert.Equal(0, cia.ReadRegister(10, 255, 320, out _));
        Assert.Equal(1, cia.ReadRegister(9, 255, 320, out _));
        Assert.Equal(57, cia.ReadRegister(8, 255, 320, out _));
    }

    [Theory]
    [InlineData(true, 14)]
    [InlineData(false, 28)]
    public void CausalReceiverProducesZerosThroughNoFluxAndRecoversClock(bool fast, int nominalCell)
    {
        var receiver = new LightweightDiskReceiver(); receiver.Reset(); receiver.Enable(0, fast);
        for (var i = 0; i < 1000; i++) Assert.Equal(0, receiver.Advance(receiver.NextCycle));
        var cycle = receiver.NextCycle;
        var ones = 0; var zeros = 0;
        var pulseAt = cycle + nominalCell / 2;
        for (var i = 0; i < 2000; i++)
        {
            var next = Math.Min(receiver.NextCycle, pulseAt);
            var bit = receiver.Advance(next);
            if (i > 100) { if (bit == 1) ones++; else if (bit == 0) zeros++; }
            if (next == pulseAt) { receiver.Pulse(); pulseAt += nominalCell * 2; }
            Assert.True(receiver.NextCycle > next);
        }
        Assert.InRange(receiver.Rate, 140, 151);
        Assert.InRange(ones / (double)(ones + zeros), 0.48, 0.52);
        _ = MeasureEmptyReceiver(ref receiver);
        Assert.Equal(0, MeasureEmptyReceiver(ref receiver));
        Assert.Equal(0, receiver.Advance(receiver.NextCycle));
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static long MeasureEmptyReceiver(ref LightweightDiskReceiver receiver)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 2000; i++) receiver.Advance(receiver.NextCycle);
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}
