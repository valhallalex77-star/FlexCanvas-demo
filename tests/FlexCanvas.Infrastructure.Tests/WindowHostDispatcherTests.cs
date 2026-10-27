using FlexCanvas.Infrastructure.Win32;
using Xunit;

namespace FlexCanvas.Infrastructure.Tests;

public sealed class WindowHostDispatcherTests
{
    [Fact]
    public async Task ConcurrentRequestsRunOnOneStaThread()
    {
        await using var dispatcher = new WindowHostDispatcher();
        int value = 0;
        var tasks = Enumerable.Range(0, 16).Select(_ => dispatcher.InvokeAsync(() =>
        {
            Assert.Equal(ApartmentState.STA, Thread.CurrentThread.GetApartmentState());
            value++;
            return Environment.CurrentManagedThreadId;
        })).ToArray();
        int[] threads = await Task.WhenAll(tasks);
        Assert.Single(threads.Distinct());
        Assert.Equal(16, value);
    }

    [Fact]
    public async Task PreCancelledOperationDoesNotRun()
    {
        await using var dispatcher = new WindowHostDispatcher();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        bool executed = false;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => dispatcher.InvokeAsync(() => executed = true, cancellation.Token));
        Assert.False(executed);
    }
}
