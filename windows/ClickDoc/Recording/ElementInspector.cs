using System.Collections.Concurrent;
using System.Diagnostics;
using System.Windows.Automation;
using ClickDoc.Native;
using SkiaSharp;

namespace ClickDoc.Recording;

static unsafe class ElementInspector
{
    static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(250);
    static readonly ConcurrentDictionary<string, string?> Descriptions = new(StringComparer.OrdinalIgnoreCase);

    /// App and element names for a click at `p` on top-level window `root`. The app name never
    /// waits on UI Automation; the element name is dropped if the clicked app does not answer in time.
    public static (string? AppName, string? ElementName) Inspect(SKPointI p, nint root) =>
        (AppName(root), Naming.RunWithTimeout(() => ElementName(p), Timeout));

    static string? ElementName(SKPointI p)
    {
        var element = AutomationElement.FromPoint(new System.Windows.Point(p.X, p.Y));
        if (element is null) return null;
        var parent = TreeWalker.ControlViewWalker.GetParent(element);
        return Naming.PickElementName(Fields(element), parent is null ? null : Fields(parent));
    }

    static UiaFields Fields(AutomationElement e)
    {
        var c = e.Current;
        return new UiaFields(c.ControlType?.ProgrammaticName, c.Name, c.HelpText, c.LocalizedControlType);
    }

    static string? AppName(nint root)
    {
        if (root == 0) return null;
        var path = ExePath((uint)Win32.ProcessId(root));
        var description = path is null ? null : Descriptions.GetOrAdd(path, FileDescription);
        return Naming.AppName(description, path, Win32.WindowText(root));
    }

    static string? FileDescription(string path)
    {
        try { return FileVersionInfo.GetVersionInfo(path).FileDescription; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }

    static string? ExePath(uint pid)
    {
        nint process = Win32.OpenProcess(Win32.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (process == 0) return null;
        try
        {
            char* buffer = stackalloc char[1024];
            uint size = 1024;
            return Win32.QueryFullProcessImageName(process, 0, buffer, ref size) ? new string(buffer, 0, (int)size) : null;
        }
        finally
        {
            Win32.CloseHandle(process);
        }
    }
}
