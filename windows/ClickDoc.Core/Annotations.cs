using SkiaSharp;

namespace ClickDoc.Core;

/// A drawn mark on a screenshot. Coordinates are normalized to the image (0...1, top-left origin).
public abstract record Mark;

public sealed record RectangleMark(SKRect Rect) : Mark;

public sealed record ArrowMark(SKPoint From, SKPoint To) : Mark;

/// `Origin` is the top-left corner of the text.
public sealed record TextMark(SKPoint Origin, string Text) : Mark;

public sealed record Annotation(Guid Id, Mark Mark);

/// Sizes scale with the image, so marks look the same on thumbnails, exports and the editor.
public static class AnnotationStyle
{
    public static float LineWidth(float imageWidth) => Math.Max(1.5f, imageWidth * 0.0028f);
    public static float FontSize(float imageWidth) => Math.Max(10, imageWidth * 0.022f);
    public static float ArrowHeadLength(float lineWidth) => lineWidth * 8;
    public static float ArrowHeadHalfWidth(float lineWidth) => lineWidth * 4.5f;
    /// Total width of the white outline drawn under text, as a fraction of the font size.
    public const float TextOutlineFraction = 0.18f;
    public static readonly SKColor Red = new(237, 28, 36);
}

public static class Annotations
{
    /// Arrow outline (shaft end + head) in the given coordinate space; null when too short to draw.
    public static (SKPoint ShaftEnd, SKPath Head)? ArrowPath(SKPoint from, SKPoint tip, float lineWidth)
    {
        float dx = tip.X - from.X, dy = tip.Y - from.Y;
        float length = MathF.Sqrt(dx * dx + dy * dy);
        if (length <= 0.5f) return null;
        float ux = dx / length, uy = dy / length;
        float headLength = Math.Min(AnnotationStyle.ArrowHeadLength(lineWidth), length);
        float half = AnnotationStyle.ArrowHeadHalfWidth(lineWidth);
        var b = new SKPoint(tip.X - ux * headLength, tip.Y - uy * headLength);
        var head = new SKPath();
        head.MoveTo(tip);
        head.LineTo(b.X - uy * half, b.Y + ux * half);
        head.LineTo(b.X + uy * half, b.Y - ux * half);
        head.Close();
        return (b, head);
    }

    static SKFont TextFont(float fontSize) => new(Fonts.Get(bold: true), fontSize) { Subpixel = true };

    /// The text box size in pixels of an image `styleWidth` pixels wide, for hit-testing and layout.
    public static SKSize TextSize(string text, float styleWidth)
    {
        using var font = TextFont(AnnotationStyle.FontSize(styleWidth));
        var m = font.Metrics;
        return new SKSize(font.MeasureText(text), -m.Ascent + m.Descent);
    }

    /// Returns `image` with the annotations drawn on top. Sizes follow an image `styleWidth`
    /// pixels wide (default: this one), scaled to this image, so a downscaled preview matches the export.
    public static SKBitmap Render(SKBitmap image, IReadOnlyList<Annotation> annotations, float? styleWidth = null)
    {
        if (annotations.Count == 0) return image;
        var copy = image.Copy();
        using var canvas = new SKCanvas(copy);
        Draw(canvas, new SKSize(image.Width, image.Height), annotations, styleWidth ?? image.Width);
        return copy;
    }

    /// Draws annotations onto any canvas covering an image of `size`, e.g. a transparent overlay.
    public static void Draw(SKCanvas canvas, SKSize size, IReadOnlyList<Annotation> annotations, float styleWidth)
    {
        float k = size.Width / styleWidth;
        float lw = AnnotationStyle.LineWidth(styleWidth) * k;
        using var stroke = new SKPaint
        {
            Color = AnnotationStyle.Red, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = lw,
            StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round,
        };
        using var fill = new SKPaint { Color = AnnotationStyle.Red, IsAntialias = true };
        foreach (var annotation in annotations)
        {
            switch (annotation.Mark)
            {
                case RectangleMark r:
                    canvas.DrawRect(Geometry.Denormalize(r.Rect, size), stroke);
                    break;
                case ArrowMark a:
                    var from = new SKPoint(a.From.X * size.Width, a.From.Y * size.Height);
                    var to = new SKPoint(a.To.X * size.Width, a.To.Y * size.Height);
                    if (ArrowPath(from, to, lw) is not var (shaftEnd, head)) break;
                    using (head)
                    {
                        canvas.DrawLine(from, shaftEnd, stroke);
                        canvas.DrawPath(head, fill);
                    }
                    break;
                case TextMark t:
                    DrawText(canvas, t.Text, new SKPoint(t.Origin.X * size.Width, t.Origin.Y * size.Height),
                             AnnotationStyle.FontSize(styleWidth) * k);
                    break;
            }
        }
    }

    static void DrawText(SKCanvas canvas, string text, SKPoint topLeft, float fontSize)
    {
        using var font = TextFont(fontSize);
        float baseline = topLeft.Y - font.Metrics.Ascent;
        using var outline = new SKPaint
        {
            Color = SKColors.White, IsAntialias = true, Style = SKPaintStyle.Stroke,
            StrokeWidth = fontSize * AnnotationStyle.TextOutlineFraction, StrokeJoin = SKStrokeJoin.Round,
        };
        using var fill = new SKPaint { Color = AnnotationStyle.Red, IsAntialias = true };
        canvas.DrawText(text, topLeft.X, baseline, SKTextAlign.Left, font, outline);
        canvas.DrawText(text, topLeft.X, baseline, SKTextAlign.Left, font, fill);
    }

    /// The screenshot as readers see it: blur first, then annotations on top.
    public static SKBitmap Compose(SKBitmap image, Step step, float? styleWidth = null)
    {
        float w = styleWidth ?? image.Width;
        float sigma = Imaging.BlurSigma(w) * image.Width / w;
        return Render(Imaging.ApplyBlur(image, step.BlurRegions, sigma), step.Annotations, w);
    }
}
