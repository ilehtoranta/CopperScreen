using CopperScreen;

namespace CopperScreen.Tests;

public sealed class FrameNotificationTests
{
    [Fact]
    public void NotificationsCoalesceAndShutdownDoesNotWaitForBlockedUiDispatch()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var exited = new ManualResetEventSlim();
        var calls = 0;
        var released = false;
        using var notification = new CopperScreenFrameNotification(() =>
        {
            Interlocked.Increment(ref calls); entered.Set();
            try { released = release.Wait(TimeSpan.FromSeconds(5)); }
            finally { exited.Set(); }
        });
        try
        {
            notification.Signal();
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            for (var i = 0; i < 10000; i++) notification.Signal();
            Assert.Equal(1, Volatile.Read(ref calls));
            notification.Dispose(); // Must not wait for the dispatcher/worker.
            for (var i = 0; i < 10; i++) notification.Signal();
        }
        finally { release.Set(); Assert.True(exited.Wait(TimeSpan.FromSeconds(5))); }
        Assert.Equal(1, Volatile.Read(ref calls));
        Assert.True(released);
    }
}
