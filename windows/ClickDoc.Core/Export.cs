using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace ClickDoc.Core;

public sealed class ExportException(string message) : Exception(message);

public static partial class Export
{
    const int MaxNameLength = 100;

    public static string ImageFileName(int n) => $"step-{n:00}.png";

    public static string RenderMarkdown(Tutorial tutorial)
    {
        var lang = ContentLanguage.Current;
        var blocks = new List<string> { $"# {tutorial.Title}" };
        for (int i = 0; i < tutorial.Steps.Count; i++)
        {
            int n = i + 1;
            blocks.Add($"## {lang.StepHeading(n)}");
            var text = tutorial.Steps[i].Description.Trim();
            if (text.Length > 0) blocks.Add(text);
            blocks.Add($"![{lang.StepHeading(n)}](images/{ImageFileName(n)})");
        }
        return string.Join("\n\n", blocks) + "\n";
    }

    /// A file or folder name Windows (and macOS) accepts, derived from a tutorial title.
    public static string SanitizeName(string title)
    {
        var sb = new StringBuilder(title.Length);
        foreach (var c in title) sb.Append(char.IsControl(c) || "/:\\<>\"|?*".Contains(c) ? '-' : c);
        var name = sb.ToString().Trim().TrimStart('.');
        name = TrimEnd(name);
        if (name.Length > MaxNameLength) name = TrimEnd(name[..MaxNameLength]);
        name = name.Trim();
        if (name.Length == 0) return ContentLanguage.Current.UntitledTitle;
        // Reserved device names stay reserved with any extension, so mark the part before the first dot.
        int dot = name.IndexOf('.');
        var stem = dot < 0 ? name : name[..dot];
        return ReservedName().IsMatch(stem) ? stem + "_" + name[stem.Length..] : name;
    }

    static string TrimEnd(string s) => s.TrimEnd(' ', '.');

    [GeneratedRegex("^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$", RegexOptions.IgnoreCase)]
    private static partial Regex ReservedName();

    /// `<dir>/<name>`, or `<name> 2`, `<name> 3`… if taken.
    public static string UniqueFolder(string dir, string name, Func<string, bool> exists)
    {
        var candidate = Path.Combine(dir, name);
        for (int n = 2; exists(candidate); n++) candidate = Path.Combine(dir, $"{name} {n}");
        return candidate;
    }

    /// `<dir>/<name>.<ext>`, or `<name> 2.<ext>`, `<name> 3.<ext>`… if taken.
    public static string UniqueFile(string dir, string name, string ext, Func<string, bool> exists)
    {
        var candidate = Path.Combine(dir, $"{name}.{ext}");
        for (int n = 2; exists(candidate); n++) candidate = Path.Combine(dir, $"{name} {n}.{ext}");
        return candidate;
    }

    static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

    /// The PNG bytes written on export: the screenshot with its blur and annotations burned in.
    public static byte[] ImageData(Step step)
    {
        if (step.BlurRegions.Count == 0 && step.Annotations.Count == 0) return step.Png;
        using var source = Imaging.DecodePng(step.Png);
        var composed = Annotations.Compose(source, step);
        try { return Imaging.EncodePng(composed); }
        finally { if (!ReferenceEquals(composed, source)) composed.Dispose(); }
    }

    /// Writes `<dir>/<title>/tutorial.md` and `images/step-NN.png`; returns the new folder.
    public static string ExportTutorial(Tutorial tutorial, string dir)
    {
        var folder = UniqueFolder(dir, SanitizeName(tutorial.Title), Exists);
        var images = Path.Combine(folder, "images");
        Directory.CreateDirectory(images);
        for (int i = 0; i < tutorial.Steps.Count; i++)
            File.WriteAllBytes(Path.Combine(images, ImageFileName(i + 1)), ImageData(tutorial.Steps[i]));
        File.WriteAllText(Path.Combine(folder, "tutorial.md"), RenderMarkdown(tutorial), new UTF8Encoding(false));
        return folder;
    }

    /// A zip for Notion's "Import > Markdown": `<title>.md` and `images/` at the zip root.
    public static string ExportNotionZip(Tutorial tutorial, string dir)
    {
        var name = SanitizeName(tutorial.Title);
        var path = UniqueFile(dir, name, "zip", Exists);
        var entries = new List<(string Path, byte[] Data)> { ($"{name}.md", new UTF8Encoding(false).GetBytes(RenderMarkdown(tutorial))) };
        for (int i = 0; i < tutorial.Steps.Count; i++)
            entries.Add(($"images/{ImageFileName(i + 1)}", ImageData(tutorial.Steps[i])));
        var temp = path + ".partial";
        try
        {
            using (var archive = ZipFile.Open(temp, ZipArchiveMode.Create, Encoding.UTF8))
            {
                foreach (var (entryPath, data) in entries)
                {
                    using var stream = archive.CreateEntry(entryPath, CompressionLevel.NoCompression).Open();
                    stream.Write(data);
                }
            }
            File.Move(temp, path);
        }
        finally
        {
            File.Delete(temp);
        }
        return path;
    }
}
