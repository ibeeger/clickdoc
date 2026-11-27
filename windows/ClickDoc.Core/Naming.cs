namespace ClickDoc.Core;

/// The UI Automation properties of one element that matter for naming it.
public sealed record UiaFields(string? ControlType, string? Name, string? HelpText, string? LocalizedControlType);

public static class Naming
{
    const int MaxElementNameLength = 40;

    static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    public static string DescribeStep(ClickAction action, string? appName, string? elementName)
    {
        var element = Clean(elementName);
        if (element is { Length: > MaxElementNameLength }) element = element[..MaxElementNameLength] + "…";
        return ContentLanguage.Current.Describe(action, Clean(appName), element);
    }

    /// Picks a human-readable name for a clicked element. Text boxes keep their content in the
    /// Value pattern, which is never read, so typed text cannot leak into a description.
    public static string? PickElementName(UiaFields element, UiaFields? parent) =>
        Clean(element.Name) ?? Clean(element.HelpText) ?? Clean(parent?.Name) ?? Clean(element.LocalizedControlType);

    /// Store apps all run inside ApplicationFrameHost.exe; their window title names the app.
    public static string? AppName(string? fileDescription, string? exePath, string? windowTitle)
    {
        var file = string.IsNullOrEmpty(exePath) ? null : Path.GetFileName(exePath.Replace('\\', '/'));
        if (string.Equals(file, "ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase)) return Clean(windowTitle);
        return Clean(fileDescription) ?? Clean(file is null ? null : Path.GetFileNameWithoutExtension(file));
    }

    /// Runs `work` on its own thread (never the pool, which a hung call would starve) and gives
    /// up after `timeout`, returning default.
    public static T? RunWithTimeout<T>(Func<T> work, TimeSpan timeout)
    {
        T? result = default;
        var thread = new Thread(() =>
        {
            try { result = work(); }
            catch { /* treated like a timeout */ }
        }) { IsBackground = true };
        thread.Start();
        return thread.Join(timeout) ? result : default;
    }
}
