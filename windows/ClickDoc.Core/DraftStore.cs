using System.Text.Json;
using SkiaSharp;

namespace ClickDoc.Core;

/// Persists the single current tutorial so it survives quitting the app.
/// Layout: `<directory>/manifest.json` and `<directory>/images/<step id>.png` (unblurred originals).
public sealed class DraftStore(string directory)
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string Directory { get; } = directory;

    string ManifestPath => Path.Combine(Directory, "manifest.json");
    string ImagesPath => Path.Combine(Directory, "images");
    string ImagePath(Guid id) => Path.Combine(ImagesPath, $"{id}.png");

    sealed record Manifest(int Version, string Title, bool HasUnexportedChanges, List<StepRecord> Steps);

    sealed record StepRecord(Guid Id, string Description, DateTimeOffset CapturedAt, List<float[]> BlurRegions, List<MarkRecord>? Annotations);

    /// One annotation; `Type` is "rect", "arrow" or "text" and decides which fields are set. Points are [x, y], rects [x, y, w, h].
    sealed record MarkRecord(Guid Id, string Type, float[]? Rect = null, float[]? From = null, float[]? To = null,
                             float[]? Origin = null, string? Text = null);

    public void Save(Tutorial tutorial, bool hasUnexportedChanges)
    {
        System.IO.Directory.CreateDirectory(ImagesPath);
        // Screenshots never change after capture, so each is written once.
        foreach (var step in tutorial.Steps)
            if (!File.Exists(ImagePath(step.Id))) WriteAtomically(ImagePath(step.Id), step.Png);
        var manifest = new Manifest(1, tutorial.Title, hasUnexportedChanges, tutorial.Steps.Select(ToRecord).ToList());
        // Manifest last: a crash mid-save leaves the previous manifest pointing at existing images.
        WriteAtomically(ManifestPath, JsonSerializer.SerializeToUtf8Bytes(manifest, Json));
        var keep = tutorial.Steps.Select(s => $"{s.Id}.png").ToHashSet();
        foreach (var file in System.IO.Directory.GetFiles(ImagesPath))
            if (!keep.Contains(Path.GetFileName(file))) TryDelete(file);
    }

    /// Null when there is no draft. Throws JsonException on an unreadable manifest so the caller can keep the files.
    public (Tutorial Tutorial, bool HasUnexportedChanges)? Load()
    {
        if (!File.Exists(ManifestPath)) return null;
        var manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllBytes(ManifestPath), Json)
                       ?? throw new JsonException("empty manifest");
        var tutorial = new Tutorial { Title = manifest.Title ?? "" };
        foreach (var record in manifest.Steps ?? [])
        {
            if (!File.Exists(ImagePath(record.Id))) continue;
            tutorial.Append(new Step(record.Id, record.Description ?? "", File.ReadAllBytes(ImagePath(record.Id)), record.CapturedAt)
            {
                BlurRegions = (record.BlurRegions ?? []).Select(ToRect).ToList(),
                Annotations = (record.Annotations ?? []).Select(ToAnnotation).ToList(),
            });
        }
        return (tutorial, manifest.HasUnexportedChanges);
    }

    public void Clear()
    {
        if (System.IO.Directory.Exists(Directory)) System.IO.Directory.Delete(Directory, recursive: true);
    }

    /// Moves the whole draft to a sibling `draft-unreadable-<unix seconds>` folder and returns its path.
    public string MoveAside(DateTimeOffset now)
    {
        var aside = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(Directory))!, $"draft-unreadable-{now.ToUnixTimeSeconds()}");
        System.IO.Directory.Move(Directory, aside);
        return aside;
    }

    static StepRecord ToRecord(Step s) =>
        new(s.Id, s.Description, s.CapturedAt, s.BlurRegions.Select(FromRect).ToList(), s.Annotations.Select(ToRecord).ToList());

    static MarkRecord ToRecord(Annotation a) => a.Mark switch
    {
        RectangleMark r => new(a.Id, "rect", Rect: FromRect(r.Rect)),
        ArrowMark ar => new(a.Id, "arrow", From: FromPoint(ar.From), To: FromPoint(ar.To)),
        TextMark t => new(a.Id, "text", Origin: FromPoint(t.Origin), Text: t.Text),
        _ => throw new InvalidOperationException($"unknown mark {a.Mark}"),
    };

    static Annotation ToAnnotation(MarkRecord r) => new(r.Id, r.Type switch
    {
        "rect" => new RectangleMark(ToRect(Required(r.Rect))),
        "arrow" => new ArrowMark(ToPoint(Required(r.From)), ToPoint(Required(r.To))),
        "text" => new TextMark(ToPoint(Required(r.Origin)), r.Text ?? ""),
        _ => throw new JsonException($"unknown annotation type {r.Type}"),
    });

    static float[] Required(float[]? v) => v ?? throw new JsonException("missing annotation field");

    static float[] FromRect(SKRect r) => [r.Left, r.Top, r.Width, r.Height];

    static SKRect ToRect(float[] v) => v.Length == 4 ? SKRect.Create(v[0], v[1], v[2], v[3]) : throw new JsonException("bad rect");

    static float[] FromPoint(SKPoint p) => [p.X, p.Y];

    static SKPoint ToPoint(float[] v) => v.Length == 2 ? new SKPoint(v[0], v[1]) : throw new JsonException("bad point");

    static void WriteAtomically(string path, byte[] bytes)
    {
        var temp = path + ".tmp";
        File.WriteAllBytes(temp, bytes);
        File.Move(temp, path, overwrite: true);
    }

    static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (IOException) { /* retried on the next save */ }
    }
}
