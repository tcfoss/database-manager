using System.Globalization;
using System.Text;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Expressions.Components;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Tests;
using TcfOss.DatabaseManager.Postgres.Lexing;
using TcfOss.DatabaseManager.Postgres.Parsing;

namespace TcfOss.DatabaseManager.Postgres.Tests.IO.Formatting;

public class FormatComponentsTests
{
    [Theory]
    [InlineData("SELECT result.value FROM LATERAL app.expand($1) WITH ORDINALITY AS result (value, ordinal)", "\"result\"")]
    [InlineData("SELECT result.value FROM LATERAL ROWS FROM (app.records() AS (value INT), app.other()) WITH ORDINALITY AS result (value, other, ordinal)", "\"result\"")]
    [InlineData("SELECT result.value FROM LATERAL (SELECT $1 AS original) AS result (value)", "result")]
    public void FromRelations_FormatAliasColumnsAndRetainFunctionStructure(string sql, string expectedAlias)
    {
        var state = new Core.Parsing.ParserState([.. new PgLexer().Tokenize(sql)]);
        Select statement = Assert.IsType<Select>(new PgParser().ParseStatement(state));
        using var writer = new SqlTextWriter(new StringBuilder());

        statement.FormatSql(writer, Helpers.CreateFormatManager());
        string formatted = writer.ToString();

        Assert.Contains("LATERAL ", formatted);
        Assert.Contains($"AS {expectedAlias} (\"value\"", formatted);
        if (sql.Contains("WITH ORDINALITY", StringComparison.Ordinal))
        {
            Assert.Contains("WITH ORDINALITY", formatted);
        }
        Assert.Contains("\"result\".\"value\"", formatted);
        var formattedState = new Core.Parsing.ParserState([.. new PgLexer().Tokenize(formatted)]);
        Assert.IsType<Select>(new PgParser().ParseStatement(formattedState));
        Assert.True(formattedState.PeekIs<Core.Lexing.Tokens.Eof>());
    }

    [Fact]
    public void RoutineSettingFormatting_DelegatesToSqlForBothVariants()
    {
        var name = new ObjectName([new Identifier("app"), new Identifier("mode")]);
        PgRoutineSetting[] settings =
        [
            new PgRoutineSetting.FromCurrent(name),
            new PgRoutineSetting.ToValues(name, [new Value.SingleQuotedString("it's"), new Value.Number("2", false)])
        ];
        string[] expected = ["SET app.mode FROM CURRENT", "SET app.mode TO 'it''s', 2"];
        for (int index = 0; index < settings.Length; index++)
        {
            using var writer = new SqlTextWriter(new StringBuilder());
            settings[index].FormatSql(writer, Helpers.CreateFormatManager());

            Assert.Equal(expected[index], writer.ToString());
        }
    }

    [Fact]
    public void TriggerArguments_PreserveIdentifierAndLiteralSpelling()
    {
        PgStatementTriggerArgument[] arguments =
        [
            new PgStatementTriggerArgument.Named(new Identifier("plain")),
            new PgStatementTriggerArgument.Named(new Identifier("Quoted", QuoteStyle.Ansi)),
            new PgStatementTriggerArgument.Literal(new Value.Number("1", false)),
            new PgStatementTriggerArgument.Literal(new Value.SingleQuotedString("1")),
            new PgStatementTriggerArgument.Literal(new Value.SingleQuotedString("it's"))
        ];
        string[] expected = ["plain", "\"Quoted\"", "1", "'1'", "'it''s'"];
        for (int index = 0; index < arguments.Length; index++)
        {
            using var writer = new SqlTextWriter(new StringBuilder());
            arguments[index].FormatSql(writer, Helpers.CreateFormatManager());

            Assert.Equal(expected[index], writer.ToString());
            Assert.Equal(expected[index], arguments[index].ToSql());
        }
        var execute = new PgTriggerExecute(new ObjectName(new Identifier("fire")), [.. arguments], false);
        Assert.Equal("EXECUTE FUNCTION fire(plain, \"Quoted\", 1, '1', 'it''s')", execute.ToSql());
    }

    [Theory]
    [InlineData("INSERT")]
    [InlineData("UPDATE")]
    [InlineData("DELETE")]
    [InlineData("TRUNCATE")]
    public void TriggerEventFormatting_DelegatesToSql(string text)
    {
        using var writer = new SqlTextWriter(new StringBuilder());

        TriggerEvent.Parse(text).FormatSql(writer, Helpers.CreateFormatManager());

        Assert.Equal(text, writer.ToString());
    }

    [Theory]
    [InlineData(false, false, "OLD TABLE rows")]
    [InlineData(false, true, "OLD TABLE AS rows")]
    [InlineData(true, false, "NEW TABLE rows")]
    [InlineData(true, true, "NEW TABLE AS rows")]
    public void TriggerTransitionFormatting_PreservesRowKindAndAs(bool isNew, bool useAs, string expected)
    {
        var relation = new PgTriggerTransitionRelation(isNew, new Identifier("rows"), useAs);
        using var writer = new SqlTextWriter(new StringBuilder());

        relation.FormatSql(writer, Helpers.CreateFormatManager());

        Assert.Equal(expected, writer.ToString());
    }

    [Fact]
    public void SequenceOptionFormatting_DelegatesToSqlAndUsesInvariantNumbers()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NegativeSign = "~";
        try
        {
            CultureInfo.CurrentCulture = culture;
            PgStatementSequenceOption[] options =
            [
                new PgStatementSequenceOption.Start(-1),
                new PgStatementSequenceOption.Start(-1) { UseWith = true },
                new PgStatementSequenceOption.Increment(-2),
                new PgStatementSequenceOption.Increment(-2) { UseBy = true },
                new PgStatementSequenceOption.MinValue(long.MinValue),
                new PgStatementSequenceOption.MinValue(null),
                new PgStatementSequenceOption.MaxValue(-1),
                new PgStatementSequenceOption.MaxValue(null),
                new PgStatementSequenceOption.Cache(2),
                new PgStatementSequenceOption.Cycle(true),
                new PgStatementSequenceOption.Cycle(false)
            ];
            string[] expected =
            [
                "START -1", "START WITH -1", "INCREMENT -2", "INCREMENT BY -2",
                "MINVALUE -9223372036854775808", "NO MINVALUE", "MAXVALUE -1",
                "NO MAXVALUE", "CACHE 2", "CYCLE", "NO CYCLE"
            ];
            for (int index = 0; index < options.Length; index++)
            {
                using var writer = new SqlTextWriter(new StringBuilder());
                options[index].FormatSql(writer, Helpers.CreateFormatManager());
                Assert.Equal(expected[index], writer.ToString());
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Theory]
    [InlineData("app.locale", "\"app\".\"locale\"")]
    [InlineData("\"App\".\"Locale\"", "\"App\".\"Locale\"")]
    [InlineData("\"C\"", "\"C\"")]
    public void ColumnCollations_FormatNamesUsingIdentifierSettings(string collation, string expected)
    {
        using var formatter = Helpers.CreateFormatter();

        string actual = formatter.GetFormatted($"CREATE TABLE items (label TEXT COLLATE {collation} NOT NULL)");

        Assert.Contains("COLLATE " + expected, actual);
    }

    [Theory]
    [InlineData("app.locale", "\"app\".\"locale\"")]
    [InlineData("\"C\"", "\"C\"")]
    [InlineData("\"App\".\"Locale\"", "\"App\".\"Locale\"")]
    public void ExpressionCollations_FormatOperandAndCollationNames(string collation, string expected)
    {
        using var formatter = Helpers.CreateFormatter();

        string actual = formatter.GetFormatted($"SELECT (items.label || 'suffix') COLLATE {collation} FROM items");

        Assert.Contains("(\"items\".\"label\" || 'suffix') COLLATE " + expected, actual);
    }

    [Theory]
    [InlineData("E'it\\'s\\n\\\\'")]
    [InlineData("E'\\u00e9'")]
    public void EscapeStrings_FormattingPreservesRawLiteral(string literal)
    {
        using var formatter = Helpers.CreateFormatter();

        string actual = formatter.GetFormatted("SELECT " + literal);

        Assert.Contains(literal, actual);
    }

    [Theory]
    [InlineData("it''s\\00e9\\\\", null, "U&'it''s\\00e9\\\\'")]
    [InlineData("it''s!00e9!!", '!', "U&'it''s!00e9!!' UESCAPE '!'")]
    public void UnicodeStrings_FormatWithoutRewritingEscapes(string raw, char? escape, string expected)
    {
        var value = new Value.UnicodeString(raw) { EscapeCharacter = escape };
        using var writer = new SqlTextWriter(new StringBuilder());

        ((IWriteSql)value).FormatSql(writer, Helpers.CreateFormatManager());

        Assert.Equal(expected, writer.ToString());
        Assert.Equal(expected, value.ToSql());
    }

    [Theory]
    [InlineData("B'00101'")]
    [InlineData("X'0aF'")]
    public void BitStrings_FormattingPreservesRadixAndRawDigits(string literal)
    {
        using var formatter = Helpers.CreateFormatter();

        string actual = formatter.GetFormatted("SELECT " + literal);

        Assert.Contains(literal, actual);
    }

    [Theory]
    [InlineData("SELECT $1", "$1")]
    [InlineData("SELECT $001::INT", "$1::INT")]
    [InlineData("SELECT $1 + $2", "$1 + $2")]
    public void PositionalParameters_FormattingKeepsDollarNotation(string sql, string expected)
    {
        using var formatter = Helpers.CreateFormatter();

        string actual = formatter.GetFormatted(sql);

        Assert.Contains(expected, actual);
    }
    [Theory]
    [InlineData("0x_Ff_01")]
    [InlineData("0O_7_5_5")]
    [InlineData("0B_1_0_1")]
    [InlineData("1_000.2_5")]
    [InlineData(".2_5")]
    [InlineData("1_0e+1_2")]
    public void ExtendedNumbers_FormattingPreservesPrefixAndSeparatorSpelling(string literal)
    {
        using var formatter = Helpers.CreateFormatter();
        string actual = formatter.GetFormatted("SELECT " + literal);
        Assert.Contains(literal, actual);
    }

    [Theory]
    [InlineData("ARRAY[]", "ARRAY[]")]
    [InlineData("ARRAY[[value], [other]]", "ARRAY[[\"value\"], [\"other\"]]")]
    [InlineData("ARRAY[ARRAY[value]]", "ARRAY[ARRAY[\"value\"]]")]
    [InlineData("ARRAY(VALUES (1), (2))", "ARRAY(VALUES (1), (2))")]
    [InlineData("arr[lower:upper][index]", "\"arr\"[\"lower\":\"upper\"][\"index\"]")]
    [InlineData("arr[:][1:][:2]", "\"arr\"[:][1:][:2]")]
    [InlineData("(item).field[1].name", "(\"item\").\"field\"[1].\"name\"")]
    [InlineData("arr[(item).lo:hi]", "\"arr\"[(\"item\").\"lo\":\"hi\"]")]
    [InlineData("ROW(value, 1)", "ROW(\"value\", 1)")]
    [InlineData("ROW(items.*, (item).*)", "ROW(items.*, (\"item\").*)")]
    public void ArrayRowAndCompositeExpressions_FormatRecursively(string sql, string expected)
    {
        var state = new Core.Parsing.ParserState([.. new PgLexer().Tokenize(sql)]);
        var expression = new PgParser().ExpressionParser.ParseExpr(state);
        using var writer = new SqlTextWriter(new StringBuilder());

        expression.FormatSql(writer, Helpers.CreateFormatManager());

        Assert.Equal(expected, writer.ToString().NormalizeWhitespace());
    }

    [Theory]
    [InlineData("doc ->> 'name'", "\"doc\" ->> 'name'")]
    [InlineData("doc ?| ARRAY[key]", "\"doc\" ?| ARRAY[\"key\"]")]
    [InlineData("doc OPERATOR(app.@>) other", "\"doc\" OPERATOR(\"app\".@>) \"other\"")]
    [InlineData("doc OPERATOR(@>) other", "\"doc\" OPERATOR(@>) \"other\"")]
    [InlineData("doc @> ANY(ARRAY[other])", "\"doc\" @> ANY(ARRAY[\"other\"])")]
    [InlineData("@ value", "@ \"value\"")]
    [InlineData("OPERATOR(app.@) value", "OPERATOR(\"app\".@) \"value\"")]
    public void PostgresOperators_FormatOperandsAndSchemaNames(string sql, string expected)
    {
        var state = new Core.Parsing.ParserState([.. new PgLexer().Tokenize(sql)]);
        var expression = new PgParser().ExpressionParser.ParseExpr(state);
        using var writer = new SqlTextWriter(new StringBuilder());

        expression.FormatSql(writer, Helpers.CreateFormatManager());

        Assert.Equal(expected, writer.ToString());
    }

    [Theory]
    [InlineData("INTERVAL '1 day'")]
    [InlineData("INTERVAL(3) '1.234 seconds'")]
    [InlineData("INTERVAL '1-2' YEAR TO MONTH")]
    [InlineData("INTERVAL '2 03:04:05.678' DAY TO SECOND(3)")]
    [InlineData("INTERVAL E'1\\x20day'")]
    [InlineData("INTERVAL $duration$1 day$duration$")]
    public void IntervalLiterals_FormatWithoutLosingModifiersOrLiteralSpelling(string sql)
    {
        var state = new Core.Parsing.ParserState([.. new PgLexer().Tokenize(sql)]);
        var expression = new PgParser().ExpressionParser.ParseExpr(state);
        using var writer = new SqlTextWriter(new StringBuilder());

        expression.FormatSql(writer, Helpers.CreateFormatManager());

        Assert.Equal(sql, writer.ToString());
    }

    [Theory]
    [InlineData("items.left IS DISTINCT FROM items.right", "\"items\".\"left\" IS DISTINCT FROM \"items\".\"right\"")]
    [InlineData("items.left IS NOT DISTINCT FROM items.right", "\"items\".\"left\" IS NOT DISTINCT FROM \"items\".\"right\"")]
    [InlineData("items.value = ANY(ARRAY[items.other])", "\"items\".\"value\" = ANY(ARRAY[\"items\".\"other\"])")]
    [InlineData("items.value <> ALL(ARRAY[items.other])", "\"items\".\"value\" <> ALL(ARRAY[\"items\".\"other\"])")]
    [InlineData("items.value > SOME(ARRAY[items.other])", "\"items\".\"value\" > SOME(ARRAY[\"items\".\"other\"])")]
    public void RemainingComparisons_FormatOperandsRecursively(string sql, string expected)
    {
        var state = new Core.Parsing.ParserState([.. new PgLexer().Tokenize(sql)]);
        var expression = new PgParser().ExpressionParser.ParseExpr(state);
        using var writer = new SqlTextWriter(new StringBuilder());

        expression.FormatSql(writer, Helpers.CreateFormatManager());

        Assert.Equal(expected, writer.ToString());
    }

    [Theory]
    [InlineData("items.value = ANY(SELECT items.other FROM items)", "\"items\".\"value\" = ANY( SELECT \"items\".\"other\" FROM \"items\")")]
    [InlineData("value LIKE ANY(SELECT pattern FROM items)", "\"value\" LIKE ANY( SELECT \"pattern\" FROM \"items\")")]
    public void QuantifiedSubqueries_FormatOperandsRecursively(string sql, string expected)
    {
        var state = new Core.Parsing.ParserState([.. new PgLexer().Tokenize(sql)]);
        var expression = new PgParser().ExpressionParser.ParseExpr(state);
        using var writer = new SqlTextWriter(new StringBuilder());

        expression.FormatSql(writer, Helpers.CreateFormatManager());

        Assert.Equal(expected, writer.ToString().NormalizeWhitespace());
    }

    [Theory]
    [InlineData("!", " ESCAPE '!'")]
    [InlineData("'", " ESCAPE ''''")]
    public void PatternEscapeCharacter_FormatsLiteral(string value, string expected)
    {
        var escape = new PatternEscape.Character(value);
        using var writer = new SqlTextWriter(new StringBuilder());

        escape.FormatSql(writer, Helpers.CreateFormatManager());

        Assert.Equal(expected, writer.ToString());
    }

    [Theory]
    [InlineData("items.stamp AT LOCAL", "\"items\".\"stamp\" AT LOCAL")]
    [InlineData("value BETWEEN SYMMETRIC lower AND upper", "\"value\" BETWEEN SYMMETRIC \"lower\" AND \"upper\"")]
    [InlineData("value NOT BETWEEN ASYMMETRIC lower AND upper", "\"value\" NOT BETWEEN ASYMMETRIC \"lower\" AND \"upper\"")]
    [InlineData("value BETWEEN lower AND upper", "\"value\" BETWEEN \"lower\" AND \"upper\"")]
    [InlineData("items.a ^ items.b", "\"items\".\"a\" ^ \"items\".\"b\"")]
    [InlineData("value LIKE ALL(ARRAY[pattern])", "\"value\" LIKE ALL(ARRAY[\"pattern\"])")]
    [InlineData("value ILIKE SOME(ARRAY[pattern])", "\"value\" ILIKE SOME(ARRAY[\"pattern\"])")]
    [InlineData("value LIKE pattern ESCAPE escape_value", "\"value\" LIKE \"pattern\" ESCAPE \"escape_value\"")]
    [InlineData("value ILIKE pattern ESCAPE '!' || suffix", "\"value\" ILIKE \"pattern\" ESCAPE '!' || \"suffix\"")]
    [InlineData("value SIMILAR TO pattern ESCAPE escape_value", "\"value\" SIMILAR TO \"pattern\" ESCAPE \"escape_value\"")]
    [InlineData("value SIMILAR TO pattern", "\"value\" SIMILAR TO \"pattern\"")]
    [InlineData("value NOT SIMILAR TO pattern", "\"value\" NOT SIMILAR TO \"pattern\"")]
    public void Predicates_FormatOperandsAndModifiers(string sql, string expected)
    {
        var state = new Core.Parsing.ParserState([.. new PgLexer().Tokenize(sql)]);
        var expression = new PgParser().ExpressionParser.ParseExpr(state);
        using var writer = new SqlTextWriter(new StringBuilder());

        expression.FormatSql(writer, Helpers.CreateFormatManager());

        Assert.Equal(expected, writer.ToString());
    }

    [Theory]
    [InlineData("LIKE")]
    [InlineData("ILIKE")]
    [InlineData("SIMILAR TO")]
    public void KitchenSinkPatterns_FormatLegacyAnyAndCharacterEscape(string op)
    {
        var state = new Core.Parsing.ParserState([.. new Core.Lexing.GenericLexer().Tokenize($"value NOT {op} ANY(pattern) ESCAPE '!'")]);
        var expression = new Core.Parsing.Parser().ExpressionParser.ParseExpr(state);
        using var writer = new SqlTextWriter(new StringBuilder());

        expression.FormatSql(writer, Helpers.CreateFormatManager());

        Assert.Equal($"\"value\" NOT {op} ANY (\"pattern\") ESCAPE '!'", writer.ToString());
    }

    [Theory]
    [InlineData("INSERT INTO app.items DEFAULT VALUES RETURNING *", "DEFAULT VALUES RETURNING *")]
    [InlineData("INSERT INTO app.items DEFAULT VALUES ON CONFLICT DO NOTHING", "ON CONFLICT DO NOTHING")]
    [InlineData("INSERT INTO app.items VALUES (1) ON CONFLICT ON CONSTRAINT items_pkey DO NOTHING", "ON CONFLICT ON CONSTRAINT \"items_pkey\" DO NOTHING")]
    [InlineData("INSERT INTO app.items VALUES (1) ON CONFLICT ON CONSTRAINT items_pkey DO UPDATE SET id = excluded.id", "ON CONFLICT ON CONSTRAINT \"items_pkey\" DO UPDATE SET")]
    [InlineData("INSERT INTO app.items AS target (id) OVERRIDING SYSTEM VALUE VALUES (1) ON CONFLICT (id) DO UPDATE SET id = excluded.id WHERE target.id <> excluded.id RETURNING target.*", "OVERRIDING SYSTEM VALUE")]
    [InlineData("INSERT INTO app.items VALUES (1) ON CONFLICT ((app.key(name)) COLLATE app.locale app.text_ops (option = 1)) WHERE app.active(id) DO NOTHING", "COLLATE \"app\".\"locale\" \"app\".\"text_ops\" (\"option\" = 1)")]
    [InlineData("UPDATE app.items SET id = source.id FROM app.source AS source, app.other WHERE items.id = source.id RETURNING items.*", "RETURNING \"items\".*")]
    [InlineData("DELETE FROM app.items USING app.source, app.other WHERE items.id = source.id RETURNING *", "USING \"app\".\"source\", \"app\".\"other\"")]
    public void DmlComponents_FormatAndReparseWithoutLosingClauses(string sql, string expectedClause)
    {
        using var formatter = Helpers.CreateFormatter();

        string formatted = formatter.GetFormatted(sql);

        Assert.Contains("\"app\".\"items\"", formatted);
        Assert.Contains(expectedClause, formatted.NormalizeWhitespace());
        var state = new Core.Parsing.ParserState([.. new PgLexer().Tokenize(formatted)]);
        Assert.IsType<Statement>(new PgParser().ParseStatement(state), exactMatch: false);
        Assert.True(state.ConsumeTokenIs<Core.Lexing.Tokens.Semicolon>());
        Assert.IsType<Core.Lexing.Tokens.Eof>(state.Peek());
        Assert.Equal(formatted, formatter.GetFormatted(formatted));
    }

    [Theory]
    [InlineData("WITH result AS MATERIALIZED (SELECT id FROM app.items) SELECT id FROM result", "AS MATERIALIZED (")]
    [InlineData("WITH result AS NOT MATERIALIZED (SELECT id FROM app.items) SELECT id FROM result", "AS NOT MATERIALIZED (")]
    [InlineData("WITH result AS MATERIALIZED (VALUES (1)) SELECT column1 FROM result", "AS MATERIALIZED (")]
    [InlineData("WITH changed AS (INSERT INTO app.items DEFAULT VALUES RETURNING id) SELECT id FROM changed", "DEFAULT VALUES RETURNING")]
    [InlineData("WITH changed AS (UPDATE app.items SET id = 1 RETURNING id) SELECT id FROM changed", "UPDATE \"app\".\"items\"")]
    [InlineData("WITH changed AS (DELETE FROM app.items RETURNING id) INSERT INTO app.archive SELECT id FROM changed", "DELETE FROM \"app\".\"items\"")]
    [InlineData("WITH changed AS ( /* lead */ DELETE FROM app.items RETURNING id /* tail */ ) SELECT id FROM changed", "/* tail */")]
    [InlineData("WITH RECURSIVE tree (id) AS (VALUES (1) UNION ALL SELECT id + 1 FROM tree WHERE id < 3) SEARCH DEPTH FIRST BY id SET position CYCLE id SET is_cycle TO 'yes' DEFAULT 'no' USING path SELECT id, position, is_cycle, path FROM tree", "SEARCH DEPTH FIRST BY \"id\" SET \"position\" CYCLE \"id\" SET \"is_cycle\" TO 'yes' DEFAULT 'no' USING \"path\"")]
    public void CteComponents_FormatAndReparseWithoutLosingClauses(string sql, string expectedClause)
    {
        using var formatter = Helpers.CreateFormatter();

        string formatted = formatter.GetFormatted(sql);

        Assert.Contains(expectedClause, formatted.NormalizeWhitespace());
        var state = new Core.Parsing.ParserState([.. new PgLexer().Tokenize(formatted)]);
        Assert.IsType<Statement>(new PgParser().ParseStatement(state), exactMatch: false);
        Assert.True(state.ConsumeTokenIs<Core.Lexing.Tokens.Semicolon>());
        Assert.IsType<Core.Lexing.Tokens.Eof>(state.Peek());
        Assert.Equal(formatted, formatter.GetFormatted(formatted));
    }

    [Theory]
    [InlineData("SELECT COUNT(*) FILTER (WHERE active) FROM items", "FILTER (WHERE \"active\")", Label = "Format aggregate filter")]
    [InlineData("SELECT PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY value DESC NULLS LAST) FROM items", "WITHIN GROUP (ORDER BY \"value\" DESC NULLS LAST)", Label = "Format within group")]
    [InlineData("SELECT SUM(value) OVER result FROM items WINDOW result AS (PARTITION BY category ORDER BY id NULLS FIRST)", "WINDOW \"result\" AS (PARTITION BY \"category\" ORDER BY \"id\" NULLS FIRST)", Label = "Format named window")]
    [InlineData("SELECT SUM(value) OVER (base ROWS BETWEEN (offset_value + 1) PRECEDING AND 1 FOLLOWING EXCLUDE TIES) FROM items WINDOW base AS (ORDER BY id)", "OVER (\"base\" ROWS BETWEEN (\"offset_value\" + 1) PRECEDING AND 1 FOLLOWING EXCLUDE TIES)", Label = "Format inherited frame")]
    [InlineData("SELECT SUM(value) OVER (ROWS CURRENT ROW EXCLUDE NO OTHERS) FROM items", "ROWS CURRENT ROW EXCLUDE NO OTHERS", Label = "Format single frame bound")]
    [InlineData("SELECT SUM(value) OVER (ROWS BETWEEN UNBOUNDED PRECEDING AND UNBOUNDED FOLLOWING) FROM items", "ROWS BETWEEN UNBOUNDED PRECEDING AND UNBOUNDED FOLLOWING", Label = "Format unbounded bounds")]
    public void AggregateWindows_FormatAndReparseWithoutLosingClauses(string sql, string expectedClause)
    {
        using var formatter = Helpers.CreateFormatter();

        string formatted = formatter.GetFormatted(sql);

        Assert.Contains(expectedClause, formatted.NormalizeWhitespace());
        var state = new Core.Parsing.ParserState([.. new PgLexer().Tokenize(formatted)]);
        Assert.IsType<Select>(new PgParser().ParseStatement(state));
        Assert.True(state.ConsumeTokenIs<Core.Lexing.Tokens.Semicolon>());
        Assert.IsType<Core.Lexing.Tokens.Eof>(state.Peek());
        Assert.Equal(formatted, formatter.GetFormatted(formatted));
    }
}
