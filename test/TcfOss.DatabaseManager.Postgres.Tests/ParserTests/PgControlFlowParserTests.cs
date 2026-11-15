using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
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
    [InlineData("BEGIN RAISE; END")]
    [InlineData("BEGIN RAISE 'hello %', value; END")]
    [InlineData("BEGIN RAISE NOTICE 'hello %', value USING HINT = 'try again'; END")]
    [InlineData("BEGIN RAISE USING MESSAGE := 'failed'; END")]
    [InlineData("BEGIN RAISE division_by_zero USING MESSAGE = 'failed', DETAIL = detail; END")]
    [InlineData("BEGIN RAISE SQLSTATE '22012'; END")]
    [InlineData("BEGIN RAISE EXCEPTION USING ERRCODE = '22012', MESSAGE = 'failed'; END")]
    [InlineData("BEGIN ASSERT condition; END")]
    [InlineData("BEGIN ASSERT condition, 'failed'; END")]
    [InlineData("BEGIN GET DIAGNOSTICS row_count = ROW_COUNT, context_text = PG_CONTEXT; END")]
    [InlineData("BEGIN GET CURRENT DIAGNOSTICS routine_oid := PG_ROUTINE_OID; END")]
    [InlineData("BEGIN GET STACKED DIAGNOSTICS state := RETURNED_SQLSTATE, message = MESSAGE_TEXT, detail = PG_EXCEPTION_DETAIL, hint = PG_EXCEPTION_HINT, context = PG_EXCEPTION_CONTEXT, schema_name = SCHEMA_NAME, table_name = TABLE_NAME, column_name = COLUMN_NAME, constraint_name = CONSTRAINT_NAME, datatype_name = PG_DATATYPE_NAME; END")]
    [InlineData("BEGIN NULL; EXCEPTION WHEN OTHERS THEN RAISE; END")]
    [InlineData("BEGIN NULL; EXCEPTION WHEN division_by_zero OR SQLSTATE '22012' THEN ASSERT condition; GET STACKED DIAGNOSTICS state = RETURNED_SQLSTATE, detail = PG_EXCEPTION_DETAIL; END")]
    public void ProceduralRaiseAssertAndDiagnostics_RoundTripSqlFormattingAndJson(string sql)
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
    [InlineData("BEGIN RAISE NOTICE; END")]
    [InlineData("BEGIN RAISE USING MESSAGE =; END")]
    [InlineData("BEGIN RAISE SQLSTATE 22012; END")]
    [InlineData("BEGIN RAISE 1; END")]
    [InlineData("BEGIN ASSERT; END")]
    [InlineData("BEGIN ASSERT TRUE,; END")]
    [InlineData("BEGIN GET DIAGNOSTICS; END")]
    [InlineData("BEGIN GET DIAGNOSTICS target ROW_COUNT; END")]
    [InlineData("BEGIN GET DIAGNOSTICS target = UNKNOWN; END")]
    [InlineData("BEGIN GET DIAGNOSTICS target = RESULT_OID; END")]
    [InlineData("BEGIN GET DIAGNOSTICS target = RETURNED_SQLSTATE; END")]
    [InlineData("BEGIN GET STACKED DIAGNOSTICS target = ROW_COUNT; END")]
    public void ProceduralRaiseAssertAndDiagnostics_RejectInvalidSyntax(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Theory]
    [InlineData("GET DIAGNOSTICS row_count = ROW_COUNT", false, false)]
    [InlineData("GET CURRENT DIAGNOSTICS routine_oid := PG_ROUTINE_OID", true, true)]
    public void ProceduralCurrentDiagnostics_PreserveCurrentAndAssignmentOperator(
        string sql,
        bool printCurrent,
        bool expectedAssignment)
    {
        var statement = Assert.IsType<PgGetDiagnostics.Current>(ParseStatement(sql));
        Assert.Equal(printCurrent, statement.PrintCurrent);
        PgAssignmentOperator expectedOperator = expectedAssignment
            ? PgAssignmentOperator.Assignment
            : PgAssignmentOperator.Equal;
        Assert.Equal(expectedOperator, Assert.Single(statement.Assignments).Op);
        Assert.Equal(sql, statement.ToSql());

        string formatted = Format(statement);
        var formattedStatement = Assert.IsType<PgGetDiagnostics.Current>(ParseStatement(formatted));
        Assert.Equal(printCurrent, formattedStatement.PrintCurrent);
        Assert.Equal(expectedOperator, Assert.Single(formattedStatement.Assignments).Op);

        Statement[] restoredStatements = Serialization.FromJson<Statement[]>(
            Serialization.ToJson(new Statement[] { statement }));
        var restored = Assert.IsType<PgGetDiagnostics.Current>(Assert.Single(restoredStatements));
        Assert.Equal(printCurrent, restored.PrintCurrent);
        Assert.Equal(expectedOperator, Assert.Single(restored.Assignments).Op);
    }

    [Fact]
    public void ProceduralStackedDiagnostics_PreservesAssignmentOperator()
    {
        var statement = Assert.IsType<PgGetDiagnostics.Stacked>(
            ParseStatement("GET STACKED DIAGNOSTICS state := RETURNED_SQLSTATE"));

        Assert.Equal(PgAssignmentOperator.Assignment, Assert.Single(statement.Assignments).Op);
        Assert.Equal("GET STACKED DIAGNOSTICS state := RETURNED_SQLSTATE", statement.ToSql());
    }

    [Theory]
    [InlineData("BEGIN RAISE USING MESSAGE = 'failed'; END", false)]
    [InlineData("BEGIN RAISE USING MESSAGE := 'failed'; END", true)]
    public void ProceduralRaiseUsing_PreservesAssignmentOperator(string sql, bool expectedAssignment)
    {
        var block = Assert.IsType<BeginEnd>(ParseStatement(sql));
        var raise = Assert.IsType<PgRaise.WithOptions>(Assert.Single(block.Statements.NonInert()));
        PgAssignmentOperator expectedOperator = expectedAssignment
            ? PgAssignmentOperator.Assignment
            : PgAssignmentOperator.Equal;

        Assert.Equal(expectedOperator, Assert.Single(raise.Options!).Op);
        Assert.Equal(sql, block.ToSql());
    }

    [Theory]
    [InlineData(Grammar.Pg, "RAISE;")]
    [InlineData(Grammar.My, "RAISE;")]
    [InlineData(Grammar.Ms, "RAISE;")]
    [InlineData(Grammar.Pg, "ASSERT TRUE;")]
    [InlineData(Grammar.My, "ASSERT TRUE;")]
    [InlineData(Grammar.Ms, "ASSERT TRUE;")]
    [InlineData(Grammar.Pg, "GET DIAGNOSTICS row_count = ROW_COUNT;")]
    [InlineData(Grammar.My, "GET DIAGNOSTICS row_count = ROW_COUNT;")]
    [InlineData(Grammar.Ms, "GET DIAGNOSTICS row_count = ROW_COUNT;")]
    public void ProceduralRaiseAssertAndDiagnostics_RequirePlPgSqlGrammar(Grammar grammar, string sql)
    {
        var state = new ParserState(grammar, GetTokens(sql));
        Assert.ThrowsAny<ParseException>(() => new Parser().Parse(state, null));
    }

    [Fact]
    public void ProceduralRaiseAndAssert_CollectExpressionReferences()
    {
        var block = Assert.IsType<BeginEnd>(ParseStatement(
            "BEGIN RAISE NOTICE '%', app.message USING HINT = app.hint; ASSERT app.condition, app.failure; END"));

        string[] references = [.. block.GetReferencedItems(new ReferencedItemsManager
            {
                Filters = ObjectNameFilters.Unknown
            })
            .Select(item => string.Join(".", item.Identifiers.Select(identifier => identifier.Name)))];

        Assert.Equal(["app.message", "app.hint", "app.condition", "app.failure"], references);
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

    [Theory]
    [InlineData("FOR i IN 1..10 LOOP END LOOP")]
    [InlineData("FOR i IN -2 .. +3 LOOP CALL app.work(i); END LOOP")]
    [InlineData("FOR i IN REVERSE app.upper_bound() .. app.lower_bound() BY app.step() LOOP CALL app.work(i); END LOOP")]
    [InlineData("FOR i IN (SELECT min_value FROM bounds) .. 10 BY (1 + 1) LOOP CALL app.work(i); END LOOP")]
    [InlineData("FOR i IN loop.start_value .. bounds.loop BY app.loop() LOOP END LOOP")]
    [InlineData("FOR row_value IN SELECT * FROM app.items LOOP CALL app.work(); END LOOP")]
    [InlineData("FOR first_value, second_value IN SELECT 1, 2 LOOP END LOOP")]
    [InlineData("FOR row_value.field IN SELECT 1 LOOP END LOOP")]
    [InlineData("FOR row_value IN VALUES (1), (2) LOOP END LOOP")]
    [InlineData("FOR row_value IN (SELECT 1) LOOP END LOOP")]
    [InlineData("FOR row_value IN WITH source AS (SELECT 1 AS value) SELECT source.value FROM source LOOP END LOOP")]
    [InlineData("FOR row_value IN SELECT 1 UNION ALL SELECT 2 LOOP END LOOP")]
    [InlineData("FOR row_value IN SELECT (SELECT 1 AS loop), ARRAY[1, 2], 'LOOP' LOOP END LOOP")]
    [InlineData("FOR row_value IN SELECT loop.value, source.loop FROM app.loop AS source LOOP END LOOP")]
    [InlineData("FOR row_value IN INSERT INTO app.items DEFAULT VALUES RETURNING id LOOP END LOOP")]
    [InlineData("FOR row_value IN UPDATE app.items SET id = 1 RETURNING id LOOP END LOOP")]
    [InlineData("FOR row_value IN DELETE FROM app.items RETURNING id LOOP END LOOP")]
    [InlineData("FOR row_value IN EXECUTE 'SELECT 1' LOOP END LOOP")]
    [InlineData("FOR row_value IN EXECUTE app.query_text($1) USING app.value(), (SELECT 1), ARRAY[1, 2] LOOP CALL app.work(); END LOOP")]
    [InlineData("<<outer_label>> FOR i IN 1 .. 2 LOOP FOR row_value IN SELECT 1 LOOP CALL app.work(); END LOOP; END LOOP outer_label")]
    [InlineData("BEGIN FOR i IN 1 .. 2 LOOP WHILE FALSE LOOP NULL; END LOOP; NULL; END LOOP; NULL; END")]
    public void ProceduralFor_RoundTripSqlFormattingAndJson(string sql)
    {
        Statement statement = Assert.IsType<Statement>(ParseStatement(sql), exactMatch: false);
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        string formatted = Format(statement);
        Assert.Equal(statement, ParseStatement(formatted));
        Assert.Equal(formatted, Format(ParseStatement(formatted)));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
    }

    [Fact]
    public void ProceduralFor_PreservesRangeAndTargetComponentsAndFollowingStatement()
    {
        const string sql = "<<outer_label>> FOR i IN REVERSE 10 .. 1 BY 2 LOOP CALL app.work(); END LOOP outer_label; SELECT 1";
        ParserState state = GetState(sql, sourceId: 13);
        SqlValueList<Statement> statements = ParseStatements(state);
        var labeled = Assert.IsType<LabeledStatement.Pg>(statements[0]);
        var loop = Assert.IsType<PgFor>(labeled.SubStatement);
        Identifier variable = Assert.Single(Assert.Single(loop.Targets).Values);
        Assert.Equal("i", variable.Name);
        Assert.Equal(13, variable.Source!.Value.SourceId);
        Assert.Equal(sql.IndexOf("i IN", StringComparison.Ordinal), variable.Source.Value.Start);
        var range = Assert.IsType<PgForSource.Range>(loop.Source);
        Assert.True(range.Reverse);
        Assert.Equal("10", range.LowerBound.ToSql());
        Assert.Equal("1", range.UpperBound.ToSql());
        Assert.Equal("2", range.Step!.ToSql());
        Assert.Equal("outer_label", loop.EndLabel!.Name);
        Assert.Equal(sql.LastIndexOf("outer_label", StringComparison.Ordinal), loop.EndLabel.Source!.Value.Start);
        Assert.IsType<Select>(statements[1]);
        Assert.Null(state.StatementLabel);
        Assert.Equal(ParseContext.None, state.Context);
    }

    [Theory]
    [InlineData("FOR i IN 1 .. 2 /* header */ LOOP CALL app.work(); /* closing */ END LOOP")]
    [InlineData("FOR row_value IN SELECT * FROM app.items /* header */ LOOP CALL app.work(); /* closing */ END LOOP")]
    [InlineData("FOR row_value IN EXECUTE 'SELECT 1' -- header\nLOOP CALL app.work(); -- closing\nEND LOOP")]
    public void ProceduralFor_PreservesBoundaryComments(string sql)
    {
        Statement statement = Assert.IsType<PgFor>(ParseStatement(sql));
        string formatted = Format(statement);
        Assert.Equal(1, formatted.Split("header", StringSplitOptions.None).Length - 1);
        Assert.Equal(1, formatted.Split("closing", StringSplitOptions.None).Length - 1);
        Assert.Equal(formatted, Format(ParseStatement(formatted)));
        Statement[] statements = [statement];
        Assert.Equal(formatted, Format(Assert.Single(Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)))));
    }

    [Theory]
    [InlineData("FOR i IN app.low() .. app.high() BY app.step() LOOP CALL app.work(); END LOOP", new[] { "app.low", "app.high", "app.step", "app.work" })]
    [InlineData("FOR row_value IN SELECT * FROM app.items LOOP CALL app.work(); END LOOP", new[] { "app.items", "app.work" })]
    [InlineData("FOR row_value IN EXECUTE app.query_text() USING app.value() LOOP CALL app.work(); END LOOP", new[] { "app.query_text", "app.value", "app.work" })]
    [InlineData("FOR row_value IN EXECUTE 'SELECT * FROM hidden.items' LOOP END LOOP", new string[0])]
    public void ProceduralFor_ReferencesSourceAndBodyWithoutTreatingTargetsAsObjects(string sql, string[] expected)
    {
        var loop = Assert.IsType<PgFor>(ParseStatement(sql));
        string[] names = [.. loop.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All })
            .OfType<ItemRef.ObjectNameRef>().Select(reference => reference.Name.ToSql())];

        Assert.Equal(expected, names);
        Assert.Empty(loop.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.None }));
    }

    [Theory]
    [InlineData("FOR IN 1 .. 2 LOOP END LOOP")]
    [InlineData("FOR i 1 .. 2 LOOP END LOOP")]
    [InlineData("FOR i IN 1 .. LOOP END LOOP")]
    [InlineData("FOR i IN .. 2 LOOP END LOOP")]
    [InlineData("FOR i IN REVERSE 10 LOOP END LOOP")]
    [InlineData("FOR i IN REVERSE SELECT 1 LOOP END LOOP")]
    [InlineData("FOR i IN 1 .. 2 BY LOOP END LOOP")]
    [InlineData("FOR i, j IN 1 .. 2 LOOP END LOOP")]
    [InlineData("FOR row_value.field IN 1 .. 2 LOOP END LOOP")]
    [InlineData("FOR row_value IN EXECUTE LOOP END LOOP")]
    [InlineData("FOR row_value IN EXECUTE 'SELECT 1' USING LOOP END LOOP")]
    [InlineData("FOR row_value IN EXECUTE 'SELECT 1' USING 1, LOOP END LOOP")]
    [InlineData("FOR row_value IN SELECT 1; LOOP END LOOP")]
    [InlineData("FOR row_value IN SELECT (1 LOOP END LOOP")]
    [InlineData("FOR row_value IN CALL app.work() LOOP END LOOP")]
    [InlineData("FOR row_value IN INSERT INTO app.items DEFAULT VALUES LOOP END LOOP")]
    [InlineData("FOR row_value IN SELECT 1 INTO other_value LOOP END LOOP")]
    [InlineData("FOR row_value IN (SELECT 1 INTO other_value) LOOP END LOOP")]
    [InlineData("FOR row_value IN SELECT 1 INTO other_value UNION ALL SELECT 2 LOOP END LOOP")]
    [InlineData("FOR row_value IN cursor_name(1) LOOP END LOOP")]
    [InlineData("FOR i IN 1 .. 2")]
    [InlineData("FOR i IN 1 .. 2 LOOP")]
    [InlineData("FOR i IN 1 .. 2 LOOP CALL app.work() END LOOP")]
    [InlineData("FOR i IN 1 .. 2 LOOP END FOR")]
    [InlineData("<<label>> FOR i IN 1 .. 2 LOOP END LOOP other_label")]
    [InlineData("<<\"Label\">> FOR i IN 1 .. 2 LOOP END LOOP label")]
    [InlineData("FOR i IN 1 .. 2 LOOP END LOOP unexpected_label")]
    public void ProceduralFor_RejectsInvalidOrDeferredSyntax(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Theory]
    [InlineData(Grammar.Pg)]
    [InlineData(Grammar.My)]
    [InlineData(Grammar.Ms)]
    public void ProceduralFor_RequiresPlPgSqlGrammar(Grammar grammar)
    {
        var state = new ParserState(grammar, GetTokens("FOR i IN 1 .. 2 LOOP END LOOP"));
        Assert.ThrowsAny<ParseException>(() => new Parser().Parse(state, null));
    }

    [Theory]
    [InlineData(Grammar.PgPl)]
    [InlineData(Grammar.All)]
    public void ProceduralFor_IsSupportedByCoreKitchenSinkParser(Grammar grammar)
    {
        var state = new ParserState(grammar, GetTokens("FOR i IN 1 .. 2 LOOP END LOOP"));
        var loop = Assert.IsType<PgFor>(Assert.Single(new Parser().Parse(state, null)));
        Assert.IsType<PgForSource.Range>(loop.Source);
    }

    [Theory]
    [InlineData("<<broken>> FOR i IN 1 .. LOOP END LOOP")]
    [InlineData("<<broken>> FOR row_value IN SELECT (1 LOOP END LOOP")]
    [InlineData("<<broken>> FOR i IN 1 .. 2 LOOP CALL app.work() END LOOP")]
    public void ProceduralFor_FailureRestoresContextAndLabel(string sql)
    {
        ParserState state = GetState(sql);
        Grammar grammar = state.Grammar;
        using (state.EnterContext(ParseContext.DomainCheck))
        {
            Assert.ThrowsAny<ParseException>(() => new PgParser().ParseStatement(state));
            Assert.Equal(ParseContext.DomainCheck, state.Context);
            Assert.Null(state.StatementLabel);
            Assert.Equal(grammar, state.Grammar);
        }
    }

    [Theory]
    [InlineData("FOR i IN 1 .. 2 BY 1 BY 2 LOOP END LOOP", Keyword.BY)]
    [InlineData("FOR row_value IN EXECUTE 'SELECT 1' USING 1 USING 2 LOOP END LOOP", Keyword.USING)]
    public void ProceduralFor_ExpressionSourceFailureLeavesCallerAtUnexpectedToken(string sql, Keyword unexpected)
    {
        ParserState state = GetState(sql);

        Assert.ThrowsAny<ParseException>(() => new PgParser().ParseStatement(state));

        Assert.True(state.PeekKeyword(unexpected));
    }

    [Theory]
    [InlineData("FOREACH item IN ARRAY ARRAY[1, 2] LOOP END LOOP")]
    [InlineData("FOREACH item SLICE 0 IN ARRAY $1 LOOP CALL app.work(item); END LOOP")]
    [InlineData("FOREACH row_value SLICE 1 IN ARRAY ARRAY[[1, 2], [3, 4]] LOOP CONTINUE; END LOOP")]
    [InlineData("FOREACH plane SLICE 2 IN ARRAY app.values() LOOP EXIT WHEN app.done(); END LOOP")]
    [InlineData("FOREACH first_value, second_value IN ARRAY app.records() LOOP CALL app.work(); END LOOP")]
    [InlineData("FOREACH row_value.field IN ARRAY ARRAY[1] LOOP EXIT; END LOOP")]
    [InlineData("FOREACH item IN ARRAY ARRAY(SELECT value FROM app.items) LOOP EXIT; END LOOP")]
    [InlineData("FOREACH item IN ARRAY (app.values() || ARRAY[1]) LOOP EXIT; END LOOP")]
    [InlineData("<<outer_label>> FOREACH item IN ARRAY $1 LOOP FOR i IN 1 .. 2 LOOP CONTINUE outer_label WHEN i > 0; END LOOP; EXIT outer_label; END LOOP outer_label")]
    [InlineData("<<\"Outer\">> FOREACH item IN ARRAY $1 LOOP EXIT \"Outer\" WHEN TRUE; END LOOP \"Outer\"")]
    [InlineData("BEGIN FOREACH item IN ARRAY $1 LOOP WHILE FALSE LOOP NULL; END LOOP; NULL; END LOOP; NULL; END")]
    [InlineData("<<block_label>> BEGIN EXIT block_label WHEN TRUE; END block_label")]
    public void ProceduralForeachAndJumps_RoundTripSqlFormattingAndJson(string sql)
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
    [InlineData("EXIT", typeof(PgLoopControl.Exit), null, false)]
    [InlineData("EXIT outer_label", typeof(PgLoopControl.Exit), "outer_label", false)]
    [InlineData("EXIT WHEN app.done()", typeof(PgLoopControl.Exit), null, true)]
    [InlineData("EXIT outer_label WHEN app.done()", typeof(PgLoopControl.Exit), "outer_label", true)]
    [InlineData("CONTINUE", typeof(PgLoopControl.Continue), null, false)]
    [InlineData("CONTINUE outer_label", typeof(PgLoopControl.Continue), "outer_label", false)]
    [InlineData("CONTINUE WHEN app.ready()", typeof(PgLoopControl.Continue), null, true)]
    [InlineData("CONTINUE \"when\" WHEN app.ready()", typeof(PgLoopControl.Continue), "when", true)]
    public void ProceduralJumps_PreserveOptionalLabelAndCondition(string sql, Type type, string? expectedLabel, bool hasCondition)
    {
        Statement[] statements = [.. ParseStatements(sql + "; SELECT 1")];
        var jump = Assert.IsType<PgLoopControl>(statements[0], exactMatch: false);

        Assert.IsType(type, jump);
        Assert.Equal(expectedLabel, jump.Label?.Name);
        Assert.Equal(hasCondition, jump.Condition != null);
        Assert.IsType<Select>(statements[1]);
        Assert.Equal(jump, ParseStatement(jump.ToSql()));
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
    }

    [Theory]
    [InlineData("", null)]
    [InlineData(" SLICE 0", 0)]
    [InlineData(" SLICE 1", 1)]
    public void ProceduralForeach_PreservesSliceLabelSourceAndFollowingStatement(string sliceClause, int? expectedSlice)
    {
        string sql = $"<<outer_label>> FOREACH item{sliceClause} IN ARRAY $1 LOOP CONTINUE outer_label; END LOOP outer_label; SELECT 1";
        ParserState state = GetState(sql, sourceId: 17);
        SqlValueList<Statement> statements = ParseStatements(state);
        var labeled = Assert.IsType<LabeledStatement.Pg>(statements[0]);
        var loop = Assert.IsType<PgForeach>(labeled.SubStatement);
        Identifier target = Assert.Single(Assert.Single(loop.Targets).Values);
        var jump = Assert.Single(loop.Statements.OfType<PgLoopControl.Continue>());

        Assert.Equal(expectedSlice, loop.Slice);
        Assert.Equal(17, target.Source!.Value.SourceId);
        Assert.Equal(sql.IndexOf("item", StringComparison.Ordinal), target.Source.Value.Start);
        Assert.Equal(17, jump.Label!.Source!.Value.SourceId);
        Assert.Equal("outer_label", jump.Label.Name);
        Assert.Equal(sql.LastIndexOf("outer_label", StringComparison.Ordinal), loop.EndLabel!.Source!.Value.Start);
        Assert.IsType<Select>(statements[1]);
        Assert.Null(state.StatementLabel);
        Assert.Equal(ParseContext.None, state.Context);
    }

    [Theory]
    [InlineData("<< /* first */ label /* second */ >> FOREACH item IN ARRAY $1 /* header */ LOOP /* jump */ EXIT label; /* closing */ END LOOP label")]
    [InlineData("FOREACH item IN ARRAY $1 -- header\nLOOP -- jump\nCONTINUE; -- closing\nEND LOOP")]
    public void ProceduralForeach_PreservesHeaderBodyAndClosingComments(string sql)
    {
        var statement = Assert.IsType<Statement>(ParseStatement(sql), exactMatch: false);
        string formatted = Format(statement);
        foreach (string marker in new[] { "header", "jump", "closing" })
        {
            Assert.Equal(1, formatted.Split(marker, StringSplitOptions.None).Length - 1);
        }
        Assert.Equal(formatted, Format(ParseStatement(formatted)));
        Statement[] statements = [statement];
        Assert.Equal(formatted, Format(Assert.Single(Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)))));
    }

    [Theory]
    [InlineData("FOREACH item IN ARRAY app.values() LOOP CONTINUE outer_label WHEN app.ready(); EXIT WHEN app.done(); CALL app.work(); END LOOP",
        new[] { "app.values", "app.ready", "app.done", "app.work" })]
    [InlineData("EXIT outer_label WHEN app.done()", new[] { "app.done" })]
    [InlineData("CONTINUE outer_label WHEN app.ready()", new[] { "app.ready" })]
    [InlineData("EXIT outer_label", new string[0])]
    [InlineData("CONTINUE outer_label", new string[0])]
    public void ProceduralForeachAndJumps_ReferenceExpressionsAndBodyNotTargetsOrLabels(string sql, string[] expected)
    {
        var statement = Assert.IsType<Statement>(ParseStatement(sql), exactMatch: false);
        string[] names = [.. statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All })
            .OfType<ItemRef.ObjectNameRef>().Select(reference => reference.Name.ToSql())];

        Assert.Equal(expected, names);
        Assert.Empty(statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.None }));
    }

    [Theory]
    [InlineData("FOREACH item IN ARRAY $1 LOOP END LOOP", typeof(PgForeach))]
    [InlineData("EXIT WHEN TRUE", typeof(PgLoopControl.Exit))]
    [InlineData("CONTINUE label WHEN TRUE", typeof(PgLoopControl.Continue))]
    public void ProceduralForeachAndJumps_AreSupportedByCoreKitchenSinkParser(string sql, Type type)
    {
        foreach (Grammar grammar in new[] { Grammar.PgPl, Grammar.All })
        {
            var state = new ParserState(grammar, GetTokens(sql));
            Assert.IsType(type, Assert.Single(new Parser().Parse(state, null)));
        }
    }

    [Theory]
    [InlineData("FOREACH IN ARRAY $1 LOOP END LOOP")]
    [InlineData("FOREACH item ARRAY $1 LOOP END LOOP")]
    [InlineData("FOREACH item IN $1 LOOP END LOOP")]
    [InlineData("FOREACH item SLICE -1 IN ARRAY $1 LOOP END LOOP")]
    [InlineData("FOREACH item SLICE +1 IN ARRAY $1 LOOP END LOOP")]
    [InlineData("FOREACH item SLICE 1.5 IN ARRAY $1 LOOP END LOOP")]
    [InlineData("FOREACH item SLICE 1e0 IN ARRAY $1 LOOP END LOOP")]
    [InlineData("FOREACH item SLICE 2147483648 IN ARRAY $1 LOOP END LOOP")]
    [InlineData("FOREACH item SLICE amount IN ARRAY $1 LOOP END LOOP")]
    [InlineData("FOREACH item SLICE IN ARRAY $1 LOOP END LOOP")]
    [InlineData("FOREACH item IN ARRAY LOOP END LOOP")]
    [InlineData("FOREACH item IN ARRAY $1 END LOOP")]
    [InlineData("FOREACH item IN ARRAY $1 LOOP")]
    [InlineData("FOREACH item IN ARRAY $1 LOOP EXIT END LOOP")]
    [InlineData("FOREACH item IN ARRAY $1 LOOP CONTINUE END LOOP")]
    [InlineData("FOREACH item IN ARRAY $1 LOOP END FOREACH")]
    [InlineData("<<label>> FOREACH item IN ARRAY $1 LOOP END LOOP other_label")]
    [InlineData("<<\"Label\">> FOREACH item IN ARRAY $1 LOOP END LOOP label")]
    [InlineData("EXIT WHEN")]
    [InlineData("CONTINUE WHEN")]
    [InlineData("EXIT 'label'")]
    [InlineData("CONTINUE 'label'")]
    [InlineData("EXIT outer_label WHEN TRUE WHEN FALSE")]
    [InlineData("CONTINUE outer_label, other_label")]
    public void ProceduralForeachAndJumps_RejectInvalidSyntax(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Theory]
    [InlineData("FOREACH item IN ARRAY $1 LOOP END LOOP")]
    [InlineData("EXIT")]
    [InlineData("CONTINUE WHEN TRUE")]
    public void ProceduralForeachAndJumps_RequirePlPgSqlGrammar(string sql)
    {
        foreach (Grammar grammar in new[] { Grammar.Pg, Grammar.My, Grammar.Ms })
        {
            var state = new ParserState(grammar, GetTokens(sql));
            Assert.ThrowsAny<ParseException>(() => new Parser().Parse(state, null));
        }
        var sqlState = new ParserState(Grammar.Pg, GetTokens(sql));
        Assert.ThrowsAny<ParseException>(() => new PgParser().Parse(sqlState, null));
    }

    [Fact]
    public void ProceduralForeach_FailureRestoresContextAndLabel()
    {
        ParserState state = GetState("<<broken>> FOREACH item IN ARRAY $1 LOOP EXIT END LOOP");
        using (state.EnterContext(ParseContext.DomainCheck))
        {
            Assert.ThrowsAny<ParseException>(() => new PgParser().ParseStatement(state));
            Assert.Equal(ParseContext.DomainCheck, state.Context);
            Assert.Null(state.StatementLabel);
        }
    }

    [Theory]
    [InlineData("RETURN")]
    [InlineData("RETURN NULL")]
    [InlineData("RETURN NEW")]
    [InlineData("RETURN OLD.id")]
    [InlineData("RETURN (1, 'two')")]
    [InlineData("RETURN NEXT")]
    [InlineData("RETURN NEXT NEW")]
    [InlineData("RETURN NEXT (1, 'two')")]
    [InlineData("RETURN QUERY SELECT NEW.id, TG_OP")]
    [InlineData("RETURN QUERY VALUES (1), (2)")]
    [InlineData("RETURN QUERY (SELECT 1 UNION ALL SELECT 2)")]
    [InlineData("RETURN QUERY WITH seed AS (SELECT 1 AS value) SELECT seed.value FROM seed")]
    [InlineData("RETURN QUERY INSERT INTO app.items DEFAULT VALUES RETURNING id")]
    [InlineData("RETURN QUERY UPDATE app.items SET id = 1 RETURNING id")]
    [InlineData("RETURN QUERY DELETE FROM app.items RETURNING id")]
    [InlineData("BEGIN PERFORM app.work(NEW.id); SELECT TG_OP INTO operation; IF TRUE THEN RETURN NEXT NEW; ELSE RETURN; END IF; RETURN QUERY SELECT 1; END")]
    public void ProceduralReturnsAndMixedBlocks_RoundTripSqlFormattingAndJson(string sql)
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
    [InlineData("RETURN", typeof(Return))]
    [InlineData("RETURN NEXT", typeof(ReturnNoTerminate.NextExpr))]
    [InlineData("RETURN QUERY SELECT 1", typeof(ReturnNoTerminate.Query))]
    public void ProceduralReturns_PreserveVariantAndFollowingStatement(string sql, Type type)
    {
        SqlValueList<Statement> statements = ParseStatements(sql + "; PERFORM app.work()");

        Assert.Equal(2, statements.Count);
        Assert.IsType(type, statements[0]);
        Assert.IsType<Select>(statements[1]);
    }

    [Theory]
    [InlineData("BEGIN /* before */ PERFORM app.work() /* after */; END")]
    [InlineData("BEGIN /* before */ RETURN NEXT NEW /* after */; END")]
    [InlineData("BEGIN /* before */ RETURN QUERY SELECT 1 /* after */; END")]
    [InlineData("BEGIN -- before\nRETURN QUERY SELECT 1; -- after\nEND")]
    public void ProceduralStatements_PreserveBoundaryCommentsOnce(string sql)
    {
        var statement = Assert.IsType<Statement>(ParseStatement(sql), exactMatch: false);
        string formatted = Format(statement);
        Assert.Equal(1, formatted.Split("before", StringSplitOptions.None).Length - 1);
        Assert.Equal(1, formatted.Split("after", StringSplitOptions.None).Length - 1);
        Assert.Equal(formatted, Format(ParseStatement(formatted)));
        Statement[] statements = [statement];
        Assert.Equal(formatted, Format(Assert.Single(Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)))));
    }

    [Theory]
    [InlineData("RETURN NEXT app.work()", new[] { "app.work" })]
    [InlineData("RETURN QUERY SELECT * FROM app.items", new[] { "app.items" })]
    public void ProceduralReturns_ReferenceSources(string sql, string[] expected)
    {
        var statement = Assert.IsType<Statement>(ParseStatement(sql), exactMatch: false);
        string[] names = [.. statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All })
            .OfType<ItemRef.ObjectNameRef>().Select(reference => reference.Name.ToSql())];

        Assert.Equal(expected, names);
        Assert.Empty(statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.None }));
    }

    [Theory]
    [InlineData("RETURN QUERY")]
    [InlineData("RETURN QUERY EXECUTE 'SELECT 1'")]
    [InlineData("RETURN QUERY PERFORM app.work()")]
    [InlineData("RETURN QUERY SELECT 1 INTO result")]
    [InlineData("RETURN QUERY DELETE FROM app.items")]
    [InlineData("BEGIN RETURN NEXT NEW END")]
    [InlineData("BEGIN RETURN QUERY SELECT 1 END")]
    public void ProceduralReturns_RejectInvalidOrDeferredSyntax(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Theory]
    [InlineData("RETURN NEXT 1")]
    [InlineData("RETURN QUERY SELECT 1")]
    public void ProceduralReturns_RequirePlPgSqlGrammar(string sql)
    {
        foreach (Grammar grammar in new[] { Grammar.Pg, Grammar.My, Grammar.Ms })
        {
            var state = new ParserState(grammar, GetTokens(sql));
            Assert.ThrowsAny<ParseException>(() => new Parser().Parse(state, null));
        }
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
