using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Parsing;
using BinaryOperatorExp = TcfOss.DatabaseManager.Core.Expressions.BinaryOperator;
using BinaryOperatorOp = TcfOss.DatabaseManager.Core.BuiltIn.BinaryOperator;

namespace TcfOss.DatabaseManager.Core.Tests.ParserTests;

// ReSharper disable ClassNeverInstantiated.Global
public class ExpressionParserTests : ParserTestsBase<GenericLexer, Parser>
{
    [Theory]
    [InlineData("x.y.*")]
    [InlineData("x IS NULL")]
    [InlineData("x.y IS NULL")]
    [InlineData("x IS TRUE")]
    [InlineData("a + b IS FALSE")]
    [InlineData("CONCAT(a, b) IS NOT UNKNOWN")]
    [InlineData("CONCAT(a, b) = 'mystring'")]
    [InlineData("CONCAT(first = a, second = b) = 'mystring'")]
    [InlineData("CONCAT(first := a, second := b) = 'mystring'")]
    [InlineData("CONCAT(first => a, second => b) = 'mystring'")]
    [InlineData("a IS DISTINCT FROM b")]
    [InlineData("x IS NOT NULL")]
    [InlineData("x BETWEEN 5 AND 10")]
    [InlineData("CASE WHEN x > 5 THEN TRUE END")]
    [InlineData("CASE WHEN x > 5 THEN TRUE WHEN x < 5 THEN FALSE ELSE NULL END")]
    [InlineData("CASE x WHEN 1 THEN TRUE WHEN 2 THEN FALSE ELSE NULL END")]
    [InlineData("x >= ALL(SELECT colname FROM tablename)")]
    [InlineData("x <> ANY(SELECT colname FROM tablename)")]
    [InlineData("x <= SOME(SELECT colname FROM tablename)")]
    [InlineData("x > ALL((1, 2, 3))")]
    [InlineData("GROUP_CONCAT(DISTINCT author_name ORDER BY author_name DESC SEPARATOR ', ')", Label = "GROUP_CONCAT ORDER BY")]
    [InlineData("GROUP_CONCAT(DISTINCT author_name LIMIT 10 SEPARATOR ', ')", Label = "GROUP_CONCAT LIMIT")]
    [InlineData("GROUP_CONCAT(author_name SEPARATOR ', ')")]
    [InlineData("GROUP_CONCAT(author_name ORDER BY author_name LIMIT 10 SEPARATOR ', ')")]
    [InlineData("COUNT(*) OVER ()")]
    [InlineData("COUNT(*) OVER (PARTITION BY category)")]
    [InlineData("COUNT(*) OVER (ORDER BY category)")]
    [InlineData("x NOT BETWEEN 1 AND 3")]
    [InlineData("x NOT IN (1, 2)")]
    [InlineData("x NOT LIKE 'pattern'")]
    [InlineData("x IS NOT DISTINCT FROM y")]
    [InlineData("EXISTS (SELECT 1 FROM my_table)")]
    [InlineData("NOT EXISTS (SELECT 1 FROM my_table)")]
    [InlineData("'2025-06-28T12:00:00' AT TIME ZONE 'Eastern Standard Time'", Label = "AT TIME ZONE long TZ")]
    [InlineData("+3 > 0")]
    [InlineData("-3 < 0")]
    [InlineData("4 IN (3, 4, 5)")]
    [InlineData("4 NOT IN (SELECT pk FROM my_table)")]
    [InlineData("NOT (a OR b)")]
    [InlineData("COUNT(*)")]
    [InlineData("COUNT(table.*)")]
    [InlineData("CHAR(3) 'Hello'")]
    [InlineData("x LIKE y ESCAPE '_'")]
    [InlineData("x NOT LIKE ANY ('pat1', 'pat2', 'pat3')")]
    [InlineData("x REGEXP 'pattern'")]
    [InlineData("x NOT REGEXP 'pattern'")]
    [InlineData("x RLIKE 'pattern'")]
    [InlineData("x NOT RLIKE 'pattern'")]
    [InlineData("POSITION('sub' IN 'substring')")]
    [InlineData("POSITION('x', 'not', 'a', 'real', 'function')", Label = "POSITION many args")]
    [InlineData("x % 2 = 0")]
    [InlineData("a AND b")]
    [InlineData("a OR b")]
    [InlineData("a XOR b")]
    [InlineData("4 + INTERVAL 5 SECOND_MICROSECOND")]
    [InlineData("4 + INTERVAL 5 MINUTE_MICROSECOND")]
    [InlineData("4 + INTERVAL 5 MINUTE_SECOND")]
    [InlineData("4 + INTERVAL 5 HOUR_MICROSECOND")]
    [InlineData("4 + INTERVAL 5 HOUR_SECOND")]
    [InlineData("4 + INTERVAL 5 HOUR_MINUTE")]
    [InlineData("4 + INTERVAL 5 DAY_MICROSECOND")]
    [InlineData("4 + INTERVAL 5 DAY_SECOND")]
    [InlineData("4 + INTERVAL 5 DAY_MINUTE")]
    [InlineData("4 + INTERVAL 5 DAY_HOUR")]
    [InlineData("4 + INTERVAL 5 YEAR_MONTH")]
    [InlineData("NOT (a + b)")]
    [InlineData("!(a + b)", "NOT (a + b)")]
    public static void ParseExpr_TextMatch(string sql, string? realExpected = null)
    {
        var parser = new Parser();
        var (expected, actual) = GetExpectedActual(parser.ExpressionParser.ParseExpr, sql);

        Assert.Equal(realExpected ?? expected, actual);
    }

    [Theory]
    [InlineData("x * ALL(SELECT colname FROM tablename)")]
    [InlineData("x + ANY(SELECT colname FROM tablename)")]
    [InlineData("x AND SOME(SELECT colname FROM tablename)")]
    public static void Quantifier_Non_Comparison_Op_Throws(string sql)
    {
        var parser = new Parser();
        var state = GetState(sql);
        var exception = Assert.Throws<ParseException.ExpectedButFound>(() => parser.ExpressionParser.ParseExpr(state));
        var expectedTemplate = "Expected {0}. Found {1}.";
        var found = sql[2..sql.IndexOf(' ', 2)];
        var expected = string.Format(expectedTemplate, "comparison_operator".Italic(), found);
        Assert.Equal(expected, exception.Message);
    }

    [Fact]
    public void ParseWildcardOrExpression_TextMatch()
    {
        var parser = new Parser();
        var (expected, actual) = GetExpectedActual(parser.ExpressionParser.ParseWildcardOrExpr, "*", "*");

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ParseWildcardOrExpression_InvalidToken_Throws()
    {
        var parser = new Parser();
        var ex = Assert.Throws<ParseException.ExpectedButFound>(() => parser.ExpressionParser.ParseWildcardOrExpr(GetState("mytable.;")));
        Assert.Contains("identifier or .*", ex.Message);
    }

    [Theory]
    [InlineData("a.")]
    [InlineData("a.+")]
    [InlineData("INTERVAL 1 invalid_unit")]
    [InlineData("()")]
    public void MalformedExpression_ThrowsExpectedTokenException(string sql)
    {
        var parser = new Parser();
        ParserState state = GetState(sql);

        Assert.Throws<ParseException.ExpectedButFound>(() => parser.ExpressionParser.ParseExpr(state));
    }

    [Theory]
    [InlineData("x IS DISTINCT y")]
    [InlineData("x IS")]
    [InlineData("x IS NOT invalid_predicate")]
    public void MalformedIsPredicate_ThrowsExpectedAlternativesException(string sql)
    {
        var parser = new Parser();
        ParserState state = GetState(sql);

        Assert.Throws<ParseException.ExpectedOneOfButFound>(() => parser.ExpressionParser.ParseExpr(state));
    }

    [Fact]
    public void ParseEmptyParens_Throws()
    {
        var parser = new Parser();
        var state = GetState("()");

        var ex = Assert.Throws<ParseException.ExpectedButFound>(() => parser.ExpressionParser.ParseExpr(state));
        Assert.Contains("expression", ex.Message);
    }

    [Fact]
    public static void And_Higher_Precedence_Than_Or()
    {
        var parser = new Parser();
        var state = GetState("a AND b OR c");

        var actual = parser.ExpressionParser.ParseExpr(state);

        var actualOp = Assert.IsType<BinaryOperatorExp>(actual);
        Assert.Equal(BinaryOperatorOp.Or, actualOp.Operator);

        state = GetState("a OR b AND c");
        actual = parser.ExpressionParser.ParseExpr(state);

        var actualOp2 = Assert.IsType<BinaryOperatorExp>(actual);
        Assert.Equal(BinaryOperatorOp.Or, actualOp2.Operator);
    }

    [Fact]
    public void Similar_MissingTo_Throws()
    {
        var parser = new Parser();
        var state = GetState("x SIMILAR");

        var x = Assert.Throws<ParseException.ExpectedButFound>(() => parser.ExpressionParser.ParseExpr(state));
        Assert.Contains("TO", x.Message);
    }

    [Theory]
    [InlineData("x ILIKE 'pattern'")]
    [InlineData("x SIMILAR TO 'pattern'")]
    public void NotYetImplemented_Throws(string sql)
    {
        var parser = new Parser();
        var state = GetState(sql);

        Assert.Throws<NotImplementedException>(() => parser.ExpressionParser.ParseExpr(state));
    }
}
