namespace ClickDoc.Core;

/// Runs queued work one item at a time, in order. A failing item is reported and skipped,
/// so one bad click never stops the ones after it.
public sealed class SerialQueue(Action<Exception> onError)
{
    readonly object _lock = new();
    Task _tail = Task.CompletedTask;

    public void Enqueue(Func<Task> work)
    {
        lock (_lock) _tail = Run(_tail, work);
    }

    /// Completes when everything queued so far has run; never throws.
    public Task Drain()
    {
        lock (_lock) return _tail;
    }

    async Task Run(Task previous, Func<Task> work)
    {
        await previous;  // never faults: every item catches its own exceptions
        try { await work(); }
        catch (Exception e) { onError(e); }
    }
}
