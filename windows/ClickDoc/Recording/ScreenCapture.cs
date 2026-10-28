using ClickDoc.Native;
using SkiaSharp;

namespace ClickDoc.Recording;

static unsafe class ScreenCapture
{
    /// The monitor under `p`: its bounds in physical pixels and its scale (DPI / 96).
    public static (nint Handle, SKRectI Bounds, float Scale) MonitorAt(SKPointI p)
    {
        nint monitor = Win32.MonitorFromPoint(new POINT { X = p.X, Y = p.Y }, Win32.MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { cbSize = (uint)sizeof(MONITORINFO) };
        Win32.GetMonitorInfo(monitor, ref info);
        var r = info.rcMonitor;
        float scale = Win32.GetDpiForMonitor(monitor, Win32.MDT_EFFECTIVE_DPI, out uint dpi, out _) == 0 ? dpi / 96f : 1f;
        return (monitor, new SKRectI(r.Left, r.Top, r.Right, r.Bottom), scale);
    }

    /// Screenshot of the whole monitor under `p`, mouse pointer included.
    public static (SKBitmap Image, SKRectI Monitor, float Scale) CaptureMonitorAt(SKPointI p)
    {
        var (_, bounds, scale) = MonitorAt(p);
        int w = bounds.Width, h = bounds.Height;
        nint screen = Win32.GetDC(0);
        nint memory = Win32.CreateCompatibleDC(screen);
        var header = new BITMAPINFOHEADER
        {
            biSize = (uint)sizeof(BITMAPINFOHEADER), biWidth = w, biHeight = -h,  // negative: top-down rows
            biPlanes = 1, biBitCount = 32,
        };
        nint dib = Win32.CreateDIBSection(screen, ref header, 0, out nint bits, 0, 0);
        if (dib == 0)
        {
            Win32.DeleteDC(memory);
            Win32.ReleaseDC(0, screen);
            throw new InvalidOperationException("CreateDIBSection failed");
        }
        nint previous = Win32.SelectObject(memory, dib);
        try
        {
            if (!Win32.BitBlt(memory, 0, 0, w, h, screen, bounds.Left, bounds.Top, Win32.SRCCOPY | Win32.CAPTUREBLT))
                throw new InvalidOperationException("BitBlt failed");
            DrawCursor(memory, bounds);
            Win32.GdiFlush();
            var bitmap = new SKBitmap(new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Opaque));
            byte* src = (byte*)bits, dst = (byte*)bitmap.GetPixels();
            long count = (long)w * h * 4;
            Buffer.MemoryCopy(src, dst, count, count);
            // GDI leaves the alpha byte at 0.
            for (long i = 3; i < count; i += 4) dst[i] = 255;
            return (bitmap, bounds, scale);
        }
        finally
        {
            Win32.SelectObject(memory, previous);
            Win32.DeleteObject(dib);
            Win32.DeleteDC(memory);
            Win32.ReleaseDC(0, screen);
        }
    }

    static void DrawCursor(nint hdc, SKRectI bounds)
    {
        var info = new CURSORINFO { cbSize = (uint)sizeof(CURSORINFO) };
        if (!Win32.GetCursorInfo(ref info) || (info.flags & Win32.CURSOR_SHOWING) == 0 || info.hCursor == 0) return;
        if (!Win32.GetIconInfo(info.hCursor, out var icon)) return;
        try
        {
            Win32.DrawIconEx(hdc, info.ptScreenPos.X - (int)icon.xHotspot - bounds.Left, info.ptScreenPos.Y - (int)icon.yHotspot - bounds.Top,
                             info.hCursor, 0, 0, 0, 0, Win32.DI_NORMAL);
        }
        finally
        {
            if (icon.hbmMask != 0) Win32.DeleteObject(icon.hbmMask);
            if (icon.hbmColor != 0) Win32.DeleteObject(icon.hbmColor);
        }
    }
}
