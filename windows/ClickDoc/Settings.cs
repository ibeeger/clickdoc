using System.Text.Json;

namespace ClickDoc;

public enum CaptureScope { Window, Screen }

/// Preferences kept in %LocalAppData%\ClickDoc\settings.json.
sealed class Settings
{
    public CaptureScope Scope { get; set; } = CaptureScope.Window;

    /// Whether the user has been told that closing the editor keeps ClickDoc in the tray.
    public bool TrayHintShown { get; set; }

    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    static string FilePath => Path.Combine(AppPaths.Root, "settings.json");

    public static Settings Load()
    {
        try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), Json) ?? new Settings(); }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { return new Settings(); }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.Root);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { /* a lost preference is harmless */ }
    }
}

static class AppPaths
{
    /// %LocalAppData%\ClickDoc: not roaming, since screenshots are large.
    public static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClickDoc");

    public static string Draft => Path.Combine(Root, "draft");
}
