using SkiaSharp;

namespace ClickDoc.Core;

// Screen coordinates: physical pixels of the Windows virtual screen (the process is
// per-monitor DPI aware), origin at the primary monitor's top-left, y down.
// Image coordinates: pixels with a top-left origin. Normalized: 0...1 of the image.

/// One visible top-level window, listed front to back.
/// `IsPopup`: a menu, popover or other ownerless popup. `IsShell`: taskbar, desktop, Start, tray overflow.
public sealed record WindowSnapshot(nint Handle, int Pid, SKRectI Bounds, bool IsPopup, bool IsShell);

public static class Geometry
{
    /// Normalizes a rect in an image/view of `size` (top-left origin), clamped to the bounds.
    public static SKRect Normalize(SKRect rect, SKSize size)
    {
        var clipped = SKRect.Intersect(rect.Standardized, SKRect.Create(size));
        if (clipped.IsEmpty || size.Width <= 0 || size.Height <= 0) return SKRect.Empty;
        return SKRect.Create(clipped.Left / size.Width, clipped.Top / size.Height,
                             clipped.Width / size.Width, clipped.Height / size.Height);
    }

    public static SKRect Denormalize(SKRect rect, SKSize size) =>
        SKRect.Create(rect.Left * size.Width, rect.Top * size.Height, rect.Width * size.Width, rect.Height * size.Height);

    /// The click highlight's bounding box in image pixels. `imageOrigin` is where the image's
    /// top-left pixel sits on screen; `scale` is the monitor's DPI / 96.
    public static SKRect HighlightRect(SKPointI click, SKPointI imageOrigin, float scale, float diameterLogical = 60)
    {
        float d = diameterLogical * scale;
        float cx = click.X - imageOrigin.X, cy = click.Y - imageOrigin.Y;
        return SKRect.Create(cx - d / 2, cy - d / 2, d, d);
    }

    /// Screen rect to crop a screenshot to the window that was clicked, or null for the whole
    /// monitor (desktop, taskbar, or a window that nearly fills it). The clicked window or popup,
    /// same-app popups near the window (menus) and room for the click highlight are always
    /// included. `hitWindow` is the window the OS says received the click; it skips overlays.
    public static SKRectI? CaptureCropRect(SKPointI click, IReadOnlyList<WindowSnapshot> windows, SKRectI monitor, float scale,
                                           nint? hitWindow = null)
    {
        var osHit = hitWindow is { } h ? windows.FirstOrDefault(w => w.Handle == h && w.Bounds.Contains(click.X, click.Y)) : null;
        var hit = osHit ?? windows.FirstOrDefault(w => w.Bounds.Contains(click.X, click.Y));
        if (hit is null || hit.IsShell) return null;
        var main = hit.IsPopup ? windows.FirstOrDefault(w => w.Pid == hit.Pid && !w.IsPopup && !w.IsShell) : hit;
        if (main is null) return null;

        float margin = 8 * scale, popupReach = 200 * scale, highlightReach = 32 * scale;
        var rect = Union(ToFloat(main.Bounds), ToFloat(hit.Bounds));
        var reach = Inflate(ToFloat(main.Bounds), popupReach);
        foreach (var popup in windows)
        {
            if (popup.Pid == main.Pid && popup.IsPopup && ToFloat(popup.Bounds).IntersectsWith(reach))
                rect = Union(rect, ToFloat(popup.Bounds));
        }
        rect = Union(rect, SKRect.Create(click.X - highlightReach, click.Y - highlightReach, highlightReach * 2, highlightReach * 2));
        var clamped = SKRectI.Intersect(SKRectI.Ceiling(Inflate(rect, margin), outwards: true), monitor);
        if (clamped.IsEmpty || !clamped.Contains(click.X, click.Y)) return null;
        if ((long)clamped.Width * clamped.Height >= (long)monitor.Width * monitor.Height * 0.95) return null;
        return clamped;
    }

    /// A screen crop rect as pixels of a screenshot of `monitor`, clamped to the image.
    public static SKRectI ToImagePixels(SKRectI crop, SKRectI monitor)
    {
        var moved = SKRectI.Create(crop.Left - monitor.Left, crop.Top - monitor.Top, crop.Width, crop.Height);
        return SKRectI.Intersect(moved, SKRectI.Create(monitor.Width, monitor.Height));
    }

    /// How many steps at the end were clicks on the taskbar or tray within `window` before
    /// recording stopped: the clicks it took to reach the tray icon.
    public static int TrailingShellSteps(IReadOnlyList<(DateTimeOffset At, bool IsShell)> steps, DateTimeOffset stoppedAt, TimeSpan window)
    {
        int n = 0;
        for (int i = steps.Count - 1; i >= 0 && steps[i].IsShell && steps[i].At >= stoppedAt - window; i--) n++;
        return n;
    }

    static SKRect ToFloat(SKRectI r) => new(r.Left, r.Top, r.Right, r.Bottom);

    static SKRect Union(SKRect a, SKRect b) =>
        new(Math.Min(a.Left, b.Left), Math.Min(a.Top, b.Top), Math.Max(a.Right, b.Right), Math.Max(a.Bottom, b.Bottom));

    static SKRect Inflate(SKRect r, float d) => new(r.Left - d, r.Top - d, r.Right + d, r.Bottom + d);
}
