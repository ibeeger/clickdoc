// Builds the Windows icons from the repository's icon.png:
//   dotnet run --project windows/tools/MakeIcons -- icon.png windows/ClickDoc/Assets
// ClickDoc.ico (app), tray-idle.ico and tray-recording.ico (red dot) with PNG-compressed entries.
using SkiaSharp;

if (args.Length != 2)
{
    Console.Error.WriteLine("usage: MakeIcons <icon.png> <output dir>");
    return 1;
}
using var source = SKBitmap.Decode(args[0]) ?? throw new InvalidOperationException($"cannot read {args[0]}");
Directory.CreateDirectory(args[1]);
WriteIco(Path.Combine(args[1], "ClickDoc.ico"), [16, 24, 32, 48, 64, 128, 256], size => Render(source, size, recording: false));
WriteIco(Path.Combine(args[1], "tray-idle.ico"), [16, 20, 24, 32], size => Render(source, size, recording: false));
WriteIco(Path.Combine(args[1], "tray-recording.ico"), [16, 20, 24, 32], size => Render(source, size, recording: true));
return 0;

/// The centered square of `source` with rounded corners, plus a red dot bottom-right while recording.
static byte[] Render(SKBitmap source, int size, bool recording)
{
    int side = Math.Min(source.Width, source.Height);
    var crop = SKRectI.Create((source.Width - side) / 2, (source.Height - side) / 2, side, side);
    using var surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul));
    var canvas = surface.Canvas;
    canvas.Clear(SKColors.Transparent);
    float radius = size * 0.18f;
    canvas.Save();
    using (var clip = new SKPath())
    {
        clip.AddRoundRect(SKRect.Create(size, size), radius, radius);
        canvas.ClipPath(clip, antialias: true);
    }
    using (var image = SKImage.FromBitmap(source))
        canvas.DrawImage(image, crop, SKRect.Create(size, size), new SKSamplingOptions(SKCubicResampler.Mitchell));
    canvas.Restore();
    if (recording)
    {
        float d = size * 0.45f;
        var center = new SKPoint(size - d / 2, size - d / 2);
        using var white = new SKPaint { Color = SKColors.White, IsAntialias = true };
        using var red = new SKPaint { Color = new SKColor(237, 28, 36), IsAntialias = true };
        canvas.DrawCircle(center, d / 2, white);
        canvas.DrawCircle(center, d / 2 - 1, red);
    }
    using var snapshot = surface.Snapshot();
    using var png = snapshot.Encode(SKEncodedImageFormat.Png, 100);
    return png.ToArray();
}

static void WriteIco(string path, int[] sizes, Func<int, byte[]> render)
{
    var images = sizes.Select(render).ToArray();
    using var w = new BinaryWriter(File.Create(path));
    w.Write((ushort)0);            // reserved
    w.Write((ushort)1);            // type: icon
    w.Write((ushort)sizes.Length);
    int offset = 6 + 16 * sizes.Length;
    for (int i = 0; i < sizes.Length; i++)
    {
        w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));  // width, 0 means 256
        w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));  // height
        w.Write((byte)0);          // palette colors
        w.Write((byte)0);          // reserved
        w.Write((ushort)1);        // color planes
        w.Write((ushort)32);       // bits per pixel
        w.Write(images[i].Length);
        w.Write(offset);
        offset += images[i].Length;
    }
    foreach (var image in images) w.Write(image);
}
