namespace ClickDoc.Core.Tests;

public class StringsTests
{
    [Fact]
    public void ParsesMacEnglishTable()
    {
        var table = StringsFile.Parse(File.ReadAllText(RepoPaths.MacEnglishStrings));
        Assert.True(table.Count >= 60, $"{table.Count} entries");
        Assert.Equal("Stop Recording", table["停止录制"]);
        Assert.Equal("Delete %lld steps?", table["删除 %lld 个步骤？"]);
    }

    [Fact]
    public void ParsesEscapesAndComments()
    {
        var table = StringsFile.Parse("/* c */\n\"a \\\"q\\\"\" = \"b\\nc\";\n// line\n\"x\"=\"y\";");
        Assert.Equal("b\nc", table["a \"q\""]);
        Assert.Equal("y", table["x"]);
    }

    [Fact]
    public void FormatReplacesPlaceholdersInOrder()
    {
        Assert.Equal("删除 3 个步骤？", Localizer.Format("删除 %lld 个步骤？", 3));
        Assert.Equal("a 1 b x", Localizer.Format("a %lld b %@", 1, "x"));
        Assert.Equal("100%", Localizer.Format("100%"));
    }
}
