using SkiaSharp;

namespace ClickDoc.Core;

/// The bold and regular UI font for annotations and PDFs; must cover Chinese.
public static class Fonts
{
    static readonly string[] Families = ["Microsoft YaHei UI", "Microsoft YaHei", "PingFang SC"];
    static SKTypeface? _regular, _bold;

    public static SKTypeface Get(bool bold) => bold ? _bold ??= Resolve(SKFontStyle.Bold) : _regular ??= Resolve(SKFontStyle.Normal);

    static SKTypeface Resolve(SKFontStyle style)
    {
        foreach (var family in Families)
        {
            var face = SKTypeface.FromFamilyName(family, style);
            if (face is not null && face.FamilyName == family) return face;
        }
        return SKFontManager.Default.MatchCharacter(null, style, null, '中') ?? SKTypeface.Default;
    }
}

public static class Imaging
{
    static readonly SKSamplingOptions Smooth = new(SKFilterMode.Linear, SKMipmapMode.Linear);

    public static byte[] EncodePng(SKBitmap image)
    {
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// Throws ExportException for bytes that are not an image (SKBitmap.Decode would throw ArgumentNullException).
    public static SKBitmap DecodePng(byte[] png)
    {
        using var data = SKData.CreateCopy(png);
        using var codec = SKCodec.Create(data);
        return (codec is null ? null : SKBitmap.Decode(codec)) ?? throw new ExportException(ContentLanguage.Current.ImageProcessingFailed);
    }

    /// Downscaled copy whose longer side is at most `maxPixelSize`; never upscales.
    public static SKBitmap Thumbnail(byte[] png, int maxPixelSize)
    {
        var full = DecodePng(png);
        float k = Math.Min(1f, (float)maxPixelSize / Math.Max(full.Width, full.Height));
        if (k >= 1) return full;
        using (full)
        {
            var info = new SKImageInfo(Math.Max(1, (int)Math.Round(full.Width * k)), Math.Max(1, (int)Math.Round(full.Height * k)),
                                       full.ColorType, full.AlphaType);
            return full.Resize(info, Smooth);
        }
    }

    /// Draws the click highlight onto a copy of `image`. `circle` is in image pixels; `scale` is pixels per logical pixel.
    public static SKBitmap RenderHighlight(SKBitmap image, SKRect circle, float scale)
    {
        var copy = image.Copy();
        using var canvas = new SKCanvas(copy);
        using var fill = new SKPaint { Color = new SKColor(255, 0, 0, 77), IsAntialias = true };
        using var stroke = new SKPaint { Color = new SKColor(255, 0, 0), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 4 * scale };
        canvas.DrawOval(circle, fill);
        canvas.DrawOval(circle, stroke);
        return copy;
    }

    /// Gaussian sigma in pixels: strong enough that short text cannot be read.
    public static float BlurSigma(float imageWidth) => Math.Max(imageWidth / 150, 10);

    /// Returns `image` with each normalized region (top-left origin) Gaussian-blurred.
    public static SKBitmap ApplyBlur(SKBitmap image, IReadOnlyList<SKRect> regions, float? sigma = null)
    {
        if (regions.Count == 0) return image;
        float s = sigma ?? BlurSigma(image.Width);
        using var source = SKImage.FromBitmap(image);
        using var filter = SKImageFilter.CreateBlur(s, s, SKShaderTileMode.Clamp);
        using var blurred = source.ApplyImageFilter(filter, SKRectI.Create(image.Width, image.Height),
                                                    SKRectI.Create(image.Width, image.Height), out _, out SKPointI offset);
        var copy = image.Copy();
        using var canvas = new SKCanvas(copy);
        var size = new SKSize(image.Width, image.Height);
        foreach (var region in regions)
        {
            canvas.Save();
            canvas.ClipRect(Geometry.Denormalize(region, size));
            canvas.DrawImage(blurred, offset.X, offset.Y);
            canvas.Restore();
        }
        return copy;
    }
}
