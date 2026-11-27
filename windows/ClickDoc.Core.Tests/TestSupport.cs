// ContentLanguage.Override is process-wide, so tests must not run in parallel.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace ClickDoc.Core.Tests;

static class TestSupport
{
    public static Step MakeStep(string description) => new(Guid.NewGuid(), description, [1], DateTimeOffset.UnixEpoch);

    public static IDisposable InLanguage(ContentLanguage language)
    {
        var previous = ContentLanguage.Override;
        ContentLanguage.Override = language;
        return new Restore(() => ContentLanguage.Override = previous);
    }

    sealed class Restore(Action undo) : IDisposable
    {
        public void Dispose() => undo();
    }
}
