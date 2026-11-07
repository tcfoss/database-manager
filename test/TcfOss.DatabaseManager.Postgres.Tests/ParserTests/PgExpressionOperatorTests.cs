using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;
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

    [Theory]
    [InlineData("B''", "", false, "B''")]
    [InlineData("b'00101'", "00101", false, "B'00101'")]
    [InlineData("B'1'", "1", false, "B'1'")]
    [InlineData("X''", "", true, "X''")]
    [InlineData("x'f'", "f", true, "X'f'")]
    [InlineData("X'0aF'", "0aF", true, "X'0aF'")]
    public void BitStrings_ParsePreserveRadixAndRoundTrip(string sql, string expected, bool hexadecimal, string emitted)
    {
        var expression = Assert.IsType<LiteralValue>(ParseExpression(sql));
        var value = Assert.IsType<Core.Common.Value.BitString>(expression.Value);

        Assert.Equal(expected, value.Value);
        Assert.Equal(hexadecimal, value.IsHexadecimal);
        Assert.Equal(emitted, expression.ToSql());
        Assert.Equal(expression, ParseExpression(expression.ToSql()));
        Assert.Empty(expression.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All }));
    }

    [Theory]
    [InlineData("SELECT B'00101'")]
    [InlineData("SELECT X'f'::BIT(4)")]
    [InlineData("SELECT CAST(B'101' AS BIT VARYING(8))")]
    [InlineData("CREATE TABLE items (flags BIT(5) DEFAULT B'00101')")]
    [InlineData("CREATE TABLE items (flags BIT VARYING(8) DEFAULT X'0a')")]
    [InlineData("CREATE DOMAIN flags AS BIT(5) DEFAULT B'00101'")]
    [InlineData("CREATE FUNCTION f (flags BIT(5) DEFAULT B'00101') RETURNS BIT(5) LANGUAGE plpgsql AS 'opaque'")]
    public void BitStrings_ParseInStatementsAndRoundTrip(string sql)
    {
        var statement = ParseStatement(sql);

        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement.ToSql(), ParseStatement(statement.ToSql()).ToSql());
    }

    [Fact]
    public void BitStrings_SerializeInsideExpressionCollections()
    {
        Expression[] expressions = [ParseExpression("B'001'"), ParseExpression("X'0aF'")];

        Assert.Equal(expressions, Serialization.FromJson<Expression[]>(Serialization.ToJson(expressions)));
    }

    [Fact]
    public void BitStrings_BinaryFormIsAcceptedByKitchenSinkLexerAndParser()
    {
        var parser = new Parser();
        var state = new ParserState([.. new Core.Lexing.GenericLexer().Tokenize("B'00101'")]);
        var expression = Assert.IsType<LiteralValue>(parser.ExpressionParser.ParseExpr(state));

        Assert.Equal(new Core.Common.Value.BitString("00101"), expression.Value);
        Assert.Equal("B'00101'", expression.ToSql());
    }

    [Theory]
    [InlineData("B'2'")]
    [InlineData("B'10a'")]
    [InlineData("B'10 01'")]
    [InlineData("B'10\n01'")]
    [InlineData("B'10_01'")]
    [InlineData("B'10''01'")]
    [InlineData("B'1''; DROP TABLE items; --'")]
    [InlineData("B'\\1'")]
    [InlineData("X'g'")]
    [InlineData("X'0xA'")]
    [InlineData("X'0_a'")]
    [InlineData("X'0 a'")]
    [InlineData("X'\\a'")]
    [InlineData("B'\u0661'")]
    [InlineData("X'\uFF21'")]
    public void InvalidBitStrings_AreRejected(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseExpression(sql));
    }

    [Theory]
    [InlineData("COMMENT ON TABLE items IS B'1'")]
    [InlineData("COMMENT ON TABLE items IS X'f'")]
    [InlineData("CREATE FUNCTION f() RETURNS INT LANGUAGE plpgsql AS B'1'")]
    [InlineData("CREATE PROCEDURE p() LANGUAGE plpgsql AS X'f'")]
    public void BitStrings_AreNotAcceptedAsTextLiterals(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatement(sql));
    }

    [Theory]
    [InlineData("B'1'")]
    [InlineData("X'f'")]
    public void BitStrings_LiteralStringConsumersRejectThem(string sql)
    {
        var state = new ParserState([.. new PgLexer().Tokenize(sql)]);
        Assert.ThrowsAny<ParseException>(() => ValueParser.ParseLiteralString(state));

        state = new ParserState([.. new PgLexer().Tokenize(sql)]);
        Assert.ThrowsAny<ParseException>(() => ValueParser.ParseLiteralStringOptionalQuotes(state));
    }

    [Theory]
    [InlineData("$1", 1, "$1")]
    [InlineData("$12", 12, "$12")]
    [InlineData("$0001", 1, "$1")]
    [InlineData("$2147483647", int.MaxValue, "$2147483647")]
    [InlineData("$0", 0, "$0")]
    public void PositionalParameters_ParseCanonicalizeAndPreserveSourceSpan(string sql, int expected, string canonical)
    {
        var state = new ParserState([.. new PgLexer().Tokenize(sql)], 37);
        var parameter = Assert.IsType<PositionalParameter>(new PgParser().ExpressionParser.ParseExpr(state));

        Assert.Equal(expected, parameter.Number);
        Assert.Equal(new Core.Statements.LabelAttributes.SourceRef(37, 0, sql.Length), parameter.Source);
        Assert.Equal(canonical, parameter.ToSql());
        Assert.Equal(parameter, ParseExpression(parameter.ToSql()));
        Assert.Empty(parameter.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All }));
    }

    [Theory]
    [InlineData("$1 + $2 * 3")]
    [InlineData("-$1::INT")]
    [InlineData("fn($1, $2)")]
    [InlineData("$1 COLLATE \"C\"")]
    public void PositionalParameters_ParseInsideExpressionsAndRoundTrip(string sql)
    {
        Expression expression = ParseExpression(sql);

        Assert.Equal(sql, expression.ToSql());
        Assert.Equal(expression, ParseExpression(expression.ToSql()));
    }

    [Theory]
    [InlineData("SELECT $1")]
    [InlineData("SELECT $1::INT, $2 FROM items WHERE id = $3")]
    [InlineData("CREATE FUNCTION f () RETURNS INT LANGUAGE SQL RETURN $1")]
    [InlineData("CREATE PROCEDURE p () LANGUAGE SQL BEGIN ATOMIC SELECT $1; END")]
    public void PositionalParameters_ParseInStatementsAndRoundTrip(string sql)
    {
        var statement = ParseStatement(sql);

        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement.ToSql(), ParseStatement(statement.ToSql()).ToSql());
    }

    [Fact]
    public void PositionalParameters_SerializeInsideExpressionCollections()
    {
        Expression[] expressions = [ParseExpression("$001"), ParseExpression("$2 + $3")];

        Assert.Equal(expressions, Serialization.FromJson<Expression[]>(Serialization.ToJson(expressions)));
    }

    [Fact]
    public void PositionalParameters_AreAcceptedByKitchenSinkLexerAndParser()
    {
        var parser = new Parser();
        var state = new ParserState([.. new Core.Lexing.GenericLexer().Tokenize("$1")]);

        Assert.Equal(new PositionalParameter(1), parser.ExpressionParser.ParseExpr(state));
    }

    [Fact]
    public void PositionalParameters_EqualityAndHashingIgnoreSourceSpelling()
    {
        Expression plain = ParseExpression("$1");
        Expression padded = ParseExpression("$001");

        Assert.Equal(plain, padded);
        Assert.Equal(plain.GetHashCode(), padded.GetHashCode());
        Assert.NotEqual(plain, ParseExpression("$2"));
    }

    [Theory]
    [InlineData("0x_Ff_01", "65281")]
    [InlineData("0O_7_5_5", "493")]
    [InlineData("0B_1_0_1", "5")]
    [InlineData("1_000", "1000")]
    [InlineData("1_000.2_5", "1000.25")]
    [InlineData(".2_5", ".25")]
    [InlineData("1_0e+1_2", "10e+12")]
    [InlineData("0xFFFFFFFFFFFFFFFFFFFFFFFF", "79228162514264337593543950335")]
    public void ExtendedNumbers_ParseAsNumbersAndPreserveRawSpelling(string sql, string decimalText)
    {
        var expression = Assert.IsType<LiteralValue>(ParseExpression(sql));
        var number = Assert.IsType<Core.Common.Value.Number>(expression.Value);

        Assert.Equal(sql, number.Value);
        Assert.False(number.IsLong);
        Assert.Equal(decimalText, number.DecimalText);
        Assert.Equal(sql, expression.ToSql());
        Assert.Equal(expression, ParseExpression(expression.ToSql()));
        Assert.Empty(expression.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All }));
    }

    [Theory]
    [InlineData("SELECT 0x10 + 0b1_0 * 0o7")]
    [InlineData("SELECT -0x10::INT, .2_5, 1_0e+1_2")]
    [InlineData("CREATE TABLE items (id INT DEFAULT 0x10)")]
    [InlineData("CREATE DOMAIN counter AS INT DEFAULT 1_000")]
    [InlineData("CREATE FUNCTION f () RETURNS INT LANGUAGE SQL COST 0x10 RETURN 0b1_0")]
    [InlineData("CREATE PROCEDURE p () LANGUAGE SQL BEGIN ATOMIC SELECT 0o7; END")]
    public void ExtendedNumbers_ParseInStatementsAndRoundTrip(string sql)
    {
        var statement = ParseStatement(sql);

        Assert.Equal(sql, statement.ToSql());
    }

    [Fact]
    public void ExtendedNumbers_SerializeInsideExpressionCollections()
    {
        Expression[] expressions = [ParseExpression("0x_Ff"), ParseExpression("0b1_0"), ParseExpression("1_000.2_5")];

        Assert.Equal(expressions, Serialization.FromJson<Expression[]>(Serialization.ToJson(expressions)));
    }

    [Theory]
    [InlineData("app..mood")]
    [InlineData("app...mood")]
    [InlineData("app..\"Mood\"")]
    public void DoubleDots_StopExpressionWithoutConsumption(string sql)
    {
        var state = GetState(sql);
        Expression expression = ParseExpression(state, false);

        Assert.Equal("app", expression.ToSql());
        Assert.IsType<DotDot>(state.Peek());
        Assert.Equal(3, state.Peek().Location.Position);
    }

    [Theory]
    [InlineData("app..mood")]
    [InlineData("app...mood")]
    [InlineData("app..\"Mood\"")]
    public void DoubleDots_AreRejectedInCompleteStatements(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatement($"SELECT {sql}"));
    }

    [Theory]
    [InlineData("ARRAY[]")]
    [InlineData("ARRAY[1, 2, ARRAY[3]]")]
    [InlineData("ARRAY(SELECT value FROM items)")]
    public void ArrayConstructors_ParseAndRoundTrip(string sql)
    {
        ArrayConstructor array = Assert.IsType<ArrayConstructor>(ParseExpression(sql), exactMatch: false);

        Assert.Equal(sql, array.ToSql());
        Assert.Equal(array, ParseExpression(array.ToSql()));
    }

    [Theory]
    [InlineData("arr[1]")]
    [InlineData("arr[1][2:3]")]
    [InlineData("arr[lower:upper]")]
    [InlineData("arr[:2]")]
    [InlineData("arr[1:]")]
    [InlineData("arr[:]")]
    public void ArraySubscriptsAndSlices_ParseAndRoundTrip(string sql)
    {
        ArraySubscript subscript = Assert.IsType<ArraySubscript>(ParseExpression(sql));

        Assert.Equal(sql, subscript.ToSql());
        Assert.Equal(subscript, ParseExpression(subscript.ToSql()));
    }

    [Theory]
    [InlineData("ROW()")]
    [InlineData("ROW(value, nested + 1)")]
    [InlineData("ROW(items.*, 42)")]
    [InlineData("ROW((item).*, 42)")]
    public void RowConstructors_ParseAndRoundTrip(string sql)
    {
        RowConstructor row = Assert.IsType<RowConstructor>(ParseExpression(sql));

        Assert.Equal(sql, row.ToSql());
        Assert.Equal(row, ParseExpression(row.ToSql()));
    }

    [Theory]
    [InlineData("array")]
    [InlineData("row")]
    public void ArrayAndRowKeywordsWithoutConstructors_RemainIdentifiers(string sql)
    {
        Assert.IsType<SingleIdentifier>(ParseExpression(sql));
    }

    [Theory]
    [InlineData("(items.value).field")]
    [InlineData("((left + right)).\"Field\"")]
    [InlineData("(items.value).field.other")]
    public void CompositeFieldAccess_ParsesAndRoundTrips(string sql)
    {
        CompositeAccess.Field field = Assert.IsType<CompositeAccess.Field>(ParseExpression(sql));

        Assert.Equal(sql, field.ToSql());
        Assert.Equal(field, ParseExpression(field.ToSql()));
    }

    [Fact]
    public void ArrayAndRowExpressions_AreAcceptedByKitchenSinkParser()
    {
        var parser = new Parser();
        var state = new ParserState([.. new PgLexer().Tokenize("ARRAY[ROW(value)]")]);

        Assert.IsType<ArrayConstructor.Elements>(parser.ExpressionParser.ParseExpr(state));
        Assert.True(state.PeekIs<Eof>());
    }

    [Theory]
    [InlineData("(ARRAY[items.value])[items.index:items.bound]", new string[] { "items.value", "items.index", "items.bound" })]
    [InlineData("(items.value).field", new string[] { "items.value" })]
    [InlineData("(items.value).*", new string[] { "items.value" })]
    [InlineData("items.value[items.index][items.lower:items.upper].field", new string[] { "items.value", "items.index", "items.lower", "items.upper" })]
    [InlineData("ARRAY[[items.value], [items.other]]", new string[] { "items.value", "items.other" })]
    [InlineData("ROW((items.value).*, items.other)", new string[] { "items.value", "items.other" })]
    public void ArrayRowAndCompositeExpressions_CollectOnlyOperandReferences(string sql, string[] expectedOperands)
    {
        Expression expression = ParseExpression(sql);
        var context = new ReferencedItemsManager { Filters = ObjectNameFilters.All };

        ItemRef[] references = [.. expression.GetReferencedItems(context)];
        ItemRef[] expected = [.. expectedOperands.SelectMany(operand => ParseExpression(operand).GetReferencedItems(context))];

        Assert.Equal(expected, references);
    }

    [Fact]
    public void ArrayAndRowExpressions_SerializeInsideExpressionCollections()
    {
        Expression[] expressions =
        [
            ParseExpression("ARRAY[1, ROW(value)]"),
            ParseExpression("ARRAY(SELECT value FROM items)"),
            ParseExpression("arr[1:items.upper].field")
        ];

        Assert.Equal(expressions, Serialization.FromJson<Expression[]>(Serialization.ToJson(expressions)));
    }

    [Theory]
    [InlineData("(f())[1]")]
    [InlineData("(f()).field")]
    [InlineData("(ARRAY[1, 2])[1]")]
    [InlineData("(ROW(1)).field")]
    [InlineData("(item).arr[1].field")]
    [InlineData("arr[1].nested[2].field")]
    [InlineData("arr[(item).lo:hi]")]
    [InlineData("arr[lo + step:hi]")]
    [InlineData("$1.field[2].name")]
    [InlineData("$1[1:2]")]
    [InlineData("(item).*")]
    [InlineData("$1.*")]
    [InlineData("arr[1].*")]
    public void PostfixChains_ParseInCompleteStatementsAndRoundTrip(string sql)
    {
        Expression expression = ParseExpression(sql);

        Assert.Equal(sql, expression.ToSql());
        Assert.Equal(expression, ParseExpression(expression.ToSql()));
        Assert.Equal("SELECT " + sql, ParseStatement("SELECT " + sql).ToSql());
        Expression[] expressions = [expression];
        Assert.Equal(expressions, Serialization.FromJson<Expression[]>(Serialization.ToJson(expressions)));
    }

    [Fact]
    public void ConsecutiveSubscripts_AreDimensionsOfOneOperation()
    {
        var multidimensional = Assert.IsType<ArraySubscript>(ParseExpression("arr[1][2]"));
        var sequential = Assert.IsType<ArraySubscript>(ParseExpression("(arr[1])[2]"));

        Assert.Equal(2, multidimensional.Dimensions.Count);
        Assert.IsType<SingleIdentifier>(multidimensional.Array);
        Assert.Single(sequential.Dimensions);
        Assert.IsType<ArraySubscript>(Assert.IsType<Nested>(sequential.Array).Expression);
    }

    [Fact]
    public void Subscripts_BindAboveCastsUnarySignsAndArithmetic()
    {
        var addition = Assert.IsType<BinaryOperator>(ParseExpression("-arr[1]::INT + other[2]"));
        var sign = Assert.IsType<UnaryOperator>(addition.Left);
        Assert.IsType<ArraySubscript>(Assert.IsType<Cast>(sign.Expression).Expression);
        Assert.IsType<ArraySubscript>(addition.Right);
        var manager = new PrecedenceManager();
        Assert.True(manager.GetPrecedence(Precedence.ArraySubscript) > manager.GetPrecedence(Precedence.DoubleColon));
    }

    [Fact]
    public void CompositeSliceBounds_PreserveIdentifierSourceSpans()
    {
        const string sql = "arr[(item).\"Lo\":bounds.hi]";
        var state = new ParserState(GetTokens(sql), 37);
        var array = Assert.IsType<ArraySubscript>(new PgParser().ExpressionParser.ParseExpr(state));
        var slice = Assert.IsType<ArraySubscriptDimension.Slice>(Assert.Single(array.Dimensions));
        var lower = Assert.IsType<CompositeAccess.Field>(slice.LowerBound);
        var upper = Assert.IsType<CompoundIdentifier>(slice.UpperBound);

        Assert.Equal(new Core.Statements.LabelAttributes.SourceRef(37, 11, 15), lower.Name.Source);
        Assert.Equal(new Core.Statements.LabelAttributes.SourceRef(37, 16, 25), upper.Source);
        Assert.IsType<Eof>(state.Peek());
    }

    [Theory]
    [InlineData("ARRAY[[1, 2], [3, 4]]")]
    [InlineData("ARRAY[ARRAY[1, 2], ARRAY[3, 4]]")]
    [InlineData("ARRAY[[[1]], [[2]]]")]
    [InlineData("ARRAY[[], []]")]
    [InlineData("ARRAY(VALUES (1), (2))")]
    [InlineData("ARRAY(WITH c AS (SELECT value FROM items) SELECT value FROM c)")]
    [InlineData("ARRAY(WITH c AS (SELECT 1) VALUES (1))")]
    [InlineData("ARRAY((SELECT 1) UNION SELECT 2)")]
    public void NestedArraysAndArraySubqueries_ParseAndRoundTrip(string sql)
    {
        Expression expression = ParseExpression(sql);

        Assert.Equal(sql, expression.ToSql());
        Assert.Equal(expression, ParseExpression(expression.ToSql()));
        Assert.Equal("SELECT " + sql, ParseStatement("SELECT " + sql).ToSql());
        Expression[] expressions = [expression];
        Assert.Equal(expressions, Serialization.FromJson<Expression[]>(Serialization.ToJson(expressions)));
    }

    [Theory]
    [InlineData("ARRAY[1,]")]
    [InlineData("ARRAY(1)")]
    [InlineData("arr[]")]
    [InlineData("arr[1:2")]
    [InlineData("ROW(1,)")]
    public void ArrayAndRowExpressions_RejectMalformedForms(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatement("SELECT " + sql));
    }

    [Theory]
    [InlineData("f()[1]")]
    [InlineData("f().field")]
    [InlineData("ARRAY[1][1]")]
    [InlineData("ROW(1).field")]
    [InlineData("1[2]")]
    [InlineData("arr[(lo:hi)]")]
    [InlineData("arr[1:2:3]")]
    [InlineData("(item).*.field")]
    public void InvalidPostfixForms_AreRejectedInCompleteStatements(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatement("SELECT " + sql));
    }

    [Theory]
    [InlineData("ARRAY[[1],]")]
    [InlineData("ARRAY[[1], 2]")]
    [InlineData("ARRAY[1, [2]]")]
    [InlineData("[1, 2]")]
    [InlineData("(item).*[1]")]
    public void InvalidNestedArrayForms_AreRejected(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatement("SELECT " + sql));
    }

    [Theory]
    [InlineData("doc -> 'name'")]
    [InlineData("doc ->> 'name'")]
    [InlineData("doc #> '{a,b}'")]
    [InlineData("doc #>> '{a,b}'")]
    [InlineData("doc @> '{\"active\":true}'")]
    [InlineData("doc <@ other")]
    [InlineData("doc ? 'name'")]
    [InlineData("doc ?| ARRAY['name', 'age']")]
    [InlineData("doc ?& ARRAY['name', 'age']")]
    [InlineData("search @@ query")]
    [InlineData("tags && ARRAY['red']")]
    [InlineData("period -|- other")]
    [InlineData("point <-> target")]
    [InlineData("embedding <=> target")]
    [InlineData("value OPERATOR(app.===) other")]
    [InlineData("value OPERATOR(\"App\".@>) other")]
    [InlineData("value OPERATOR(+) other")]
    [InlineData("doc @> ANY(ARRAY[other])")]
    [InlineData("doc @> ALL(SELECT value FROM items)")]
    [InlineData("doc @> SOME(VALUES (other))")]
    public void PostgresOperators_ParseAndRoundTripWithoutResolvingTypes(string sql)
    {
        var expression = Assert.IsType<PgBinaryOperator>(ParseExpression(sql));

        Assert.Equal(sql, expression.ToSql());
        Assert.Equal(expression, ParseExpression(expression.ToSql()));
        Assert.Equal("SELECT " + sql, ParseStatement("SELECT " + sql).ToSql());
        Expression[] expressions = [expression];
        Assert.Equal(expressions, Serialization.FromJson<Expression[]>(Serialization.ToJson(expressions)));
    }

    [Theory]
    [InlineData("@ value")]
    [InlineData("~ value")]
    [InlineData("!! value")]
    [InlineData("OPERATOR(app.@) value")]
    [InlineData("OPERATOR(pg_catalog.-) value")]
    public void PostgresPrefixOperators_ParseAndRoundTrip(string sql)
    {
        var expression = Assert.IsType<PgUnaryOperator>(ParseExpression(sql));

        Assert.Equal(sql, expression.ToSql());
        Assert.Equal(expression, ParseExpression(expression.ToSql()));
        Expression[] expressions = [expression];
        Assert.Equal(expressions, Serialization.FromJson<Expression[]>(Serialization.ToJson(expressions)));
    }

    [Fact]
    public void PostgresOperators_UseGenericPrecedenceAndLeftAssociativity()
    {
        var comparison = Assert.IsType<BinaryOperator>(ParseExpression("doc ->> 'count' = other + 1"));
        Assert.IsType<PgBinaryOperator>(comparison.Left);
        Assert.IsType<BinaryOperator>(comparison.Right);
        var chain = Assert.IsType<PgBinaryOperator>(ParseExpression("doc -> 'a' ->> 'b'"));
        Assert.IsType<PgBinaryOperator>(chain.Left);
        var qualified = Assert.IsType<PgBinaryOperator>(ParseExpression("a OPERATOR(pg_catalog.*) b + c"));
        Assert.IsType<BinaryOperator>(qualified.Right);
        var prefix = Assert.IsType<PgUnaryOperator>(ParseExpression("@ a + b"));
        Assert.IsType<BinaryOperator>(prefix.Expression);
    }

    [Theory]
    [InlineData("items.doc OPERATOR(ops.@>) items.other", new string[] { "items.doc", "items.other" })]
    [InlineData("OPERATOR(ops.@) items.value", new string[] { "items.value" })]
    [InlineData("items.doc @> ANY(ARRAY[items.other])", new string[] { "items.doc", "items.other" })]
    public void PostgresOperators_ReferenceOperandsButNotOperatorSchemas(string sql, string[] operands)
    {
        var context = new ReferencedItemsManager { Filters = ObjectNameFilters.All };
        ItemRef[] expected = [.. operands.SelectMany(operand => ParseExpression(operand).GetReferencedItems(context))];

        Assert.Equal(expected, ParseExpression(sql).GetReferencedItems(context));
    }

    [Fact]
    public void QualifiedOperators_PreserveSchemaSourceSpansAndKitchenSinkSupport()
    {
        const string sql = "left OPERATOR(\"app\".@>) right";
        var state = new ParserState(GetTokens(sql), 37);
        var expression = Assert.IsType<PgBinaryOperator>(new Parser().ExpressionParser.ParseExpr(state));

        Assert.Equal(new Core.Statements.LabelAttributes.SourceRef(37, 14, 19), expression.Operator.Schema!.Source);
        Assert.IsType<Eof>(state.Peek());
        Assert.Equal(sql, expression.ToSql());
    }

    [Theory]
    [InlineData("doc ->")]
    [InlineData("doc OPERATOR(app.) value")]
    [InlineData("doc OPERATOR(app.name) value")]
    [InlineData("doc OPERATOR(app.schema.+) value")]
    [InlineData("doc OPERATOR(app.::) value")]
    [InlineData("doc OPERATOR(app.@> value")]
    [InlineData("doc @> ANY()")]
    [InlineData("value !!")]
    public void MalformedPostgresOperators_AreRejected(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatement("SELECT " + sql));
    }

    [Theory]
    [InlineData("INTERVAL ''")]
    [InlineData("INTERVAL '1 day'")]
    [InlineData("INTERVAL 'P1Y2M3DT4H5M6S'")]
    [InlineData("INTERVAL '@ 1 day ago'")]
    [InlineData("INTERVAL 'not yet validated'")]
    [InlineData("INTERVAL E'1\\x20day'")]
    [InlineData("INTERVAL U&'1\\0020day'")]
    [InlineData("INTERVAL U&'1!0020day' UESCAPE '!'")]
    [InlineData("INTERVAL '1 day''s worth'")]
    [InlineData("INTERVAL $duration$1 day$duration$")]
    [InlineData("INTERVAL(0) '1.5 seconds'")]
    [InlineData("INTERVAL(6) '1.123456 seconds'")]
    [InlineData("INTERVAL '1' YEAR")]
    [InlineData("INTERVAL '1' MONTH")]
    [InlineData("INTERVAL '1' DAY")]
    [InlineData("INTERVAL '1' HOUR")]
    [InlineData("INTERVAL '1' MINUTE")]
    [InlineData("INTERVAL '1' SECOND")]
    [InlineData("INTERVAL '1-2' YEAR TO MONTH")]
    [InlineData("INTERVAL '2 03' DAY TO HOUR")]
    [InlineData("INTERVAL '2 03:04' DAY TO MINUTE")]
    [InlineData("INTERVAL '2 03:04:05' DAY TO SECOND")]
    [InlineData("INTERVAL '03:04' HOUR TO MINUTE")]
    [InlineData("INTERVAL '03:04:05' HOUR TO SECOND")]
    [InlineData("INTERVAL '04:05' MINUTE TO SECOND")]
    [InlineData("INTERVAL '1.5' SECOND(0)")]
    [InlineData("INTERVAL '1.123456' SECOND(6)")]
    [InlineData("INTERVAL '2 03:04:05.678' DAY TO SECOND(3)")]
    public void IntervalLiterals_PreserveSyntaxAndRoundTripWithoutResolvingValues(string sql)
    {
        var interval = Assert.IsType<PgIntervalLiteral>(ParseExpression(sql));

        Assert.Equal(sql, interval.ToSql());
        Assert.Equal(interval, ParseExpression(interval.ToSql()));
        Assert.Equal("SELECT " + sql, ParseStatement("SELECT " + sql).ToSql());
        Assert.Empty(interval.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All }));
        Expression[] expressions = [interval];
        Assert.Equal(expressions, Serialization.FromJson<Expression[]>(Serialization.ToJson(expressions)));
    }

    [Fact]
    public void IntervalLiterals_PreserveModifierPlacementAndFollowingSource()
    {
        var prefix = Assert.IsType<PgIntervalLiteral>(ParseExpression("INTERVAL(3) '1.234 seconds'"));
        var qualified = Assert.IsType<PgIntervalLiteral>(ParseExpression("INTERVAL '1.234' SECOND(3)"));
        Assert.Equal(3u, prefix.Precision);
        Assert.Null(prefix.Qualifier);
        Assert.Null(qualified.Precision);
        Assert.Equal(Core.BuiltIn.DateTimeUnit.Second, qualified.Qualifier!.StartField);
        Assert.Equal(3u, qualified.Qualifier.Precision);

        const string sql = "INTERVAL '1-2' YEAR TO MONTH + next_value";
        var state = new ParserState(GetTokens(sql), 37);
        var addition = Assert.IsType<BinaryOperator>(new PgParser().ExpressionParser.ParseExpr(state));
        var interval = Assert.IsType<PgIntervalLiteral>(addition.Left);
        Assert.Equal(Core.BuiltIn.DateTimeUnit.Year, interval.Qualifier!.StartField);
        Assert.Equal(Core.BuiltIn.DateTimeUnit.Month, interval.Qualifier.EndField);
        int position = sql.IndexOf("next_value", StringComparison.Ordinal);
        Assert.Equal(new Core.Statements.LabelAttributes.SourceRef(37, position, sql.Length), Assert.IsType<SingleIdentifier>(addition.Right).Source);
        Assert.IsType<Eof>(state.Peek());
    }

    [Theory]
    [InlineData("left IS DISTINCT FROM right AND flag", false)]
    [InlineData("left IS NOT DISTINCT FROM right AND flag", true)]
    [InlineData("left IS DISTINCT FROM right OR flag", false)]
    [InlineData("left IS NOT DISTINCT FROM right OR flag", true)]
    public void DistinctFrom_DoesNotAbsorbFollowingLogicalOperators(string sql, bool negated)
    {
        var logical = Assert.IsType<BinaryOperator>(ParseExpression(sql));
        var distinct = Assert.IsType<IsDistinctFrom>(logical.Left);

        Assert.Equal(negated, distinct.Negated);
        Assert.IsType<SingleIdentifier>(distinct.Right);
        Assert.IsType<SingleIdentifier>(logical.Right);
        Assert.Equal(sql, logical.ToSql());
        Assert.Equal(logical, ParseExpression(logical.ToSql()));
    }

    [Fact]
    public void DistinctFrom_KeepsArithmeticAndExplicitGroupingInItsOperands()
    {
        var distinct = Assert.IsType<IsDistinctFrom>(ParseExpression("left + 1 IS DISTINCT FROM right * 2"));
        Assert.IsType<BinaryOperator>(distinct.Left);
        Assert.IsType<BinaryOperator>(distinct.Right);
        var grouped = Assert.IsType<IsDistinctFrom>(ParseExpression("left IS DISTINCT FROM (right OR flag)"));
        Assert.IsType<BinaryOperator>(Assert.IsType<Nested>(grouped.Right).Expression);
    }

    [Theory]
    [InlineData("value = ANY(VALUES (1), (2))")]
    [InlineData("value <> ALL(VALUES (1), (2))")]
    [InlineData("value > SOME(WITH c AS (SELECT 1) VALUES (1))")]
    public void QuantifiedComparisons_AcceptValuesSubqueries(string sql)
    {
        var expression = Assert.IsType<QuantifiedBinaryOperator>(ParseExpression(sql));

        Assert.IsType<Subquery>(expression.Right);
        Assert.Equal(sql, expression.ToSql());
        Assert.Equal(expression, ParseExpression(expression.ToSql()));
    }

    [Theory]
    [InlineData("value = ANY(left_values OR right_values)")]
    [InlineData("value <> ALL(left_values AND right_values)")]
    [InlineData("value < SOME(left_values = right_values)")]
    public void QuantifiedComparisonParentheses_ParseCompleteOperandsWithoutTypeResolution(string sql)
    {
        var expression = Assert.IsType<QuantifiedBinaryOperator>(ParseExpression(sql));

        Assert.IsType<BinaryOperator>(expression.Right);
        Assert.Equal(sql, expression.ToSql());
        Assert.Equal(expression, ParseExpression(expression.ToSql()));
    }

    [Theory]
    [InlineData("value = ANY(ARRAY[1, 2])")]
    [InlineData("value <> ALL(ARRAY[1, 2])")]
    [InlineData("value >= SOME(ARRAY[1, 2])")]
    [InlineData("value = ANY(SELECT other FROM items)")]
    [InlineData("value < ALL(WITH c AS (SELECT other FROM items) SELECT other FROM c)")]
    [InlineData("value > ANY(ARRAY[]) AND active")]
    public void QuantifiedComparisons_ParseArraysAndQueriesWithOuterBoundaries(string sql)
    {
        Expression expression = ParseExpression(sql);

        Assert.Equal(sql, expression.ToSql());
        Assert.Equal(expression, ParseExpression(expression.ToSql()));
        Assert.Equal("SELECT " + sql, ParseStatement("SELECT " + sql).ToSql());
        Expression[] expressions = [expression];
        Assert.Equal(expressions, Serialization.FromJson<Expression[]>(Serialization.ToJson(expressions)));
    }

    [Theory]
    [InlineData("items.value = ANY(ARRAY[items.other])")]
    [InlineData("items.value IS NOT DISTINCT FROM items.other")]
    public void RemainingComparisons_CollectBothOperandReferences(string sql)
    {
        var context = new ReferencedItemsManager { Filters = ObjectNameFilters.All };
        ItemRef[] expected =
        [
            .. ParseExpression("items.value").GetReferencedItems(context),
            .. ParseExpression("items.other").GetReferencedItems(context)
        ];

        Assert.Equal(expected, ParseExpression(sql).GetReferencedItems(context));
    }

    [Theory]
    [InlineData("INTERVAL 1 DAY")]
    [InlineData("INTERVAL amount DAY")]
    [InlineData("INTERVAL (amount + 1) DAY")]
    [InlineData("INTERVAL DAY '1'")]
    [InlineData("INTERVAL '1' YEAR TO DAY")]
    [InlineData("INTERVAL '1' SECOND TO MINUTE")]
    [InlineData("INTERVAL '1' DAY TO")]
    [InlineData("INTERVAL '1' WEEK TO DAY")]
    [InlineData("INTERVAL(3) '1' DAY TO SECOND")]
    [InlineData("INTERVAL '1' DAY(3)")]
    [InlineData("INTERVAL '1' SECOND(7)")]
    [InlineData("INTERVAL(7) '1'")]
    [InlineData("INTERVAL(-1) '1'")]
    [InlineData("INTERVAL(3,2) '1'")]
    [InlineData("INTERVAL(3)")]
    [InlineData("INTERVAL NULL")]
    [InlineData("INTERVAL B'01'")]
    public void IntervalLiterals_RejectMalformedAndOtherDialectForms(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatement("SELECT " + sql));
    }

    [Theory]
    [InlineData("INTERVAL(7) '1'", "7")]
    [InlineData("INTERVAL '1' SECOND(7)", "7")]
    [InlineData("INTERVAL '1' DAY TO SECOND(7)", "7")]
    [InlineData("INTERVAL(4294967296) '1'", "4294967296")]
    public void IntervalLiteralPrecision_ReportsBoundsAndSource(string sql, string value)
    {
        var exception = Assert.Throws<ParseException.ValueOutOfRange>(() => ParseExpression(sql));

        Assert.Equal("precision", exception.Label);
        Assert.Equal(value, exception.Value);
        Assert.Equal(0, exception.Minimum);
        Assert.Equal(6, exception.Maximum);
        int position = sql.IndexOf('(') + 1;
        Assert.Contains($"{new string(' ', position)}^", new ParseException.WithText(exception, null, sql).Message);
    }
}
