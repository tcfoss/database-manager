using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Parsing;

namespace TcfOss.DatabaseManager.Core.Tests.ToolTests;

public static class PrecedenceManagerTests
{
    [Theory]
    [InlineData(Precedence.DoubleColon, Precedence.AtTimezone, Label = "'::' > 'AT TZ'")]
    [InlineData(Precedence.AtTimezone, Precedence.MultiplyDivide, Label = "'AT TZ' > '*/'")]
    [InlineData(Precedence.MultiplyDivide, Precedence.AddSubtract, Label = "'*/' > '+-'")]
    [InlineData(Precedence.AddSubtract, Precedence.Xor, Label = "'+-' > 'XOR'")]
    [InlineData(Precedence.Xor, Precedence.Ampersand, Label = "'XOR' > '&'")]
    [InlineData(Precedence.Ampersand, Precedence.Caret, Label = "'&' > '^'")]
    [InlineData(Precedence.Caret, Precedence.Pipe, Label = "'^' > '|'")]
    [InlineData(Precedence.Pipe, Precedence.Between, Label = "'|' > 'BETWEEN'")]
    [InlineData(Precedence.Between, Precedence.Like, Label = "'BETWEEN' > 'LIKE'")]
    [InlineData(Precedence.Like, Precedence.Is, Label = "'LIKE' > 'IS'")]
    [InlineData(Precedence.Is, Precedence.UnaryNot, Label = "'IS' > 'NOT'")]
    [InlineData(Precedence.UnaryNot, Precedence.And, Label = "'NOT' > 'AND'")]
    [InlineData(Precedence.And, Precedence.Or, Label = "'AND' > 'OR'")]
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
    [InlineData("or", Precedence.Or, null, Label = "OR")]
    [InlineData("and", Precedence.And, null, Label = "AND")]
    [InlineData("xor", Precedence.Xor, null, Label = "XOR")]
    [InlineData("@", Precedence.Or, 0, Label = "unknown operator")]
    [InlineData("not", Precedence.Or, 0, Label = "NOT")]
    [InlineData("is", Precedence.Is, null, Label = "IS")]
    [InlineData("in", Precedence.Between, null, Label = "IN")]
    [InlineData("between", Precedence.Between, null, Label = "BETWEEN")]
    [InlineData("operator", Precedence.Between, null, Label = "OPERATOR")]
    [InlineData("like", Precedence.Like, null, Label = "LIKE")]
    [InlineData("ilike", Precedence.Like, null, Label = "ILIKE")]
    [InlineData("regexp", Precedence.Like, null, Label = "REGEXP")]
    [InlineData("rlike", Precedence.Like, null, Label = "RLIKE")]
    [InlineData("similar", Precedence.Like, null, Label = "SIMILAR")]
    [InlineData("div", Precedence.MultiplyDivide, null, Label = "DIV")]
    [InlineData("=", Precedence.Between, null, Label = "=")]
    [InlineData("<>", Precedence.Between, null, Label = "<>")]
    [InlineData("<", Precedence.Between, null, Label = "<")]
    [InlineData("<=", Precedence.Between, null, Label = "<=")]
    [InlineData(">", Precedence.Between, null, Label = ">")]
    [InlineData(">=", Precedence.Between, null, Label = ">=")]
    [InlineData("+", Precedence.AddSubtract, null, Label = "+")]
    [InlineData("-", Precedence.AddSubtract, null, Label = "-")]
    [InlineData("*", Precedence.MultiplyDivide, null, Label = "*")]
    [InlineData("/", Precedence.MultiplyDivide, null, Label = "/")]
    [InlineData("%", Precedence.MultiplyDivide, null, Label = "%")]
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
    [InlineData("NOT SIMILAR TO 'dog%'")]
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
