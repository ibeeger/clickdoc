namespace ClickDoc.Core.Tests;

static class Pixels
{
    public static SKBitmap Blank(int w, int h)
    {
        var b = new SKBitmap(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
        b.Erase(SKColors.White);
        return b;
    }

    /// Black/white vertical stripes, 2px wide: maximum high-frequency detail, like text.
    public static SKBitmap Stripes(int w, int h)
    {
        var b = Blank(w, h);
        using var canvas = new SKCanvas(b);
        using var black = new SKPaint { Color = SKColors.Black };
        for (int x = 0; x < w; x += 4) canvas.DrawRect(x, 0, 2, h, black);
        return b;
    }

    public static bool IsRed(SKColor c) => c.Red > 180 && c.Green < 90 && c.Blue < 90;

    public static bool IsWhite(SKColor c) => c.Red > 245 && c.Green > 245 && c.Blue > 245;

    public static int RedCount(SKBitmap b, SKRectI r)
    {
        int n = 0;
        for (int y = r.Top; y < r.Bottom; y++)
            for (int x = r.Left; x < r.Right; x++)
                if (IsRed(b.GetPixel(x, y))) n++;
        return n;
    }

    /// Bounds of every pixel that is not white.
    public static SKRectI InkBounds(SKBitmap b)
    {
        int l = int.MaxValue, t = int.MaxValue, r = -1, bottom = -1;
        for (int y = 0; y < b.Height; y++)
            for (int x = 0; x < b.Width; x++)
                if (!IsWhite(b.GetPixel(x, y)))
                {
                    l = Math.Min(l, x); t = Math.Min(t, y); r = Math.Max(r, x + 1); bottom = Math.Max(bottom, y + 1);
                }
        return r < 0 ? SKRectI.Empty : new SKRectI(l, t, r, bottom);
    }

    public static int RedSpread(SKBitmap b, int x0, int x1, int y)
    {
        var reds = Enumerable.Range(x0, x1 - x0).Select(x => (int)b.GetPixel(x, y).Red).ToList();
        return reds.Max() - reds.Min();
    }
}
