namespace ClickDoc.Core.Tests;

public class AnnotationEditingTests
{
    static readonly SKSize Canvas = new(1000, 500);
    static readonly Annotation Box = new(Guid.NewGuid(), new RectangleMark(SKRect.Create(0.1f, 0.2f, 0.4f, 0.4f)));  // (100,100)-(500,300)
    static readonly Annotation Arrow = new(Guid.NewGuid(), new ArrowMark(new(0.6f, 0.1f), new(0.9f, 0.1f)));       // y=50, x 600..900
    static readonly Annotation Label = new(Guid.NewGuid(), new TextMark(new(0.6f, 0.8f), "提示"));                  // top-left (600,400)
    static readonly SKRect[] Blur = [SKRect.Create(0, 0, 0.3f, 0.3f)];                                              // (0,0)-(300,150)

    static CanvasHit? Hit(float x, float y, CanvasItem? selected = null) =>
        Editing.HitTest(new(x, y), Blur, [Box, Arrow, Label], selected, Canvas);

    static CanvasHit ItemHit(Annotation a) => new(new AnnotationItem(a.Id));

    static void Close(float expected, float actual) => Assert.InRange(actual, expected - 1e-4f, expected + 1e-4f);

    static void Close(SKPoint expected, SKPoint actual) { Close(expected.X, actual.X); Close(expected.Y, actual.Y); }

    static void Close(SKRect expected, SKRect actual)
    {
        Close(expected.Left, actual.Left); Close(expected.Top, actual.Top);
        Close(expected.Right, actual.Right); Close(expected.Bottom, actual.Bottom);
    }

    [Fact]
    public void HitRectangleBorderNotInterior()
    {
        Assert.Equal(ItemHit(Box), Hit(300, 102));
        Assert.Equal(ItemHit(Box), Hit(498, 200));
        Assert.Null(Hit(300, 220));
    }

    [Fact]
    public void HitArrowNearShaft()
    {
        Assert.Equal(ItemHit(Arrow), Hit(750, 54));
        Assert.Null(Hit(750, 80));
    }

    [Fact]
    public void HitTextBox()
    {
        Assert.Equal(ItemHit(Label), Hit(610, 410));
        Assert.Null(Hit(610, 480));
    }

    [Fact]
    public void AnnotationsBeatBlurAndBlurHitsInside()
    {
        Assert.Equal(ItemHit(Box), Hit(120, 100));
        Assert.Equal(new CanvasHit(new BlurItem(0)), Hit(50, 50));
    }

    [Fact]
    public void HandlesOnlyForSelection()
    {
        CanvasItem box = new AnnotationItem(Box.Id), arrow = new AnnotationItem(Arrow.Id), blur = new BlurItem(0);
        Assert.Equal(new CanvasHit(box, CanvasHandle.Resize), Hit(500, 300, box));
        Assert.Equal(new CanvasHit(arrow, CanvasHandle.ArrowEnd), Hit(900, 50, arrow));
        Assert.Equal(new CanvasHit(arrow, CanvasHandle.ArrowStart), Hit(600, 50, arrow));
        Assert.Equal(new CanvasHit(blur, CanvasHandle.Resize), Hit(300, 150, blur));
        Assert.Equal(ItemHit(Arrow), Hit(900, 50));
    }

    [Fact]
    public void TranslateClampsInsideImage()
    {
        var moved = (RectangleMark)Editing.Translated(new RectangleMark(SKRect.Create(0.1f, 0.2f, 0.4f, 0.4f)), new(0.9f, -0.5f));
        Close(SKRect.Create(0.6f, 0, 0.4f, 0.4f), moved.Rect);
        // The arrow keeps its shape: the move is limited by the endpoint that hits the edge first.
        var a = (ArrowMark)Editing.Translated(new ArrowMark(new(0.5f, 0.5f), new(0.8f, 0.6f)), new(0.5f, 0));
        Close(new SKPoint(0.7f, 0.5f), a.From);
        Close(new SKPoint(1.0f, 0.6f), a.To);
        var t = (TextMark)Editing.Translated(new TextMark(new(0.5f, 0.5f), "t"), new(-1, 0.2f));
        Close(new SKPoint(0, 0.7f), t.Origin);
        Assert.Equal("t", t.Text);
    }

    [Fact]
    public void ResizeKeepsMinimumAndBounds()
    {
        var r = Editing.Resized(SKRect.Create(0.5f, 0.5f, 0.2f, 0.2f), new(0.6f, -0.5f), new SKSize(0.01f, 0.02f));
        Close(SKRect.Create(0.5f, 0.5f, 0.5f, 0.02f), r);
    }

    [Fact]
    public void TextFrameScalesFromImagePixels()
    {
        // A 600px-wide image shown 120 wide: text keeps its size relative to the image.
        var f = Editing.TextFrame(new(0.5f, 0.5f), "提示", new SKSize(120, 60), imageWidth: 600);
        var px = Annotations.TextSize("提示", 600);
        Close(px.Width * 0.2f, f.Width);
        Close(new SKPoint(60, 30), f.Location);
    }

    [Fact]
    public void ClampTextOriginKeepsTextInsideImage()
    {
        var size = new SKSize(1000, 500);
        var t = Annotations.TextSize("很长的一段提示文字", 1000);
        var o = Editing.ClampTextOrigin(new(0.99f, 0.99f), "很长的一段提示文字", size);
        Close(1 - t.Width / 1000, o.X);
        Close(1 - t.Height / 500, o.Y);
        Assert.Equal(new SKPoint(0.1f, 0.1f), Editing.ClampTextOrigin(new(0.1f, 0.1f), "a", size));
    }

    [Fact]
    public void HitTestUsesImageScaledText()
    {
        var a = new Annotation(Guid.NewGuid(), new TextMark(new(0.1f, 0.1f), "提示"));
        var canvas = new SKSize(120, 60);
        Assert.Null(Editing.HitTest(new(13, 36), [], [a], null, canvas, imageWidth: 600));
        Assert.Equal(new CanvasHit(new AnnotationItem(a.Id)), Editing.HitTest(new(13, 7), [], [a], null, canvas, imageWidth: 600));
    }

    [Fact]
    public void DrawingToolsIgnoreBlurBodies()
    {
        SKRect[] blur = [SKRect.Create(0, 0, 0.5f, 0.5f)];
        Assert.Equal(new CanvasHit(new BlurItem(0)), Editing.HitTest(new(100, 100), blur, [], null, Canvas));
        Assert.Null(Editing.HitTest(new(100, 100), blur, [], null, Canvas, includeBlurBodies: false));
    }
}
