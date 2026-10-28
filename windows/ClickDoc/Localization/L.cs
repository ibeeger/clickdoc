using System.Reflection;

namespace ClickDoc.Localization;

/// Every user-facing string goes through `L`. Keys are the Chinese text; the English table is
/// the Mac app's Localizable.strings plus windows.strings, checked by LocalizationTests.
public static class Strings
{
    static readonly IReadOnlyDictionary<string, string>? English =
        ContentLanguage.Current == ContentLanguage.English ? LoadEnglish() : null;

    public static string L(string zh, params object[] args)
    {
        var template = English?.GetValueOrDefault(zh) ?? zh;
        return args.Length == 0 ? template : Localizer.Format(template, args);
    }

    static Dictionary<string, string> LoadEnglish()
    {
        var table = new Dictionary<string, string>();
        foreach (var name in new[] { "en.strings", "windows.strings" })
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
            if (stream is null) continue;
            using var reader = new StreamReader(stream);
            foreach (var (k, v) in StringsFile.Parse(reader.ReadToEnd())) table[k] = v;
        }
        return table;
    }
}
