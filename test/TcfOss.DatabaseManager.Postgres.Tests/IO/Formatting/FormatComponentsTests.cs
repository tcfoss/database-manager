using System.Globalization;
using System.Text;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Tests;
using TcfOss.DatabaseManager.Postgres.Lexing;
using TcfOss.DatabaseManager.Postgres.Parsing;

namespace TcfOss.DatabaseManager.Postgres.Tests.IO.Formatting;

public class FormatComponentsTests
{
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
}
