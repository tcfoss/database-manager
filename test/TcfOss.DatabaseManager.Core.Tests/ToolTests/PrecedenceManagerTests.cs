using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Parsing;

namespace TcfOss.DatabaseManager.Core.Tests.ToolTests;

public static class PrecedenceManagerTests
{
    [Theory]
    [InlineData(Precedence.DoubleColon, Precedence.AtTimezone)]
    [InlineData(Precedence.AtTimezone, Precedence.MultiplyDivide)]
    [InlineData(Precedence.MultiplyDivide, Precedence.AddSubtract)]
    [InlineData(Precedence.AddSubtract, Precedence.Xor)]
    [InlineData(Precedence.Xor, Precedence.Ampersand)]
    [InlineData(Precedence.Ampersand, Precedence.Caret)]
    [InlineData(Precedence.Caret, Precedence.Pipe)]
    [InlineData(Precedence.Pipe, Precedence.Between)]
    [InlineData(Precedence.Between, Precedence.Like)]
    [InlineData(Precedence.Like, Precedence.Is)]
    [InlineData(Precedence.Is, Precedence.UnaryNot)]
    [InlineData(Precedence.UnaryNot, Precedence.And)]
    [InlineData(Precedence.And, Precedence.Or)]
    public static void Relative_Precedence(Precedence greater, Precedence less)
    {
        var pm = new PrecedenceManager();

        var greaterPrec = pm.GetPrecedence(greater);
        var lessPrec = pm.GetPrecedence(less);

        Assert.True(greaterPrec > lessPrec);
    }

    [Fact]
    public static void Same_Precedence_Between_Equals()
    {
        var pm = new PrecedenceManager();
        Assert.Equal(pm.GetPrecedence(Precedence.Between), pm.GetPrecedence(Precedence.Equals));
    }

    [Theory]
    [InlineData("or", Precedence.Or, null)]
    [InlineData("and", Precedence.And, null)]
    [InlineData("xor", Precedence.Xor, null)]
    [InlineData("@", Precedence.Or, 0)]
    [InlineData("not", Precedence.Or, 0)]
    [InlineData("is", Precedence.Is, null)]
    [InlineData("in", Precedence.Between, null)]
    [InlineData("between", Precedence.Between, null)]
    [InlineData("operator", Precedence.Between, null)]
    [InlineData("like", Precedence.Like, null)]
    [InlineData("ilike", Precedence.Like, null)]
    [InlineData("regexp", Precedence.Like, null)]
    [InlineData("rlike", Precedence.Like, null)]
    [InlineData("div", Precedence.MultiplyDivide, null)]
    [InlineData("=", Precedence.Between, null)]
    [InlineData("<>", Precedence.Between, null)]
    [InlineData("<", Precedence.Between, null)]
    [InlineData("<=", Precedence.Between, null)]
    [InlineData(">", Precedence.Between, null)]
    [InlineData(">=", Precedence.Between, null)]
    [InlineData("+", Precedence.AddSubtract, null)]
    [InlineData("-", Precedence.AddSubtract, null)]
    [InlineData("*", Precedence.MultiplyDivide, null)]
    [InlineData("/", Precedence.MultiplyDivide, null)]
    [InlineData("%", Precedence.MultiplyDivide, null)]
    public static void Next_Precedence_By_Keyword_String(string keyword, Precedence expectedPrecedence, int? overrideExpected)
    {
        var state = new ParserState([.. new GenericLexer().Tokenize(keyword)]);

        var pm = new PrecedenceManager();

        var actual = pm.GetNextPrecedence(state);
        short expected = (short?)overrideExpected ?? pm.GetPrecedence(expectedPrecedence);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public static void At_Precedence_Time_Zone_Match()
    {
        var state = new ParserState([.. new GenericLexer().Tokenize("AT TIME ZONE")]);
        var pm = new PrecedenceManager();

        var actual = pm.GetNextPrecedence(state);
        var expected = pm.GetPrecedence(Precedence.AtTimezone);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public static void At_Precedence_Time_Zone_Failure()
    {
        var state = new ParserState([.. new GenericLexer().Tokenize("AT SELECT")]);
        var pm = new PrecedenceManager();

        var actual = pm.GetNextPrecedence(state);
        var expected = 0;

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("NOT IN ('a', 'b', 'c')")]
    [InlineData("NOT BETWEEN a AND b")]
    public static void Not_In_Between(string text)
    {
        var state = new ParserState([.. new GenericLexer().Tokenize(text)]);
        var pm = new PrecedenceManager();

        var actual = pm.GetNextPrecedence(state);
        var expected = pm.GetPrecedence(Precedence.Between);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("NOT LIKE 'dog%'")]
    [InlineData("NOT ILIKE 'dog%'")]
    [InlineData("NOT SIMILAR 'dog%'")]
    [InlineData("NOT REGEXP 'dog%'")]
    [InlineData("NOT RLIKE 'dog%'")]
    public static void Not_Like_Etc(string text)
    {
        var state = new ParserState([.. new GenericLexer().Tokenize(text)]);
        var pm = new PrecedenceManager();

        var actual = pm.GetNextPrecedence(state);
        var expected = pm.GetPrecedence(Precedence.Like);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("NOT NULL")]
    [InlineData("NOT (a > b)")]
    public static void Not_Other(string text)
    {
        var state = new ParserState([.. new GenericLexer().Tokenize(text)]);
        var pm = new PrecedenceManager();

        var actual = pm.GetNextPrecedence(state);
        var expected = 0;

        Assert.Equal(expected, actual);
    }
}
