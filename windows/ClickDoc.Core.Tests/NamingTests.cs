using System.Diagnostics;
using static ClickDoc.Core.Tests.TestSupport;

namespace ClickDoc.Core.Tests;

public class NamingTests
{
    [Fact]
    public void DescribeFull()
    {
        using var _ = InLanguage(ContentLanguage.Chinese);
        Assert.Equal("在 Safari 中点击「新建标签页」", Naming.DescribeStep(ClickAction.Click, "Safari", "新建标签页"));
        Assert.Equal("在 Finder 中双击「文稿」", Naming.DescribeStep(ClickAction.DoubleClick, "Finder", "文稿"));
        Assert.Equal("在 Finder 中右键点击「文稿」", Naming.DescribeStep(ClickAction.RightClick, "Finder", "文稿"));
    }

    [Fact]
    public void DescribeMissingParts()
    {
        using var _ = InLanguage(ContentLanguage.Chinese);
        Assert.Equal("在 Safari 中点击", Naming.DescribeStep(ClickAction.Click, "Safari", null));
        Assert.Equal("在 Safari 中点击", Naming.DescribeStep(ClickAction.Click, "Safari", "  "));
        Assert.Equal("点击「确定」", Naming.DescribeStep(ClickAction.Click, null, "确定"));
        Assert.Equal("点击", Naming.DescribeStep(ClickAction.Click, "", null));
        Assert.Equal("点击", Naming.DescribeStep(ClickAction.Click, null, null));
    }

    [Fact]
    public void DescribeTruncates()
    {
        using var _ = InLanguage(ContentLanguage.Chinese);
        Assert.Equal("在 X 中点击「" + new string('a', 40) + "…」", Naming.DescribeStep(ClickAction.Click, "X", new string('a', 50)));
        var forty = new string('b', 40);
        Assert.Equal($"在 X 中点击「{forty}」", Naming.DescribeStep(ClickAction.Click, "X", forty));
    }

    [Fact]
    public void EnglishDescriptions()
    {
        using var _ = InLanguage(ContentLanguage.English);
        Assert.Equal("Click \"New Tab\" in Safari", Naming.DescribeStep(ClickAction.Click, "Safari", "New Tab"));
        Assert.Equal("Double-click \"Documents\" in Finder", Naming.DescribeStep(ClickAction.DoubleClick, "Finder", "Documents"));
        Assert.Equal("Right-click in Finder", Naming.DescribeStep(ClickAction.RightClick, "Finder", null));
        Assert.Equal("Click \"OK\"", Naming.DescribeStep(ClickAction.Click, null, "OK"));
        Assert.Equal("Click", Naming.DescribeStep(ClickAction.Click, null, null));
    }

    [Fact]
    public void PickElementName_PriorityAndParent()
    {
        Assert.Equal("保存", Naming.PickElementName(new("Button", " 保存 ", "帮助", "按钮"), null));
        Assert.Equal("帮助", Naming.PickElementName(new("Button", "", "帮助", "按钮"), null));
        Assert.Equal("工具栏", Naming.PickElementName(new("Image", null, null, "图像"), new("ToolBar", "工具栏", null, null)));
        Assert.Equal("图像", Naming.PickElementName(new("Image", null, null, "图像"), new("Pane", null, null, null)));
        Assert.Null(Naming.PickElementName(new("Group", "  ", null, null), null));
    }

    [Fact]
    public void PickElementName_TextInputFallsBackToControlType()
    {
        // UIA keeps a text box's content in the Value pattern, which is never read.
        Assert.Equal("编辑", Naming.PickElementName(new("Edit", null, null, "编辑"), null));
    }

    [Fact]
    public void AppName_UsesWindowTitleForFrameHost()
    {
        Assert.Equal("计算器", Naming.AppName("Application Frame Host", @"C:\Windows\System32\ApplicationFrameHost.exe", "计算器"));
        Assert.Equal("Google Chrome", Naming.AppName("Google Chrome", @"C:\x\chrome.exe", "t"));
        Assert.Equal("notepad", Naming.AppName("  ", @"C:\Windows\notepad.exe", "t"));
        Assert.Null(Naming.AppName(null, null, null));
    }

    [Fact]
    public void RunWithTimeout_ReturnsDefaultWhenSlow()
    {
        var sw = Stopwatch.StartNew();
        Assert.Null(Naming.RunWithTimeout<string>(() => { Thread.Sleep(2000); return "x"; }, TimeSpan.FromMilliseconds(250)));
        Assert.True(sw.ElapsedMilliseconds < 600);
        Assert.Equal("ok", Naming.RunWithTimeout(() => "ok", TimeSpan.FromMilliseconds(250)));
    }
}
