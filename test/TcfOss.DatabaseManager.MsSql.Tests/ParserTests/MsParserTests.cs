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
}
