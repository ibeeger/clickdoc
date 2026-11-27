using System.Runtime.CompilerServices;

namespace ClickDoc.Core.Tests;

static class RepoPaths
{
    static string ThisFile([CallerFilePath] string path = "") => path;

    /// The repository root, found from this source file's location.
    public static string Root => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ThisFile())!, "..", ".."));

    public static string MacEnglishStrings => Path.Combine(Root, "Localization", "en.lproj", "Localizable.strings");

    public static string WindowsStrings => Path.Combine(Root, "windows", "ClickDoc", "Localization", "windows.strings");

    public static string AppSources => Path.Combine(Root, "windows", "ClickDoc");
}
