using static ClickDoc.Core.Tests.TestSupport;

namespace ClickDoc.Core.Tests;

public class ContentLanguageTests
{
    [Theory]
    [InlineData("zh-CN", ContentLanguage.Chinese)]
    [InlineData("zh-Hans", ContentLanguage.Chinese)]
    [InlineData("zh-TW", ContentLanguage.Chinese)]
    [InlineData("en-US", ContentLanguage.English)]
    [InlineData("ja-JP", ContentLanguage.English)]
    [InlineData("", ContentLanguage.English)]
    [InlineData(null, ContentLanguage.English)]
    public void Resolve(string? culture, ContentLanguage expected) => Assert.Equal(expected, ContentLanguage.Resolve(culture));

    [Fact]
    public void DefaultsPerLanguage()
    {
        using (InLanguage(ContentLanguage.English))
        {
            Assert.Equal("Untitled Tutorial", new Tutorial().Title);
            Assert.Equal("Step 3", ContentLanguage.Current.StepHeading(3));
        }
        using (InLanguage(ContentLanguage.Chinese))
        {
            Assert.Equal("未命名教程", new Tutorial().Title);
            Assert.Equal("步骤 3", ContentLanguage.Current.StepHeading(3));
        }
    }
}
