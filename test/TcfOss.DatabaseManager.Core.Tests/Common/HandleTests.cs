using System.Text;
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
    [InlineData(NameHandling.None, QuoteStyle.None, "Hello")]
    [InlineData(NameHandling.None, QuoteStyle.Ansi, "Hello")]
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
        Assert.Equal(expected, Handle.GetNamePart(identifier.Name, identifier.QuoteStyle, handling));
    }

    [Theory]
    [InlineData(QuoteStyle.None, "A\u00c9\u0130\u03a9Z", "a\u00c9\u0130\u03a9z")]
    [InlineData(QuoteStyle.Ansi, "A\u00c9\u0130\u03a9Z", "A\u00c9\u0130\u03a9Z")]
    [InlineData(QuoteStyle.None, "E\u0301", "e\u0301")]
    public void GetNamePart_PostgresFoldsOnlyUnquotedAsciiLetters(QuoteStyle quoteStyle, string input, string expected)
    {
        Assert.Equal(expected, Handle.GetNamePart(input, quoteStyle, NameHandling.Postgres));
    }

    [Theory]
    [InlineData("x", 1)]
    [InlineData("\u00e9", 2)]
    [InlineData("\u4e2d", 3)]
    [InlineData("\U0001f600", 4)]
    public void GetNamePart_PostgresClipsAtUtf8CharacterBoundaries(string character, int utf8Length)
    {
        foreach (QuoteStyle quoteStyle in new[] { QuoteStyle.None, QuoteStyle.Ansi })
        {
            string prefix = new('A', 63 - utf8Length);
            string expectedPrefix = quoteStyle == QuoteStyle.None ? prefix.ToLowerInvariant() : prefix;
            string exact = Handle.GetNamePart(prefix + character, quoteStyle, NameHandling.Postgres);
            string longer = Handle.GetNamePart(prefix + character + "tail", quoteStyle, NameHandling.Postgres);
            string split = Handle.GetNamePart(prefix + "A" + character, quoteStyle, NameHandling.Postgres);

            Assert.Equal(expectedPrefix + character, exact);
            Assert.Equal(63, Encoding.UTF8.GetByteCount(exact));
            Assert.Equal(exact, longer);
            Assert.Equal(expectedPrefix + (quoteStyle == QuoteStyle.None ? "a" : "A"), split);
            Assert.True(Encoding.UTF8.GetByteCount(split) <= 63);
        }
    }

    [Theory]
    [InlineData("Mixed", true)]
    [InlineData("\u00c9cho", false)]
    [InlineData("mixed", false)]
    public void RequiresQuotingForCase_PostgresPreservesNonAsciiCase(string name, bool expected)
    {
        Assert.Equal(expected, Handle.RequiresQuotingForCase(new Identifier(name), NameHandling.Postgres));
    }

    [Fact]
    public void GetNamePart_InvalidNameHandling_Throws()
    {
        var identifier = new Identifier("Hello");
        Assert.Throws<ArgumentOutOfRangeException>(() => Handle.GetNamePart(identifier, (NameHandling)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => Handle.GetNamePart(identifier.Name, identifier.QuoteStyle, (NameHandling)999));
    }

    [Fact]
    public void RequiresQuotingForCase_InvalidNameHandling_Throws()
    {
        var identifier = new Identifier("Hello");
        var invalidHandling = (NameHandling)999;

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => Handle.RequiresQuotingForCase(identifier, invalidHandling));

        Assert.Equal("nameHandling", exception.ParamName);
        Assert.Equal(invalidHandling, exception.ActualValue);
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
