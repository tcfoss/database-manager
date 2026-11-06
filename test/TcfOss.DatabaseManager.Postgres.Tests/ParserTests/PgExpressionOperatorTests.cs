using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Tests.ParserTests;
using TcfOss.DatabaseManager.Postgres.Lexing;
using TcfOss.DatabaseManager.Postgres.Parsing;

namespace TcfOss.DatabaseManager.Postgres.Tests.ParserTests;

public class PgExpressionOperatorTests : ParserTestsBase<PgLexer, PgParser>
{
    [Theory]
    [InlineData("value::INT")]
    [InlineData("(left + right)::NUMERIC(10,2)")]
    [InlineData("value::TEXT[]")]
    public void PostfixCast_ParsesAndRoundTrips(string sql)
    {
        Cast cast = Assert.IsType<Cast>(ParseExpression(sql));

        Assert.True(cast.UsePostgresCastSyntax);
        Assert.Contains("::", cast.ToSql());
        Assert.Equal(cast, ParseExpression(cast.ToSql()));
    }

    [Fact]
    public void CoreParser_AcceptsPostgresPostfixCast()
    {
        var parser = new Parser();
        Token[] tokens = [.. new PgLexer().Tokenize("value::INT")];

        Cast cast = Assert.IsType<Cast>(parser.ExpressionParser.ParseExpr(new ParserState(tokens)));
        Assert.True(cast.UsePostgresCastSyntax);
    }

    [Theory]
    [InlineData("value COLLATE \"C\"")]
    [InlineData("value COLLATE app.locale")]
    [InlineData("(left + right) COLLATE \"App\".\"Locale\"")]
    [InlineData("value::TEXT COLLATE \"C\"")]
    [InlineData("-value COLLATE \"C\"")]
    [InlineData("value COLLATE \"C\" COLLATE \"POSIX\"")]
    public void Collations_ParseAndRoundTrip(string sql)
    {
        var collate = Assert.IsType<Collate>(ParseExpression(sql));

        Assert.Equal(sql, collate.ToSql());
        Assert.Equal(collate, ParseExpression(collate.ToSql()));
    }

    [Fact]
    public void Collations_BindBelowCastsAndUnarySigns()
    {
        var collate = Assert.IsType<Collate>(ParseExpression("-value::TEXT COLLATE \"C\""));

        var unary = Assert.IsType<UnaryOperator>(collate.Expression);
        Assert.IsType<Cast>(unary.Expression);
    }

    [Fact]
    public void Collations_BindAboveArithmeticAndComparison()
    {
        var comparison = Assert.IsType<BinaryOperator>(ParseExpression("left + right COLLATE \"C\" = other"));

        var addition = Assert.IsType<BinaryOperator>(comparison.Left);
        Assert.IsType<Collate>(addition.Right);
        Assert.IsType<SingleIdentifier>(addition.Left);
    }

    [Fact]
    public void Collations_BindAboveAtTimeZone()
    {
        var zone = Assert.IsType<AtTimeZone>(ParseExpression("value COLLATE \"C\" AT TIME ZONE timezone"));

        Assert.IsType<Collate>(zone.Timestamp);
    }

    [Fact]
    public void Collations_CollectOperandReferencesWithoutTreatingCollationsAsObjects()
    {
        var collate = Assert.IsType<Collate>(ParseExpression("app.fn(items.label) COLLATE app.locale"));
        var context = new ReferencedItemsManager { Filters = ObjectNameFilters.All };

        ItemRef[] references = [.. collate.GetReferencedItems(context)];

        Assert.NotEmpty(references);
        Assert.Equal(collate.Expression.GetReferencedItems(context), references);
    }

    [Fact]
    public void Collations_PreserveCollationNameSourceSpan()
    {
        const string sql = "value COLLATE app.\"Locale\"";
        var collate = Assert.IsType<Collate>(ParseExpression(sql));

        Assert.NotNull(collate.Collation.Source);
        Assert.Equal(sql.IndexOf("app", StringComparison.Ordinal), collate.Collation.Source.Value.Start);
        Assert.Equal(sql.Length, collate.Collation.Source.Value.End);
    }

    [Fact]
    public void Collations_AreAcceptedByKitchenSinkParser()
    {
        var parser = new Parser();
        var state = new ParserState([.. new PgLexer().Tokenize("value COLLATE \"C\"")]);

        Assert.IsType<Collate>(parser.ExpressionParser.ParseExpr(state));
    }

    [Fact]
    public void Collations_SerializeInsideExpressionCollections()
    {
        Expression[] expressions = [ParseExpression("value COLLATE app.\"Locale\"")];

        Assert.Equal(expressions, Serialization.FromJson<Expression[]>(Serialization.ToJson(expressions)));
    }

    [Theory]
    [InlineData("SELECT label COLLATE \"C\" FROM items")]
    [InlineData("CREATE TABLE items (label TEXT DEFAULT ('default' COLLATE \"C\"))")]
    [InlineData("CREATE INDEX ix ON items ((label COLLATE \"C\"))")]
    [InlineData("CREATE VIEW v AS SELECT label COLLATE \"C\" FROM items")]
    public void Collations_ParseInStatementsAndRoundTrip(string sql)
    {
        var statement = ParseStatement(sql);

        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement.ToSql(), ParseStatement(statement.ToSql()).ToSql());
    }

    [Theory]
    [InlineData("value COLLATE")]
    [InlineData("value COLLATE 'C'")]
    [InlineData("value COLLATE 1")]
    [InlineData("value COLLATE = locale")]
    [InlineData("value COLLATE app.")]
    public void Collations_RejectInvalidNames(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseExpression(sql));
    }
}
