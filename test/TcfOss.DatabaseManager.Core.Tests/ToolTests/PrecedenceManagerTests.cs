using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Parsing;

namespace TcfOss.DatabaseManager.Core.Tests.ToolTests;

public static class PrecedenceManagerTests
{
    [Theory]
    [InlineData(Precedence.DoubleColon, Precedence.AtTimezone, Label = "'::' > 'AT TZ'")]
    [InlineData(Precedence.AtTimezone, Precedence.MultiplyDivide, Label = "'AT TZ' > '*/'")]
    [InlineData(Precedence.MultiplyDivide, Precedence.AddSubtract, Label = "'*/' > '+-'")]
    [InlineData(Precedence.AddSubtract, Precedence.LogicalXor, Label = "'+-' > 'XOR'")]
    [InlineData(Precedence.AddSubtract, Precedence.StringConcat, Label = "'+-' > '||'")]
    [InlineData(Precedence.AddSubtract, Precedence.PgOperator)]
    [InlineData(Precedence.PgOperator, Precedence.Between)]
    [InlineData(Precedence.LogicalXor, Precedence.BitwiseAnd, Label = "'XOR' > '&'")]
    [InlineData(Precedence.BitwiseAnd, Precedence.BitwiseXor, Label = "'&' > '^'")]
    [InlineData(Precedence.BitwiseXor, Precedence.BitwiseOr, Label = "'^' > '|'")]
    [InlineData(Precedence.BitwiseOr, Precedence.Between, Label = "'|' > 'BETWEEN'")]
    [InlineData(Precedence.StringConcat, Precedence.Between, Label = "'||' > 'BETWEEN'")]
    [InlineData(Precedence.Between, Precedence.Comparison)]
    [InlineData(Precedence.Like, Precedence.Is, Label = "'LIKE' > 'IS'")]
    [InlineData(Precedence.Is, Precedence.UnaryNot, Label = "'IS' > 'NOT'")]
    [InlineData(Precedence.UnaryNot, Precedence.LogicalAnd, Label = "'NOT' > 'AND'")]
    [InlineData(Precedence.LogicalAnd, Precedence.LogicalOr, Label = "'AND' > 'OR'")]
    public static void Relative_Precedence(Precedence greater, Precedence less)
    {
        var pm = new PrecedenceManager();

        var greaterPrec = pm.GetPrecedence(greater);
        var lessPrec = pm.GetPrecedence(less);

        Assert.True(greaterPrec > lessPrec);
    }

    [Fact]
    public static void Same_Precedence_Between_Like()
    {
        var pm = new PrecedenceManager();
        Assert.Equal(pm.GetPrecedence(Precedence.Between), pm.GetPrecedence(Precedence.Like));
    }

    [Theory]
    [InlineData("or", Precedence.LogicalOr, null, Label = "OR")]
    [InlineData("and", Precedence.LogicalAnd, null, Label = "AND")]
    [InlineData("xor", Precedence.LogicalXor, null, Label = "XOR")]
    [InlineData("@", Precedence.LogicalOr, 0, Label = "unknown operator")]
    [InlineData("@>", Precedence.PgOperator, null, Label = "custom operator")]
    [InlineData("not", Precedence.LogicalOr, 0, Label = "NOT")]
    [InlineData("is", Precedence.Is, null, Label = "IS")]
    [InlineData("in", Precedence.Between, null, Label = "IN")]
    [InlineData("between", Precedence.Between, null, Label = "BETWEEN")]
    [InlineData("operator", Precedence.PgOperator, null, Label = "OPERATOR")]
    [InlineData("like", Precedence.Like, null, Label = "LIKE")]
    [InlineData("ilike", Precedence.Like, null, Label = "ILIKE")]
    [InlineData("regexp", Precedence.Like, null, Label = "REGEXP")]
    [InlineData("rlike", Precedence.Like, null, Label = "RLIKE")]
    [InlineData("similar", Precedence.LogicalOr, 0, Label = "SIMILAR without TO")]
    [InlineData("similar to", Precedence.Like, null, Label = "SIMILAR TO")]
    [InlineData("div", Precedence.MultiplyDivide, null, Label = "DIV")]
    [InlineData("=", Precedence.Comparison, null, Label = "=")]
    [InlineData("<>", Precedence.Comparison, null, Label = "<>")]
    [InlineData("<", Precedence.Comparison, null, Label = "<")]
    [InlineData("<=", Precedence.Comparison, null, Label = "<=")]
    [InlineData(">", Precedence.Comparison, null, Label = ">")]
    [InlineData(">=", Precedence.Comparison, null, Label = ">=")]
    [InlineData("+", Precedence.AddSubtract, null, Label = "+")]
    [InlineData("-", Precedence.AddSubtract, null, Label = "-")]
    [InlineData("*", Precedence.MultiplyDivide, null, Label = "*")]
    [InlineData("/", Precedence.MultiplyDivide, null, Label = "/")]
    [InlineData("%", Precedence.MultiplyDivide, null, Label = "%")]
    [InlineData("::", Precedence.DoubleColon, null, Label = "::")]
    [InlineData("||", Precedence.StringConcat, null, Label = "||")]
    [InlineData("|", Precedence.BitwiseOr, null, Label = "|")]
    [InlineData("^", Precedence.BitwiseXor, null, Label = "^")]
    [InlineData("&", Precedence.BitwiseAnd, null, Label = "&")]
    [InlineData("~", Precedence.PgOperator, null, Label = "~")]
    [InlineData("~*", Precedence.PgOperator, null, Label = "~*")]
    [InlineData("!~", Precedence.PgOperator, null, Label = "!~")]
    [InlineData("!~*", Precedence.PgOperator, null, Label = "!~*")]
    public static void Next_Precedence_By_Keyword_String(string keyword, Precedence expectedPrecedence, int? overrideExpected)
    {
        var state = new ParserState([.. new GenericLexer().Tokenize(keyword)]);

        var pm = new PrecedenceManager();

        var actual = pm.GetNextPrecedence(state);
        short expected = (short?)overrideExpected ?? pm.GetPrecedence(expectedPrecedence);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public static void DoublePipe_Precedence_IsStringConcat()
    {
        var state = new ParserState([.. new GenericLexer().Tokenize("||")]);
        var pm = new PrecedenceManager();

        Assert.Equal(pm.GetPrecedence(Precedence.StringConcat), pm.GetNextPrecedence(state));
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
