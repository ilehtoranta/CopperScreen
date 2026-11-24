using Avalonia.Threading;
using CopperScreen;

namespace CopperScreen.Tests;

public sealed class MainWindowPresentationTests
{
    [Fact]
    public void OverdueFrameProgressesWhileFreshFrameYieldsToInput()
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        var order = new List<string>();
        var first = dispatcher.InvokeAsync(() => order.Add("first frame"), MainWindow.FramePresentationPriority);
        var second = dispatcher.InvokeAsync(() => order.Add("second frame"), MainWindow.FramePresentationPriority);
        var secondOfTicks = System.Diagnostics.Stopwatch.Frequency;

        dispatcher.Post(() => order.Add("next input"), DispatcherPriority.Input);
        MainWindow.PromoteOverdueFrame(first, secondOfTicks, secondOfTicks);
        Assert.Equal(MainWindow.FramePresentationPriority, first.Priority);

        var overdue = secondOfTicks + secondOfTicks / 20;
        MainWindow.PromoteOverdueFrame(first, secondOfTicks, overdue);
        MainWindow.PromoteOverdueFrame(second, overdue, overdue + 1);
        dispatcher.RunJobs();

        Assert.Equal(new[] { "first frame", "next input", "second frame" }, order);
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
