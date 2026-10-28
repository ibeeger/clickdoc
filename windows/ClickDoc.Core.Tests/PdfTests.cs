using System.Text;
using System.Text.RegularExpressions;
using static ClickDoc.Core.Tests.Pixels;
using static ClickDoc.Core.Tests.TestSupport;

namespace ClickDoc.Core.Tests;

public sealed class PdfTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("clickdoc-pdf-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    static Tutorial Make(string title, params Step[] steps)
    {
        var t = new Tutorial { Title = title };
        foreach (var s in steps) t.Append(s);
        return t;
    }

    static Step Shot(string description, int w, int h) => MakeStep(description) with { Png = Imaging.EncodePng(Blank(w, h)) };

    static int PageCount(byte[] pdf) => Regex.Matches(Encoding.Latin1.GetString(pdf), @"/Type\s*/Page\b(?!s)").Count;

    static bool Contains(byte[] haystack, byte[] needle) => haystack.AsSpan().IndexOf(needle) >= 0;

    [Fact]
    public void Paginate_TwoTypicalStepsPerPage()
    {
        var steps = Enumerable.Repeat(new StepBlock(50, new SKSize(1000, 1000)), 3).ToList();
        var placed = PdfLayout.Paginate(32, steps);
        Assert.Equal([0, 0, 1], placed.Select(p => p.Page));
        Assert.Equal([100f, 442f, 50f], placed.Select(p => p.Top));
        Assert.All(placed, p => Assert.Equal(new SKSize(270, 270), p.ImageSize));
    }

    [Fact]
    public void Paginate_WideImageUsesContentWidth()
    {
        var placed = PdfLayout.Paginate(32, [new StepBlock(50, new SKSize(1980, 990))]);
        Assert.Equal(new SKSize(495, 247.5f), placed[0].ImageSize);
    }

    [Fact]
    public void Paginate_VeryTallImageFitsOnePage()
    {
        var placed = PdfLayout.Paginate(32, [new StepBlock(50, new SKSize(400, 6000))]);
        Assert.Equal(0, placed[0].Page);
        Assert.Equal(270, placed[0].ImageSize.Height, 3);
        Assert.Equal(18, placed[0].ImageSize.Width, 3);
    }

    [Fact]
    public void Paginate_LongTextLimitsImageHeight()
    {
        // 700pt of text leaves 42pt for the image on a page.
        var placed = PdfLayout.Paginate(32, [new StepBlock(50, new SKSize(100, 100)), new StepBlock(700, new SKSize(1000, 1000))]);
        Assert.Equal(1, placed[1].Page);
        Assert.Equal(42, placed[1].ImageSize.Height, 3);
    }

    [Fact]
    public void WrapLines_BreaksLongWordAndCjk()
    {
        using var font = new SKFont(Fonts.Get(false), 12);
        foreach (var text in new[] { new string('a', 400), "https://example.com/" + new string('x', 300), new string('中', 300), "hello 世界 " + new string('b', 200) })
        {
            var lines = Pdf.WrapLines(text, font, 495);
            Assert.True(lines.Count > 1);
            Assert.All(lines, line => Assert.True(font.MeasureText(line) <= 495.5f, line));
            Assert.Equal(text.Replace(" ", ""), string.Concat(lines).Replace(" ", ""));
        }
        Assert.Equal(["a", "", "b"], Pdf.WrapLines("a\n\nb", font, 495));
    }

    [Fact]
    public void WrapLines_BreaksAtSpaces()
    {
        using var font = new SKFont(Fonts.Get(false), 12);
        var words = string.Join(" ", Enumerable.Repeat("word", 100));
        Assert.All(Pdf.WrapLines(words, font, 495), line => Assert.Matches("^(word ?)+$", line));
    }

    [Fact]
    public void RenderIsA4PdfWithPages()
    {
        var pdf = Pdf.Render(Make("如何新建标签页", Shot("在 Safari 中点击「新建标签页」", 1440, 900), Shot("  ", 1440, 900)));
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
        Assert.Matches(@"/MediaBox\s*\[0 0 595 842\]", Encoding.Latin1.GetString(pdf));
        Assert.Equal(1, PageCount(pdf));
    }

    [Fact]
    public void RenderPaginatesTallContent()
    {
        var steps = Enumerable.Range(1, 6).Select(i => Shot($"第 {i} 步", 1000, 1000)).ToArray();
        Assert.Equal(3, PageCount(Pdf.Render(Make("T", steps))));
    }

    [Fact]
    public void RenderScalesVeryTallImageToFitPage()
    {
        Assert.True(PageCount(Pdf.Render(Make("T", Shot("长图", 400, 6000)))) <= 2);
    }

    [Fact]
    public void RenderEmbedsBlurredJpeg()
    {
        var s = MakeStep("x") with { Png = Imaging.EncodePng(Stripes(600, 400)) };
        var blurred = s with { BlurRegions = [SKRect.Create(0, 0, 0.5f, 0.5f)] };
        var plainJpeg = Pdf.PageImage(s);
        var blurredJpeg = Pdf.PageImage(blurred);
        Assert.NotEqual(plainJpeg, blurredJpeg);
        Assert.Equal(0xFF, blurredJpeg[0]);
        Assert.Equal(0xD8, blurredJpeg[1]);
        Assert.True(Contains(Pdf.Render(Make("T", blurred)), blurredJpeg), "JPEG embedded as-is");
    }

    [Fact]
    public void PageImageIsBounded()
    {
        var jpeg = Pdf.PageImage(Shot("x", 4000, 1000));
        using var image = SKBitmap.Decode(jpeg);
        Assert.Equal(1600, image.Width);
        Assert.Equal(400, image.Height);
    }

    [Fact]
    public void ExportPdfFileNaming()
    {
        var t = Make("a/b", Shot("x", 100, 100));
        Assert.Equal("a-b.pdf", Path.GetFileName(Pdf.ExportPdf(t, _dir)));
        var second = Pdf.ExportPdf(t, _dir);
        Assert.Equal("a-b 2.pdf", Path.GetFileName(second));
        Assert.StartsWith("%PDF", File.ReadAllText(second)[..4]);
    }
}
