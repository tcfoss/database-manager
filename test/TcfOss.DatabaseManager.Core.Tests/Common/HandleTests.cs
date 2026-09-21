using TcfOss.DatabaseManager.Core.Common;

namespace TcfOss.DatabaseManager.Core.Tests.Common;

public class HandleTests
{
    private static Handle GetHandle(string nameScope, string name, QuoteStyle quoteStyle = QuoteStyle.Ansi, NameHandling nameHandling = NameHandling.LowercaseUnlessQuoted)
    {
        var nameId = new Identifier(name, quoteStyle);
        var scopeId = new Identifier(nameScope, quoteStyle);
        return Handle.Create(nameId, scopeId, nameHandling);
    }

    // === GetNamePart with various NameHandling values ===

    [Theory]
    [InlineData(NameHandling.Uppercase, QuoteStyle.None, "HELLO")]
    [InlineData(NameHandling.Uppercase, QuoteStyle.Backticks, "HELLO")]
    [InlineData(NameHandling.UppercaseUnlessQuoted, QuoteStyle.None, "HELLO")]
    [InlineData(NameHandling.UppercaseUnlessQuoted, QuoteStyle.Backticks, "Hello")]
    [InlineData(NameHandling.UppercaseUnlessQuoted, QuoteStyle.Ansi, "Hello")]
    [InlineData(NameHandling.Lowercase, QuoteStyle.None, "hello")]
    [InlineData(NameHandling.Lowercase, QuoteStyle.Backticks, "hello")]
    [InlineData(NameHandling.LowercaseUnlessQuoted, QuoteStyle.None, "hello")]
    [InlineData(NameHandling.LowercaseUnlessQuoted, QuoteStyle.Backticks, "Hello")]
    [InlineData(NameHandling.LowercaseUnlessQuoted, QuoteStyle.Ansi, "Hello")]
    public void GetNamePart_Variants(NameHandling handling, QuoteStyle quoteStyle, string expected)
    {
        var identifier = new Identifier("Hello", quoteStyle);

        string result = Handle.GetNamePart(identifier, handling);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetNamePart_InvalidNameHandling_Throws()
    {
        var identifier = new Identifier("Hello");
        Assert.Throws<ArgumentOutOfRangeException>(() => Handle.GetNamePart(identifier, (NameHandling)999));
    }

    // === Comparison operators ===

    [Fact]
    public void CompareTo_Equal()
    {
        Handle a = GetHandle("scope", "name");
        Handle b = GetHandle("scope", "name");

        Assert.Equal(0, a.CompareTo(b));
    }

    [Fact]
    public void CompareTo_DifferentScopes()
    {
        Handle a = GetHandle("scope1", "name");
        Handle b = GetHandle("scope2", "name");

        Assert.True(a.CompareTo(b) < 0);
        Assert.True(b.CompareTo(a) > 0);
    }

    [Fact]
    public void CompareTo_DifferentNames()
    {
        Handle a = GetHandle("scope", "name1");
        Handle b = GetHandle("scope", "name2");

        Assert.True(a.CompareTo(b) < 0);
        Assert.True(b.CompareTo(a) > 0);
    }

    [Fact]
    public void CompareTo_DifferentScopesAndNames()
    {
        Handle a = GetHandle("scope1", "name1");
        Handle b = GetHandle("scope2", "name2");

        Assert.True(a.CompareTo(b) < 0);
        Assert.True(b.CompareTo(a) > 0);
    }

    [Fact]
    public void LessThan_Operator()
    {
        Handle a = GetHandle("scope", "name1");
        Handle b = GetHandle("scope", "name2");
        Handle a2 = GetHandle("scope", "name1");

        Assert.True(a < b);
        Assert.False(b < a);
        Assert.False(a < a2);
    }

    [Fact]
    public void LessThanOrEqual_Operator()
    {
        Handle a = GetHandle("scope", "name1");
        Handle b = GetHandle("scope", "name2");
        Handle a2 = GetHandle("scope", "name1");

        Assert.True(a <= b);
        Assert.True(a <= a2);
        Assert.False(b <= a);
    }

    [Fact]
    public void GreaterThan_Operator()
    {
        Handle a = GetHandle("scope", "name1");
        Handle b = GetHandle("scope", "name2");
        Handle a2 = GetHandle("scope", "name1");

        Assert.True(b > a);
        Assert.False(a > b);
        Assert.False(a > a2);
    }

    [Fact]
    public void GreaterThanOrEqual_Operator()
    {
        Handle a = GetHandle("scope", "name1");
        Handle b = GetHandle("scope", "name2");
        Handle a2 = GetHandle("scope", "name1");

        Assert.True(b >= a);
        Assert.True(a >= a2);
        Assert.False(a >= b);
    }
}
