using SkiaSharp;

namespace ClickDoc.Core;

/// Something editable on a step's screenshot.
public abstract record CanvasItem;

public sealed record BlurItem(int Index) : CanvasItem;

public sealed record AnnotationItem(Guid Id) : CanvasItem;

public enum CanvasHandle
{
    /// Bottom-right corner of a box or blur region.
    Resize,
    ArrowStart,
    ArrowEnd,
}

public sealed record CanvasHit(CanvasItem Item, CanvasHandle? Handle = null);

public static class Editing
{
    static float Distance(SKPoint a, SKPoint b) => SKPoint.Distance(a, b);

    static float DistanceToSegment(SKPoint p, SKPoint a, SKPoint b)
    {
        float dx = b.X - a.X, dy = b.Y - a.Y, len2 = dx * dx + dy * dy;
        if (len2 <= 0) return Distance(p, a);
        float t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2, 0, 1);
        return Distance(p, new SKPoint(a.X + t * dx, a.Y + t * dy));
    }

    static SKPoint Scaled(SKPoint p, SKSize size) => new(p.X * size.Width, p.Y * size.Height);

    /// The text's box on a canvas of `canvas` size showing an image `imageWidth` pixels wide
    /// (defaults to the canvas width). Sizes follow the exported image, scaled to the canvas.
    public static SKRect TextFrame(SKPoint origin, string text, SKSize canvas, float? imageWidth = null)
    {
        float w = imageWidth ?? canvas.Width;
        var px = Annotations.TextSize(text, w);
        float k = canvas.Width / w;
        var o = Scaled(origin, canvas);
        return SKRect.Create(o.X, o.Y, px.Width * k, px.Height * k);
    }

    /// Keeps a text's whole box inside the image.
    public static SKPoint ClampTextOrigin(SKPoint origin, string text, SKSize imageSize)
    {
        var t = Annotations.TextSize(text, imageSize.Width);
        float maxX = Math.Max(0, 1 - t.Width / imageSize.Width), maxY = Math.Max(0, 1 - t.Height / imageSize.Height);
        return new SKPoint(Math.Clamp(origin.X, 0, maxX), Math.Clamp(origin.Y, 0, maxY));
    }

    /// What is under `p` (canvas units, top-left origin). Handles of the selection win, then
    /// annotations from the top down, then blur regions. Boxes are hit on their border only, so
    /// new marks can still be drawn inside them. Drawing tools pass `includeBlurBodies: false`
    /// so a drag inside a blur draws instead of moving the blur.
    public static CanvasHit? HitTest(SKPoint p, IReadOnlyList<SKRect> blur, IReadOnlyList<Annotation> annotations, CanvasItem? selected,
                                     SKSize canvas, float? imageWidth = null, bool includeBlurBodies = true,
                                     float tolerance = 6, float handleRadius = 9)
    {
        float imageW = imageWidth ?? canvas.Width;
        if (selected is not null && HandleHit(p, blur, annotations, selected, canvas, handleRadius) is { } handle)
            return new CanvasHit(selected, handle);

        for (int i = annotations.Count - 1; i >= 0; i--)
        {
            var a = annotations[i];
            bool hit = a.Mark switch
            {
                RectangleMark r => OnBorder(p, Geometry.Denormalize(r.Rect, canvas), tolerance),
                ArrowMark ar => DistanceToSegment(p, Scaled(ar.From, canvas), Scaled(ar.To, canvas))
                                <= tolerance + AnnotationStyle.LineWidth(imageW) * canvas.Width / imageW,
                TextMark t => Inflate(TextFrame(t.Origin, t.Text, canvas, imageW), tolerance).Contains(p),
                _ => false,
            };
            if (hit) return new CanvasHit(new AnnotationItem(a.Id));
        }
        if (includeBlurBodies)
        {
            for (int i = blur.Count - 1; i >= 0; i--)
                if (Geometry.Denormalize(blur[i], canvas).Contains(p)) return new CanvasHit(new BlurItem(i));
        }
        return null;
    }

    static CanvasHandle? HandleHit(SKPoint p, IReadOnlyList<SKRect> blur, IReadOnlyList<Annotation> annotations, CanvasItem selected,
                                   SKSize canvas, float radius)
    {
        switch (selected)
        {
            case BlurItem b when b.Index >= 0 && b.Index < blur.Count:
                var br = Geometry.Denormalize(blur[b.Index], canvas);
                return Distance(p, new SKPoint(br.Right, br.Bottom)) <= radius ? CanvasHandle.Resize : null;
            case AnnotationItem item:
                switch (annotations.FirstOrDefault(a => a.Id == item.Id)?.Mark)
                {
                    case RectangleMark r:
                        var rr = Geometry.Denormalize(r.Rect, canvas);
                        return Distance(p, new SKPoint(rr.Right, rr.Bottom)) <= radius ? CanvasHandle.Resize : null;
                    case ArrowMark a:
                        if (Distance(p, Scaled(a.To, canvas)) <= radius) return CanvasHandle.ArrowEnd;
                        if (Distance(p, Scaled(a.From, canvas)) <= radius) return CanvasHandle.ArrowStart;
                        return null;
                }
                return null;
        }
        return null;
    }

    static bool OnBorder(SKPoint p, SKRect r, float tolerance)
    {
        var outer = Inflate(r, tolerance);
        var inner = Inflate(r, -tolerance);
        return outer.Contains(p) && (inner.Width <= 0 || inner.Height <= 0 || !inner.Contains(p));
    }

    static SKRect Inflate(SKRect r, float d) => new(r.Left - d, r.Top - d, r.Right + d, r.Bottom + d);

    /// Moves a mark by a normalized delta, keeping it inside the image without changing its shape.
    public static Mark Translated(Mark mark, SKPoint d) => mark switch
    {
        RectangleMark r => new RectangleMark(SKRect.Create(Math.Clamp(r.Rect.Left + d.X, 0, 1 - r.Rect.Width),
                                                           Math.Clamp(r.Rect.Top + d.Y, 0, 1 - r.Rect.Height),
                                                           r.Rect.Width, r.Rect.Height)),
        ArrowMark a => MoveArrow(a, d),
        TextMark t => t with { Origin = new SKPoint(Math.Clamp(t.Origin.X + d.X, 0, 1), Math.Clamp(t.Origin.Y + d.Y, 0, 1)) },
        _ => mark,
    };

    static ArrowMark MoveArrow(ArrowMark a, SKPoint d)
    {
        float dx = Math.Clamp(d.X, -Math.Min(a.From.X, a.To.X), 1 - Math.Max(a.From.X, a.To.X));
        float dy = Math.Clamp(d.Y, -Math.Min(a.From.Y, a.To.Y), 1 - Math.Max(a.From.Y, a.To.Y));
        return new ArrowMark(new SKPoint(a.From.X + dx, a.From.Y + dy), new SKPoint(a.To.X + dx, a.To.Y + dy));
    }

    /// Grows or shrinks a normalized rect from its bottom-right corner, within the image.
    public static SKRect Resized(SKRect r, SKPoint d, SKSize minSize) =>
        SKRect.Create(r.Left, r.Top,
                      Math.Clamp(r.Width + d.X, minSize.Width, Math.Max(minSize.Width, 1 - r.Left)),
                      Math.Clamp(r.Height + d.Y, minSize.Height, Math.Max(minSize.Height, 1 - r.Top)));
}
