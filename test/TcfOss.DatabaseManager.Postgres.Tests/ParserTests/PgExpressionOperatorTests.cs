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

    [Theory]
    [InlineData("E''")]
    [InlineData("E'it\\'s'")]
    [InlineData("E'it''s'")]
    [InlineData("E'\\\\'")]
    [InlineData("E'line\\nnext'")]
    public void EscapeStrings_ParseAndRoundTrip(string sql)
    {
        var expression = Assert.IsType<LiteralValue>(ParseExpression(sql));
        Assert.IsType<Core.Common.Value.EscapeString>(expression.Value);
        Assert.Equal(sql, expression.ToSql());
        Assert.Equal(expression, ParseExpression(expression.ToSql()));
    }

    [Theory]
    [InlineData("SELECT E'it\\'s'")]
    [InlineData("CREATE TABLE items (label TEXT DEFAULT E'line\\nnext')")]
    [InlineData("CREATE DOMAIN label AS TEXT DEFAULT E'line\\nnext'")]
    [InlineData("COMMENT ON TABLE items IS E'it\\'s'")]
    public void EscapeStrings_ParseInStatementsAndRoundTrip(string sql)
    {
        var statement = ParseStatement(sql);
        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement.ToSql(), ParseStatement(statement.ToSql()).ToSql());
    }

    [Theory]
    [InlineData("E'\\u123'")]
    [InlineData("E'\\uZZZZ'")]
    [InlineData("E'\\U00110000'")]
    [InlineData("E'\\UFFFFFFFF'")]
    [InlineData("E'\\uD800'")]
    [InlineData("E'\\uDC00'")]
    [InlineData("E'\\uD800\\u0041'")]
    [InlineData("E'\\0'")]
    [InlineData("E'\\x00'")]
    [InlineData("E'\\u0000'")]
    [InlineData("E'\\xFF'")]
    public void InvalidEscapeStrings_AreRejected(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseExpression(sql));
    }

    [Fact]
    public void EscapeStrings_SerializeInsideExpressionCollections()
    {
        Expression[] expressions = [ParseExpression("E'it\\'s \\u00e9 \\\\'")];

        Assert.Equal(expressions, Serialization.FromJson<Expression[]>(Serialization.ToJson(expressions)));
    }

    [Fact]
    public void EscapeStrings_LiteralStringConsumersReceiveDecodedText()
    {
        var state = new ParserState([.. new PgLexer().Tokenize("E'line\\n\\u00e9'")]);
        Assert.Equal("line\n\u00e9", ValueParser.ParseLiteralString(state));

        state = new ParserState([.. new PgLexer().Tokenize("E'\\x41'")]);
        Assert.Equal("A", ValueParser.ParseLiteralStringOptionalQuotes(state));
    }

    [Fact]
    public void EscapeStrings_AreAcceptedByKitchenSinkLexerAndParser()
    {
        var parser = new Parser();
        var state = new ParserState([.. new Core.Lexing.GenericLexer().Tokenize("E'it\\'s'")]);

        var expression = Assert.IsType<LiteralValue>(parser.ExpressionParser.ParseExpr(state));
        Assert.Equal("it's", Assert.IsType<Core.Common.Value.EscapeString>(expression.Value).DecodedValue);
    }

    [Theory]
    [InlineData("U&''", "")]
    [InlineData("U&'d\\0061t\\+000061'", "data")]
    [InlineData("U&'d!0061t!+000061' UESCAPE '!'", "data")]
    [InlineData("U&'it''s\\\\'", "it's\\")]
    [InlineData("U&'it''s!!' UESCAPE '!'", "it's!")]
    public void UnicodeStrings_ParseAndRoundTrip(string sql, string expected)
    {
        var expression = Assert.IsType<LiteralValue>(ParseExpression(sql));
        Assert.Equal(expected, Assert.IsType<Core.Common.Value.UnicodeString>(expression.Value).DecodedValue);
        Assert.Equal(sql, expression.ToSql());
        Assert.Equal(expression, ParseExpression(expression.ToSql()));
    }

    [Theory]
    [InlineData("SELECT U&'d\\0061ta'")]
    [InlineData("CREATE TABLE items (label TEXT DEFAULT U&'d!0061ta' UESCAPE '!')")]
    [InlineData("CREATE DOMAIN label AS TEXT DEFAULT U&'d\\0061ta'")]
    [InlineData("COMMENT ON TABLE items IS U&'it''s'")]
    public void UnicodeStrings_ParseInStatementsAndRoundTrip(string sql)
    {
        var statement = ParseStatement(sql);
        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement.ToSql(), ParseStatement(statement.ToSql()).ToSql());
    }

    [Theory]
    [InlineData("U&'\\'")]
    [InlineData("U&'\\123'")]
    [InlineData("U&'\\ZZZZ'")]
    [InlineData("U&'\\+00000'")]
    [InlineData("U&'\\+110000'")]
    [InlineData("U&'\\+FFFFFF'")]
    [InlineData("U&'\\D800'")]
    [InlineData("U&'\\DC00'")]
    [InlineData("U&'\\D800\\0041'")]
    [InlineData("U&'\\0000'")]
    [InlineData("U&'\\+000000'")]
    public void InvalidUnicodeStrings_AreRejected(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseExpression(sql));
    }

    [Fact]
    public void UnicodeStrings_SerializeInsideExpressionCollections()
    {
        Expression[] expressions = [ParseExpression("U&'it''s !00e9 !!' UESCAPE '!'")];
        Assert.Equal(expressions, Serialization.FromJson<Expression[]>(Serialization.ToJson(expressions)));
    }

    [Fact]
    public void UnicodeStrings_LiteralStringConsumersReceiveDecodedText()
    {
        var state = new ParserState([.. new PgLexer().Tokenize("U&'it''s !00e9' UESCAPE '!'")]);
        Assert.Equal("it's \u00e9", ValueParser.ParseLiteralString(state));

        state = new ParserState([.. new PgLexer().Tokenize("U&'\\0041'")]);
        Assert.Equal("A", ValueParser.ParseLiteralStringOptionalQuotes(state));
    }

    [Theory]
    [InlineData("U&'d\\0061ta'")]
    [InlineData("U&'d\\0061ta' UESCAPE '\\'")]
    public void UnicodeStrings_AreAcceptedByKitchenSinkLexerAndParser(string sql)
    {
        var parser = new Parser();
        var state = new ParserState([.. new Core.Lexing.GenericLexer().Tokenize(sql)]);
        var expression = Assert.IsType<LiteralValue>(parser.ExpressionParser.ParseExpr(state));
        Assert.Equal("data", Assert.IsType<Core.Common.Value.UnicodeString>(expression.Value).DecodedValue);
    }

    [Theory]
    [InlineData("U&\"d\\0061ta\"", "\"data\"")]
    [InlineData("U&\"d!0061ta\" UESCAPE '!'", "\"data\"")]
    [InlineData("U&\"caf\\00e9\"", "\"caf\u00e9\"")]
    [InlineData("U&\"has\\0022quote\"", "\"has\"\"quote\"")]
    [InlineData("U&\"has\"\"quote\"", "\"has\"\"quote\"")]
    public void UnicodeIdentifiers_EmitCanonicalQuotedNamesAndRoundTrip(string sql, string expected)
    {
        Expression expression = ParseExpression(sql);

        Assert.Equal(expected, expression.ToSql());
        Assert.Equal(expression, ParseExpression(expression.ToSql()));
    }

    [Fact]
    public void UnicodeIdentifiers_KeepQualifiedReferencesAndJsonRoundTrips()
    {
        const string sql = "U&\"a\\0070p\".U&\"f!006e\" UESCAPE '!'()";
        Expression expression = ParseExpression(sql);
        Expression canonical = ParseExpression("\"app\".\"fn\"()");
        var context = new ReferencedItemsManager { Filters = ObjectNameFilters.All };

        Assert.Equal(canonical, expression);
        Assert.Equal(canonical.GetReferencedItems(context), expression.GetReferencedItems(context));
        Expression[] expressions = [expression];
        Assert.Equal(expressions, Serialization.FromJson<Expression[]>(Serialization.ToJson(expressions)));
    }

    [Fact]
    public void UnicodeIdentifiers_AreAcceptedByKitchenSinkLexerAndParser()
    {
        var parser = new Parser();
        const string sql = "U&\"d!0061ta\" UESCAPE '!'";
        var state = new ParserState([.. new Core.Lexing.GenericLexer().Tokenize(sql)]);
        Expression expression = parser.ExpressionParser.ParseExpr(state);

        Assert.Equal("\"data\"", expression.ToSql());
    }

    [Fact]
    public void EscapeStrings_CacheDecodedValueWithoutChangingEqualityHashOrJson()
    {
        var value = new Core.Common.Value.EscapeString("it\\'s \\u00e9");
        var undecoded = value with { };
        string json = Serialization.ToJson(value);
        int hash = value.GetHashCode();
        HashSet<Core.Common.Value> values = [value];

        string decoded = value.DecodedValue;

        Assert.Equal("it's \u00e9", decoded);
        Assert.Same(decoded, value.DecodedValue);
        Assert.Equal(undecoded, value);
        Assert.Equal((Core.Common.Value)undecoded, value);
        Assert.Equal(hash, value.GetHashCode());
        Assert.Contains(undecoded, values);
        Assert.Equal(json, Serialization.ToJson(value));
        Assert.DoesNotContain("DecodedValue", json, StringComparison.Ordinal);
        var restored = Serialization.FromJson<Core.Common.Value.EscapeString>(json);
        Assert.Equal(value, restored);
        Assert.Equal(decoded, restored.DecodedValue);
        Assert.DoesNotContain("DecodedValue", System.Text.Json.JsonSerializer.Serialize(value), StringComparison.Ordinal);
    }

    [Fact]
    public void UnicodeStrings_CacheDecodedValueWithoutChangingEqualityHashOrJson()
    {
        var value = new Core.Common.Value.UnicodeString("it''s !00e9") { EscapeCharacter = '!' };
        var undecoded = value with { };
        string json = Serialization.ToJson(value);
        int hash = value.GetHashCode();
        HashSet<Core.Common.Value> values = [value];

        string decoded = value.DecodedValue;

        Assert.Equal("it's \u00e9", decoded);
        Assert.Same(decoded, value.DecodedValue);
        Assert.Equal(undecoded, value);
        Assert.Equal((Core.Common.Value)undecoded, value);
        Assert.Equal(hash, value.GetHashCode());
        Assert.Contains(undecoded, values);
        Assert.Equal(json, Serialization.ToJson(value));
        Assert.DoesNotContain("DecodedValue", json, StringComparison.Ordinal);
        var restored = Serialization.FromJson<Core.Common.Value.UnicodeString>(json);
        Assert.Equal(value, restored);
        Assert.Equal(decoded, restored.DecodedValue);
        Assert.DoesNotContain("DecodedValue", System.Text.Json.JsonSerializer.Serialize(value), StringComparison.Ordinal);
        Assert.NotEqual(value, value with { EscapeCharacter = '#' });
    }

    [Fact]
    public void EscapeStrings_RecordCopiesDecodeChangedRawText()
    {
        var original = new Core.Common.Value.EscapeString("\\u0041");
        Assert.Equal("A", original.DecodedValue);

        var unchanged = original with { };
        var changed = original with { Value = "\\u0042" };

        Assert.Equal(original, unchanged);
        Assert.Equal("A", unchanged.DecodedValue);
        Assert.Equal("B", changed.DecodedValue);
        Assert.Equal("A", original.DecodedValue);
        Assert.Equal("E'\\u0042'", changed.ToSql());
    }

    [Fact]
    public void UnicodeStrings_RecordCopiesDecodeChangedRawTextAndEscapeCharacter()
    {
        var original = new Core.Common.Value.UnicodeString("!0041") { EscapeCharacter = '!' };
        Assert.Equal("A", original.DecodedValue);

        var unchanged = original with { };
        var changedRaw = original with { Value = "!0042" };
        var changedEscape = original with { EscapeCharacter = '#' };
        var changedBoth = original with { Value = "#0042", EscapeCharacter = '#' };

        Assert.Equal(original, unchanged);
        Assert.Equal("A", unchanged.DecodedValue);
        Assert.Equal("B", changedRaw.DecodedValue);
        Assert.Equal("!0041", changedEscape.DecodedValue);
        Assert.Equal("B", changedBoth.DecodedValue);
        Assert.Equal("A", original.DecodedValue);
        Assert.Equal("U&'#0042' UESCAPE '#'", changedBoth.ToSql());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EscapeAndUnicodeStrings_InvalidRawTextRemainsLazilyValidated(bool unicode)
    {
        Core.Common.Value value = unicode
            ? new Core.Common.Value.UnicodeString("\\0000")
            : new Core.Common.Value.EscapeString("\\u0000");

        string json = Serialization.ToJson(value);
        Assert.DoesNotContain("DecodedValue", json, StringComparison.Ordinal);
        Assert.Throws<FormatException>(() => unicode
            ? ((Core.Common.Value.UnicodeString)value).DecodedValue
            : ((Core.Common.Value.EscapeString)value).DecodedValue);
    }
}
