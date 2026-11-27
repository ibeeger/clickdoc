using System.Text.RegularExpressions;

namespace ClickDoc.Core.Tests;

public partial class LocalizationTests
{
    /// Guards against the scan silently finding nothing.
    const int MinimumKeys = 40;

    [GeneratedRegex("""\bL\("((?:[^"\\]|\\.)*)"[,)]""")]
    private static partial Regex CodeKey();

    [GeneratedRegex(@"\{l:L ([^}]+)\}")]
    private static partial Regex XamlKey();

    [GeneratedRegex("""(?<!L\()"(?:[^"\\]|\\.)*[一-龥](?:[^"\\]|\\.)*"(?!\s*=)""")]
    private static partial Regex BareChinese();

    static IEnumerable<string> Sources(string pattern) =>
        Directory.Exists(RepoPaths.AppSources)
            ? Directory.EnumerateFiles(RepoPaths.AppSources, pattern, SearchOption.AllDirectories)
                .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                         && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            : [];

    static HashSet<string> AppKeys()
    {
        var keys = new HashSet<string>();
        foreach (var file in Sources("*.cs"))
            foreach (Match m in CodeKey().Matches(File.ReadAllText(file))) keys.Add(Regex.Unescape(m.Groups[1].Value));
        foreach (var file in Sources("*.xaml"))
            foreach (Match m in XamlKey().Matches(File.ReadAllText(file))) keys.Add(m.Groups[1].Value);
        return keys;
    }

    static Dictionary<string, string> English()
    {
        var all = new Dictionary<string, string>(StringsFile.Parse(File.ReadAllText(RepoPaths.MacEnglishStrings)));
        if (File.Exists(RepoPaths.WindowsStrings))
            foreach (var (k, v) in StringsFile.Parse(File.ReadAllText(RepoPaths.WindowsStrings))) all[k] = v;
        return all;
    }

    [Fact]
    public void EveryUIStringHasEnglish()
    {
        var keys = AppKeys();
        Assert.True(keys.Count >= MinimumKeys, $"found only {keys.Count} L() keys");
        var missing = keys.Except(English().Keys).Order().ToList();
        Assert.True(missing.Count == 0, "missing English for: " + string.Join(" | ", missing));
    }

    [Fact]
    public void EnglishKeepsPlaceholders()
    {
        foreach (var (key, value) in English())
            foreach (var spec in new[] { "%lld", "%@" })
                Assert.True(key.Split(spec).Length == value.Split(spec).Length, $"{key} → {value}");
    }

    [Fact]
    public void NoUnwrappedChineseInApp()
    {
        var offenders = new List<string>();
        foreach (var file in Sources("*.cs"))
        {
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                var code = lines[i].Split("//")[0];
                if (BareChinese().IsMatch(code)) offenders.Add($"{Path.GetFileName(file)}:{i + 1}");
            }
        }
        Assert.True(offenders.Count == 0, "Chinese outside L(): " + string.Join(", ", offenders));
    }
}
