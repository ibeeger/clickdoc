namespace ClickDoc.Core.Tests;

public class SmokeTests
{
    [Fact]
    public void Core_Loads() => Assert.NotNull(typeof(Steps).Assembly);
}
