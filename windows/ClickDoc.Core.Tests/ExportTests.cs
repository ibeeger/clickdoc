using System.IO.Compression;
using static ClickDoc.Core.Tests.Pixels;
using static ClickDoc.Core.Tests.TestSupport;

namespace ClickDoc.Core.Tests;

public sealed class ExportTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("clickdoc-export-").FullName;
    readonly IDisposable _lang = InLanguage(ContentLanguage.Chinese);

    public void Dispose()
    {
        _lang.Dispose();
        Directory.Delete(_dir, recursive: true);
    }

    static Tutorial Make(string title, params Step[] steps)
    {
        var t = new Tutorial { Title = title };
        foreach (var s in steps) t.Append(s);
        return t;
    }

    static Step WithPng(string description, byte[] png) => MakeStep(description) with { Png = png };

    [Fact]
    public void ImageFileName()
    {
        Assert.Equal("step-01.png", Export.ImageFileName(1));
        Assert.Equal("step-09.png", Export.ImageFileName(9));
        Assert.Equal("step-12.png", Export.ImageFileName(12));
        Assert.Equal("step-100.png", Export.ImageFileName(100));
    }

    [Fact]
    public void RenderMarkdown()
    {
        var t = Make("演示", MakeStep("第一步"), MakeStep("第二步"));
        Assert.Equal("# 演示\n\n## 步骤 1\n\n第一步\n\n![步骤 1](images/step-01.png)\n\n## 步骤 2\n\n第二步\n\n![步骤 2](images/step-02.png)\n",
                     Export.RenderMarkdown(t));
    }

    [Fact]
    public void MarkdownSkipsEmptyDescription()
    {
        Assert.Equal("# T\n\n## 步骤 1\n\n![步骤 1](images/step-01.png)\n", Export.RenderMarkdown(Make("T", MakeStep("  "))));
    }

    [Fact]
    public void EnglishMarkdownAndDefaults()
    {
        using var _ = InLanguage(ContentLanguage.English);
        Assert.Equal("# Demo\n\n## Step 1\n\nFirst\n\n![Step 1](images/step-01.png)\n", Export.RenderMarkdown(Make("Demo", MakeStep("First"))));
        Assert.Equal("Untitled Tutorial", Export.SanitizeName("  "));
    }

    [Theory]
    [InlineData("a/b:c", "a-b-c")]
    [InlineData("a\\b", "a-b")]
    [InlineData("   ", "未命名教程")]
    [InlineData("", "未命名教程")]
    [InlineData(".hidden", "hidden")]
    [InlineData("  我的教程 ", "我的教程")]
    [InlineData("a\nb", "a-b")]
    public void SanitizeFolderName(string input, string expected) => Assert.Equal(expected, Export.SanitizeName(input));

    [Theory]
    [InlineData("CON", "CON_")]
    [InlineData("nul", "nul_")]
    [InlineData("com1", "com1_")]
    [InlineData("LPT9", "LPT9_")]
    [InlineData("con.txt", "con_.txt")]
    [InlineData("console", "console")]
    [InlineData("a?b*c", "a-b-c")]
    [InlineData("x<y>z|\"q\"", "x-y-z--q-")]
    [InlineData("报告.", "报告")]
    [InlineData("  报告 . . ", "报告")]
    [InlineData("a/b:c\\d", "a-b-c-d")]
    public void Sanitize_WindowsRules(string input, string expected) => Assert.Equal(expected, Export.SanitizeName(input));

    [Fact]
    public void Sanitize_TruncatesTo100() => Assert.Equal(100, Export.SanitizeName(new string('长', 200)).Length);

    [Fact]
    public void Sanitize_EmptyFallsBack() => Assert.Equal("未命名教程", Export.SanitizeName(" ... "));

    [Fact]
    public void UniqueNames()
    {
        var taken = new HashSet<string> { "X", "X 2", "Z.zip" };
        Assert.Equal("X 3", Path.GetFileName(Export.UniqueFolder("/tmp/x", "X", p => taken.Contains(Path.GetFileName(p)))));
        Assert.Equal("Y", Path.GetFileName(Export.UniqueFolder("/tmp/x", "Y", p => taken.Contains(Path.GetFileName(p)))));
        Assert.Equal("Z 2.zip", Path.GetFileName(Export.UniqueFile("/tmp/x", "Z", "zip", p => taken.Contains(Path.GetFileName(p)))));
    }

    [Fact]
    public void ExportWritesFiles()
    {
        var t = Make("导出/测试", WithPng("一", [1, 2, 3]), WithPng("二", [4, 5]));
        var out_ = Export.ExportTutorial(t, _dir);
        Assert.Equal("导出-测试", Path.GetFileName(out_));
        Assert.Equal(Export.RenderMarkdown(t), File.ReadAllText(Path.Combine(out_, "tutorial.md")));
        Assert.Equal([1, 2, 3], File.ReadAllBytes(Path.Combine(out_, "images", "step-01.png")));
        Assert.Equal([4, 5], File.ReadAllBytes(Path.Combine(out_, "images", "step-02.png")));
        var again = Export.ExportTutorial(t, _dir);
        Assert.EndsWith(" 2", again);
    }

    [Fact]
    public void ExportBurnsInBlur()
    {
        var png = Imaging.EncodePng(Stripes(200, 100));
        var blurred = WithPng("blur", png) with { BlurRegions = [SKRect.Create(0, 0, 1, 1)] };
        var out_ = Export.ExportTutorial(Make("B", blurred, WithPng("plain", png)), _dir);
        var first = Imaging.DecodePng(File.ReadAllBytes(Path.Combine(out_, "images", "step-01.png")));
        Assert.Equal(png, File.ReadAllBytes(Path.Combine(out_, "images", "step-02.png")));
        Assert.True(RedSpread(first, 60, 140, 50) < 40, "blur burned in");
    }

    [Fact]
    public void ExportImageAppliesAnnotations()
    {
        var s = WithPng("x", Imaging.EncodePng(Blank(1000, 500))) with
        {
            Annotations = [new Annotation(Guid.NewGuid(), new RectangleMark(SKRect.Create(0.25f, 0.25f, 0.5f, 0.5f)))],
        };
        var img = Imaging.DecodePng(Export.ImageData(s));
        Assert.True(IsRed(img.GetPixel(500, 125)));
        Assert.True(IsWhite(img.GetPixel(500, 250)));
    }

    [Fact]
    public void ImageDataFailsOnBrokenPng()
    {
        var s = WithPng("x", [1, 2, 3]) with { BlurRegions = [SKRect.Create(0, 0, 1, 1)] };
        Assert.Equal("处理截图失败", Assert.Throws<ExportException>(() => Export.ImageData(s)).Message);
    }

    [Fact]
    public void ExportNotionZip()
    {
        var t = Make("演示", WithPng("x", Imaging.EncodePng(Blank(100, 100))));
        var zip = Export.ExportNotionZip(t, _dir);
        Assert.Equal("演示.zip", Path.GetFileName(zip));
        Assert.Equal(["演示.zip"], Directory.GetFileSystemEntries(_dir).Select(Path.GetFileName));
        using (var archive = ZipFile.OpenRead(zip))
        {
            Assert.Equal(["演示.md", "images/step-01.png"], archive.Entries.Select(e => e.FullName));
            using var md = new StreamReader(archive.Entries[0].Open());
            Assert.Equal(Export.RenderMarkdown(t), md.ReadToEnd());
        }
        Assert.All(CentralDirectoryFlags(File.ReadAllBytes(zip)), f => Assert.True((f & 0x0800) != 0, "UTF-8 name flag"));
    }

    [Fact]
    public void ExportNotionZipNameIsUnique()
    {
        var t = Make("Z", WithPng("x", Imaging.EncodePng(Blank(10, 10))));
        Export.ExportNotionZip(t, _dir);
        Assert.Equal("Z 2.zip", Path.GetFileName(Export.ExportNotionZip(t, _dir)));
    }

    /// General-purpose flags of every central directory entry.
    static List<int> CentralDirectoryFlags(byte[] b)
    {
        var flags = new List<int>();
        for (int i = 0; i + 46 <= b.Length; i++)
        {
            if (b[i] == 0x50 && b[i + 1] == 0x4b && b[i + 2] == 0x01 && b[i + 3] == 0x02) flags.Add(b[i + 8] | b[i + 9] << 8);
        }
        Assert.NotEmpty(flags);
        return flags;
    }
}
