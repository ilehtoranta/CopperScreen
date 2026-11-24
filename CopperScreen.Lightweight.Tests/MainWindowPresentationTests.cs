using Avalonia.Threading;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed class MainWindowPresentationTests
{
    [Fact]
    public void DueFrameRunsBetweenInputBatchesAndEarlyFrameYields()
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        var order = new List<string>();
        const long due = 100;
        dispatcher.Post(() =>
        {
            order.Add("input");
            dispatcher.Post(() => order.Add("more input"), DispatcherPriority.Input);
            dispatcher.Post(() =>
            {
                order.Add("due frame");
                dispatcher.Post(() => order.Add("early frame"), MainWindow.FramePriorityAt(due, due + 1));
            }, MainWindow.FramePriorityAt(due, due));
        }, DispatcherPriority.Input);

        dispatcher.RunJobs();
        Assert.Equal(new[] { "input", "due frame", "more input", "early frame" }, order);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 64)]
    public void ContinuousPresentationYieldsToInput(bool legacyRenderPriority, int expectedFramesBeforeInput)
    {
        // Real dispatcher ordering with a bounded, continuously replenished frame
        // queue. No window, native input injection, sleeps or emulator are needed.
        var dispatcher = Dispatcher.CurrentDispatcher;
        var priority = legacyRenderPriority ? DispatcherPriority.Render : MainWindow.FramePresentationPriority;
        var presented = 0;
        var framesBeforeInput = -1;
        void Present()
        {
            presented++;
            if (presented == 1)
                dispatcher.Post(() => framesBeforeInput = presented, DispatcherPriority.Input);
            if (presented < 64)
                dispatcher.Post(Present, priority);
        }

        dispatcher.Post(Present, priority);
        dispatcher.RunJobs();

        Assert.Equal(64, presented); // Yielding must not discard the remaining work.
        Assert.Equal(expectedFramesBeforeInput, framesBeforeInput);
    }
}
