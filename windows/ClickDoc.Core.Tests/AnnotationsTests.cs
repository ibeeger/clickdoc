using static ClickDoc.Core.Tests.Pixels;

namespace ClickDoc.Core.Tests;

public class AnnotationsTests
{
    static Annotation A(Mark m) => new(Guid.NewGuid(), m);

    [Fact]
    public void RectangleStrokesEdgeOnly()
    {
        var out_ = Annotations.Render(Blank(1000, 500), [A(new RectangleMark(SKRect.Create(0.1f, 0.2f, 0.4f, 0.4f)))]);
        Assert.True(IsRed(out_.GetPixel(300, 100)), "top edge");
        Assert.True(IsRed(out_.GetPixel(100, 200)), "left edge");
        Assert.True(IsRed(out_.GetPixel(300, 300)), "bottom edge");
        Assert.True(IsWhite(out_.GetPixel(300, 200)), "interior untouched");
        Assert.True(IsWhite(out_.GetPixel(300, 450)), "outside untouched");
    }

    [Fact]
    public void ArrowDrawsShaftAndHead()
    {
        var out_ = Annotations.Render(Blank(1000, 400), [A(new ArrowMark(new(0.1f, 0.5f), new(0.9f, 0.5f)))]);
        Assert.True(IsRed(out_.GetPixel(300, 200)), "shaft");
        Assert.True(IsRed(out_.GetPixel(880, 190)) && IsRed(out_.GetPixel(880, 210)), "head width");
        Assert.False(IsRed(out_.GetPixel(300, 190)), "shaft is thin");
        Assert.True(IsWhite(out_.GetPixel(50, 200)), "nothing before the start");
    }

    [Fact]
    public void TextDrawsNearItsOrigin()
    {
        var out_ = Annotations.Render(Blank(1000, 600), [A(new TextMark(new(0.1f, 0.1f), "点这里"))]);
        Assert.True(RedCount(out_, SKRectI.Create(90, 50, 200, 70)) > 50);
        Assert.Equal(0, RedCount(out_, SKRectI.Create(0, 300, 1000, 300)));
    }

    [Fact]
    public void TextBoxMatchesTextSize()
    {
        var out_ = Annotations.Render(Blank(1000, 600), [A(new TextMark(new(0.1f, 0.1f), "点这里"))]);
        var ink = InkBounds(out_);
        var size = Annotations.TextSize("点这里", 1000);
        // The white outline adds a little around the glyphs; the ink must sit inside the box plus outline.
        float pad = AnnotationStyle.FontSize(1000) * AnnotationStyle.TextOutlineFraction;
        Assert.InRange(ink.Left, 100 - pad - 1, 100 + size.Width);
        Assert.InRange(ink.Top, 60 - pad - 1, 60 + size.Height);
        Assert.InRange(ink.Right, 100, 100 + size.Width + pad + 1);
        Assert.InRange(ink.Bottom, 60, 60 + size.Height + pad + 1);
    }

    [Fact]
    public void NoneReturnsImage()
    {
        var img = Blank(20, 10);
        Assert.Same(img, Annotations.Render(img, []));
    }

    [Fact]
    public void Render_PreviewUsesStyleWidthPastTheFloor()
    {
        var text = A(new TextMark(new(0.1f, 0.1f), "测试文字"));
        // Full size 1000 wide: font 22. A 250-wide preview styled as 1000 wide: 5.5 (the floor would make it 10).
        var full = InkBounds(Annotations.Render(Blank(1000, 500), [text]));
        var preview = InkBounds(Annotations.Render(Blank(250, 125), [text], styleWidth: 1000));
        Assert.InRange(preview.Width, full.Width / 4 - 2, full.Width / 4 + 2);
    }

    [Fact]
    public void ArrowPathTooShortIsNull()
    {
        Assert.Null(Annotations.ArrowPath(new(10, 10), new(10.2f, 10.2f), 3));
        Assert.NotNull(Annotations.ArrowPath(new(10, 10), new(100, 10), 3));
    }

    [Fact]
    public void ComposeBlursThenAnnotates()
    {
        var step = TestSupport.MakeStep("x") with
        {
            BlurRegions = [SKRect.Create(0, 0, 1, 1)],
            Annotations = [A(new RectangleMark(SKRect.Create(0.25f, 0.25f, 0.5f, 0.5f)))],
        };
        var out_ = Annotations.Compose(Stripes(400, 200), step);
        Assert.True(IsRed(out_.GetPixel(200, 50)), "box on top of the blur");
        Assert.True(RedSpread(out_, 10, 60, 150) < 40, "stripes blurred");
    }

    [Fact]
    public void Fonts_CanDrawChinese() => Assert.True(Fonts.Get(true).ContainsGlyph('中'));
}
