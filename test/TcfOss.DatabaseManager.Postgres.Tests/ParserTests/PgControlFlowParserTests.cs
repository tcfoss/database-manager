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
using TcfOss.DatabaseManager.Core.Statements.PgDeclarations;
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
        var variable = Assert.Single(options.Declarations!.OfType<SubVariableDeclaration>());
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
        Assert.NotEmpty(block.PgOptions!.Declarations!.OfType<SubVariableDeclaration>());
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
    [InlineData("BEGIN EXECUTE 'SELECT 1'; END")]
    [InlineData("BEGIN EXECUTE format('SELECT * FROM app.items WHERE id = $1') INTO STRICT result USING app.id; END")]
    [InlineData("DECLARE cursor_one NO SCROLL CURSOR (p INT, q TEXT) IS SELECT id FROM app.items WHERE id = p; BEGIN OPEN cursor_one(p := 1, q => 'x'); FETCH FIRST FROM cursor_one INTO result; MOVE BACKWARD ALL IN cursor_one; CLOSE cursor_one; END")]
    [InlineData("BEGIN OPEN cursor_one SCROLL FOR SELECT 1; OPEN cursor_two NO SCROLL FOR EXECUTE 'SELECT $1' USING app.value; END")]
    [InlineData("BEGIN FETCH cursor_name INTO result; FETCH ABSOLUTE -1 IN cursor_name INTO first, second; MOVE FORWARD 2 FROM cursor_name; MOVE cursor_name; END")]
    [InlineData("BEGIN CLOSE \"ALL\"; END")]
    [InlineData("BEGIN COMMIT WORK AND CHAIN; ROLLBACK TRANSACTION AND NO CHAIN; END")]
    [InlineData("FOR row_value IN cursor_name(app.value()) LOOP CALL app.process(row_value); END LOOP")]
    public void ProceduralExecuteAndCursors_RoundTripSqlFormattingAndJson(string sql)
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
    public void ProceduralExecuteAndCursors_PreserveShapeAndReferences()
    {
        var block = Assert.IsType<BeginEnd>(ParseStatement(
            "DECLARE cursor_one CURSOR FOR SELECT id FROM app.items; BEGIN EXECUTE app.command_text() INTO STRICT result USING app.value(); FETCH FORWARD FROM cursor_one INTO result; END"));
        var cursor = Assert.IsType<SubCursorDeclaration>(Assert.Single(block.PgOptions!.Declarations!.OfType<SubCursorDeclaration>()));
        var execute = Assert.IsType<PgExecute>(Assert.Single(block.Statements.OfType<PgExecute>()));
        var fetch = Assert.IsType<Fetch>(Assert.Single(block.Statements.OfType<Fetch>()));

        Assert.Equal("cursor_one", cursor.Name.Name);
        Assert.True(execute.Strict);
        Assert.Equal("result", Assert.Single(execute.Into!).ToSql());
        var fetchDirection = Assert.IsType<CursorDirection.Forward>(fetch.Direction);
        Assert.Null(fetchDirection.Quantifier);
        Assert.Equal("FROM", fetch.Connector!.ToString());

        string[] references = [.. execute.GetReferencedItems(new ReferencedItemsManager
            {
                Filters = ObjectNameFilters.All
            })
            .OfType<ItemRef.ObjectNameRef>()
            .Select(reference => reference.Name.ToSql())];
        Assert.Equal(["app.command_text", "app.value"], references);

        var cursorLoop = Assert.IsType<PgFor>(ParseStatement(
            "FOR row_value IN cursor_name(app.cursor_value()) LOOP CALL app.process(row_value); END LOOP"));
        string[] cursorReferences = [.. cursorLoop.GetReferencedItems(new ReferencedItemsManager
            {
                Filters = ObjectNameFilters.All
            })
            .OfType<ItemRef.ObjectNameRef>()
            .Select(reference => reference.Name.ToSql())];
        Assert.Equal(["app.cursor_value", "app.process"], cursorReferences);
    }

    [Theory]
    [InlineData("BEGIN EXECUTE 'SELECT 1' INTO STRICT; END")]
    [InlineData("BEGIN EXECUTE 'SELECT 1' USING; END")]
    [InlineData("BEGIN OPEN cursor_name SCROLL; END")]
    [InlineData("BEGIN FETCH NEXT cursor_name INTO result; END")]
    [InlineData("BEGIN FETCH FORWARD 2 FROM cursor_name INTO result; END")]
    [InlineData("BEGIN FETCH 2 FROM cursor_name INTO result; END")]
    [InlineData("BEGIN FETCH ALL FROM cursor_name INTO result; END")]
    [InlineData("BEGIN FETCH BACKWARD ALL FROM cursor_name INTO result; END")]
    [InlineData("BEGIN CLOSE ALL; END")]
    [InlineData("BEGIN MOVE FORWARD ALL cursor_name; END")]
    [InlineData("BEGIN COMMIT AND NO; END")]
    [InlineData("DECLARE cursor_name CURSOR; BEGIN NULL; END")]
    public void ProceduralExecuteAndCursors_RejectInvalidSyntax(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Fact]
    public void ProceduralExecuteAndCursors_RequirePlPgSqlGrammar()
    {
        var state = new ParserState(Grammar.Pg, GetTokens("BEGIN EXECUTE 'SELECT 1'; END"));
        Assert.ThrowsAny<ParseException>(() => new PgParser().Parse(state, null));
    }

    [Theory]
    [InlineData("DECLARE cursor_one SCROLL CURSOR WITH HOLD FOR SELECT 1", false, null, "SCROLL", "WITH HOLD")]
    [InlineData("DECLARE cursor_one BINARY INSENSITIVE NO SCROLL CURSOR WITHOUT HOLD FOR SELECT 1", true, "INSENSITIVE", "NO SCROLL", "WITHOUT HOLD")]
    public void SqlCursorDeclarations_ParsePostgresOptions(
        string sql,
        bool expectedBinary,
        string? expectedSensitivity,
        string expectedScrollMode,
        string expectedHoldability)
    {
        var state = new ParserState(Grammar.Pg, GetTokens(sql));
        var cursor = Assert.IsType<DeclareCursor>(Assert.Single(new PgParser().Parse(state, null)));

        Assert.Equal(expectedBinary, cursor.Binary);
        Assert.Equal(expectedSensitivity, cursor.Sensitivity?.ToString());
        Assert.Equal(expectedScrollMode, cursor.ScrollMode?.ToString());
        Assert.Equal(expectedHoldability, cursor.Holdability?.ToString());
    }

    [Fact]
    public void SqlMove_IsAvailableOutsideProceduralBlocks()
    {
        var state = new ParserState(Grammar.Pg, GetTokens("MOVE FORWARD 5 FROM cursor_name"));
        var move = Assert.IsType<PgMove>(Assert.Single(new PgParser().Parse(state, null)));

        var direction = Assert.IsType<CursorDirection.Forward>(move.Direction);
        var quantifier = Assert.IsType<PgCursorDirectionQuantifier.Specified>(direction.Quantifier);
        Assert.Equal("5", quantifier.Count.ToSql());
    }

    [Theory]
    [InlineData("MOVE cursor_name")]
    [InlineData("MOVE NEXT cursor_name")]
    [InlineData("MOVE FROM cursor_name")]
    [InlineData("MOVE -5 FROM cursor_name")]
    [InlineData("MOVE FORWARD +5 FROM cursor_name")]
    [InlineData("MOVE ALL FROM cursor_name")]
    public void SqlMove_RoundTripsOptionalDirectionConnectorAndSignedCounts(string sql)
    {
        static Statement ParseSqlMove(string text)
        {
            var state = new ParserState(Grammar.Pg, GetTokens(text));
            return Assert.IsType<PgMove>(Assert.Single(new PgParser().Parse(state, null)));
        }

        Statement statement = ParseSqlMove(sql);
        Assert.Equal(statement, ParseSqlMove(statement.ToSql()));
        string formatted = Format(statement);
        Assert.Equal(statement, ParseSqlMove(formatted));
        Assert.Equal(formatted, Format(ParseSqlMove(formatted)));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
    }

    [Theory]
    [InlineData("ABSOLUTE")]
    [InlineData("RELATIVE")]
    public void ProceduralMove_TracksDirectionCountReferences(string direction)
    {
        var block = Assert.IsType<BeginEnd>(ParseStatement(
            $"BEGIN MOVE {direction} app.position() FROM cursor_name; END"));
        var move = Assert.IsType<PgMove>(Assert.Single(block.Statements.OfType<PgMove>()));

        string[] references = [.. move.GetReferencedItems(new ReferencedItemsManager
            {
                Filters = ObjectNameFilters.All
            })
            .OfType<ItemRef.ObjectNameRef>()
            .Select(reference => reference.Name.ToSql())];
        Assert.Equal(["app.position"], references);
    }

    [Theory]
    [InlineData("FETCH cursor_name")]
    [InlineData("FETCH NEXT cursor_name")]
    [InlineData("FETCH FROM cursor_name")]
    [InlineData("FETCH ABSOLUTE -2 IN \"CursorName\"")]
    [InlineData("FETCH 3 FROM cursor_name")]
    [InlineData("FETCH FORWARD -2 FROM cursor_name")]
    [InlineData("FETCH BACKWARD ALL cursor_name")]
    public void SqlFetch_RoundTripsSqlFormattingAndJson(string sql)
    {
        static Fetch ParseSqlFetch(string text)
        {
            var state = new ParserState(Grammar.Pg, GetTokens(text));
            return Assert.IsType<Fetch>(Assert.Single(new PgParser().Parse(state, null)));
        }

        Fetch fetch = ParseSqlFetch(sql);
        Assert.Null(fetch.Targets);
        Assert.Equal(fetch, ParseSqlFetch(fetch.ToSql()));
        string formatted = Format(fetch);
        Assert.Equal(fetch, ParseSqlFetch(formatted));
        Assert.Equal(formatted, Format(ParseSqlFetch(formatted)));
        Statement[] statements = [fetch];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
    }

    [Theory]
    [InlineData("FETCH ALL FROM cursor_name")]
    [InlineData("FETCH 3 FROM cursor_name")]
    [InlineData("FETCH -3 FROM cursor_name")]
    [InlineData("FETCH FORWARD -2 FROM cursor_name")]
    [InlineData("FETCH BACKWARD ALL FROM cursor_name")]
    public void SqlFetch_SubParserConsumesMultirowDirections(string sql)
    {
        var state = new ParserState(Grammar.Pg, GetTokens(sql));
        var fetch = Assert.IsType<Fetch>(new PgParser().ControlFlowParser.ParseFetch(state));

        Assert.Null(fetch.Targets);
        Assert.NotNull(fetch.Direction);
        Assert.Equal("FROM", fetch.Connector!.ToString());
        Assert.Equal("cursor_name", fetch.CursorName.Name);
        Assert.Equal(sql, fetch.ToSql());
        Assert.IsType<Core.Lexing.Tokens.Eof>(state.Peek());
    }

    [Theory]
    [InlineData(false, "FROM", "FETCH FROM cursor_name")]
    [InlineData(true, null, "FETCH NEXT cursor_name")]
    [InlineData(true, "IN", "FETCH NEXT IN cursor_name")]
    public void SqlFetch_EmissionPreservesIndependentDirectionAndConnector(
        bool hasDirection, string? connector, string expected)
    {
        var fetch = new Fetch(
            hasDirection ? new CursorDirection.Next() : null,
            connector != null ? CursorNameConnector.Parse(connector) : null,
            new Identifier("cursor_name"),
            Targets: null);

        Assert.Equal(expected, Format(fetch));
        Assert.Equal(expected, fetch.ToSql());
    }

    [Theory]
    [InlineData("CLOSE cursor_name", false)]
    [InlineData("CLOSE \"ALL\"", false)]
    [InlineData("CLOSE ALL", true)]
    public void SqlClose_RetainsNamedAndAllVariants(string sql, bool closesAll)
    {
        static Close ParseSqlClose(string text)
        {
            var state = new ParserState(Grammar.Pg, GetTokens(text));
            return Assert.IsType<Close>(Assert.Single(new PgParser().Parse(state, null)), exactMatch: false);
        }

        Close close = ParseSqlClose(sql);
        if (closesAll)
        {
            Assert.IsType<Close.All>(close);
        }
        else
        {
            var named = Assert.IsType<Close.Named>(close);
            Assert.False(named.Global);
            Assert.Equal(sql["CLOSE ".Length..], named.Identifier.ToSql());
        }
        Assert.Equal(sql, close.ToSql());
        Assert.Equal(close, ParseSqlClose(close.ToSql()));
        string formatted = Format(close);
        Assert.Equal(close, ParseSqlClose(formatted));
        Assert.Equal(formatted, Format(ParseSqlClose(formatted)));
        Statement[] statements = [close];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
    }

    [Theory]
    [InlineData("FETCH")]
    [InlineData("FETCH NEXT FROM")]
    [InlineData("FETCH ALL FROM")]
    [InlineData("FETCH cursor_name INTO result")]
    [InlineData("CLOSE")]
    [InlineData("CLOSE ALL cursor_name")]
    public void SqlFetchAndClose_RejectInvalidSyntax(string sql)
    {
        var state = new ParserState(Grammar.Pg, GetTokens(sql));
        Assert.ThrowsAny<ParseException>(() => new PgParser().Parse(state, null));
    }

    [Theory]
    [InlineData("COMMIT")]
    [InlineData("COMMIT WORK AND CHAIN")]
    [InlineData("ROLLBACK TRANSACTION AND NO CHAIN")]
    public void SqlTransactions_ParsePostgreSqlChainOptions(string sql)
    {
        var state = new ParserState(Grammar.Pg, GetTokens(sql));
        Statement statement = Assert.Single(new PgParser().Parse(state, null));

        Assert.Equal(sql, statement.ToSql());
    }

    [Theory]
    [InlineData("COMMIT RELEASE")]
    [InlineData("COMMIT AND CHAIN RELEASE")]
    [InlineData("ROLLBACK NO RELEASE")]
    [InlineData("ROLLBACK AND NO CHAIN NO RELEASE")]
    public void PostgresTransactions_RejectReleaseOption(string sql)
    {
        var state = new ParserState(Grammar.Pg, GetTokens(sql));
        Assert.ThrowsAny<ParseException>(() => new PgParser().Parse(state, null));
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
    [InlineData("FOR row_value IN cursor_name(app.value()) LOOP CALL app.work(); END LOOP")]
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

    [Theory]
    [InlineData("BEGIN", "BEGIN", 0)]
    [InlineData("BEGIN WORK", "BEGIN WORK", 0)]
    [InlineData("BEGIN TRANSACTION", "BEGIN TRANSACTION", 0)]
    [InlineData("START TRANSACTION", "START TRANSACTION", 0)]
    [InlineData("BEGIN ISOLATION LEVEL SERIALIZABLE, READ ONLY, DEFERRABLE", "BEGIN ISOLATION LEVEL SERIALIZABLE, READ ONLY, DEFERRABLE", 3)]
    [InlineData("BEGIN WORK ISOLATION LEVEL REPEATABLE READ READ WRITE NOT DEFERRABLE", "BEGIN WORK ISOLATION LEVEL REPEATABLE READ, READ WRITE, NOT DEFERRABLE", 3)]
    [InlineData("START TRANSACTION ISOLATION LEVEL READ COMMITTED", "START TRANSACTION ISOLATION LEVEL READ COMMITTED", 1)]
    [InlineData("START TRANSACTION READ ONLY ISOLATION LEVEL READ UNCOMMITTED, NOT DEFERRABLE", "START TRANSACTION READ ONLY, ISOLATION LEVEL READ UNCOMMITTED, NOT DEFERRABLE", 3)]
    public void SqlTransactionStart_RoundTripsSqlFormattingAndJson(string sql, string expectedSql, int expectedModes)
    {
        var state = new ParserState(Grammar.Pg, GetTokens(sql));
        var statement = Assert.IsType<StartTransaction>(ParseStatement(state), exactMatch: false);

        Assert.Equal(expectedModes, statement.Modes.Count);
        Assert.Equal(expectedSql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        string formatted = Format(statement);
        Assert.Equal(statement, ParseStatement(formatted));
        Assert.Equal(formatted, Format(ParseStatement(formatted)));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
        Assert.Empty(statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All }));
    }

    [Fact]
    public void SqlTransactionStart_PreservesBeginNounAndModeOrder()
    {
        var statement = Assert.IsType<StartTransaction.Begin>(ParseStatement(
            "BEGIN TRANSACTION READ ONLY, ISOLATION LEVEL SERIALIZABLE DEFERRABLE"));

        Assert.Equal(TransactionNoun.Transaction, statement.Noun);
        Assert.Equal(new[] { "READ ONLY", "ISOLATION LEVEL SERIALIZABLE", "DEFERRABLE" },
            statement.Modes!.Select(mode => mode.ToString()));
    }

    [Theory]
    [InlineData("END", true)]
    [InlineData("END WORK AND CHAIN", true)]
    [InlineData("END TRANSACTION AND NO CHAIN", true)]
    [InlineData("ABORT", false)]
    [InlineData("ABORT WORK AND NO CHAIN", false)]
    [InlineData("ABORT TRANSACTION AND CHAIN", false)]
    public void SqlTransactionAliases_RetainSpellingAndRoundTrip(string sql, bool commit)
    {
        var state = new ParserState(Grammar.Pg, GetTokens(sql));
        Statement statement = Assert.Single(new PgParser().Parse(state, null));
        if (commit)
        {
            Assert.IsType<Commit.EndTransaction>(statement);
        }
        else
        {
            Assert.IsType<Rollback.Abort>(statement);
        }

        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
        string formatted = Format(statement);
        Assert.Equal(statement, ParseStatement(formatted));
        Assert.Equal(formatted, Format(ParseStatement(formatted)));
        Statement[] statements = [statement];
        Assert.Equal(statements, Serialization.FromJson<Statement[]>(Serialization.ToJson(statements)));
        Assert.Empty(statement.GetReferencedItems(new ReferencedItemsManager { Filters = ObjectNameFilters.All }));
    }

    [Fact]
    public void SqlTransactions_RemainDistinctFromProceduralBlocksInMixedScripts()
    {
        const string sql = "BEGIN; SELECT 1; END WORK AND CHAIN; START TRANSACTION READ ONLY; ABORT TRANSACTION AND NO CHAIN; BEGIN NULL; END";
        ParserState state = GetState(sql);
        Statement[] statements = [.. ParseStatements(state).Where(statement => statement is not InertOnly)];

        Assert.Equal(6, statements.Length);
        Assert.IsType<StartTransaction.Begin>(statements[0]);
        Assert.IsType<Select>(statements[1]);
        Assert.IsType<Commit.EndTransaction>(statements[2]);
        Assert.IsType<StartTransaction>(statements[3]);
        Assert.IsType<Rollback.Abort>(statements[4]);
        Assert.IsType<BeginEnd>(statements[5]);
        Assert.Equal(ParseContext.None, state.Context);
        Assert.Equal(Grammar.Pg | Grammar.PgPl, state.Grammar);
    }

    [Theory]
    [InlineData("BEGIN TRAN")]
    [InlineData("BEGIN TRANSACTION WORK")]
    [InlineData("BEGIN ISOLATION LEVEL")]
    [InlineData("BEGIN ISOLATION LEVEL SNAPSHOT")]
    [InlineData("START")]
    [InlineData("START WORK")]
    [InlineData("START TRANSACTION READ")]
    [InlineData("START TRANSACTION ISOLATION LEVEL REPEATABLE")]
    [InlineData("START TRANSACTION READ ONLY,")]
    [InlineData("START TRANSACTION READ ONLY,, DEFERRABLE")]
    [InlineData("BEGIN , READ ONLY")]
    [InlineData("START TRANSACTION NOT")]
    [InlineData("BEGIN WITH CONSISTENT SNAPSHOT")]
    [InlineData("START TRANSACTION WITH CONSISTENT SNAPSHOT")]
    [InlineData("START TRANSACTION READ ONLY WITH CONSISTENT SNAPSHOT")]
    [InlineData("END TRAN")]
    [InlineData("END AND NO")]
    [InlineData("END AND CHAIN RELEASE")]
    [InlineData("ABORT TRANSACTION named")]
    [InlineData("ABORT RELEASE")]
    public void SqlTransactionLifecycle_RejectsMalformedAndOtherDialectSyntax(string sql)
    {
        var state = new ParserState(Grammar.Pg, GetTokens(sql));
        Assert.ThrowsAny<ParseException>(() => ParseStatements(state));
    }

    [Theory]
    [InlineData("BEGIN WORK")]
    [InlineData("BEGIN READ ONLY")]
    [InlineData("START TRANSACTION")]
    [InlineData("END WORK")]
    [InlineData("END AND CHAIN")]
    [InlineData("ABORT")]
    public void SqlTransactionLifecycle_DoesNotIntroduceProceduralControls(string sql)
    {
        var state = new ParserState(Grammar.PgPl, GetTokens(sql));
        Assert.ThrowsAny<ParseException>(() => ParseStatements(state));
        Assert.ThrowsAny<ParseException>(() => ParseStatements($"BEGIN {sql}; NULL; END"));
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
