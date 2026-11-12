using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Attributes;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Tests.ParserTests;
using TcfOss.DatabaseManager.Postgres.Lexing;
using TcfOss.DatabaseManager.Postgres.Parsing;
using Helpers = TcfOss.DatabaseManager.Postgres.Tests.IO.Formatting.Helpers;

namespace TcfOss.DatabaseManager.Postgres.Tests.ParserTests;

public class PgCreateTypeParserTests : ParserTestsBase<PgLexer, PgParser>
{
    [Theory]
    [InlineData(
        "CREATE TYPE app.int4range AS RANGE (SUBTYPE = int4)",
        "CREATE TYPE app.int4range AS RANGE (SUBTYPE = INT)")]
    [InlineData(
        "CREATE TYPE app.float_range AS RANGE (MULTIRANGE_TYPE_NAME = app.float_multirange, SUBTYPE_DIFF = app.float_diff, CANONICAL = app.canonical, COLLATION = app.casefold, SUBTYPE_OPCLASS = app.float_ops, SUBTYPE = app.float)",
        "CREATE TYPE app.float_range AS RANGE (SUBTYPE = app.float, SUBTYPE_OPCLASS = app.float_ops, COLLATION = app.casefold, CANONICAL = app.canonical, SUBTYPE_DIFF = app.float_diff, MULTIRANGE_TYPE_NAME = app.float_multirange)")]
    public void RangeTypes_RoundTripSqlJsonAndFormatting(string sql, string expectedSql)
    {
        var statement = Assert.IsType<CreateType.PgRange>(ParseStatement(sql));

        Assert.Equal(expectedSql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
        using var formatter = Helpers.CreateFormatter();
        string formatted = formatter.GetFormatted(sql);
        Assert.IsType<CreateType.PgRange>(ParseStatement(formatted));
        Assert.Equal(formatted, formatter.GetFormatted(formatted));
    }

    [Theory]
    [InlineData("SUBTYPE_OPCLASS")]
    [InlineData("COLLATION")]
    [InlineData("CANONICAL")]
    [InlineData("SUBTYPE_DIFF")]
    [InlineData("MULTIRANGE_TYPE_NAME")]
    public void RangeTypes_PreserveIndividualOptionalClauses(string optionName)
    {
        string sql = $"CREATE TYPE \"app\".\"r\" AS RANGE (SUBTYPE = INT, {optionName} = \"App\".\"MixedName\")";
        var statement = Assert.IsType<CreateType.PgRange>(ParseStatement(sql));
        ObjectName? value = optionName switch
        {
            "SUBTYPE_OPCLASS" => statement.SubtypeOpClass,
            "COLLATION" => statement.Collation,
            "CANONICAL" => statement.Canonical,
            "SUBTYPE_DIFF" => statement.SubtypeDiff,
            "MULTIRANGE_TYPE_NAME" => statement.MultirangeTypeName,
            _ => throw new InvalidOperationException($"Unexpected option: {optionName}")
        };

        Assert.Equal("\"App\".\"MixedName\"", Assert.IsType<ObjectName>(value).ToSql());
        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
        using var formatter = Helpers.CreateFormatter();
        Assert.Equal(statement, ParseStatement(formatter.GetFormatted(sql)));
    }

    [Theory]
    [InlineData("SuBtYpE")]
    [InlineData("\"subtype\"")]
    public void RangeTypes_AcceptEquivalentOptionNames(string optionName)
    {
        var statement = Assert.IsType<CreateType.PgRange>(
            ParseStatement($"CREATE TYPE r AS RANGE ({optionName} = INT)"));

        Assert.Equal("CREATE TYPE r AS RANGE (SUBTYPE = INT)", statement.ToSql());
    }

    [Theory]
    [InlineData("subtype_opclass")]
    [InlineData("collation")]
    [InlineData("canonical")]
    [InlineData("subtype_diff")]
    [InlineData("multirange_type_name")]
    public void RangeTypes_RespectQuotedOptionalNamesAndDetectDuplicates(string optionName)
    {
        string options = $"SUBTYPE = INT, \"{optionName}\" = app.option_value";
        var statement = Assert.IsType<CreateType.PgRange>(ParseStatement($"CREATE TYPE r AS RANGE ({options})"));
        Assert.Equal(statement, ParseStatement(statement.ToSql()));

        Assert.Throws<ParseException.Duplicate>(() =>
            ParseStatements($"CREATE TYPE r AS RANGE ({options}, {optionName.ToUpperInvariant()} = app.other)"));
        Assert.ThrowsAny<ParseException>(() =>
            ParseStatements($"CREATE TYPE r AS RANGE (SUBTYPE = INT, \"{optionName.ToUpperInvariant()}\" = app.option_value)"));
    }

    [Theory]
    [InlineData("CREATE TYPE mood AS RANGE ()")]
    [InlineData("CREATE TYPE mood AS RANGE (SUBTYPE = INT, )")]
    [InlineData("CREATE TYPE mood AS RANGE (SUBTYPE = INT, SUBTYPE = BIGINT)")]
    [InlineData("CREATE TYPE mood AS RANGE (UNKNOWN = INT, SUBTYPE = INT)")]
    [InlineData("CREATE TYPE mood AS RANGE (SUBTYPE_OPCLASS = app.int_ops)")]
    [InlineData("CREATE TYPE mood AS RANGE (SUBTYPE INT)")]
    [InlineData("CREATE TYPE mood AS RANGE (\"SUBTYPE\" = INT)")]
    [InlineData("CREATE TYPE mood AS RANGE (\"Subtype\" = INT)")]
    [InlineData("CREATE TYPE mood AS RANGE (SUBTYPE = INT, \"subtype\" = BIGINT)")]
    public void RangeTypes_RejectMalformedOrIncompleteOptions(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Theory]
    [InlineData("CREATE TYPE app.widget")]
    [InlineData("CREATE TYPE \"App\".\"Widget\"")]
    [InlineData("CREATE TYPE app.widget (INPUT = app.widget_in, OUTPUT = app.widget_out)")]
    [InlineData("CREATE TYPE app.widget (OUTPUT = \"App\".\"WidgetOut\", \"input\" = 'WidgetIn')")]
    [InlineData("CREATE TYPE app.widget (INPUT = widget_in, OUTPUT = widget_out, INTERNALLENGTH = VARIABLE, ALIGNMENT = double, STORAGE = extended, PASSEDBYVALUE = false)")]
    public void BaseAndShellTypes_RoundTripSqlJsonAndFormatting(string sql)
    {
        var statement = Assert.IsType<CreateType>(ParseStatement(sql), exactMatch: false);

        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
        using var formatter = Helpers.CreateFormatter();
        string formatted = formatter.GetFormatted(sql);
        var formattedStatement = Assert.IsType<CreateType>(ParseStatement(formatted), exactMatch: false);
        Assert.Equal(statement.GetType(), formattedStatement.GetType());
        Assert.Equal(formatted, formatter.GetFormatted(formatted));
    }

    [Theory]
    [InlineData("RECEIVE = \"App\".\"Read\"", typeof(PgStatementBaseTypeOption.Named))]
    [InlineData("SEND = app.write", typeof(PgStatementBaseTypeOption.Named))]
    [InlineData("TYPMOD_IN = app.mod_in", typeof(PgStatementBaseTypeOption.Named))]
    [InlineData("TYPMOD_OUT = app.mod_out", typeof(PgStatementBaseTypeOption.Named))]
    [InlineData("ANALYZE = app.analyze", typeof(PgStatementBaseTypeOption.Named))]
    [InlineData("ANALYSE = app.analyze", typeof(PgStatementBaseTypeOption.Named))]
    [InlineData("SUBSCRIPT = app.subscript", typeof(PgStatementBaseTypeOption.Named))]
    [InlineData("INTERNALLENGTH = 8", typeof(PgStatementBaseTypeOption.Literal))]
    [InlineData("INTERNALLENGTH = -1", typeof(PgStatementBaseTypeOption.Literal))]
    [InlineData("INTERNALLENGTH = +8", typeof(PgStatementBaseTypeOption.Literal))]
    [InlineData("INTERNALLENGTH = VARIABLE", typeof(PgStatementBaseTypeOption.Named))]
    [InlineData("PASSEDBYVALUE", typeof(PgStatementBaseTypeOption.Flag))]
    [InlineData("PASSEDBYVALUE = FALSE", typeof(PgStatementBaseTypeOption.Literal))]
    [InlineData("ALIGNMENT = int4", typeof(PgStatementBaseTypeOption.Named))]
    [InlineData("ALIGNMENT = INT", typeof(PgStatementBaseTypeOption.Named))]
    [InlineData("STORAGE = 'extended'", typeof(PgStatementBaseTypeOption.Literal))]
    [InlineData("LIKE = NUMERIC(10,2)", typeof(PgStatementBaseTypeOption.TypeReference))]
    [InlineData("CATEGORY = 'U'", typeof(PgStatementBaseTypeOption.Literal))]
    [InlineData("PREFERRED = TRUE", typeof(PgStatementBaseTypeOption.Literal))]
    [InlineData("PREFERRED = off", typeof(PgStatementBaseTypeOption.Named))]
    [InlineData("PREFERRED", typeof(PgStatementBaseTypeOption.Flag))]
    [InlineData("DEFAULT = 'it''s a value'", typeof(PgStatementBaseTypeOption.Literal))]
    [InlineData("DEFAULT = $$opaque$$", typeof(PgStatementBaseTypeOption.Literal))]
    [InlineData("DEFAULT = INT", typeof(PgStatementBaseTypeOption.Named))]
    [InlineData("ELEMENT = \"App\".\"Element\"[]", typeof(PgStatementBaseTypeOption.TypeReference))]
    [InlineData("DELIMITER = ';'", typeof(PgStatementBaseTypeOption.Literal))]
    [InlineData("COLLATABLE = TRUE", typeof(PgStatementBaseTypeOption.Literal))]
    [InlineData("COLLATABLE = 0", typeof(PgStatementBaseTypeOption.Literal))]
    [InlineData("COLLATABLE", typeof(PgStatementBaseTypeOption.Flag))]
    public void BaseTypes_PreserveOptionalClauses(string clause, Type optionType)
    {
        string sql = $"CREATE TYPE app.widget (INPUT = app.widget_in, OUTPUT = app.widget_out, {clause})";
        var statement = Assert.IsType<CreateType.PgBase>(ParseStatement(sql));

        Assert.Equal(3, statement.Options.Count);
        Assert.IsType(optionType, statement.Options[2]);
        Assert.Equal(PgBaseTypeOptionName.Input, statement.Options[0].Name);
        Assert.Equal("app.widget_in", Assert.IsType<PgStatementBaseTypeOption.Named>(statement.Options[0]).Value.ToSql());
        Assert.Equal(PgBaseTypeOptionName.Output, statement.Options[1].Name);
        Assert.Equal("app.widget_out", Assert.IsType<PgStatementBaseTypeOption.Named>(statement.Options[1]).Value.ToSql());
        string expectedClause = clause.StartsWith("ANALYSE", StringComparison.Ordinal)
            ? clause.Replace("ANALYSE", "ANALYZE", StringComparison.Ordinal)
            : clause;
        Assert.EndsWith($", {expectedClause})", statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
        using var formatter = Helpers.CreateFormatter();
        string formatted = formatter.GetFormatted(sql);
        var formattedStatement = Assert.IsType<CreateType.PgBase>(ParseStatement(formatted));
        Assert.Equal(statement.Options[2].Name, formattedStatement.Options[2].Name);
        Assert.Equal(formatted, formatter.GetFormatted(formatted));
    }

    [Fact]
    public void ShellTypes_AllowFollowingFunctionAndRangeDeclarations()
    {
        const string sql = """
            CREATE TYPE app.r;
            CREATE FUNCTION app.canonical(value app.r) RETURNS app.r LANGUAGE c AS 'library';
            CREATE TYPE app.r AS RANGE (SUBTYPE = INT, CANONICAL = app.canonical);
            """;

        var statements = ParseStatements(sql);

        Assert.Equal(3, statements.Count);
        Assert.Equal("app.r", Assert.IsType<CreateType.PgShell>(statements[0]).Name.ToSql());
        Assert.IsType<CreateFunction>(statements[1]);
        Assert.IsType<CreateType.PgRange>(statements[2]);
    }

    [Fact]
    public void BaseTypes_FormattingPreservesBareKeywordValues()
    {
        const string sql = "CREATE TYPE app.widget (INPUT = app.widget_in, OUTPUT = app.widget_out, ALIGNMENT = INT, DEFAULT = INT)";
        using var formatter = Helpers.CreateFormatter();

        string formatted = formatter.GetFormatted(sql);

        Assert.Contains("ALIGNMENT = INT", formatted);
        Assert.Contains("DEFAULT = INT", formatted);
        var statement = Assert.IsType<CreateType.PgBase>(ParseStatement(sql));
        var formattedStatement = Assert.IsType<CreateType.PgBase>(ParseStatement(formatted));
        Assert.Equal(statement.Options, formattedStatement.Options);
    }

    [Theory]
    [InlineData("CREATE TYPE t ()")]
    [InlineData("CREATE TYPE t (INPUT = f)")]
    [InlineData("CREATE TYPE t (OUTPUT = f)")]
    [InlineData("CREATE TYPE t (INPUT f, OUTPUT = g)")]
    [InlineData("CREATE TYPE t (INPUT = f, OUTPUT = g,)")]
    [InlineData("CREATE TYPE t (INPUT = f, OUTPUT = g, UNKNOWN = 1)")]
    [InlineData("CREATE TYPE t (\"INPUT\" = f, OUTPUT = g)")]
    [InlineData("CREATE TYPE t (INPUT = f, OUTPUT = g, RECEIVE = 1)")]
    [InlineData("CREATE TYPE t (INPUT = f, OUTPUT = g, INTERNALLENGTH)")]
    [InlineData("CREATE TYPE t (INPUT = f, OUTPUT = g, PASSEDBYVALUE =)")]
    [InlineData("CREATE TYPE t (INPUT = f, OUTPUT = g, DEFAULT = 1 + 2)")]
    [InlineData("CREATE TYPE t AS (INPUT = f, OUTPUT = g)")]
    [InlineData("CREATE TYPE t extra")]
    public void BaseAndShellTypes_RejectMalformedSyntax(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Theory]
    [InlineData("INPUT = f, \"input\" = g, OUTPUT = h")]
    [InlineData("INPUT = f, OUTPUT = g, ANALYZE = a, ANALYSE = b")]
    [InlineData("INPUT = f, OUTPUT = g, PASSEDBYVALUE, passedbyvalue = FALSE")]
    public void BaseTypes_RejectDuplicateOptions(string options)
    {
        Assert.Throws<ParseException.Duplicate>(() => ParseStatements($"CREATE TYPE t ({options})"));
    }
}
