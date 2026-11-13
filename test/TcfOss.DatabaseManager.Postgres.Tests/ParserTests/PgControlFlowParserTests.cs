using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Attributes;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Tests.ParserTests;
using TcfOss.DatabaseManager.Postgres.Lexing;
using TcfOss.DatabaseManager.Postgres.Parsing;

namespace TcfOss.DatabaseManager.Postgres.Tests.ParserTests;

public class PgControlFlowParserTests : ParserTestsBase<PgLexer, PgParser>
{
    [Theory]
    [InlineData("BEGIN NULL; END")]
    [InlineData("DECLARE x INT; y CONSTANT TEXT NOT NULL DEFAULT 'a'; BEGIN NULL; END")]
    [InlineData("DECLARE x INT := 1; y INT = 2; BEGIN BEGIN NULL; END; END")]
    [InlineData("DECLARE value TEXT COLLATE app.my_collation; BEGIN NULL; END")]
    [InlineData("BEGIN NULL; EXCEPTION WHEN division_by_zero OR SQLSTATE '22012' THEN NULL; WHEN OTHERS THEN NULL; END")]
    [InlineData("<<outer_label>> DECLARE x INT DEFAULT 1; BEGIN <<inner_label>> BEGIN NULL; END inner_label; EXCEPTION WHEN OTHERS THEN NULL; END outer_label")]
    [InlineData("<<\"Outer\">> BEGIN NULL; END \"Outer\"")]
    public void ProceduralBlocks_RoundTripSqlFormattingAndJson(string sql)
    {
        var statement = Assert.IsType<Statement>(ParseStatement(sql), exactMatch: false);
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        string formatted = Format(statement);
        Assert.Equal(statement, ParseStatement(formatted));
        Assert.Equal(formatted, Format(ParseStatement(formatted)));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
    }

    [Theory]
    [InlineData("BEGIN NULL END")]
    [InlineData("DECLARE x INT BEGIN NULL; END")]
    [InlineData("BEGIN NULL; EXCEPTION END")]
    [InlineData("BEGIN NULL; EXCEPTION WHEN OTHERS THEN NULL END")]
    [InlineData("<<a>> BEGIN NULL; END b")]
    [InlineData("<<a>> SELECT 1")]
    [InlineData("<<'a'>> BEGIN NULL; END")]
    [InlineData("<<\"A\">> BEGIN NULL; END a")]
    [InlineData("BEGIN NULL; END missing_label")]
    [InlineData("BEGIN NULL; EXCEPTION WHEN SQLSTATE 22012 THEN NULL; END")]
    [InlineData("label: BEGIN NULL; END")]
    public void ProceduralBlocks_RejectInvalidSyntax(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Fact]
    public void ProceduralBlocks_PreserveComponentsAndLabelSource()
    {
        const string sql = "/* leading */ <<outer_label>> DECLARE x CONSTANT INT NOT NULL := 1; BEGIN NULL; EXCEPTION WHEN division_by_zero OR SQLSTATE '22012' THEN NULL; END outer_label;";
        var labeled = Assert.IsType<LabeledStatement.Pg>(ParseStatement(sql, sourceId: 7));
        Assert.Equal(0, labeled.Meta.Start);
        Assert.Equal(sql.Length - 1, labeled.Meta.End);
        Assert.Equal(sql.Length, labeled.Meta.EndPlusTerminatorLength);
        Assert.Equal(7, labeled.Label.Source!.Value.SourceId);
        Assert.Equal(sql.IndexOf("outer_label", StringComparison.Ordinal), labeled.Label.Source.Value.Start);
        var block = Assert.IsType<BeginEnd>(labeled.SubStatement);
        var options = Assert.IsType<PgBlockOptions>(block.PgOptions);
        var variable = Assert.Single(options.Declarations!.OfType<PgBlockVariable>());
        Assert.True(variable.Constant);
        Assert.True(variable.NotNull);
        Assert.NotNull(variable.Default);
        Assert.Equal(PgInitializerOperator.Assignment, variable.Default.Op);
        Assert.Single(block.Statements.OfType<PgNull>());
        var handler = Assert.Single(options.Handlers!);
        Assert.IsType<PgExceptionCondition.Named>(handler.Conditions[0]);
        Assert.Equal("22012", Assert.IsType<PgExceptionCondition.SqlState>(handler.Conditions[1]).Value);
        Assert.Single(handler.Statements.OfType<PgNull>());
        Assert.Equal("outer_label", options.EndLabel!.Name);
    }

    [Fact]
    public void ProceduralBlockFailure_RestoresContextAndLabel()
    {
        ParserState state = GetState("<<broken>> BEGIN NULL END");
        using (state.EnterContext(ParseContext.DomainCheck))
        {
            Assert.ThrowsAny<ParseException>(() => new PgParser().ParseStatement(state));
            Assert.Equal(ParseContext.DomainCheck, state.Context);
            Assert.Null(state.StatementLabel);
        }
    }

    [Theory]
    [InlineData("DECLARE x INT; y TEXT; BEGIN NULL; END")]
    [InlineData("<<outer_label>> DECLARE x INT; BEGIN NULL; END outer_label")]
    public void ProceduralDeclarations_AreSupportedByCoreKitchenSinkParser(string sql)
    {
        var state = GetState(sql);
        Statement statement = Assert.Single(new Parser().Parse(state, null));
        if (statement is LabeledStatement.Pg labeled)
        {
            statement = labeled.SubStatement;
        }
        var block = Assert.IsType<BeginEnd>(statement);
        Assert.NotEmpty(block.PgOptions!.Declarations!.OfType<PgBlockVariable>());
    }

    [Fact]
    public void ProceduralDeclarations_CoreFallbackRestoresPosition()
    {
        var state = new ParserState(Grammar.All, GetTokens("DECLARE x INT; SELECT 1; BEGIN NULL; END"));
        var exception = Assert.Throws<ParseException.DialectRequired>(() => new Parser().Parse(state, null));
        Assert.Contains("Local variable declaration", exception.Message);
        Assert.True(state.PeekKeyword(Keyword.DECLARE));
        Assert.Equal(ParseContext.None, state.Context);
        Assert.Null(state.StatementLabel);
        Assert.ThrowsAny<ParseException>(() => new Parser().Parse(GetTokens("<<'a'>> BEGIN NULL; END")));
    }

    [Fact]
    public void ProceduralBlocks_PreserveBoundaryComments()
    {
        const string sql = "/* label */ <<outer_label>> DECLARE /* variable */ x INT; /* begin */ BEGIN NULL; /* handler */ EXCEPTION /* when */ WHEN OTHERS THEN NULL; /* end */ END outer_label";
        string formatted = Format(ParseStatement(sql));
        Assert.Contains("/* label */", formatted);
        Assert.Contains("/* variable */", formatted);
        Assert.Contains("/* begin */", formatted);
        Assert.Contains("/* handler */", formatted);
        Assert.Contains("/* when */", formatted);
        Assert.Contains("/* end */", formatted);
        Assert.Equal(formatted, Format(ParseStatement(formatted)));
    }

    [Theory]
    [InlineData("<< /* before */ label /* after */ >> BEGIN NULL; END label")]
    [InlineData("<< -- before\nlabel -- after\n>> BEGIN NULL; END label")]
    [InlineData("BEGIN << /* before */ label /* after */ >> BEGIN NULL; END label; END")]
    public void ProceduralLabels_PreserveInternalComments(string sql)
    {
        var statement = Assert.IsType<Statement>(ParseStatement(sql), exactMatch: false);
        string formatted = Format(statement);
        Assert.Equal(1, formatted.Split("before", StringSplitOptions.None).Length - 1);
        Assert.Equal(1, formatted.Split("after", StringSplitOptions.None).Length - 1);
        Assert.True(formatted.IndexOf("before", StringComparison.Ordinal) < formatted.IndexOf("label", StringComparison.Ordinal));
        Assert.True(formatted.IndexOf("label", StringComparison.Ordinal) < formatted.IndexOf("after", StringComparison.Ordinal));
        Assert.Equal(formatted, Format(ParseStatement(formatted)));
        Statement[] statements = [statement];
        var restored = Assert.Single(Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
        Assert.Equal(formatted, Format(restored));
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
    }

    [Fact]
    public void ProceduralLabels_UseComponentParserIdentifier()
    {
        ParserState state = GetState("<<original>> BEGIN NULL; END");
        var statement = Assert.IsType<LabeledStatement.Pg>(new LabelTransformingParser().ParseStatement(state));
        Assert.Equal("transformed", statement.Label.Name);
    }

    [Theory]
    [InlineData(Grammar.PgPl)]
    [InlineData(Grammar.Pg | Grammar.PgPl)]
    public void ProceduralGrammar_AllowsBlocksAndSharedPostgresSql(Grammar grammar)
    {
        const string sql = "<<label>> DECLARE x INT; BEGIN CREATE TYPE mood AS ENUM ('happy'); NULL; END label";
        var state = new ParserState(grammar, GetTokens(sql));
        var labeled = Assert.IsType<LabeledStatement.Pg>(Assert.Single(new PgParser().Parse(state, null)));
        var block = Assert.IsType<BeginEnd>(labeled.SubStatement);
        Assert.IsType<CreateType.Enumerated>(block.Statements[0]);
        Assert.Equal(grammar, state.Grammar);
    }

    private sealed class LabelTransformingComponentParser(Parser parser) : PgComponentParser(parser)
    {
        public override Identifier ParseIdentifier(ParserState state, string expectedLabel)
        {
            Identifier identifier = base.ParseIdentifier(state, expectedLabel);
            return expectedLabel == "block_label" ? identifier with { Name = "transformed" } : identifier;
        }
    }

    private sealed class LabelTransformingParser : PgParser
    {
        public LabelTransformingParser()
        {
            ComponentParser = new LabelTransformingComponentParser(this);
        }
    }
}
