using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkiaSharp;

namespace ClickDoc.Editor;

static class Bitmaps
{
    /// A frozen WPF copy of a Skia bitmap.
    public static BitmapSource ToImageSource(SKBitmap bitmap)
    {
        using var bgra = bitmap.ColorType == SKColorType.Bgra8888 ? null : bitmap.Copy(SKColorType.Bgra8888);
        var source = bgra ?? bitmap;
        var result = BitmapSource.Create(source.Width, source.Height, 96, 96, PixelFormats.Pbgra32, null,
                                         source.GetPixels(), source.ByteCount, source.RowBytes);
        result.Freeze();
        return result;
    }

    /// Decodes PNG bytes into a frozen bitmap.
    public static BitmapSource FromPng(byte[] png)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = new MemoryStream(png);
        image.EndInit();
        image.Freeze();
        return image;
    }
}
