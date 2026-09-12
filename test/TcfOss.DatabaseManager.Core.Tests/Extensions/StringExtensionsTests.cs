using TcfOss.DatabaseManager.Core.Extensions;

namespace TcfOss.DatabaseManager.Core.Tests.Extensions;

public class StringExtensionsTests
{
    // === EscapeQuotedString ===

    [Fact]
    public void EscapeQuotedString_Null_Returns_Null()
    {
        string? result = ((string?)null).EscapeQuotedString('\'');

        Assert.Null(result);
    }

    [Fact]
    public void EscapeQuotedString_Empty_Returns_Null()
    {
        string? result = "".EscapeQuotedString('\'');

        Assert.Null(result);
    }

    [Fact]
    public void EscapeQuotedString_No_Quotes_Returns_Same()
    {
        string? result = "hello world".EscapeQuotedString('\'');

        Assert.Equal("hello world", result);
    }

    [Fact]
    public void EscapeQuotedString_Single_Quote_Gets_Doubled()
    {
        string? result = "it's".EscapeQuotedString('\'');

        Assert.Equal("it''s", result);
    }

    [Fact]
    public void EscapeQuotedString_Already_Doubled_Quote_Preserved()
    {
        // Input has '' (already escaped pair) — the method recognizes this and preserves it
        string? result = "a''b".EscapeQuotedString('\'');

        Assert.Equal("a''b", result);
    }

    [Fact]
    public void EscapeQuotedString_Backslash_Escaped_Quote_Preserved()
    {
        // A backslash-escaped quote should be preserved as-is (not doubled)
        string? result = "a\\'b".EscapeQuotedString('\'');

        Assert.Equal("a\\'b", result);
    }

    [Fact]
    public void EscapeQuotedString_Double_Quote_Character()
    {
        string? result = "say \"hello\"".EscapeQuotedString('"');

        Assert.Equal("say \"\"hello\"\"", result);
    }

    // === EscapeSingleQuotedString ===

    [Fact]
    public void EscapeSingleQuotedString_Escapes_Single_Quotes()
    {
        string? result = "it's a test".EscapeSingleQuotedString();

        Assert.Equal("it''s a test", result);
    }
}
