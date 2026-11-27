namespace ClickDoc.Core.Tests;

public class GeometryTests
{
    static readonly SKRectI Display = new(0, 0, 1440, 900);

    static WindowSnapshot W(int x, int y, int w, int h, int pid, bool popup = false, bool shell = false, nint handle = 0) =>
        new(handle, pid, SKRectI.Create(x, y, w, h), popup, shell);

    [Fact]
    public void NormalizeAndClampRect()
    {
        var size = new SKSize(200, 100);
        Assert.Equal(SKRect.Create(0.25f, 0.25f, 0.5f, 0.5f), Geometry.Normalize(SKRect.Create(50, 25, 100, 50), size));
        Assert.Equal(SKRect.Create(0, 0.9f, 0.4f, 0.1f), Geometry.Normalize(SKRect.Create(-20, 90, 100, 40), size));
        // Dragged up-left: a rect with negative size is standardized first.
        Assert.Equal(SKRect.Create(0.25f, 0.25f, 0.5f, 0.5f), Geometry.Normalize(new SKRect(150, 75, 50, 25), size));
        Assert.Equal(SKRect.Empty, Geometry.Normalize(SKRect.Create(300, 300, 10, 10), size));
        Assert.Equal(SKRect.Create(50, 25, 100, 50), Geometry.Denormalize(SKRect.Create(0.25f, 0.25f, 0.5f, 0.5f), size));
    }

    [Fact]
    public void HighlightRectHighDpi()
    {
        Assert.Equal(SKRect.Create(140, 40, 120, 120), Geometry.HighlightRect(new(200, 100), new(0, 0), 2f));
    }

    [Fact]
    public void HighlightRect_NegativeOriginMonitorAt150Percent()
    {
        // A 150% monitor left of the primary one: virtual x ∈ [-2880, 0).
        var r = Geometry.HighlightRect(new(-1440, 300), new(-2880, 0), 1.5f);
        Assert.Equal(new SKRect(1440 - 45, 300 - 45, 1440 + 45, 300 + 45), r);
    }

    [Fact]
    public void CropToClickedWindowWithMargin()
    {
        var windows = new[] { W(100, 100, 600, 400, 7), W(0, 0, 1440, 900, 8) };
        Assert.Equal(SKRectI.Create(92, 92, 616, 416), Geometry.CaptureCropRect(new(300, 300), windows, Display, 1f));
    }

    [Fact]
    public void CropScalesMarginsWithDpi()
    {
        var windows = new[] { W(200, 200, 1200, 800, 7) };
        Assert.Equal(SKRectI.Create(184, 184, 1232, 832), Geometry.CaptureCropRect(new(600, 600), windows, new SKRectI(0, 0, 2880, 1800), 2f));
    }

    [Fact]
    public void CropIncludesMenuOfSameApp()
    {
        var windows = new[] { W(650, 200, 200, 300, 7, popup: true), W(100, 100, 600, 400, 7) };
        Assert.Equal(SKRectI.Create(92, 92, 766, 416), Geometry.CaptureCropRect(new(700, 250), windows, Display, 1f));
    }

    [Fact]
    public void CropIgnoresFarAwayPopupOfSameApp()
    {
        var windows = new[] { W(1300, 0, 30, 24, 7, popup: true), W(100, 100, 600, 400, 7) };
        Assert.Equal(SKRectI.Create(92, 92, 616, 416), Geometry.CaptureCropRect(new(300, 300), windows, Display, 1f));
    }

    [Fact]
    public void FullScreenForShellAndNothing()
    {
        Assert.Null(Geometry.CaptureCropRect(new(400, 850), [W(300, 820, 800, 80, 9, shell: true)], Display, 1f));
        Assert.Null(Geometry.CaptureCropRect(new(400, 400), [W(0, 0, 1440, 900, 10, shell: true)], Display, 1f));
        Assert.Null(Geometry.CaptureCropRect(new(400, 400), [], Display, 1f));
        // A popup whose app has no ordinary window (e.g. a tooltip-like overlay).
        Assert.Null(Geometry.CaptureCropRect(new(400, 400), [W(300, 300, 200, 200, 11, popup: true)], Display, 1f));
    }

    [Fact]
    public void Crop_ShellWindowMeansFullScreen()
    {
        var bar = new WindowSnapshot(2, 4, new SKRectI(0, 1040, 1920, 1080), false, true);
        Assert.Null(Geometry.CaptureCropRect(new(900, 1060), [bar], new SKRectI(0, 0, 1920, 1080), 1f));
    }

    [Fact]
    public void FullScreenWhenWindowNearlyFillsDisplay()
    {
        Assert.Null(Geometry.CaptureCropRect(new(400, 400), [W(0, 25, 1440, 875, 7)], Display, 1f));
    }

    [Fact]
    public void CropClampsToDisplayAboveMain()
    {
        var upper = SKRectI.Create(0, -1080, 1920, 1080);
        Assert.Equal(SKRectI.Create(0, -1008, 758, 516), Geometry.CaptureCropRect(new(100, -900), [W(-50, -1000, 800, 500, 7)], upper, 1f));
    }

    [Fact]
    public void Crop_ClampsToNegativeOriginMonitor()
    {
        var monitor = new SKRectI(-2880, -200, 0, 1420);
        var win = new WindowSnapshot(1, 10, new SKRectI(-2900, 100, -2000, 700), false, false);
        Assert.Equal(new SKRectI(-2880, 88, -1988, 712), Geometry.CaptureCropRect(new(-2500, 400), [win], monitor, 1.5f));
    }

    [Fact]
    public void CropIncludesClickedMenuFarFromWindow()
    {
        var windows = new[] { W(10, 24, 220, 300, 7, popup: true), W(800, 300, 500, 400, 7) };
        var crop = Geometry.CaptureCropRect(new(50, 60), windows, Display, 1f);
        Assert.NotNull(crop);
        Assert.True(crop.Value.Contains(50, 60));
        Assert.True(crop.Value.Contains(SKRectI.Create(10, 24, 220, 300)));
    }

    [Fact]
    public void CropKeepsWholeHighlightNearWindowEdge()
    {
        var crop = Geometry.CaptureCropRect(new(104, 104), [W(100, 100, 600, 400, 7)], Display, 1f)!.Value;
        Assert.True(crop.Contains(SKRectI.Create(74, 74, 60, 60)), crop.ToString());
    }

    [Fact]
    public void CropPrefersWindowActuallyHitByMouse()
    {
        var overlay = W(0, 0, 1000, 800, 3, handle: 1);
        var target = W(100, 100, 600, 400, 7, handle: 42);
        Assert.Equal(SKRectI.Create(92, 92, 616, 416), Geometry.CaptureCropRect(new(300, 300), [overlay, target], Display, 1f, hitWindow: 42));
    }

    [Fact]
    public void ToImagePixels()
    {
        var monitor = SKRectI.Create(-1920, -200, 1920, 1080);
        Assert.Equal(SKRectI.Create(20, 100, 200, 100), Geometry.ToImagePixels(SKRectI.Create(-1900, -100, 200, 100), monitor));
        Assert.Equal(SKRectI.Create(0, 0, 100, 100), Geometry.ToImagePixels(SKRectI.Create(-2000, -300, 180, 200), monitor));
    }

    [Fact]
    public void TrailingShellSteps_CountsOnlyRecentTail()
    {
        var t = DateTimeOffset.UnixEpoch.AddSeconds(100);
        var steps = new[] { (t.AddSeconds(-9), true), (t.AddSeconds(-8), false), (t.AddSeconds(-3), true), (t.AddSeconds(-1), true) };
        Assert.Equal(2, Geometry.TrailingShellSteps(steps, t, TimeSpan.FromSeconds(5)));
        Assert.Equal(0, Geometry.TrailingShellSteps(steps[..2], t, TimeSpan.FromSeconds(5)));
    }
}
