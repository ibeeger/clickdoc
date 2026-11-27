using SkiaSharp;

namespace ClickDoc.Core;

/// Height of a step's heading and description (gaps included) and its screenshot's pixel size.
public sealed record StepBlock(float TextHeight, SKSize ImagePixels);

/// Where a step starts (`Top`, from the page's top edge) and how big its screenshot is drawn.
public sealed record PlacedStep(int Page, float Top, SKSize ImageSize);

public static class PdfLayout
{
    public static readonly SKSize Page = new(595, 842);  // A4 in points
    public const float Margin = 50;
    public static float ContentWidth => Page.Width - Margin * 2;
    public static float ContentHeight => Page.Height - Margin * 2;
    public const int MaxImagePixels = 1600;
    /// Keeps two typical steps on one page.
    public const float MaxImageHeight = 270;
    public const float TitleGap = 18;
    public const float HeadingGap = 6;
    public const float TextGap = 8;
    public const float StepGap = 22;

    /// Lays steps out top-down, starting a new page when heading, text and screenshot would not fit together.
    public static IReadOnlyList<PlacedStep> Paginate(float titleHeight, IReadOnlyList<StepBlock> steps)
    {
        var placed = new List<PlacedStep>(steps.Count);
        int page = 0;
        float y = Margin + titleHeight + TitleGap;
        foreach (var step in steps)
        {
            // Scale to the content width, but never taller than what fits beside the text on one page.
            var size = new SKSize(ContentWidth, ContentWidth * step.ImagePixels.Height / step.ImagePixels.Width);
            float maxHeight = Math.Min(MaxImageHeight, ContentHeight - step.TextHeight);
            if (size.Height > maxHeight) size = new SKSize(size.Width * maxHeight / size.Height, maxHeight);
            bool atPageTop = y == Margin;
            if (step.TextHeight + size.Height > Page.Height - Margin - y && !atPageTop)
            {
                page++;
                y = Margin;
            }
            placed.Add(new PlacedStep(page, y, size));
            y += step.TextHeight + size.Height + StepGap;
        }
        return placed;
    }
}

public static class Pdf
{
    static readonly SKColor Black = SKColors.Black;
    static readonly SKColor Gray = new(64, 64, 64);

    static bool IsCjk(char c) => c >= '⺀';

    /// Wraps text to `maxWidth`: at spaces for Latin text, between any two CJK characters,
    /// and inside a word only when the word alone is too wide. Blank paragraphs stay as empty lines.
    public static IReadOnlyList<string> WrapLines(string text, SKFont font, float maxWidth)
    {
        var lines = new List<string>();
        foreach (var paragraph in text.Replace("\r\n", "\n").Split('\n'))
        {
            var line = "";
            foreach (var unit in Units(paragraph))
            {
                if (font.MeasureText(line + unit) <= maxWidth)
                {
                    line += unit;
                    continue;
                }
                if (line.TrimEnd().Length > 0) lines.Add(line.TrimEnd());
                line = unit == " " ? "" : unit;
                while (font.MeasureText(line) > maxWidth)
                {
                    int fit = 1;
                    while (fit < line.Length && font.MeasureText(line[..(fit + 1)]) <= maxWidth) fit++;
                    lines.Add(line[..fit]);
                    line = line[fit..];
                }
            }
            lines.Add(line.TrimEnd());
        }
        return lines;
    }

    /// Breakable units: runs of Latin characters, single spaces and single CJK characters.
    static IEnumerable<string> Units(string s)
    {
        int i = 0;
        while (i < s.Length)
        {
            if (s[i] == ' ' || IsCjk(s[i]))
            {
                yield return s[i++].ToString();
                continue;
            }
            int start = i;
            while (i < s.Length && s[i] != ' ' && !IsCjk(s[i])) i++;
            yield return s[start..i];
        }
    }

    /// The step's screenshot as drawn in the PDF: blur and marks burned in, longer side at most
    /// 1600 px, JPEG quality 80.
    public static byte[] PageImage(Step step)
    {
        using var small = Imaging.Thumbnail(Export.ImageData(step), PdfLayout.MaxImagePixels);
        using var data = small.Encode(SKEncodedImageFormat.Jpeg, 80);
        return data.ToArray();
    }

    sealed class TextBlock(IReadOnlyList<string> lines, SKFont font, SKColor color)
    {
        public float Height => lines.Count * font.Spacing;

        public void Draw(SKCanvas canvas, float top)
        {
            using var paint = new SKPaint { Color = color, IsAntialias = true };
            float baseline = top - font.Metrics.Ascent;
            foreach (var line in lines)
            {
                canvas.DrawText(line, PdfLayout.Margin, baseline, SKTextAlign.Left, font, paint);
                baseline += font.Spacing;
            }
        }
    }

    public static byte[] Render(Tutorial tutorial)
    {
        var lang = ContentLanguage.Current;
        var title = tutorial.Title.Trim();
        if (title.Length == 0) title = lang.UntitledTitle;
        using var titleFont = new SKFont(Fonts.Get(true), 24);
        using var headingFont = new SKFont(Fonts.Get(true), 15);
        using var bodyFont = new SKFont(Fonts.Get(false), 12);
        float width = PdfLayout.ContentWidth;

        var titleBlock = new TextBlock(WrapLines(title, titleFont, width), titleFont, Black);
        var headings = new List<TextBlock>();
        var bodies = new List<TextBlock?>();
        var images = new List<SKImage>();
        var blocks = new List<StepBlock>();
        try
        {
            for (int i = 0; i < tutorial.Steps.Count; i++)
            {
                var step = tutorial.Steps[i];
                var heading = new TextBlock([lang.StepHeading(i + 1)], headingFont, Black);
                var text = step.Description.Trim();
                var body = text.Length == 0 ? null : new TextBlock(WrapLines(text, bodyFont, width), bodyFont, Gray);
                // Decoded lazily from the JPEG bytes, so the PDF embeds the JPEG stream itself.
                var image = SKImage.FromEncodedData(PageImage(step)) ?? throw new ExportException(lang.ImageProcessingFailed);
                headings.Add(heading);
                bodies.Add(body);
                images.Add(image);
                float textHeight = heading.Height + PdfLayout.HeadingGap + (body is null ? 0 : body.Height + PdfLayout.TextGap);
                blocks.Add(new StepBlock(textHeight, new SKSize(image.Width, image.Height)));
            }

            var placed = PdfLayout.Paginate(titleBlock.Height, blocks);
            using var stream = new SKDynamicMemoryWStream();
            using (var doc = SKDocument.CreatePdf(stream, new SKDocumentPdfMetadata { Title = title, Creator = "ClickDoc" }))
            {
                int page = 0;
                var canvas = doc.BeginPage(PdfLayout.Page.Width, PdfLayout.Page.Height);
                titleBlock.Draw(canvas, PdfLayout.Margin);
                for (int i = 0; i < placed.Count; i++)
                {
                    var p = placed[i];
                    if (p.Page != page)
                    {
                        doc.EndPage();
                        canvas = doc.BeginPage(PdfLayout.Page.Width, PdfLayout.Page.Height);
                        page = p.Page;
                    }
                    float y = p.Top;
                    headings[i].Draw(canvas, y);
                    y += headings[i].Height + PdfLayout.HeadingGap;
                    if (bodies[i] is { } body)
                    {
                        body.Draw(canvas, y);
                        y += body.Height + PdfLayout.TextGap;
                    }
                    float x = PdfLayout.Margin + (width - p.ImageSize.Width) / 2;
                    canvas.DrawImage(images[i], SKRect.Create(x, y, p.ImageSize.Width, p.ImageSize.Height),
                                     new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
                }
                doc.EndPage();
                doc.Close();
            }
            using var data = stream.DetachAsData();
            return data.ToArray();
        }
        finally
        {
            foreach (var image in images) image.Dispose();
        }
    }

    public static string ExportPdf(Tutorial tutorial, string dir)
    {
        var path = Export.UniqueFile(dir, Export.SanitizeName(tutorial.Title), "pdf", p => File.Exists(p) || Directory.Exists(p));
        var bytes = Render(tutorial);
        var temp = path + ".partial";
        File.WriteAllBytes(temp, bytes);
        File.Move(temp, path);
        return path;
    }
}
