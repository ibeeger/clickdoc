using System.Globalization;
using System.Text;

namespace ClickDoc.Core;

/// Reads Apple `.strings` tables: `"key" = "value";` lines with C-style comments.
public static class StringsFile
{
    public static IReadOnlyDictionary<string, string> Parse(string content)
    {
        var table = new Dictionary<string, string>();
        int i = 0;
        string? key = null;
        while (i < content.Length)
        {
            char c = content[i];
            if (c == '/' && i + 1 < content.Length && content[i + 1] == '*')
            {
                int end = content.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = end < 0 ? content.Length : end + 2;
            }
            else if (c == '/' && i + 1 < content.Length && content[i + 1] == '/')
            {
                int end = content.IndexOf('\n', i);
                i = end < 0 ? content.Length : end + 1;
            }
            else if (c == '"')
            {
                var s = ReadQuoted(content, ref i);
                if (key is null) key = s;
                else
                {
                    table[key] = s;
                    key = null;
                }
            }
            else i++;
        }
        return table;
    }

    static string ReadQuoted(string content, ref int i)
    {
        var sb = new StringBuilder();
        i++;  // opening quote
        while (i < content.Length && content[i] != '"')
        {
            if (content[i] == '\\' && i + 1 < content.Length)
            {
                i++;
                sb.Append(content[i] switch { 'n' => '\n', 't' => '\t', 'r' => '\r', var other => other });
            }
            else sb.Append(content[i]);
            i++;
        }
        i++;  // closing quote
        return sb.ToString();
    }
}

public static class Localizer
{
    /// Replaces `%lld` and `%@` placeholders, in order, with `args`.
    public static string Format(string template, params object[] args)
    {
        var sb = new StringBuilder();
        int next = 0, i = 0;
        while (i < template.Length)
        {
            if (template.AsSpan(i).StartsWith("%lld") && next < args.Length)
            {
                sb.Append(Convert.ToString(args[next++], CultureInfo.CurrentCulture));
                i += 4;
            }
            else if (template.AsSpan(i).StartsWith("%@") && next < args.Length)
            {
                sb.Append(Convert.ToString(args[next++], CultureInfo.CurrentCulture));
                i += 2;
            }
            else sb.Append(template[i++]);
        }
        return sb.ToString();
    }
}
