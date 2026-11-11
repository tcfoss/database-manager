using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Tests.ParserTests;
using TcfOss.DatabaseManager.MsSql.Lexing;
using TcfOss.DatabaseManager.MsSql.Parsing;

namespace TcfOss.DatabaseManager.MsSql.Tests.ParserTests;

public class MsParserTests : ParserTestsBase<MsLexer, MsParser>
{
    [Theory]
    [InlineData("CREATE TABLE Users (Id INT PRIMARY KEY)", Label = "CREATE TABLE INT PRIMARY KEY")]
    [InlineData("CREATE TABLE Users (Id INT IDENTITY PRIMARY KEY, Name NVARCHAR(50))", Label = "CREATE TABLE IDENTITY + NVARCHAR")]
    [InlineData("CREATE TABLE T (X DATETIME)", Label = "CREATE TABLE DATETIME no precision")]
    [InlineData("CREATE TABLE T (X DATETIME2)", Label = "CREATE TABLE DATETIME2 no precision")]
    [InlineData("CREATE TABLE T (X DATETIME2(7))", Label = "CREATE TABLE DATETIME2 with precision")]
    [InlineData("CREATE TABLE T (X DATETIMEOFFSET(3))", Label = "CREATE TABLE DATETIMEOFFSET")]
    [InlineData("CREATE TABLE T (X UNIQUEIDENTIFIER)", Label = "CREATE TABLE UNIQUEIDENTIFIER")]
    [InlineData("CREATE TABLE T (X MONEY, Y SMALLMONEY)", Label = "CREATE TABLE MONEY")]
    [InlineData("CREATE TABLE T (X BIT)", Label = "CREATE TABLE BIT")]
    [InlineData("CREATE TABLE T (X SMALLINT)", Label = "CREATE TABLE SMALLINT")]
    [InlineData("CREATE TABLE T (X DATE, Y TIME(3), Z SMALLDATETIME)", Label = "CREATE TABLE date/time variants")]
    [InlineData("CREATE TABLE T (X VARCHAR(MAX), Y NVARCHAR(MAX))", Label = "CREATE TABLE VARCHAR(MAX) / NVARCHAR(MAX)")]
    [InlineData("CREATE TABLE T (X VARBINARY(MAX))", Label = "CREATE TABLE VARBINARY(MAX)")]
    [InlineData("CREATE TABLE T (X BINARY(16), Y NCHAR(10))", Label = "CREATE TABLE BINARY + NCHAR")]
    [InlineData("CREATE TABLE T (X ROWVERSION)", Label = "CREATE TABLE ROWVERSION")]
    [InlineData("CREATE TABLE T (X IMAGE)", Label = "CREATE TABLE IMAGE")]
    [InlineData("CREATE TABLE T (X NTEXT)", Label = "CREATE TABLE NTEXT")]
    public void CreateTable_TextMatch(string text)
    {
        var (expected, actual) = GetExpectedActual(text, expectedSql: text, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(expected, actual);
    }


    [Theory]
    [InlineData("CREATE TABLE items (total INT DEFAULT 1 + 2 NOT NULL)", "CREATE TABLE items (total INT DEFAULT (1 + 2) NOT NULL)")]
    [InlineData("CREATE TABLE items (total INT CONSTRAINT initial_total DEFAULT 1 + 2)", "CREATE TABLE items (total INT CONSTRAINT initial_total DEFAULT (1 + 2))")]
    [InlineData("ALTER TABLE items ADD total INT DEFAULT 1 + 2", "ALTER TABLE items ADD total INT DEFAULT (1 + 2)")]
    public void DefaultExpressionsWithoutParentheses_RoundTrip(string sql, string expectedSql)
    {
        Assert.True(new MsParser().SupportsDefaultExpressionsWithoutParentheses);
        IWriteSql statement = ParseStatement(sql);

        Assert.Equal(expectedSql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
    }

    [Theory]
    [InlineData("ALTER TABLE items DROP label", false)]
    [InlineData("ALTER TABLE items DROP COLUMN label", true)]
    public void AlterTable_DropPreservesOptionalColumnKeyword(string sql, bool printColumn)
    {
        var statement = Assert.IsType<AlterTable>(ParseStatement(sql));
        var operation = Assert.IsType<AlterTableOperation.DropColumn>(Assert.Single(statement.Operations));

        Assert.Equal(printColumn, operation.PrintColumn);
        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
    }

    [Theory]
    [InlineData("NCHAR")]
    [InlineData("BINARY")]
    [InlineData("VARBINARY")]
    public void DataType_RequiredLengthMissing_Throws(string dataType)
    {
        var sql = $"CREATE TABLE T (X {dataType})";
        var ex = Assert.Throws<ParseException.ExpectedButFound>(() => ParseStatements(sql));
        Assert.Contains("data_type_length", ex.Message);
    }

    [Theory]
    [InlineData("X AS (Y + 1)")]
    [InlineData("X AS Y + 1")]
    [InlineData("X AS (Y + 1) PERSISTED")]
    [InlineData("X AS Y + 1 PERSISTED")]
    [InlineData("X AS (Y + 1) PERSISTED NOT NULL")]
    [InlineData("X AS Y + 1 PERSISTED NOT NULL")]
    [InlineData("X AS (Y) + 1")]
    [InlineData("X AS (Y) + 1 PERSISTED")]
    [InlineData("X AS (Y + 1) PERSISTED CONSTRAINT CK_T_X CHECK (X > 0)")]
    [InlineData("X AS (Y + 1) PERSISTED CONSTRAINT UC_T_X UNIQUE")]
    [InlineData("X AS (Y + 1) PERSISTED CONSTRAINT CK_T_X CHECK (X > 0) CONSTRAINT UC_T_X UNIQUE")]
    public void ComputedColumn_TextMatch(string columnDefinition)
    {
        var createTableSql = $"CREATE TABLE T ({columnDefinition})";
        var statements = ParseStatements(createTableSql);

        var createTable = Assert.IsType<CreateTable>(Assert.Single(statements));
        var column = Assert.IsType<StatementColumn.MsComputed>(Assert.Single(createTable.Columns));

        var actual = column.ToSql();
        Assert.Equal(columnDefinition, actual);
    }

    [Theory]
    [InlineData("CREATE TABLE [dbo].[Users] (Id INT)", Label = "Bracketed schema-qualified")]
    [InlineData("SELECT [Id], [Name] FROM [Users]", Label = "Bracketed columns")]
    public void BracketedIdentifiers_TextMatch(string text)
    {
        Assert.Equal(QuoteStyle.Brackets, new MsParser().DefaultQuoteStyle);
        var (expected, actual) = GetExpectedActual(text, normalizeActualWhitespace: true, normalizeExpectedWhitespace: true);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("SELECT 1\nGO\nSELECT 2", Label = "GO separator")]
    [InlineData("SELECT 1\nGO 3\nSELECT 2", Label = "GO with count")]
    [InlineData("SELECT 1; SELECT 2;\nGO", Label = "Trailing GO after semicolons")]
    public void BatchSeparator_Parses(string text)
    {
        var state = GetState(text);
        var parser = new MsParser();
        var statements = parser.Parse(state, null);
        Assert.Contains(statements, s => s is Statements.MsBatchSeparator);
    }

    [Fact]
    public void BatchSeparator_NoSemicolonRequired()
    {
        // T-SQL: GO acts as an implicit terminator. No semicolon needed before GO.
        var state = GetState("SELECT 1\nGO");
        var parser = new MsParser();
        var statements = parser.Parse(state, null);

        Assert.Equal(2, statements.Count);
        Assert.IsType<Statements.MsBatchSeparator>(statements[1]);
    }

    [Fact]
    public void BatchSeparator_CountPreserved()
    {
        var state = GetState("SELECT 1\nGO 7");
        var parser = new MsParser();
        var statements = parser.Parse(state, null);

        var sep = Assert.IsType<Statements.MsBatchSeparator>(statements[^1]);
        Assert.Equal(7, sep.Count);
    }

    [Fact]
    public void TryCatch_TextMatch()
    {
        var text = "BEGIN TRY SELECT 1; END TRY BEGIN CATCH SELECT 2; END CATCH";
        var (expected, actual) = GetExpectedActual(text, normalizeActualWhitespace: true, normalizeExpectedWhitespace: true);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TryCatch_TryNotEnded_Throws()
    {
        var state = GetState("BEGIN TRY SELECT 1");
        var parser = new MsParser();
        var ex = Assert.Throws<ParseException.ExpectedButFound>(() => parser.Parse(state, null));
        Assert.Contains("END TRY", ex.Message);
    }

    [Fact]
    public void TryCatch_NoCatch_Throws()
    {
        var state = GetState("BEGIN TRY SELECT 1; END TRY");
        var parser = new MsParser();
        var ex = Assert.Throws<ParseException.ExpectedButFound>(() => parser.Parse(state, null));
        Assert.Contains("BEGIN", ex.Message);
    }

    [Fact]
    public void TryCatch_CatchNotEnded_Throws()
    {
        var state = GetState("BEGIN TRY SELECT 1 END TRY BEGIN CATCH SELECT 2");
        var parser = new MsParser();
        var ex = Assert.Throws<ParseException.ExpectedButFound>(() => parser.Parse(state, null));
        Assert.Contains("END CATCH", ex.Message);
    }

    [Theory]
    [InlineData("TRAN")]
    [InlineData("TRANSACTION")]
    public void SaveTransaction_TextMatch(string transactionKeyword)
    {
        var text = $"SAVE {transactionKeyword} mySavePoint";
        var (expected, actual) = GetExpectedActual(text, normalizeActualWhitespace: true, normalizeExpectedWhitespace: true);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("SELECT")]
    [InlineData("3")]
    public void SaveTransaction_NoTranKeyword_Throws(string nextText)
    {
        var state = GetState($"SAVE {nextText}");
        var parser = new MsParser();
        var ex = Assert.Throws<ParseException.ExpectedOneOfButFound>(() => parser.Parse(state, null));
        Assert.Contains("TRAN | TRANSACTION", ex.Message);
    }

    [Fact]
    public void ParseBegin_NonWordAfter_Throws()
    {
        var state = GetState("BEGIN 3");
        var parser = new MsParser();
        var ex = Assert.Throws<ParseException.ExpectedButFound>(() => parser.Parse(state, null));
        Assert.Contains("statement", ex.Message);
    }

    [Fact]
    public void Statements_WithoutDelimiter_Throws()
    {
        var ex = Assert.Throws<ParseException.ExpectedButFound>(() => ParseStatements("SELECT 1 SELECT 2"));
        Assert.Contains("end of statement", ex.Message);
    }

    [Theory]
    [InlineData("CREATE TYPE mood AS ENUM ('happy')")]
    [InlineData("CREATE TYPE address AS (street TEXT)")]
    [InlineData("CREATE DOMAIN postcode AS TEXT")]
    [InlineData("CREATE EVENT reminder ON SCHEDULE EVERY 1 DAY DO SELECT 1")]
    [InlineData("CREATE MATERIALIZED VIEW summary AS SELECT 1")]
    [InlineData("CREATE MATERIALIZED VIEW IF NOT EXISTS summary AS SELECT 1 WITH NO DATA")]
    public void UnsupportedCreateDeclarations_AreRejected(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Theory]
    [InlineData("INT[]")]
    [InlineData("INT ARRAY")]
    [InlineData("TEXT ARRAY[3]")]
    public void ArrayTypeDeclarations_AreNotSupported(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements($"CREATE TABLE sample (value {sql})"));
    }

    [Theory]
    [InlineData("CREATE INDEX ix ON items USING BTREE (id)")]
    [InlineData("CREATE UNIQUE INDEX ix ON items USING BTREE (id)")]
    public void PostgresIndexMethodPlacement_IsRejected(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Theory]
    [InlineData("CREATE FULLTEXT INDEX ix ON items (id)")]
    [InlineData("CREATE SPATIAL INDEX ix ON items (id)")]
    [InlineData("CREATE CLUSTERED INDEX ix ON items (id)")]
    [InlineData("CREATE NONCLUSTERED INDEX ix ON items (id)")]
    public void CreateIndex_KindKeywords_TextMatch(string sql)
    {
        var (expected, actual) = GetExpectedActual(sql, expectedSql: sql, normalizeExpectedWhitespace: true, normalizeActualWhitespace: true);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("CREATE OR ALTER TABLE items (id INT)")]
    [InlineData("CREATE OR ALTER INDEX ix ON items (id)")]
    public void CreateOrAlter_UnsupportedTargets_Throws(string sql)
    {
        var exception = Assert.Throws<ParseException.ExpectedOneOfButFound>(() => ParseStatement(sql));
        Assert.Contains("TRIGGER | FUNCTION | PROCEDURE | VIEW", exception.Message);
    }

    [Theory]
    [InlineData("CREATE *")]
    [InlineData("CREATE unsupported_object")]
    public void InvalidCreateTargets_AreRejected(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Theory]
    [InlineData("ALWAYS")]
    [InlineData("BY DEFAULT")]
    public void PostgresIdentityColumns_AreRejected(string mode)
    {
        var error = Assert.Throws<ParseException.ExpectedButFound>(() => ParseStatement(
            $"CREATE TABLE items (id INT GENERATED {mode} AS IDENTITY)"));

        Assert.Contains("generated expression", error.Message);
    }

    [Fact]
    public void PostgresOperators_AreNarrowed()
    {
        var parser = new MsParser();
        Assert.False(parser.SupportsPostgresCastSyntax);
        Assert.False(parser.SupportsPostgresRegexOperators);
        Assert.ThrowsAny<ParseException>(() => ParseExpression("value::INT"));
        Assert.ThrowsAny<ParseException>(() => ParseExpression("value ~ 'pattern'"));
    }

    [Theory]
    [InlineData("CREATE TRIGGER audit ON items AFTER INSERT, UPDATE, DELETE AS SELECT 1")]
    [InlineData("CREATE OR ALTER TRIGGER audit ON items INSTEAD OF INSERT AS SELECT 1")]
    [InlineData("CREATE TRIGGER audit ON items FOR UPDATE AS SELECT 1")]
    public void Triggers_RetainMsSqlSyntax(string sql)
    {
        Assert.Equal(sql, ParseStatement(sql).ToSql());
    }

    [Theory]
    [InlineData("CREATE CONSTRAINT TRIGGER audit ON items AFTER INSERT AS SELECT 1")]
    [InlineData("CREATE TRIGGER audit ON items AFTER TRUNCATE AS SELECT 1")]
    [InlineData("CREATE TRIGGER audit ON items AFTER UPDATE OF id AS SELECT 1")]
    [InlineData("CREATE TRIGGER audit ON items AFTER INSERT OR UPDATE AS SELECT 1")]
    [InlineData("CREATE TRIGGER audit ON items AFTER INSERT FOR ROW AS SELECT 1")]
    [InlineData("CREATE TRIGGER audit ON items AFTER INSERT REFERENCING NEW TABLE new_rows AS SELECT 1")]
    [InlineData("CREATE TRIGGER audit ON items AFTER INSERT WHEN (id > 0) AS SELECT 1")]
    [InlineData("CREATE TRIGGER audit ON items AFTER INSERT EXECUTE FUNCTION audit()")]
    public void PostgresTriggerSyntax_IsRejected(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Theory]
    [InlineData("COMMENT ON TABLE items IS 'table'")]
    [InlineData("COMMENT ON COLUMN items.id IS NULL")]
    [InlineData("COMMENT ON FUNCTION f(INT) IS 'function'")]
    public void PostgresCommentSyntax_IsRejected(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Theory]
    [InlineData("CREATE TABLE items (label VARCHAR(20) COLLATE app.locale)")]
    [InlineData("CREATE TABLE items (label VARCHAR(20) NOT NULL COLLATE app.locale)")]
    public void PostgresColumnCollations_AreRejected(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Fact]
    public void PostgresExpressionCollations_AreRejected()
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements("SELECT label COLLATE app.locale FROM items"));
    }

    [Theory]
    [InlineData("SELECT ARRAY[1, 2] + 1")]
    [InlineData("SELECT ARRAY(SELECT 1)")]
    [InlineData("SELECT arr[1:2] + 1")]
    [InlineData("SELECT (item).field")]
    [InlineData("SELECT (item).*")]
    public void PostgresArrayAndCompositeExpressions_AreRejected(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Fact]
    public void BracketedAliases_AreNotParsedAsArraySubscripts()
    {
        Assert.Equal("SELECT arr AS [1:2]", ParseStatement("SELECT arr[1:2]").ToSql());
    }

    [Theory]
    [InlineData("SELECT value OPERATOR(app.@>) other")]
    [InlineData("SELECT OPERATOR(+) value")]
    [InlineData("SELECT value <-> other")]
    public void PostgresGeneralOperators_AreRejected(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Fact]
    public void PostgresQuestionOperators_AreRejectedByLexer()
    {
        Assert.Throws<LexException.UnexpectedCharacter>(() => GetTokens("SELECT value ?| other"));
    }

    [Theory]
    [InlineData("SELECT INTERVAL '1 day'")]
    [InlineData("SELECT INTERVAL(3) '1.234 seconds'")]
    [InlineData("SELECT INTERVAL '1-2' YEAR TO MONTH")]
    public void PostgresIntervalLiterals_AreRejected(string sql)
    {
        Assert.False(new MsParser().SupportsPostgresIntervalLiteralSyntax);
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Fact]
    public void Precedence_PreservesSqlServerOperatorBehavior()
    {
        var expression = Assert.IsType<Core.Expressions.BinaryOperator>(ParseExpression("a + b ^ c"));
        Assert.Equal(Core.BuiltIn.BinaryOperator.BitwiseXor, expression.Operator);
        Assert.IsType<Core.Expressions.BinaryOperator>(expression.Left);
        Assert.IsType<MsPrecedenceManager>(new MsParser().PrecedenceManager);
        Assert.Equal("ISNULL(value, other)", ParseExpression("ISNULL(value, other)").ToSql());
        var pattern = Assert.IsType<Core.Expressions.Like>(ParseExpression("a LIKE b = c"));
        Assert.IsType<Core.Expressions.BinaryOperator>(pattern.Pattern);
    }

    [Theory]
    [InlineData("=", "ANY")]
    [InlineData("<>", "ALL")]
    [InlineData("<", "SOME")]
    [InlineData("<=", "ANY")]
    [InlineData(">", "ALL")]
    [InlineData(">=", "SOME")]
    public void QuantifiedComparisons_PreserveAllComparisonOperators(string op, string quantifier)
    {
        var expression = Assert.IsType<Core.Expressions.QuantifiedBinaryOperator>(ParseExpression($"a {op} {quantifier}(SELECT result FROM items)"));

        Assert.Equal(op, expression.Operator.ToString());
        Assert.Equal(quantifier, expression.Quantifier.ToString());
        Assert.IsType<Core.Expressions.Subquery>(expression.Right);
        Assert.Equal(expression, ParseExpression(expression.ToSql()));
    }

    [Theory]
    [InlineData("ANY")]
    [InlineData("ALL")]
    [InlineData("SOME")]
    public void QuantifiedComparisons_ParseScalarOperands(string quantifier)
    {
        var expression = Assert.IsType<Core.Expressions.QuantifiedBinaryOperator>(ParseExpression($"a = {quantifier}(1)"));

        Assert.Equal(Core.BuiltIn.BinaryOperator.Equal, expression.Operator);
        Assert.Equal(quantifier, expression.Quantifier.ToString());
        Assert.IsType<Core.Expressions.LiteralValue>(expression.Right);
        Assert.Equal(expression, ParseExpression(expression.ToSql()));
    }

    [Fact]
    public void AtTimeZone_ParsesAndRoundTripsWithoutPostgresOperatorSyntax()
    {
        const string sql = "stamp AT TIME ZONE 'UTC'";
        var expression = Assert.IsType<Core.Expressions.AtTimeZone>(ParseExpression(sql));

        Assert.False(new MsParser().SupportsPostgresOperatorSyntax);
        Assert.Equal(sql, expression.ToSql());
        Assert.Equal(expression, ParseExpression(expression.ToSql()));
        Assert.Equal("SELECT " + sql, ParseStatement("SELECT " + sql).ToSql());
    }

    [Fact]
    public void TableValuedFunctions_ParseInSqlServerFromAndApply()
    {
        const string sql = "SELECT * FROM dbo.items CROSS APPLY dbo.expand(items.value) AS result";
        var statement = Assert.IsType<Select>(ParseStatement(sql));

        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
    }

    [Theory]
    [InlineData("SELECT * FROM ROWS FROM (app.first())")]
    [InlineData("SELECT * FROM app.first() WITH ORDINALITY")]
    [InlineData("SELECT * FROM LATERAL app.first()")]
    public void PostgresFromComponents_AreNarrowed(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Theory]
    [InlineData("SELECT stamp AT LOCAL + value")]
    [InlineData("SELECT value BETWEEN SYMMETRIC lower AND upper")]
    [InlineData("SELECT value ISNULL AND other")]
    [InlineData("SELECT x * ALL(SELECT colname FROM tablename)")]
    [InlineData("SELECT x + ANY(SELECT colname FROM tablename)")]
    [InlineData("SELECT x AND SOME(SELECT colname FROM tablename)")]
    public void PostgresPredicates_AreNarrowed(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Theory]
    [InlineData("INSERT INTO items DEFAULT VALUES")]
    [InlineData("INSERT INTO items OUTPUT inserted.id DEFAULT VALUES")]
    public void InsertDefaultValues_RemainsSupported(string sql)
    {
        Assert.Equal(sql, ParseStatement(sql).ToSql());
    }

    [Theory]
    [InlineData("INSERT INTO items AS target VALUES (1)")]
    [InlineData("INSERT INTO items OVERRIDING SYSTEM VALUE VALUES (1)")]
    [InlineData("INSERT INTO items VALUES (1) ON CONFLICT DO NOTHING")]
    [InlineData("INSERT INTO items VALUES (1) RETURNING *")]
    [InlineData("UPDATE items SET id = 1 RETURNING *")]
    [InlineData("DELETE FROM items USING source")]
    public void PostgresDmlComponents_AreNarrowed(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Theory]
    [InlineData("WITH changed AS (INSERT INTO items VALUES (1)) SELECT 1")]
    [InlineData("WITH changed AS (UPDATE items SET id = 1) SELECT 1")]
    [InlineData("WITH changed AS (DELETE FROM items) SELECT 1")]
    [InlineData("WITH result AS MATERIALIZED (SELECT 1) SELECT 1")]
    [InlineData("WITH result AS NOT MATERIALIZED (SELECT 1) SELECT 1")]
    [InlineData("WITH result (id) AS (SELECT 1) SEARCH DEPTH FIRST BY id SET position SELECT id FROM result")]
    [InlineData("WITH result (id) AS (SELECT 1) CYCLE id SET is_cycle USING path SELECT id FROM result")]
    public void PostgresCteComponents_AreNarrowed(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Theory]
    [InlineData("SELECT SUM(value) OVER (ORDER BY id ROWS BETWEEN 1 PRECEDING AND CURRENT ROW) FROM items", Label = "SQL Server rows frame")]
    [InlineData("SELECT SUM(value) OVER (ORDER BY id RANGE UNBOUNDED PRECEDING) FROM items", Label = "SQL Server range frame")]
    [InlineData("SELECT PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY value) OVER () FROM items", Label = "SQL Server ordered set function")]
    public void WindowFramesAndOrderedSetFunctions_RemainSupported(string sql)
    {
        Assert.Equal(sql, ParseStatement(sql).ToSql());
    }

    [Theory]
    [InlineData("SELECT COUNT(*) FILTER (WHERE active) FROM items", Label = "Reject aggregate FILTER")]
    [InlineData("SELECT SUM(value) OVER (ORDER BY id GROUPS CURRENT ROW) FROM items", Label = "Reject GROUPS frame")]
    [InlineData("SELECT SUM(value) OVER (ROWS CURRENT ROW EXCLUDE TIES) FROM items", Label = "Reject EXCLUDE")]
    [InlineData("SELECT id FROM items ORDER BY id NULLS LAST", Label = "Reject NULLS ordering")]
    public void AggregateWindows_AreNarrowed(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Fact]
    public void SelectTails_PreserveSqlServerOffsetFetch()
    {
        const string sql = "SELECT id FROM items ORDER BY id OFFSET 2 ROWS FETCH NEXT 3 ROWS ONLY";

        Assert.Equal(sql, ParseStatement(sql).ToSql());
    }

    [Theory]
    [InlineData("SELECT 1 OFFSET 2", Label = "Reject offset without rows")]
    [InlineData("SELECT 1 FETCH FIRST ROW ONLY", Label = "Reject standalone fetch")]
    [InlineData("SELECT 1 OFFSET 2 ROWS FETCH NEXT 3 ROWS WITH TIES", Label = "Reject fetch with ties")]
    [InlineData("SELECT 1 FOR UPDATE", Label = "Reject PostgreSQL update lock")]
    [InlineData("SELECT 1 FOR SHARE", Label = "Reject PostgreSQL share lock")]
    public void SelectTails_NarrowPostgresSyntax(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Theory]
    [InlineData("WITH result AS (SELECT 1 AS id) SELECT id FROM result")]
    [InlineData("WITH result AS (VALUES (1)) SELECT column1 FROM result")]
    [InlineData("WITH result AS ((SELECT 1 AS id)) SELECT id FROM result")]
    [InlineData("WITH result AS (WITH source AS (SELECT 1 AS id) SELECT id FROM source) SELECT id FROM result")]
    public void CteBodyQuery_PreservesAcceptedQueryStarters(string sql)
    {
        Assert.Equal(sql, ParseStatement(sql).ToSql());
    }

    [Theory]
    [InlineData("INSERT")]
    [InlineData("UPDATE")]
    [InlineData("DELETE")]
    public void CteBodyQuery_RejectsDmlAtOpeningTokenWithoutConsumingIt(string keyword)
    {
        string sql = $"WITH changed AS (\n  {keyword} invalid syntax\n) SELECT 1";
        var state = GetState(sql);
        var first = state.PeekNth(4);

        var exception = Assert.Throws<ParseException.ExpectedButFound>(() => ParseStatement(state));

        Assert.Same(first, state.Peek());
        ParseException expected = ParserState.ExpectedException("SELECT", first);
        Assert.Equal(new ParseException.WithText(expected, null, sql).Message, new ParseException.WithText(exception, null, sql).Message);
    }

    [Fact]
    public void CteBodyQuery_RejectsNestedWithDmlAtOpeningToken()
    {
        const string sql = "WITH changed AS (\n  WITH source AS (SELECT 1 AS id) INSERT INTO items SELECT id FROM source\n) SELECT 1";
        var state = GetState(sql);
        var first = state.PeekNth(4);

        var exception = Assert.Throws<ParseException.ExpectedButFound>(() => ParseStatement(state));

        ParseException expected = ParserState.ExpectedException("SELECT", first);
        Assert.Equal(new ParseException.WithText(expected, null, sql).Message, new ParseException.WithText(exception, null, sql).Message);
    }

    [Theory]
    [InlineData("CALL app.run()", Label = "Reject standalone CALL")]
    [InlineData("CREATE PROCEDURE app.run AS CALL app.other()", Label = "Reject CALL in TSQL routine")]
    public void Call_IsNotSqlServerInvocationSyntax(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Theory]
    [InlineData("REFRESH MATERIALIZED VIEW summary")]
    [InlineData("REFRESH MATERIALIZED VIEW CONCURRENTLY app.summary WITH DATA")]
    [InlineData("REFRESH MATERIALIZED VIEW summary WITH NO DATA")]
    public void RefreshMaterializedViews_AreRejectedBySqlServer(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Theory]
    [InlineData("DROP MATERIALIZED VIEW summary")]
    [InlineData("DROP MATERIALIZED VIEW IF EXISTS app.summary, app.other CASCADE")]
    [InlineData("DROP MATERIALIZED VIEW summary RESTRICT")]
    public void DropMaterializedViews_AreRejectedBySqlServer(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Theory]
    [InlineData("CREATE SEQUENCE ids")]
    [InlineData("CREATE TEMPORARY SEQUENCE ids")]
    [InlineData("CREATE UNLOGGED SEQUENCE ids")]
    [InlineData("ALTER SEQUENCE ids RESTART WITH 10")]
    [InlineData("ALTER TABLE items ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY")]
    [InlineData("ALTER TABLE items ALTER id SET GENERATED ALWAYS")]
    [InlineData("ALTER TABLE items ALTER id RESTART WITH 10")]
    [InlineData("ALTER TABLE items ALTER id SET INCREMENT BY 2")]
    [InlineData("ALTER TABLE items ALTER id DROP IDENTITY IF EXISTS")]
    public void SequenceSyntax_RemainsUnsupportedBySqlServerParser(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Theory]
    [InlineData("DROP SEQUENCE ids")]
    [InlineData("DROP SEQUENCE IF EXISTS dbo.ids, [Other Schema].[Other Ids]")]
    [InlineData("DROP TYPE dbo.label")]
    [InlineData("DROP TYPE IF EXISTS [App].[Label]")]
    public void DropSharedTargets_AcceptsSqlServerSyntax(string sql)
    {
        var statement = Assert.IsType<DropObject>(ParseStatement(sql));

        Assert.Null(statement.PgBehavior);
        Assert.Equal(sql, statement.ToSql());
        Assert.Equal(statement, ParseStatement(statement.ToSql()));
    }

    [Theory]
    [InlineData("DROP SEQUENCE ids CASCADE")]
    [InlineData("DROP SEQUENCE ids RESTRICT")]
    [InlineData("DROP TYPE dbo.label, dbo.other_label")]
    [InlineData("DROP TYPE dbo.label CASCADE")]
    [InlineData("DROP DOMAIN dbo.label")]
    [InlineData("DROP ROUTINE app.run")]
    [InlineData("DROP FUNCTION app.run()")]
    [InlineData("DROP FUNCTION app.run(INT)")]
    [InlineData("DROP PROCEDURE app.run(IN id INT)")]
    [InlineData("DROP FUNCTION app.run CASCADE")]
    [InlineData("DROP PROCEDURE app.run RESTRICT")]
    [InlineData("DROP TRIGGER audit ON app.items")]
    [InlineData("DROP TABLE items CASCADE")]
    [InlineData("DROP VIEW visible RESTRICT")]
    [InlineData("DROP SCHEMA app, archive")]
    [InlineData("DROP SCHEMA app CASCADE")]
    [InlineData("DROP INDEX items_idx, other_idx ON items")]
    [InlineData("DROP INDEX items_idx ON items CASCADE")]
    [InlineData("DROP TEMPORARY TABLE items")]
    [InlineData("DROP TEMPORARY SEQUENCE ids")]
    public void DropSharedTargets_RejectsNonSqlServerSyntax(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Theory]
    [InlineData("DROP FUNCTION app.run")]
    [InlineData("DROP FUNCTION IF EXISTS app.run")]
    [InlineData("DROP PROCEDURE [App].[Run]")]
    [InlineData("DROP PROCEDURE IF EXISTS app.run")]
    [InlineData("DROP TRIGGER app.audit")]
    public void DropRoutineAndTrigger_RetainNativeSqlServerSimpleTargets(string sql)
    {
        var statement = Assert.IsType<DropObject>(ParseStatement(sql));

        Assert.Equal(sql, statement.ToSql());
        Assert.Single(statement.Names);
        Assert.Null(statement.OnObject);
        Assert.Null(statement.PgBehavior);
    }

    [Theory]
    [InlineData("ALTER TABLE items ADD COLUMN IF NOT EXISTS label TEXT")]
    [InlineData("ALTER TABLE items DROP COLUMN IF EXISTS label CASCADE")]
    [InlineData("ALTER TABLE items ALTER label TYPE TEXT")]
    [InlineData("ALTER TABLE items ALTER label SET DATA TYPE TEXT USING label")]
    [InlineData("ALTER TABLE items ALTER label SET NOT NULL")]
    [InlineData("ALTER TABLE items ALTER label DROP NOT NULL")]
    [InlineData("ALTER TABLE items ALTER label SET EXPRESSION AS (label)")]
    [InlineData("ALTER TABLE items ALTER label DROP EXPRESSION IF EXISTS")]
    [InlineData("ALTER TABLE items ALTER label SET STATISTICS 100")]
    [InlineData("ALTER TABLE items ALTER label SET (n_distinct = 5)")]
    [InlineData("ALTER TABLE items ALTER label RESET (n_distinct)")]
    [InlineData("ALTER TABLE items ALTER label SET STORAGE EXTENDED")]
    [InlineData("ALTER TABLE items ALTER label SET COMPRESSION lz4")]
    public void AlterTableColumnOperations_RejectPostgresForms(string sql)
    {
        Assert.ThrowsAny<ParseException>(() => ParseStatements(sql));
    }

    [Fact]
    public void AlterDispatch_AcceptsTable()
    {
        const string sql = "ALTER TABLE items ADD COLUMN label TEXT";

        var statement = Assert.IsType<AlterTable>(ParseStatement(sql));

        Assert.Equal(sql, statement.ToSql());
    }

    [Theory]
    [InlineData("ALTER MATERIALIZED VIEW summary RENAME TO summary_next")]
    [InlineData("ALTER MATERIALIZED VIEW IF EXISTS summary RENAME COLUMN id TO item_id")]
    [InlineData("ALTER MATERIALIZED VIEW summary SET SCHEMA archive")]
    [InlineData("ALTER MATERIALIZED VIEW summary SET TABLESPACE fast")]
    [InlineData("ALTER MATERIALIZED VIEW summary SET (fillfactor = 70)")]
    [InlineData("ALTER MATERIALIZED VIEW summary RESET (fillfactor)")]
    [InlineData("ALTER MATERIALIZED VIEW summary OWNER TO owner_name")]
    [InlineData("ALTER MATERIALIZED VIEW summary OWNER TO CURRENT_ROLE")]
    [InlineData("ALTER MATERIALIZED VIEW summary OWNER TO CURRENT_USER")]
    [InlineData("ALTER MATERIALIZED VIEW summary OWNER TO SESSION_USER")]
    [InlineData("ALTER MATERIALIZED VIEW summary SET ACCESS METHOD heap")]
    [InlineData("ALTER MATERIALIZED VIEW summary SET ACCESS METHOD DEFAULT")]
    [InlineData("ALTER MATERIALIZED VIEW summary CLUSTER ON summary_idx")]
    [InlineData("ALTER MATERIALIZED VIEW summary SET WITHOUT CLUSTER")]
    [InlineData("ALTER MATERIALIZED VIEW summary ALTER id SET STATISTICS 100, SET (fillfactor = 70)")]
    [InlineData("ALTER MATERIALIZED VIEW summary NO DEPENDS ON EXTENSION ext")]
    [InlineData("ALTER MATERIALIZED VIEW ALL IN TABLESPACE fast OWNED BY CURRENT_USER SET TABLESPACE slow NOWAIT")]
    public void AlterMaterializedViews_AreRejectedBySqlServer(string sql)
    {
        var state = GetState(sql);
        var target = state.PeekNth(1);

        var exception = Assert.Throws<ParseException.ExpectedButFound>(() => new MsParser().ParseStatement(state));

        Assert.Contains("Expected TABLE.", exception.Message);
        Assert.Same(target, state.Peek());
    }

    [Fact]
    public void DropIndex_RequiresOnTarget()
    {
        Assert.IsType<DropObject>(ParseStatement("DROP INDEX summary_idx ON summary"));
        Assert.ThrowsAny<ParseException>(() => ParseStatement("DROP INDEX summary_idx"));
    }
}
