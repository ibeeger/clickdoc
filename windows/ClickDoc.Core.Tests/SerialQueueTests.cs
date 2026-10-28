namespace ClickDoc.Core.Tests;

public class SerialQueueTests
{
    [Fact]
    public async Task RunsInOrderEvenWhenEarlierWorkIsSlower()
    {
        var queue = new SerialQueue(_ => { });
        var order = new List<int>();
        queue.Enqueue(async () => { await Task.Delay(100); lock (order) order.Add(1); });
        queue.Enqueue(() => { lock (order) order.Add(2); return Task.CompletedTask; });
        await queue.Drain();
        Assert.Equal([1, 2], order);
    }

    [Fact]
    public async Task FailureIsReportedAndLaterWorkStillRuns()
    {
        var errors = new List<Exception>();
        var queue = new SerialQueue(e => { lock (errors) errors.Add(e); });
        bool ranAfter = false;
        queue.Enqueue(() => throw new InvalidOperationException("boom"));
        queue.Enqueue(async () => { await Task.Yield(); throw new InvalidOperationException("async boom"); });
        queue.Enqueue(() => { ranAfter = true; return Task.CompletedTask; });
        await queue.Drain();  // never throws
        Assert.True(ranAfter);
        Assert.Equal(["boom", "async boom"], errors.Select(e => e.Message));
    }
}
