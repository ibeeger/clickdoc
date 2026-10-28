namespace ClickDoc.Recording;

/// Turns global clicks into steps. Filled in by the recording pipeline.
sealed class Recorder
{
    public bool Start() => true;

    public Task Stop(bool fromTray) => Task.CompletedTask;

    /// Forgets the previous click so it cannot be upgraded to a double-click.
    public void ForgetLastClick() { }
}
