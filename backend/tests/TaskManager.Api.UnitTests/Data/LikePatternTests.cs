using TaskManager.Api.Data;

namespace TaskManager.Api.UnitTests.Data;

public sealed class LikePatternTests
{
    [Theory]
    [InlineData("milk", "milk")]
    [InlineData("100%", "100\\%")]
    [InlineData("a_b", "a\\_b")]
    [InlineData("back\\slash", "back\\\\slash")]
    [InlineData("[x]", "[x]")]
    [InlineData("%_\\[", "\\%\\_\\\\[")]
    public void Escape_Sqlite_EscapesBackslashPercentUnderscore_ButNotBracket(string input, string expected)
    {
        Assert.Equal(expected, LikePattern.Escape(input, isSqlServer: false));
    }

    [Theory]
    [InlineData("milk", "milk")]
    [InlineData("100%", "100\\%")]
    [InlineData("a_b", "a\\_b")]
    [InlineData("back\\slash", "back\\\\slash")]
    [InlineData("[x]", "\\[x]")]
    [InlineData("%_\\[", "\\%\\_\\\\\\[")]
    public void Escape_SqlServer_AlsoEscapesOpeningBracket(string input, string expected)
    {
        Assert.Equal(expected, LikePattern.Escape(input, isSqlServer: true));
    }

    [Fact]
    public void Contains_WrapsEscapedTextInPercentSigns()
    {
        Assert.Equal("%50\\%%", LikePattern.Contains("50%", isSqlServer: false));
    }

    [Fact]
    public void EscapeCharacter_IsBackslash()
    {
        Assert.Equal("\\", LikePattern.EscapeCharacter);
    }
}
