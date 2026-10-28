using static ClickDoc.Core.Tests.TestSupport;

namespace ClickDoc.Core.Tests;

public class TutorialTests
{
    static Tutorial With(params Step[] steps)
    {
        var t = new Tutorial();
        foreach (var s in steps) t.Append(s);
        return t;
    }

    static Step[] Numbered(int n) => Enumerable.Range(1, n).Select(i => MakeStep($"{i}")).ToArray();

    [Fact]
    public void AppendAndRemove()
    {
        Step a = MakeStep("A"), b = MakeStep("B"), c = MakeStep("C");
        var t = With(a, b, c);
        Assert.Equal(3, t.Steps.Count);
        t.Remove(new HashSet<Guid> { b.Id });
        Assert.Equal([a.Id, c.Id], t.Steps.Select(s => s.Id));
    }

    [Fact]
    public void MoveDownAndUp()
    {
        var t = With(MakeStep("A"), MakeStep("B"), MakeStep("C"));
        t.Move([0], 3);
        Assert.Equal(["B", "C", "A"], t.Steps.Select(s => s.Description));
        t.Move([2], 0);
        Assert.Equal(["A", "B", "C"], t.Steps.Select(s => s.Description));
    }

    [Fact]
    public void MoveSeveral()
    {
        var t = With(Numbered(5));
        t.Move([0, 2], 4);
        Assert.Equal(["2", "4", "1", "3", "5"], t.Steps.Select(s => s.Description));
    }

    [Fact]
    public void UpdateDescription()
    {
        Step a = MakeStep("A"), b = MakeStep("B"), c = MakeStep("C");
        var t = With(a, b, c);
        t.UpdateDescription(b.Id, "B2");
        Assert.Equal(["A", "B2", "C"], t.Steps.Select(s => s.Description));
    }

    [Fact]
    public void UpdateLastStepOnEmptyIsNoop()
    {
        using var _ = InLanguage(ContentLanguage.Chinese);
        var t = new Tutorial();
        t.UpdateLastStep(s => s with { Description = "x" });
        Assert.True(t.IsEmpty);
        Assert.Equal("未命名教程", t.Title);
    }

    [Fact]
    public void UpdateLastStepChangesOnlyTheLast()
    {
        var t = With(MakeStep("A"), MakeStep("B"));
        t.UpdateLastStep(s => s with { Description = "B2" });
        Assert.Equal(["A", "B2"], t.Steps.Select(s => s.Description));
    }

    [Fact]
    public void SetBlurRegionsAndAnnotations()
    {
        var a = MakeStep("A");
        var t = With(a);
        t.SetBlurRegions(a.Id, [new SKRect(0, 0, 0.5f, 0.5f)]);
        Assert.Single(t.Steps[0].BlurRegions);
        t.SetAnnotations(a.Id, []);
        Assert.Empty(t.Steps[0].Annotations);
    }

    [Fact]
    public void RemoveManyKeepsOrderOfTheRest()
    {
        var s = Numbered(5);
        var t = With(s);
        t.Remove(new HashSet<Guid> { s[1].Id, s[3].Id });
        Assert.Equal(["1", "3", "5"], t.Steps.Select(x => x.Description));
        t.Remove(new HashSet<Guid>());
        Assert.Equal(3, t.Steps.Count);
    }

    [Fact]
    public void DeletionTargetsFollowSelection()
    {
        Guid a = Guid.NewGuid(), b = Guid.NewGuid(), c = Guid.NewGuid();
        Assert.Equal(new HashSet<Guid> { a, b }, Selection.DeletionTargets(a, new HashSet<Guid> { a, b }));
        Assert.Equal(new HashSet<Guid> { c }, Selection.DeletionTargets(c, new HashSet<Guid> { a, b }));
    }

    [Fact]
    public void SelectionAfterDeletingPicksNextThenPrevious()
    {
        var s = Numbered(5);
        Assert.Equal(s[3].Id, Selection.AfterDeleting(new HashSet<Guid> { s[1].Id, s[2].Id }, s));
        Assert.Equal(s[2].Id, Selection.AfterDeleting(new HashSet<Guid> { s[3].Id, s[4].Id }, s));
        Assert.Null(Selection.AfterDeleting(s.Select(x => x.Id).ToHashSet(), s));
    }
}
