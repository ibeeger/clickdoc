using static ClickDoc.Core.Tests.Pixels;

namespace ClickDoc.Core.Tests;

public class ImagingTests
{
    [Fact]
    public void RenderHighlightPixels()
    {
        var out_ = Imaging.RenderHighlight(Blank(200, 200), SKRect.Create(50, 50, 100, 100), 1);
        Assert.Equal(200, out_.Width);
        Assert.Equal(200, out_.Height);
        var center = out_.GetPixel(100, 100);
        Assert.True(center.Red > 200 && center.Green < 220 && center.Blue < 220, $"center tinted red: {center}");
        Assert.Equal(SKColors.White, out_.GetPixel(5, 5));
        Assert.True(IsRed(out_.GetPixel(100, 51)), $"top stroke {out_.GetPixel(100, 51)}");
        Assert.True(IsRed(out_.GetPixel(100, 148)), $"bottom stroke {out_.GetPixel(100, 148)}");
    }

    [Fact]
    public void RenderHighlightTopLeftOrigin()
    {
        var out_ = Imaging.RenderHighlight(Blank(200, 200), SKRect.Create(0, 0, 40, 40), 1);
        Assert.True(out_.GetPixel(20, 20).Green < 220);
        Assert.Equal(SKColors.White, out_.GetPixel(20, 180));
    }

    [Fact]
    public void RenderHighlightStrokeScales()
    {
        // Stroke 4 logical px at 200%: 8 px, centered on the circle's edge.
        var out_ = Imaging.RenderHighlight(Blank(300, 300), SKRect.Create(50, 50, 200, 200), 2);
        Assert.True(IsRed(out_.GetPixel(150, 53)));
        Assert.False(IsRed(out_.GetPixel(150, 57)));
    }

    [Fact]
    public void PngRoundTrip()
    {
        var png = Imaging.EncodePng(Blank(30, 20));
        Assert.NotEmpty(png);
        var back = Imaging.DecodePng(png);
        Assert.Equal(30, back.Width);
        Assert.Equal(20, back.Height);
    }

    [Fact]
    public void ThumbnailIsBounded()
    {
        var t = Imaging.Thumbnail(Imaging.EncodePng(Blank(1000, 400)), 240);
        Assert.Equal(240, t.Width);
        Assert.Equal(96, t.Height);
        var small = Imaging.Thumbnail(Imaging.EncodePng(Blank(100, 40)), 240);
        Assert.Equal(100, small.Width);
    }

    [Fact]
    public void DecodeWidthReadsHeaderOnly()
    {
        Assert.Equal(1000, Imaging.DecodeWidth(Imaging.EncodePng(Blank(1000, 400))));
        Assert.Throws<ExportException>(() => Imaging.DecodeWidth([1, 2, 3]));
    }

    [Fact]
    public void BlurFlattensDetailInsideRegionOnly()
    {
        var out_ = Imaging.ApplyBlur(Stripes(400, 200), [SKRect.Create(0, 0, 0.5f, 1)]);
        Assert.Equal(400, out_.Width);
        Assert.True(RedSpread(out_, 60, 140, 100) < 40, "inside flat");
        Assert.True(RedSpread(out_, 300, 340, 100) > 200, "outside keeps stripes");
    }

    [Fact]
    public void BlurRegionUsesTopLeftOrigin()
    {
        var out_ = Imaging.ApplyBlur(Stripes(200, 200), [SKRect.Create(0, 0, 1, 0.25f)]);
        Assert.True(RedSpread(out_, 80, 120, 20) < 40, "top blurred");
        Assert.True(RedSpread(out_, 80, 120, 180) > 200, "bottom sharp");
    }

    [Fact]
    public void BlurNoRegionsReturnsSameImage()
    {
        var img = Stripes(50, 40);
        Assert.Same(img, Imaging.ApplyBlur(img, []));
    }

    [Fact]
    public void BlurSigmaScalesWithWidthAndHasFloor()
    {
        Assert.Equal(20, Imaging.BlurSigma(3000));
        Assert.Equal(10, Imaging.BlurSigma(240));
    }
}
