using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Parsing;

using BinaryOperatorExp = TcfOss.DatabaseManager.Core.Expressions.BinaryOperator;
using BinaryOperatorOp = TcfOss.DatabaseManager.Core.BuiltIn.BinaryOperator;

namespace TcfOss.DatabaseManager.Core.Tests.ParserTests;

// ReSharper disable ClassNeverInstantiated.Global
public class ExpressionParserTests : ParserTestsBase<GenericLexer, Parser>
{
    /* Identifiers and wildcards */
    [Theory]
    [InlineData("x.y.*", Label = "qualified wildcard")]
    public static void ParseExpr_IdentifiersAndWildcards_TextMatch(string sql, string? realExpected = null)
    {
        var parser = new Parser();
        var (expected, actual) = GetExpectedActual(parser.ExpressionParser.ParseExpr, sql);

        Assert.Equal(realExpected ?? expected, actual);
    }

    /* IS and comparison predicates */
    [Theory]
    [InlineData("x IS NULL", Label = "is null")]
    [InlineData("x.y IS NULL", Label = "qualified is null")]
    [InlineData("x IS TRUE", Label = "is true")]
    [InlineData("a + b IS FALSE", Label = "expression is false")]
    [InlineData("CONCAT(a, b) IS NOT UNKNOWN", Label = "is not unknown")]
    [InlineData("CONCAT(a, b) = 'mystring'", Label = "function equality")]
    [InlineData("CONCAT(first = a, second = b) = 'mystring'", Label = "function named arguments equals")]
    [InlineData("CONCAT(first := a, second := b) = 'mystring'", Label = "function named arguments assign")]
    [InlineData("CONCAT(first => a, second => b) = 'mystring'", Label = "function named arguments arrow")]
    [InlineData("a IS DISTINCT FROM b", Label = "is distinct from")]
    [InlineData("x IS NOT NULL", Label = "is not null")]
    [InlineData("x IS NOT DISTINCT FROM y", Label = "is not distinct from")]
    public static void ParseExpr_IsAndComparisonPredicates_TextMatch(string sql, string? realExpected = null)
    {
        var parser = new Parser();
        var (expected, actual) = GetExpectedActual(parser.ExpressionParser.ParseExpr, sql);

        Assert.Equal(realExpected ?? expected, actual);
    }

    /* Range and membership predicates */
    [Theory]
    [InlineData("x BETWEEN 5 AND 10", Label = "between")]
    [InlineData("x NOT BETWEEN 1 AND 3", Label = "not between")]
    [InlineData("4 IN (3, 4, 5)", Label = "in values")]
    [InlineData("4 NOT IN (SELECT pk FROM my_table)", Label = "not in subquery")]
    [InlineData("x NOT IN (1, 2)", Label = "not in values")]
    public static void ParseExpr_RangeAndMembershipPredicates_TextMatch(string sql, string? realExpected = null)
    {
        var parser = new Parser();
        var (expected, actual) = GetExpectedActual(parser.ExpressionParser.ParseExpr, sql);

        Assert.Equal(realExpected ?? expected, actual);
    }

    /* CASE expressions */
    [Theory]
    [InlineData("CASE WHEN x > 5 THEN TRUE END", Label = "searched case")]
    [InlineData("CASE WHEN x > 5 THEN TRUE WHEN x < 5 THEN FALSE ELSE NULL END", Label = "searched case branches")]
    [InlineData("CASE x WHEN 1 THEN TRUE WHEN 2 THEN FALSE ELSE NULL END", Label = "simple case branches")]
    public static void ParseExpr_CaseExpressions_TextMatch(string sql, string? realExpected = null)
    {
        var parser = new Parser();
        var (expected, actual) = GetExpectedActual(parser.ExpressionParser.ParseExpr, sql);

        Assert.Equal(realExpected ?? expected, actual);
    }

    /* Quantified comparisons */
    [Theory]
    [InlineData("x >= ALL(SELECT colname FROM tablename)", Label = "greater equal all subquery")]
    [InlineData("x <> ANY(SELECT colname FROM tablename)", Label = "not equal any subquery")]
    [InlineData("x <= SOME(SELECT colname FROM tablename)", Label = "less equal some subquery")]
    [InlineData("x > ALL((1, 2, 3))", Label = "greater all values")]
    public static void ParseExpr_QuantifiedComparisons_TextMatch(string sql, string? realExpected = null)
    {
        var parser = new Parser();
        var (expected, actual) = GetExpectedActual(parser.ExpressionParser.ParseExpr, sql);

        Assert.Equal(realExpected ?? expected, actual);
    }

    /* Aggregate and window functions */
    [Theory]
    [InlineData("GROUP_CONCAT(DISTINCT author_name ORDER BY author_name DESC SEPARATOR ', ')", Label = "group concat order by")]
    [InlineData("GROUP_CONCAT(DISTINCT author_name LIMIT 10 SEPARATOR ', ')", Label = "group concat limit")]
    [InlineData("GROUP_CONCAT(author_name SEPARATOR ', ')", Label = "group concat separator")]
    [InlineData("GROUP_CONCAT(author_name ORDER BY author_name LIMIT 10 SEPARATOR ', ')", Label = "group concat order and limit")]
    [InlineData("COUNT(*)", Label = "count wildcard")]
    [InlineData("COUNT(table.*)", Label = "count qualified wildcard")]
    [InlineData("COUNT(*) OVER ()", Label = "window empty")]
    [InlineData("COUNT(*) OVER (PARTITION BY category)", Label = "window partition")]
    [InlineData("COUNT(*) OVER (ORDER BY category)", Label = "window order")]
    public static void ParseExpr_AggregateAndWindowFunctions_TextMatch(string sql, string? realExpected = null)
    {
        var parser = new Parser();
        var (expected, actual) = GetExpectedActual(parser.ExpressionParser.ParseExpr, sql);

        Assert.Equal(realExpected ?? expected, actual);
    }

    /* Existence predicates */
    [Theory]
    [InlineData("EXISTS (SELECT 1 FROM my_table)", Label = "exists subquery")]
    [InlineData("NOT EXISTS (SELECT 1 FROM my_table)", Label = "not exists subquery")]
    public static void ParseExpr_ExistencePredicates_TextMatch(string sql, string? realExpected = null)
    {
        var parser = new Parser();
        var (expected, actual) = GetExpectedActual(parser.ExpressionParser.ParseExpr, sql);

        Assert.Equal(realExpected ?? expected, actual);
    }

    /* Pattern predicates */
    [Theory]
    [InlineData("x LIKE y ESCAPE '_'", Label = "like escape")]
    [InlineData("x NOT LIKE 'pattern'", Label = "not like")]
    [InlineData("x NOT LIKE ANY ('pat1', 'pat2', 'pat3')", Label = "not like any")]
    [InlineData("x REGEXP 'pattern'", Label = "regexp")]
    [InlineData("x NOT REGEXP 'pattern'", Label = "not regexp")]
    [InlineData("x RLIKE 'pattern'", Label = "rlike")]
    [InlineData("x NOT RLIKE 'pattern'", Label = "not rlike")]
    public static void ParseExpr_PatternPredicates_TextMatch(string sql, string? realExpected = null)
    {
        var parser = new Parser();
        var (expected, actual) = GetExpectedActual(parser.ExpressionParser.ParseExpr, sql);

        Assert.Equal(realExpected ?? expected, actual);
    }

    /* Function calls and typed literals */
    [Theory]
    [InlineData("CHAR(3) 'Hello'", Label = "typed character literal")]
    [InlineData("POSITION('sub' IN 'substring')", Label = "position in syntax")]
    [InlineData("POSITION('x', 'not', 'a', 'real', 'function')", Label = "position many arguments")]
    [InlineData("'2025-06-28T12:00:00' AT TIME ZONE 'Eastern Standard Time'", Label = "at time zone")]
    public static void ParseExpr_FunctionCallsAndTypedLiterals_TextMatch(string sql, string? realExpected = null)
    {
        var parser = new Parser();
        var (expected, actual) = GetExpectedActual(parser.ExpressionParser.ParseExpr, sql);

        Assert.Equal(realExpected ?? expected, actual);
    }

    /* Arithmetic, comparison, and logical operators */
    [Theory]
    [InlineData("+3 > 0", Label = "unary plus comparison")]
    [InlineData("-3 < 0", Label = "unary minus comparison")]
    [InlineData("x % 2 = 0", Label = "modulo comparison")]
    [InlineData("a || b", Label = "string concatenation")]
    [InlineData("a | b ^ c & d", Label = "bitwise precedence")]
    [InlineData("a || b | c", Label = "bitwise binds tighter than postgres concatenation")]
    [InlineData("a AND b", Label = "and")]
    [InlineData("a OR b", Label = "or")]
    [InlineData("a XOR b", Label = "xor")]
    public static void ParseExpr_ArithmeticComparisonAndLogicalOperators_TextMatch(string sql, string? realExpected = null)
    {
        var parser = new Parser();
        var (expected, actual) = GetExpectedActual(parser.ExpressionParser.ParseExpr, sql);

        Assert.Equal(realExpected ?? expected, actual);
    }

    [Fact]
    public void ParseExpr_SymbolicOperatorPrecedence()
    {
        var bitwise = Assert.IsType<BinaryOperatorExp>(ParseExpression("a | b ^ c & d"));
        Assert.Equal(BinaryOperatorOp.BitwiseOr, bitwise.Operator);
        var xor = Assert.IsType<BinaryOperatorExp>(bitwise.Right);
        Assert.Equal(BinaryOperatorOp.BitwiseXor, xor.Operator);
        var and = Assert.IsType<BinaryOperatorExp>(xor.Right);
        Assert.Equal(BinaryOperatorOp.BitwiseAnd, and.Operator);

        var concat = Assert.IsType<BinaryOperatorExp>(ParseExpression("a || b | c"));
        Assert.Equal(BinaryOperatorOp.StringConcat, concat.Operator);
        Assert.Equal(BinaryOperatorOp.BitwiseOr, Assert.IsType<BinaryOperatorExp>(concat.Right).Operator);
    }

    [Theory]
    [InlineData("a ~ 'pattern'", "~")]
    [InlineData("a ~* 'pattern'", "~*")]
    [InlineData("a !~ 'pattern'", "!~")]
    [InlineData("a !~* 'pattern'", "!~*")]
    public void ParseExpr_TildeRegexOperators(string sql, string symbolOperator)
    {
        var expression = Assert.IsType<BinaryOperatorExp>(ParseExpression(sql));

        Assert.Equal(symbolOperator, expression.Operator.ToString());
        Assert.Equal(sql, expression.ToSql());
        Assert.Equal(expression, ParseExpression(expression.ToSql()));
    }

    [Theory]
    [InlineData("~")]
    [InlineData("~*")]
    [InlineData("!~")]
    [InlineData("!~*")]
    public void ParseExpr_TildeRegexOperatorPrecedence(string symbolOperator)
    {
        var logical = Assert.IsType<BinaryOperatorExp>(ParseExpression($"a {symbolOperator} b || c AND d"));
        Assert.Equal(BinaryOperatorOp.And, logical.Operator);
        var concat = Assert.IsType<BinaryOperatorExp>(logical.Left);
        Assert.Equal(BinaryOperatorOp.StringConcat, concat.Operator);
        var regex = Assert.IsType<BinaryOperatorExp>(concat.Left);
        Assert.Equal(symbolOperator, regex.Operator.ToString());
    }

    /* INTERVAL expressions */
    [Theory]
    [InlineData("4 + INTERVAL 5 SECOND_MICROSECOND", Label = "interval second microsecond")]
    [InlineData("4 + INTERVAL 5 MINUTE_MICROSECOND", Label = "interval minute microsecond")]
    [InlineData("4 + INTERVAL 5 MINUTE_SECOND", Label = "interval minute second")]
    [InlineData("4 + INTERVAL 5 HOUR_MICROSECOND", Label = "interval hour microsecond")]
    [InlineData("4 + INTERVAL 5 HOUR_SECOND", Label = "interval hour second")]
    [InlineData("4 + INTERVAL 5 HOUR_MINUTE", Label = "interval hour minute")]
    [InlineData("4 + INTERVAL 5 DAY_MICROSECOND", Label = "interval day microsecond")]
    [InlineData("4 + INTERVAL 5 DAY_SECOND", Label = "interval day second")]
    [InlineData("4 + INTERVAL 5 DAY_MINUTE", Label = "interval day minute")]
    [InlineData("4 + INTERVAL 5 DAY_HOUR", Label = "interval day hour")]
    [InlineData("4 + INTERVAL 5 YEAR_MONTH", Label = "interval year month")]
    public static void ParseExpr_IntervalExpressions_TextMatch(string sql, string? realExpected = null)
    {
        var parser = new Parser();
        var (expected, actual) = GetExpectedActual(parser.ExpressionParser.ParseExpr, sql);

        Assert.Equal(realExpected ?? expected, actual);
    }

    /* Unary logical expressions */
    [Theory]
    [InlineData("NOT (a OR b)", Label = "not grouped expression")]
    [InlineData("NOT (a + b)", Label = "not arithmetic expression")]
    [InlineData("!(a + b)", "NOT (a + b)", Label = "bang as not")]
    public static void ParseExpr_UnaryLogicalExpressions_TextMatch(string sql, string? realExpected = null)
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
    public void Similar_WithoutTo_IsNotTreatedAsAnOperator()
    {
        var parser = new Parser();
        var state = GetState("x SIMILAR");
        Assert.Equal("x", parser.ExpressionParser.ParseExpr(state).ToSql());
        Assert.True(state.PeekKeyword(Keyword.SIMILAR));
    }

    [Theory]
    [InlineData("x ILIKE 'pattern'", false, false, null)]
    [InlineData("x NOT ILIKE 'pattern' ESCAPE '!'", true, false, "!")]
    [InlineData("x ILIKE ANY ('one', 'two')", false, true, null)]
    public void ParseExpr_ILike(string sql, bool negated, bool any, string? escapeChar)
    {
        ILike expression = Assert.IsType<ILike>(ParseExpression(sql));

        Assert.Equal(negated, expression.Negated);
        Assert.Equal(any, expression.Any);
        Assert.Equal(escapeChar, expression.EscapeChar);
        Assert.Equal(sql, expression.ToSql());
    }

    [Theory]
    [InlineData("x SIMILAR TO 'pattern'", false, false, null)]
    [InlineData("x NOT SIMILAR TO 'pattern' ESCAPE '!'", true, false, "!")]
    [InlineData("x SIMILAR TO ANY ('one', 'two')", false, true, null)]
    public void ParseExpr_SimilarTo(string sql, bool negated, bool any, string? escapeChar)
    {
        SimilarTo expression = Assert.IsType<SimilarTo>(ParseExpression(sql));

        Assert.Equal(negated, expression.Negated);
        Assert.Equal(any, expression.Any);
        Assert.Equal(escapeChar, expression.EscapeChar);
        Assert.Equal(sql, expression.ToSql());
    }

    [Fact]
    public void CoreParser_DefaultsToPostgresSyntaxSupport()
    {
        var parser = new Parser();
        Assert.True(parser.SupportsPostgresCastSyntax);
        Assert.True(parser.SupportsPostgresRegexOperators);
    }
}
