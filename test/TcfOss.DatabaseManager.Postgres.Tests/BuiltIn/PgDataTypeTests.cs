using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Tests.ParserTests;
using TcfOss.DatabaseManager.Postgres.BuiltIn;
using TcfOss.DatabaseManager.Postgres.Lexing;
using TcfOss.DatabaseManager.Postgres.Parsing;

namespace TcfOss.DatabaseManager.Postgres.Tests.BuiltIn;

public class PgDataTypeTests : ParserTestsBase<PgLexer, PgParser>
{
    [Theory]
    [InlineData("INT", DataTypeClass.Integral)]
    [InlineData("BIGINT", DataTypeClass.Integral)]
    [InlineData("BOOLEAN", DataTypeClass.Boolean)]
    public void SharedDataTypes_Parse(string sql, DataTypeClass expectedClass)
    {
        var table = Assert.IsType<CreateTable>(Assert.Single(ParseStatements($"CREATE TABLE sample (value {sql})")));
        var column = Assert.IsType<StatementColumn.Typed>(Assert.Single(table.Columns));

        Assert.Equal(expectedClass, column.DataType.Class);
    }

    [Theory]
    [InlineData("int2", "SMALLINT", DataTypeClass.Integral)]
    [InlineData("smallint", "SMALLINT", DataTypeClass.Integral)]
    [InlineData("int4", "INT", DataTypeClass.Integral)]
    [InlineData("integer", "INT", DataTypeClass.Integral)]
    [InlineData("int8", "BIGINT", DataTypeClass.Integral)]
    [InlineData("bool", "BOOLEAN", DataTypeClass.Boolean)]
    [InlineData("serial2", "SMALLSERIAL", DataTypeClass.Integral)]
    [InlineData("smallserial", "SMALLSERIAL", DataTypeClass.Integral)]
    [InlineData("serial4", "SERIAL", DataTypeClass.Integral)]
    [InlineData("serial", "SERIAL", DataTypeClass.Integral)]
    [InlineData("serial8", "BIGSERIAL", DataTypeClass.Integral)]
    [InlineData("bigserial", "BIGSERIAL", DataTypeClass.Integral)]
    [InlineData("float4", "REAL", DataTypeClass.ApproximateDecimal)]
    [InlineData("real", "REAL", DataTypeClass.ApproximateDecimal)]
    [InlineData("float8", "DOUBLE PRECISION", DataTypeClass.ApproximateDecimal)]
    [InlineData("double precision", "DOUBLE PRECISION", DataTypeClass.ApproximateDecimal)]
    [InlineData("float", "DOUBLE PRECISION", DataTypeClass.ApproximateDecimal)]
    [InlineData("float(1)", "REAL", DataTypeClass.ApproximateDecimal)]
    [InlineData("float(24)", "REAL", DataTypeClass.ApproximateDecimal)]
    [InlineData("float(25)", "DOUBLE PRECISION", DataTypeClass.ApproximateDecimal)]
    [InlineData("float(53)", "DOUBLE PRECISION", DataTypeClass.ApproximateDecimal)]
    [InlineData("money", "MONEY", DataTypeClass.ExactDecimal)]
    [InlineData("numeric", "NUMERIC", DataTypeClass.ExactDecimal)]
    [InlineData("decimal(10)", "NUMERIC(10)", DataTypeClass.ExactDecimal)]
    [InlineData("numeric(10,2)", "NUMERIC(10,2)", DataTypeClass.ExactDecimal)]
    [InlineData("numeric(2,-3)", "NUMERIC(2,-3)", DataTypeClass.ExactDecimal)]
    [InlineData("numeric(3,+5)", "NUMERIC(3,5)", DataTypeClass.ExactDecimal)]
    [InlineData("numeric(1000,-1000)", "NUMERIC(1000,-1000)", DataTypeClass.ExactDecimal)]
    [InlineData("numeric(1000,1000)", "NUMERIC(1000,1000)", DataTypeClass.ExactDecimal)]
    [InlineData("char", "CHAR", DataTypeClass.String)]
    [InlineData("character(3)", "CHAR(3)", DataTypeClass.String)]
    [InlineData("varchar", "VARCHAR", DataTypeClass.String)]
    [InlineData("varchar(20)", "VARCHAR(20)", DataTypeClass.String)]
    [InlineData("character varying", "VARCHAR", DataTypeClass.String)]
    [InlineData("char varying(20)", "VARCHAR(20)", DataTypeClass.String)]
    [InlineData("character varying(20)", "VARCHAR(20)", DataTypeClass.String)]
    [InlineData("bpchar", "BPCHAR", DataTypeClass.String)]
    [InlineData("bpchar(20)", "BPCHAR(20)", DataTypeClass.String)]
    [InlineData("text", "TEXT", DataTypeClass.String)]
    [InlineData("bit", "BIT", DataTypeClass.Binary)]
    [InlineData("bit(8)", "BIT(8)", DataTypeClass.Binary)]
    [InlineData("bit varying", "BIT VARYING", DataTypeClass.Binary)]
    [InlineData("varbit(12)", "BIT VARYING(12)", DataTypeClass.Binary)]
    [InlineData("bytea", "BYTEA", DataTypeClass.Binary)]
    [InlineData("date", "DATE", DataTypeClass.Date)]
    [InlineData("time", "TIME", DataTypeClass.Time)]
    [InlineData("time(0)", "TIME(0)", DataTypeClass.Time)]
    [InlineData("time(6) without time zone", "TIME(6) WITHOUT TIME ZONE", DataTypeClass.Time)]
    [InlineData("timetz(3)", "TIME(3) WITH TIME ZONE", DataTypeClass.Time)]
    [InlineData("timestamp", "TIMESTAMP", DataTypeClass.DateTime)]
    [InlineData("timestamp(6) without time zone", "TIMESTAMP(6) WITHOUT TIME ZONE", DataTypeClass.DateTime)]
    [InlineData("timestamp with time zone", "TIMESTAMP WITH TIME ZONE", DataTypeClass.DateTime)]
    [InlineData("timestamptz(3)", "TIMESTAMP(3) WITH TIME ZONE", DataTypeClass.DateTime)]
    [InlineData("interval", "INTERVAL", DataTypeClass.Interval)]
    [InlineData("interval(6)", "INTERVAL(6)", DataTypeClass.Interval)]
    [InlineData("interval year", "INTERVAL YEAR", DataTypeClass.Interval)]
    [InlineData("interval year to month", "INTERVAL YEAR TO MONTH", DataTypeClass.Interval)]
    [InlineData("interval day to second(3)", "INTERVAL DAY TO SECOND(3)", DataTypeClass.Interval)]
    [InlineData("uuid", "UUID", DataTypeClass.Guid)]
    [InlineData("xml", "XML", DataTypeClass.Xml)]
    [InlineData("json", "JSON", DataTypeClass.Json)]
    [InlineData("jsonb", "JSONB", DataTypeClass.Json)]
    [InlineData("jsonpath", "JSONPATH", DataTypeClass.Json)]
    [InlineData("inet", "INET", DataTypeClass.Network)]
    [InlineData("cidr", "CIDR", DataTypeClass.Network)]
    [InlineData("macaddr", "MACADDR", DataTypeClass.Network)]
    [InlineData("macaddr8", "MACADDR8", DataTypeClass.Network)]
    [InlineData("point", "POINT", DataTypeClass.Geometry)]
    [InlineData("line", "LINE", DataTypeClass.Geometry)]
    [InlineData("lseg", "LSEG", DataTypeClass.Geometry)]
    [InlineData("box", "BOX", DataTypeClass.Geometry)]
    [InlineData("path", "PATH", DataTypeClass.Geometry)]
    [InlineData("polygon", "POLYGON", DataTypeClass.Geometry)]
    [InlineData("circle", "CIRCLE", DataTypeClass.Geometry)]
    [InlineData("tsvector", "TSVECTOR", DataTypeClass.TextSearch)]
    [InlineData("tsquery", "TSQUERY", DataTypeClass.TextSearch)]
    [InlineData("pg_lsn", "PG_LSN", DataTypeClass.System)]
    [InlineData("pg_snapshot", "PG_SNAPSHOT", DataTypeClass.System)]
    [InlineData("txid_snapshot", "TXID_SNAPSHOT", DataTypeClass.System)]
    [InlineData("int4range", "INT4RANGE", DataTypeClass.Range)]
    [InlineData("int8range", "INT8RANGE", DataTypeClass.Range)]
    [InlineData("numrange", "NUMRANGE", DataTypeClass.Range)]
    [InlineData("tsrange", "TSRANGE", DataTypeClass.Range)]
    [InlineData("tstzrange", "TSTZRANGE", DataTypeClass.Range)]
    [InlineData("daterange", "DATERANGE", DataTypeClass.Range)]
    [InlineData("int4multirange", "INT4MULTIRANGE", DataTypeClass.Multirange)]
    [InlineData("int8multirange", "INT8MULTIRANGE", DataTypeClass.Multirange)]
    [InlineData("nummultirange", "NUMMULTIRANGE", DataTypeClass.Multirange)]
    [InlineData("tsmultirange", "TSMULTIRANGE", DataTypeClass.Multirange)]
    [InlineData("tstzmultirange", "TSTZMULTIRANGE", DataTypeClass.Multirange)]
    [InlineData("datemultirange", "DATEMULTIRANGE", DataTypeClass.Multirange)]
    public void DataTypes_ParseAndRoundTrip(string sql, string canonical, DataTypeClass expectedClass)
    {
        DataType dataType = ParseColumnType(sql);

        Assert.Equal(expectedClass, dataType.Class);
        Assert.Equal(canonical, dataType.ToSql());
        Assert.Equal(dataType, ParseColumnType(dataType.ToSql()));
        Assert.True(new PgDataTypeParser().IsDataTypeStart(Assert.IsType<Word>(GetTokens(sql)[0])));
    }

    [Theory]
    [InlineData("YEAR")]
    [InlineData("MONTH")]
    [InlineData("DAY")]
    [InlineData("HOUR")]
    [InlineData("MINUTE")]
    [InlineData("SECOND")]
    [InlineData("YEAR TO MONTH")]
    [InlineData("DAY TO HOUR")]
    [InlineData("DAY TO MINUTE")]
    [InlineData("DAY TO SECOND")]
    [InlineData("HOUR TO MINUTE")]
    [InlineData("HOUR TO SECOND")]
    [InlineData("MINUTE TO SECOND")]
    public void IntervalFields_RoundTrip(string fields)
    {
        Assert.Equal($"INTERVAL {fields}", ParseColumnType($"INTERVAL {fields}").ToSql());
    }

    [Fact]
    public void IntervalRanges_AcceptOnlySupportedFieldPairs()
    {
        string[] fields = ["YEAR", "MONTH", "DAY", "HOUR", "MINUTE", "SECOND"];
        HashSet<(string Start, string End)> validRanges =
        [
            ("YEAR", "MONTH"),
            ("DAY", "HOUR"),
            ("DAY", "MINUTE"),
            ("DAY", "SECOND"),
            ("HOUR", "MINUTE"),
            ("HOUR", "SECOND"),
            ("MINUTE", "SECOND"),
        ];

        foreach (string start in fields)
        {
            foreach (string end in fields)
            {
                string sql = $"INTERVAL {start} TO {end}";
                if (validRanges.Contains((start, end)))
                {
                    Assert.Equal(sql, ParseColumnType(sql).ToSql());
                }
                else
                {
                    Assert.Throws<ParseException.InvalidIntervalFieldRange>(() => ParseColumnType(sql));
                }
            }
        }
    }

    [Fact]
    public void Modifiers_ArePreservedInAst()
    {
        Assert.Equal(new PgDataType.PgNumeric(2, -3), ParseColumnType("NUMERIC(2,-3)"));
        Assert.Equal(new PgDataType.PgVarchar(), ParseColumnType("VARCHAR"));
        Assert.Equal(new PgDataType.PgChar(1), ParseColumnType("CHAR(1)"));
        Assert.Equal(new PgDataType.PgTimestamp(3, true), ParseColumnType("TIMESTAMPTZ(3)"));
        Assert.Equal(new PgDataType.PgTime(0, false), ParseColumnType("TIME(0) WITHOUT TIME ZONE"));
        Assert.Equal(new PgDataType.PgInterval(PgIntervalField.Day, PgIntervalField.Second, 2),
            ParseColumnType("INTERVAL DAY TO SECOND(2)"));
        Assert.NotEqual(ParseColumnType("SERIAL"), ParseColumnType("INT"));
        Assert.NotEqual(ParseColumnType("JSON"), ParseColumnType("JSONB"));
    }

    [Theory]
    [InlineData("TINYINT")]
    [InlineData("NVARCHAR(20)")]
    [InlineData("VARCHAR(MAX)")]
    [InlineData("ENUM('a')")]
    [InlineData("SET('a')")]
    [InlineData("DATETIME")]
    [InlineData("DOUBLE")]
    [InlineData("DOUBLE(10,2)")]
    [InlineData("FLOAT(10,2)")]
    [InlineData("FLOAT(0)")]
    [InlineData("FLOAT(54)")]
    [InlineData("NUMERIC(0)")]
    [InlineData("NUMERIC(1001)")]
    [InlineData("NUMERIC(2,1001)")]
    [InlineData("NUMERIC(2,-1001)")]
    [InlineData("NUMERIC(10,)")]
    [InlineData("CHAR(0)")]
    [InlineData("VARCHAR(0)")]
    [InlineData("BIT(0)")]
    [InlineData("TIME(7)")]
    [InlineData("TIMESTAMP(-1)")]
    [InlineData("TIMESTAMP WITH ZONE")]
    [InlineData("TIMETZ WITHOUT TIME ZONE")]
    [InlineData("INTERVAL YEAR TO DAY")]
    [InlineData("INTERVAL SECOND TO MINUTE")]
    [InlineData("INTERVAL DAY TO YEAR")]
    [InlineData("INTERVAL DAY TO")]
    [InlineData("INTERVAL DAY(2)")]
    [InlineData("INTERVAL(7)")]
    [InlineData("unknown_type")]
    [InlineData("\"integer\"")]
    [InlineData("!")]
    public void InvalidOrUnsupportedTypes_Throw(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseColumnType(sql));
    }

    [Theory]
    [InlineData("CHAR(0)", "length", "0", 1, uint.MaxValue, 5)]
    [InlineData("VARCHAR(4294967296)", "length", "4294967296", 1, uint.MaxValue, 8)]
    [InlineData("BIT(0)", "length", "0", 1, uint.MaxValue, 4)]
    [InlineData("FLOAT(0)", "precision", "0", 1, 53, 6)]
    [InlineData("FLOAT(54)", "precision", "54", 1, 53, 6)]
    [InlineData("NUMERIC(0)", "precision", "0", 1, 1000, 8)]
    [InlineData("NUMERIC(1001)", "precision", "1001", 1, 1000, 8)]
    [InlineData("NUMERIC(2,1001)", "scale", "1001", -1000, 1000, 10)]
    [InlineData("NUMERIC(2,-1001)", "scale", "-1001", -1000, 1000, 10)]
    [InlineData("NUMERIC(999999999999999999999999)", "precision", "999999999999999999999999", 1, 1000, 8)]
    [InlineData("TIME(7)", "precision", "7", 0, 6, 5)]
    [InlineData("INTERVAL(7)", "precision", "7", 0, 6, 9)]
    public void InvalidModifiers_ReportRangeAndLocation(string sql, string label, string value, long minimum, long maximum, int position)
    {
        var exception = Assert.Throws<ParseException.ValueOutOfRange>(() => new PgDataTypeParser().ParseDataType(GetState(sql)));

        Assert.Equal(label, exception.Label);
        Assert.Equal(value, exception.Value);
        Assert.Equal(minimum, exception.Minimum);
        Assert.Equal(maximum, exception.Maximum);
        Assert.Contains($"{new string(' ', position)}^", new ParseException.WithText(exception, null, sql).Message);
        Assert.Contains(label, exception.Message);
        Assert.Contains(value, exception.Message);
    }

    [Theory]
    [InlineData("INTERVAL YEAR TO DAY")]
    [InlineData("INTERVAL SECOND TO MINUTE")]
    [InlineData("INTERVAL DAY TO YEAR")]
    public void InvalidIntervalRanges_ReportValidationError(string sql)
    {
        Assert.Throws<ParseException.InvalidIntervalFieldRange>(() => ParseColumnType(sql));
    }

    [Fact]
    public void IntervalPrecision_RequiresSecondField()
    {
        Assert.Throws<ParseException.IntervalPrecisionRequiresSecond>(() => ParseColumnType("INTERVAL DAY(2)"));
    }

    [Theory]
    [InlineData("YEAR")]
    [InlineData("MONTH")]
    [InlineData("DAY")]
    [InlineData("HOUR")]
    [InlineData("MINUTE")]
    [InlineData("SECOND")]
    public void IntervalField_StringEnumRoundTrips(string text)
    {
        PgIntervalField field = PgIntervalField.Parse(text);

        Assert.True(PgIntervalField.TryParse(text, out PgIntervalField? parsed));
        Assert.Equal(field, parsed);
        Assert.Equal(text, $"{field}");
        Assert.False(PgIntervalField.TryParse("WEEK", out _));
    }

    [Theory]
    [InlineData("UUID '00000000-0000-0000-0000-000000000001'")]
    [InlineData("TEXT 'C:\\'")]
    [InlineData("TEXT 'it''s'")]
    [InlineData("INET '127.0.0.1'")]
    [InlineData("JSONB '{}'")]
    [InlineData("TIMESTAMP(3) WITH TIME ZONE '2026-10-29 12:00:00+00'")]
    [InlineData("INT4RANGE '[1,3)'")]
    public void TypedLiterals_UsePostgresTypes(string sql)
    {
        var state = GetState(sql);
        var expression = Assert.IsType<TypedString>(new PgParser().ExpressionParser.ParseExpr(state));

        Assert.Equal(sql, expression.ToSql());
        Assert.IsType<Eof>(state.Peek());
    }

    [Theory]
    [InlineData("CAST('x' AS VARCHAR)")]
    [InlineData("CAST(1 AS NUMERIC(2,-3))")]
    [InlineData("CAST('2026-10-29' AS TIMESTAMP WITH TIME ZONE)")]
    public void Casts_UsePostgresTypes(string sql)
    {
        var expression = new PgParser().ExpressionParser.ParseExpr(GetState(sql));

        Assert.Equal(sql, expression.ToSql());
    }

    [Theory]
    [InlineData("VARCHAR", true)]
    [InlineData("INET", true)]
    [InlineData("inet", true)]
    [InlineData("INT4MULTIRANGE", true)]
    [InlineData("\"INET\"", false)]
    [InlineData("'INET'", false)]
    [InlineData("TINYINT", false)]
    [InlineData("ENUM", false)]
    [InlineData("NVARCHAR", false)]
    [InlineData("unknown_type", false)]
    public void TypeStart_RecognizesOnlySupportedNames(string sql, bool expected)
    {
        Assert.Equal(expected, new PgDataTypeParser().IsDataTypeStart(GetTokens(sql)[0]));
    }

    [Fact]
    public void TypeKeywords_AgreeWithTypeStart()
    {
        var parser = new PgDataTypeParser();
        foreach (Keyword keyword in Enum.GetValues<Keyword>())
        {
            Assert.Equal(parser.IsDataTypeStart(new Word(keyword.ToString()) { Location = default }), parser.IsDataTypeKeyword(keyword));
        }
        Assert.False(parser.IsDataTypeKeyword(null));
    }

    private static DataType ParseColumnType(string sql)
    {
        var table = Assert.IsType<CreateTable>(Assert.Single(ParseStatements($"CREATE TABLE sample (value {sql})")));
        return Assert.IsType<StatementColumn.Typed>(Assert.Single(table.Columns)).DataType;
    }
}