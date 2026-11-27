using System.Runtime.InteropServices;

namespace ClickDoc.Native;

[StructLayout(LayoutKind.Sequential)]
struct MSLLHOOKSTRUCT
{
    public POINT pt;
    public uint mouseData, flags, time;
    public nuint dwExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
struct MSG
{
    public nint hwnd;
    public uint message;
    public nint wParam, lParam;
    public uint time;
    public POINT pt;
    public uint lPrivate;
}

[StructLayout(LayoutKind.Sequential)]
struct MONITORINFO
{
    public uint cbSize;
    public RECT rcMonitor, rcWork;
    public uint dwFlags;
}

[StructLayout(LayoutKind.Sequential)]
struct BITMAPINFOHEADER
{
    public uint biSize;
    public int biWidth, biHeight;
    public ushort biPlanes, biBitCount;
    public uint biCompression, biSizeImage;
    public int biXPelsPerMeter, biYPelsPerMeter;
    public uint biClrUsed, biClrImportant;
}

[StructLayout(LayoutKind.Sequential)]
struct CURSORINFO
{
    public uint cbSize, flags;
    public nint hCursor;
    public POINT ptScreenPos;
}

[StructLayout(LayoutKind.Sequential)]
struct ICONINFO
{
    public int fIcon;
    public uint xHotspot, yHotspot;
    public nint hbmMask, hbmColor;
}

static unsafe partial class Win32
{
    public const int WH_MOUSE_LL = 14;
    public const int WM_QUIT = 0x0012, WM_LBUTTONDOWN = 0x0201, WM_RBUTTONDOWN = 0x0204;
    public const int SM_CXDOUBLECLK = 36, SM_CYDOUBLECLK = 37;
    public const int GWL_STYLE = -16, GWL_EXSTYLE = -20;
    public const long WS_POPUP = 0x80000000L, WS_CAPTION = 0x00C00000L;
    public const long WS_EX_TOOLWINDOW = 0x80, WS_EX_LAYERED = 0x80000, WS_EX_TRANSPARENT = 0x20;
    public const uint LWA_ALPHA = 0x2;
    public const uint GA_ROOT = 2, GW_OWNER = 4;
    public const uint DWMWA_EXTENDED_FRAME_BOUNDS = 9, DWMWA_CLOAKED = 14;
    public const uint MONITOR_DEFAULTTONEAREST = 2;
    public const int MDT_EFFECTIVE_DPI = 0;
    public const uint SRCCOPY = 0x00CC0020, CAPTUREBLT = 0x40000000;
    public const uint CURSOR_SHOWING = 1, DI_NORMAL = 3;
    public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    public const uint WDA_EXCLUDEFROMCAPTURE = 0x11;

    [LibraryImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    public static partial nint SetWindowsHookEx(int idHook, delegate* unmanaged[Stdcall]<int, nint, nint, nint> proc, nint hMod, uint threadId);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnhookWindowsHookEx(nint hook);

    [LibraryImport("user32.dll")]
    public static partial nint CallNextHookEx(nint hook, int nCode, nint wParam, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "GetMessageW")]
    public static partial int GetMessage(out MSG msg, nint hWnd, uint min, uint max);

    [LibraryImport("user32.dll", EntryPoint = "PostThreadMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool PostThreadMessage(uint threadId, int msg, nint wParam, nint lParam);

    [LibraryImport("kernel32.dll")]
    public static partial uint GetCurrentThreadId();

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint GetModuleHandle(string? name);

    [LibraryImport("user32.dll")]
    public static partial uint GetDoubleClickTime();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool EnumWindows(delegate* unmanaged[Stdcall]<nint, nint, int> proc, nint lParam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsWindowVisible(nint hWnd);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    public static partial nint GetWindowLongPtr(nint hWnd, int index);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetLayeredWindowAttributes(nint hWnd, out uint key, out byte alpha, out uint flags);

    [LibraryImport("user32.dll", EntryPoint = "GetClassNameW")]
    public static partial int GetClassName(nint hWnd, char* buffer, int maxCount);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowTextW")]
    public static partial int GetWindowText(nint hWnd, char* buffer, int maxCount);

    [LibraryImport("user32.dll")]
    public static partial uint GetWindowThreadProcessId(nint hWnd, out uint processId);

    [LibraryImport("user32.dll")]
    public static partial nint GetWindow(nint hWnd, uint cmd);

    [LibraryImport("user32.dll")]
    public static partial nint GetAncestor(nint hWnd, uint flags);

    [LibraryImport("user32.dll")]
    public static partial nint WindowFromPoint(POINT point);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetWindowRect(nint hWnd, out RECT rect);

    [LibraryImport("dwmapi.dll")]
    public static partial int DwmGetWindowAttribute(nint hWnd, uint attribute, void* value, uint size);

    [LibraryImport("user32.dll")]
    public static partial nint MonitorFromPoint(POINT point, uint flags);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetMonitorInfo(nint monitor, ref MONITORINFO info);

    [LibraryImport("shcore.dll")]
    public static partial int GetDpiForMonitor(nint monitor, int type, out uint dpiX, out uint dpiY);

    [LibraryImport("user32.dll")]
    public static partial nint GetDC(nint hWnd);

    [LibraryImport("user32.dll")]
    public static partial int ReleaseDC(nint hWnd, nint hdc);

    [LibraryImport("gdi32.dll")]
    public static partial nint CreateCompatibleDC(nint hdc);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteDC(nint hdc);

    [LibraryImport("gdi32.dll")]
    public static partial nint CreateDIBSection(nint hdc, ref BITMAPINFOHEADER info, uint usage, out nint bits, nint section, uint offset);

    [LibraryImport("gdi32.dll")]
    public static partial nint SelectObject(nint hdc, nint obj);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteObject(nint obj);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool BitBlt(nint dest, int x, int y, int w, int h, nint src, int srcX, int srcY, uint rop);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GdiFlush();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetCursorInfo(ref CURSORINFO info);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetIconInfo(nint icon, out ICONINFO info);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DrawIconEx(nint hdc, int x, int y, nint icon, int w, int h, uint step, nint brush, uint flags);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial nint OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint processId);

    [LibraryImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool QueryFullProcessImageName(nint process, uint flags, char* buffer, ref uint size);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CloseHandle(nint handle);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowDisplayAffinity(nint hWnd, uint affinity);

    public static string ClassName(nint hWnd)
    {
        char* buffer = stackalloc char[256];
        int n = GetClassName(hWnd, buffer, 256);
        return new string(buffer, 0, Math.Max(0, n));
    }

    public static string WindowText(nint hWnd)
    {
        char* buffer = stackalloc char[512];
        int n = GetWindowText(hWnd, buffer, 512);
        return new string(buffer, 0, Math.Max(0, n));
    }

    public static int ProcessId(nint hWnd)
    {
        GetWindowThreadProcessId(hWnd, out uint pid);
        return (int)pid;
    }
}
