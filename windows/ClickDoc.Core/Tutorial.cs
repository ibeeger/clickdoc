using SkiaSharp;

namespace ClickDoc.Core;

public sealed record Step(Guid Id, string Description, byte[] Png, DateTimeOffset CapturedAt)
{
    /// Areas to blur, normalized to the image (0...1, top-left origin). `Png` always keeps the
    /// unblurred screenshot so regions stay editable.
    public IReadOnlyList<SKRect> BlurRegions { get; init; } = [];

    /// Boxes, arrows and text drawn above the blur, also kept separate from `Png`.
    public IReadOnlyList<Annotation> Annotations { get; init; } = [];
}

public sealed class Tutorial
{
    readonly List<Step> _steps = [];

    public string Title { get; set; } = ContentLanguage.Current.UntitledTitle;

    public IReadOnlyList<Step> Steps => _steps;

    public bool IsEmpty => _steps.Count == 0;

    public void Append(Step step) => _steps.Add(step);

    /// A copy that later edits do not affect (steps are immutable records), for saving off the UI thread.
    public Tutorial Snapshot()
    {
        var copy = new Tutorial { Title = Title };
        copy._steps.AddRange(_steps);
        return copy;
    }

    public void Remove(IReadOnlySet<Guid> ids) => _steps.RemoveAll(s => ids.Contains(s.Id));

    /// Same semantics as SwiftUI's `move(fromOffsets:toOffset:)`.
    public void Move(IReadOnlyList<int> sourceIndices, int destination)
    {
        var sorted = sourceIndices.Distinct().Order().ToList();
        var moving = sorted.Select(i => _steps[i]).ToList();
        int insertAt = destination - sorted.Count(i => i < destination);
        foreach (var i in Enumerable.Reverse(sorted)) _steps.RemoveAt(i);
        _steps.InsertRange(insertAt, moving);
    }

    public void UpdateDescription(Guid id, string text) => Update(id, s => s with { Description = text });

    public void SetBlurRegions(Guid id, IReadOnlyList<SKRect> regions) => Update(id, s => s with { BlurRegions = regions });

    public void SetAnnotations(Guid id, IReadOnlyList<Annotation> annotations) => Update(id, s => s with { Annotations = annotations });

    public void UpdateLastStep(Func<Step, Step> transform)
    {
        if (_steps.Count > 0) _steps[^1] = transform(_steps[^1]);
    }

    void Update(Guid id, Func<Step, Step> transform)
    {
        int i = _steps.FindIndex(s => s.Id == id);
        if (i >= 0) _steps[i] = transform(_steps[i]);
    }
}

public static class Selection
{
    /// Steps a "delete" on `clicked` applies to: the whole selection if the row is part of it.
    public static IReadOnlySet<Guid> DeletionTargets(Guid clicked, IReadOnlySet<Guid> selection) =>
        selection.Contains(clicked) ? selection : new HashSet<Guid> { clicked };

    /// The step to select after deleting `ids`: the first survivor after the first deleted step,
    /// otherwise the last survivor before it.
    public static Guid? AfterDeleting(IReadOnlySet<Guid> ids, IReadOnlyList<Step> steps)
    {
        int first = steps.ToList().FindIndex(s => ids.Contains(s.Id));
        if (first < 0) return null;
        var after = steps.Skip(first).FirstOrDefault(s => !ids.Contains(s.Id));
        if (after is not null) return after.Id;
        return steps.Take(first).LastOrDefault(s => !ids.Contains(s.Id))?.Id;
    }
}
