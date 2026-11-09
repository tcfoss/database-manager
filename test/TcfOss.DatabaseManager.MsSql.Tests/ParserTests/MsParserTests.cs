using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO;
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
}
