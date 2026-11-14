using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Extensions;
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
    [InlineData("BEGIN IF TRUE THEN NULL; ELSIF FALSE THEN NULL; ELSEIF FALSE THEN NULL; ELSE END IF; END")]
    [InlineData("BEGIN CASE 1 WHEN 1, 2 THEN NULL; WHEN 3 THEN NULL; ELSE NULL; END CASE; END")]
    [InlineData("BEGIN CASE WHEN TRUE THEN IF FALSE THEN NULL; END IF; ELSE END CASE; END")]
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

    [Theory]
    [InlineData("CASE WHEN TRUE THEN NULL; END CASE", false)]
    [InlineData("CASE WHEN TRUE THEN NULL; WHEN FALSE THEN NULL; ELSE NULL; END CASE", false)]
    [InlineData("CASE 1 WHEN 1, 2 THEN NULL; END CASE", true)]
    [InlineData("CASE 1 WHEN 1 THEN NULL; WHEN 2 THEN NULL; ELSE NULL; END CASE", true)]
    [InlineData("CASE WHEN TRUE THEN CASE 1 WHEN 1 THEN NULL; END CASE; END CASE", false)]
    public void ProceduralCase_ConsumesClosingKeywordsAndPreservesFollowingStatements(string sql, bool simple)
    {
        var block = Assert.IsType<BeginEnd>(ParseStatement($"BEGIN {sql}; NULL; END"));
        Statement[] statements = [.. block.Statements.NonInert()];

        Assert.Equal(2, statements.Length);
        if (simple)
        {
            Assert.IsType<SimpleCase>(statements[0]);
        }
        else
        {
            Assert.IsType<SearchedCase>(statements[0]);
        }
        Assert.IsType<PgNull>(statements[1]);
    }

    [Theory]
    [InlineData("CASE 1 END CASE")]
    [InlineData("CASE WHEN TRUE THEN NULL; END")]
    [InlineData("CASE 1 WHEN 1 THEN NULL; END")]
    [InlineData("CASE WHEN TRUE THEN NULL END CASE")]
    [InlineData("CASE WHEN TRUE THEN NULL WHEN FALSE THEN NULL; END CASE")]
    [InlineData("CASE WHEN TRUE THEN NULL; ELSE NULL END CASE")]
    [InlineData("CASE 1 WHEN 1 THEN NULL END CASE")]
    [InlineData("CASE 1 WHEN 1 THEN NULL; ELSE NULL END CASE")]
    public void ProceduralCase_RejectsMissingBranchesClosingKeywordsAndTerminators(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements($"BEGIN {sql}; END"));
    }

    [Theory]
    [InlineData("BEGIN IF TRUE SELECT 1; END", "THEN")]
    [InlineData("BEGIN IF TRUE THEN NULL END IF; END", "end of statement")]
    [InlineData("BEGIN IF TRUE THEN NULL ELSIF FALSE THEN NULL; END IF; END", "end of statement")]
    [InlineData("BEGIN IF TRUE THEN NULL; ELSIF FALSE THEN NULL END IF; END", "end of statement")]
    [InlineData("BEGIN IF TRUE THEN NULL; ELSE NULL END IF; END", "end of statement")]
    public void ProceduralIf_ReportsSpecificSyntaxErrors(string sql, string expected)
    {
        var exception = Assert.Throws<ParseException.ExpectedButFound>(() => ParseStatements(sql));
        Assert.Contains(expected, exception.Message);
    }

    [Theory]
    [InlineData("CASE 1 WHEN 1 THEN CALL app.work();")]
    [InlineData("CASE WHEN TRUE THEN CALL app.work();")]
    public void ProceduralCase_DistinguishesAbsentAndEmptyElse(string prefix)
    {
        Statement absent = (Statement)ParseStatement(prefix + " END CASE");
        Statement empty = (Statement)ParseStatement(prefix + " ELSE END CASE");
        SqlValueList<Statement>? absentElse = absent is SimpleCase simple ? simple.ElseStatements : ((SearchedCase)absent).ElseStatements;
        SqlValueList<Statement>? emptyElse = empty is SimpleCase simpleEmpty ? simpleEmpty.ElseStatements : ((SearchedCase)empty).ElseStatements;

        Assert.Null(absentElse);
        Assert.NotNull(emptyElse);
        Assert.Empty(emptyElse.NonInert());
        Assert.NotEqual(absent, empty);
        Assert.Contains(" ELSE ", empty.ToSql());
        Assert.Equal(empty, ParseStatement(empty.ToSql()));
        Assert.Equal(empty, ParseStatement(Format(empty)));
        Statement[] statements = [absent, empty];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
    }

    [Theory]
    [InlineData("BEGIN IF TRUE THEN NULL; /* first */ ELSIF FALSE THEN NULL; /* second */ ELSE NULL; /* third */ END IF; END")]
    [InlineData("BEGIN CASE 1 /* first */ WHEN 1 THEN NULL; /* second */ WHEN 2 THEN NULL; /* third */ ELSE NULL; /* fourth */ END CASE; END")]
    [InlineData("BEGIN CASE /* first */ WHEN TRUE THEN NULL; /* second */ ELSE NULL; /* third */ END CASE; END")]
    [InlineData("BEGIN CASE -- first\nWHEN TRUE THEN NULL; -- second\nELSE NULL; -- third\nEND CASE; END")]
    [InlineData("BEGIN IF TRUE THEN NULL; -- first\nELSEIF FALSE THEN NULL; -- second\nELSE NULL; -- third\nEND IF; END")]
    public void ProceduralConditionals_PreserveBoundaryComments(string sql)
    {
        Statement statement = (Statement)ParseStatement(sql);
        string formatted = Format(statement);
        foreach (string marker in new[] { "first", "second", "third", "fourth" }.Where(marker => sql.Contains(marker, StringComparison.Ordinal)))
        {
            Assert.Equal(1, formatted.Split(marker, StringSplitOptions.None).Length - 1);
        }
        Assert.Equal(formatted, Format(ParseStatement(formatted)));
        Statement[] statements = [statement];
        Assert.Equal(formatted, Format(Assert.Single(Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)))));
    }

    [Theory]
    [InlineData("LOOP CALL app.work(); END LOOP")]
    [InlineData("LOOP END LOOP")]
    [InlineData("WHILE FALSE LOOP END LOOP")]
    [InlineData("WHILE value > 0 LOOP CALL app.work(); END LOOP")]
    [InlineData("<<outer_label>> LOOP WHILE TRUE LOOP CALL app.work(); END LOOP; END LOOP outer_label")]
    [InlineData("<<Outer_Label>> WHILE TRUE LOOP CALL app.work(); END LOOP outer_label")]
    [InlineData("<<\"Outer\">> WHILE TRUE LOOP CALL app.work(); END LOOP \"Outer\"")]
    [InlineData("BEGIN LOOP WHILE TRUE LOOP NULL; END LOOP; END LOOP; NULL; END")]
    public void ProceduralLoops_RoundTripSqlFormattingAndJson(string sql)
    {
        Statement statement = Assert.IsType<Statement>(ParseStatement(sql), exactMatch: false);
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        string formatted = Format(statement);
        Assert.Equal(statement, ParseStatement(formatted));
        Assert.Equal(formatted, Format(ParseStatement(formatted)));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
    }

    [Theory]
    [InlineData("LOOP CALL app.work() END LOOP")]
    [InlineData("LOOP CALL app.work(); END")]
    [InlineData("WHILE TRUE CALL app.work(); END LOOP")]
    [InlineData("WHILE TRUE DO CALL app.work(); END WHILE")]
    [InlineData("WHILE TRUE LOOP CALL app.work() END LOOP")]
    [InlineData("WHILE TRUE LOOP CALL app.work(); END WHILE")]
    [InlineData("<<a>> LOOP CALL app.work(); END LOOP b")]
    [InlineData("<<a>> WHILE TRUE LOOP CALL app.work(); END LOOP b")]
    [InlineData("<<\"A\">> LOOP CALL app.work(); END LOOP a")]
    [InlineData("LOOP CALL app.work(); END LOOP missing_label")]
    public void ProceduralLoops_RejectInvalidSyntax(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Theory]
    [InlineData("LOOP CALL app.work(); END LOOP")]
    [InlineData("WHILE TRUE LOOP CALL app.work(); END LOOP")]
    public void ProceduralLoops_AreRejectedBySqlOnlyGrammar(string sql)
    {
        var state = new ParserState(Grammar.Pg, GetTokens(sql));
        Assert.ThrowsAny<ParseException>(() => new PgParser().Parse(state, null));
    }

    [Fact]
    public void ProceduralLoopLabels_PreserveSourceAndRestoreOuterScope()
    {
        const string sql = "<<outer_label>> LOOP <<inner_label>> WHILE TRUE LOOP CALL app.work(); END LOOP inner_label; LOOP END LOOP; END LOOP outer_label; SELECT 1";
        var state = GetState(sql, sourceId: 9);
        SqlValueList<Statement> statements = ParseStatements(state);
        var outer = Assert.IsType<LabeledStatement.Pg>(statements[0]);
        var loop = Assert.IsType<Loop>(outer.SubStatement);
        Statement[] body = [.. loop.Statements.NonInert()];
        var inner = Assert.IsType<LabeledStatement.Pg>(body[0]);
        var innerWhile = Assert.IsType<While.Pg>(inner.SubStatement);
        Assert.Equal("inner_label", innerWhile.EndLabel!.Name);
        Assert.Null(Assert.IsType<Loop>(body[1]).EndLabel);
        Assert.Equal("outer_label", loop.EndLabel!.Name);
        Assert.Equal(9, loop.EndLabel.Source!.Value.SourceId);
        Assert.Equal(sql.LastIndexOf("outer_label", StringComparison.Ordinal), loop.EndLabel.Source.Value.Start);
        Assert.IsType<Select>(statements[1]);
        Assert.Null(state.StatementLabel);
    }

    [Theory]
    [InlineData("<< /* first */ label /* second */ >> LOOP CALL app.work(); /* third */ END LOOP label")]
    [InlineData("<< /* first */ label /* second */ >> WHILE TRUE LOOP CALL app.work(); /* third */ END LOOP label")]
    public void ProceduralLoops_PreserveLabelAndClosingComments(string sql)
    {
        string formatted = Format(ParseStatement(sql));
        foreach (string marker in new[] { "first", "second", "third" })
        {
            Assert.Equal(1, formatted.Split(marker, StringSplitOptions.None).Length - 1);
        }
        Assert.Equal(formatted, Format(ParseStatement(formatted)));
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
