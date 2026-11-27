using static ClickDoc.Core.Tests.TestSupport;

namespace ClickDoc.Core.Tests;

public sealed class DraftStoreTests : IDisposable
{
    readonly string _root = Directory.CreateTempSubdirectory("clickdoc-draft-").FullName;
    string Dir => Path.Combine(_root, "draft");
    DraftStore Store => new(Dir);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    string[] ImageFiles() =>
        Directory.Exists(Path.Combine(Dir, "images"))
            ? Directory.GetFiles(Path.Combine(Dir, "images")).Select(f => Path.GetFileName(f)).Order().ToArray()
            : [];

    static Tutorial Make(string title, params Step[] steps)
    {
        var t = new Tutorial { Title = title };
        foreach (var s in steps) t.Append(s);
        return t;
    }

    static void AssertSame(Tutorial expected, Tutorial actual)
    {
        Assert.Equal(expected.Title, actual.Title);
        Assert.Equal(expected.Steps.Count, actual.Steps.Count);
        foreach (var (e, a) in expected.Steps.Zip(actual.Steps))
        {
            Assert.Equal(e.Id, a.Id);
            Assert.Equal(e.Description, a.Description);
            Assert.Equal(e.Png, a.Png);
            Assert.Equal(e.CapturedAt, a.CapturedAt);
            Assert.Equal(e.BlurRegions, a.BlurRegions);
            Assert.Equal(e.Annotations, a.Annotations);
        }
    }

    [Fact]
    public void RoundTrip()
    {
        var a = new Step(Guid.NewGuid(), "第一步", [1, 2, 3], DateTimeOffset.FromUnixTimeSeconds(1000))
        {
            BlurRegions = [SKRect.Create(0.1f, 0.2f, 0.3f, 0.4f)],
            Annotations =
            [
                new(Guid.NewGuid(), new RectangleMark(SKRect.Create(0, 0, 0.5f, 0.5f))),
                new(Guid.NewGuid(), new ArrowMark(new(0, 0), new(1, 1))),
                new(Guid.NewGuid(), new TextMark(new(0.2f, 0.3f), "提示")),
            ],
        };
        var b = new Step(Guid.NewGuid(), "第二步", [4, 5], DateTimeOffset.FromUnixTimeSeconds(2000));
        var t = Make("草稿", a, b);
        Store.Save(t, hasUnexportedChanges: true);
        var loaded = Store.Load();
        Assert.NotNull(loaded);
        AssertSame(t, loaded.Value.Tutorial);
        Assert.True(loaded.Value.HasUnexportedChanges);
    }

    [Fact]
    public void LoadEmptyIsNull() => Assert.Null(Store.Load());

    [Fact]
    public void RemovesImagesOfDeletedSteps()
    {
        Step a = MakeStep("a"), b = MakeStep("b");
        var t = Make("T", a, b);
        Store.Save(t, true);
        Assert.Equal(2, ImageFiles().Length);
        t.Remove(new HashSet<Guid> { a.Id });
        Store.Save(t, false);
        Assert.Equal([$"{b.Id}.png"], ImageFiles());
        Assert.False(Store.Load()!.Value.HasUnexportedChanges);
    }

    [Fact]
    public void SkipsStepWithMissingImage()
    {
        Step a = MakeStep("a"), b = MakeStep("b");
        Store.Save(Make("T", a, b), true);
        File.Delete(Path.Combine(Dir, "images", $"{a.Id}.png"));
        Assert.Equal([b.Id], Store.Load()!.Value.Tutorial.Steps.Select(s => s.Id));
    }

    [Fact]
    public void CorruptManifestThrows()
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(Path.Combine(Dir, "manifest.json"), "not json");
        Assert.ThrowsAny<System.Text.Json.JsonException>(() => Store.Load());
    }

    [Fact]
    public void SaveLeavesNoTempFiles()
    {
        Store.Save(Make("T", MakeStep("a")), true);
        Store.Save(Make("T2", MakeStep("a")), true);
        Assert.Equal(["images", "manifest.json"], Directory.GetFileSystemEntries(Dir).Select(Path.GetFileName).Order());
        Assert.Equal("T2", Store.Load()!.Value.Tutorial.Title);
    }

    [Fact]
    public void Clear()
    {
        Store.Save(Make("T", MakeStep("a")), true);
        Store.Clear();
        Assert.Null(Store.Load());
        Assert.Empty(ImageFiles());
        Store.Clear();  // clearing nothing is fine
    }

    [Fact]
    public void MoveAside_KeepsFilesAndReturnsPath()
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(Path.Combine(Dir, "manifest.json"), "not json");
        var aside = Store.MoveAside(DateTimeOffset.FromUnixTimeSeconds(1700000000));
        Assert.Equal(Path.Combine(_root, "draft-unreadable-1700000000"), aside);
        Assert.Equal("not json", File.ReadAllText(Path.Combine(aside, "manifest.json")));
        Assert.False(Directory.Exists(Dir));
        Assert.Null(Store.Load());
    }
}
