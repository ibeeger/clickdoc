namespace ClickDoc;

/// A small diagnostic log at %LocalAppData%\ClickDoc\clickdoc.log, for problems only a user's machine shows.
static class Log
{
    const long MaxBytes = 1 << 20;
    static readonly object Lock = new();

    static string FilePath => Path.Combine(AppPaths.Root, "clickdoc.log");

    /// Starts a fresh log once the old one passes 1 MB.
    public static void Trim()
    {
        try
        {
            if (new FileInfo(FilePath) is { Exists: true, Length: > MaxBytes } file) file.Delete();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    public static void Write(string message)
    {
        try
        {
            lock (Lock)
            {
                Directory.CreateDirectory(AppPaths.Root);
                File.AppendAllText(FilePath, $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff} [{Environment.ProcessId}] {message}{Environment.NewLine}");
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { /* logging must never break the app */ }
    }
}
