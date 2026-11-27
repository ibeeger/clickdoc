using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ClickDoc.Native;
using SkiaSharp;

namespace ClickDoc.Recording;

static unsafe class WindowList
{
    /// Taskbars, desktop, Start, notification center and the tray overflow.
    static readonly HashSet<string> ShellClasses =
    [
        "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Progman", "WorkerW", "NotifyIconOverflowWindow",
        "TopLevelWindowForOverflowXamlIsland", "Windows.UI.Core.CoreWindow",
    ];

    [ThreadStatic] static List<nint>? _found;

    /// Visible top-level windows, front to back.
    public static IReadOnlyList<WindowSnapshot> Snapshot()
    {
        _found = [];
        Win32.EnumWindows(&Collect, 0);
        var result = new List<WindowSnapshot>(_found.Count);
        foreach (var hwnd in _found)
            if (Describe(hwnd) is { } w) result.Add(w);
        _found = null;
        return result;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    static int Collect(nint hwnd, nint lParam)
    {
        _found?.Add(hwnd);
        return 1;
    }

    /// The top-level window a click at `p` lands on.
    public static nint HitWindow(SKPointI p)
    {
        nint hwnd = Win32.WindowFromPoint(new POINT { X = p.X, Y = p.Y });
        return hwnd == 0 ? 0 : Win32.GetAncestor(hwnd, Win32.GA_ROOT);
    }

    public static bool IsShell(nint hwnd) => hwnd != 0 && ShellClasses.Contains(Win32.ClassName(hwnd));

    static WindowSnapshot? Describe(nint hwnd)
    {
        if (!Win32.IsWindowVisible(hwnd)) return null;
        int cloaked = 0;
        if (Win32.DwmGetWindowAttribute(hwnd, Win32.DWMWA_CLOAKED, &cloaked, sizeof(int)) == 0 && cloaked != 0) return null;
        long style = Win32.GetWindowLongPtr(hwnd, Win32.GWL_STYLE);
        long exStyle = Win32.GetWindowLongPtr(hwnd, Win32.GWL_EXSTYLE);
        if ((exStyle & Win32.WS_EX_TRANSPARENT) != 0) return null;  // click-through overlay
        if ((exStyle & Win32.WS_EX_LAYERED) != 0
            && Win32.GetLayeredWindowAttributes(hwnd, out _, out byte alpha, out uint flags)
            && (flags & Win32.LWA_ALPHA) != 0 && alpha == 0) return null;
        if (Bounds(hwnd) is not { } bounds || bounds.Width <= 0 || bounds.Height <= 0) return null;
        var className = Win32.ClassName(hwnd);
        // Menus and popovers: Win32 menus, or captionless popups that belong to another window
        // or are tool windows. Frameless main windows (Electron and the like) stay ordinary.
        bool popup = className == "#32768"
                     || ((style & Win32.WS_POPUP) != 0 && (style & Win32.WS_CAPTION) == 0
                         && (Win32.GetWindow(hwnd, Win32.GW_OWNER) != 0 || (exStyle & Win32.WS_EX_TOOLWINDOW) != 0));
        return new WindowSnapshot(hwnd, Win32.ProcessId(hwnd), bounds, popup, ShellClasses.Contains(className));
    }

    /// The visible frame, without the invisible resize borders Windows 10/11 add around windows.
    static SKRectI? Bounds(nint hwnd)
    {
        RECT r;
        if (Win32.DwmGetWindowAttribute(hwnd, Win32.DWMWA_EXTENDED_FRAME_BOUNDS, &r, (uint)sizeof(RECT)) != 0
            && !Win32.GetWindowRect(hwnd, out r)) return null;
        return new SKRectI(r.Left, r.Top, r.Right, r.Bottom);
    }
}
