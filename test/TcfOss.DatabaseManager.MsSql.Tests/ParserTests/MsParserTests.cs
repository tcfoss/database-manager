using TcfOss.DatabaseManager.Core.BuiltIn;
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
    [InlineData("INT", DataTypeClass.Integral)]
    [InlineData("SMALLINT", DataTypeClass.Integral)]
    [InlineData("MONEY", DataTypeClass.ExactDecimal)]
    [InlineData("SMALLMONEY", DataTypeClass.ExactDecimal)]
    [InlineData("BIT", DataTypeClass.Boolean)]
    [InlineData("DATE", DataTypeClass.DateTime)]
    [InlineData("DATETIME", DataTypeClass.DateTime)]
    [InlineData("DATETIME2", DataTypeClass.DateTime)]
    [InlineData("DATETIMEOFFSET", DataTypeClass.DateTime)]
    [InlineData("SMALLDATETIME", DataTypeClass.DateTime)]
    [InlineData("TIME", DataTypeClass.DateTime)]
    [InlineData("TIME(3)", DataTypeClass.DateTime)]
    [InlineData("DATETIMEOFFSET(3)", DataTypeClass.DateTime)]
    [InlineData("UNIQUEIDENTIFIER", DataTypeClass.Guid)]
    [InlineData("ROWVERSION", DataTypeClass.Binary)]
    [InlineData("IMAGE", DataTypeClass.Binary)]
    [InlineData("NTEXT", DataTypeClass.String)]
    [InlineData("NCHAR(3)", DataTypeClass.String)]
    [InlineData("BINARY(16)", DataTypeClass.Binary)]
    [InlineData("VARBINARY(MAX)", DataTypeClass.Binary)]
    public void DataType_Classes(string typeDefinition, DataTypeClass expectedClass)
    {
        var createTableSql = $"CREATE TABLE T (X {typeDefinition})";
        var statements = ParseStatements(createTableSql);

        var createTable = Assert.IsType<CreateTable>(Assert.Single(statements));
        var column = Assert.IsType<StatementColumn.Typed>(Assert.Single(createTable.Columns!));

        Assert.Equal(expectedClass, column.DataType.Class);
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
        var column = Assert.IsType<StatementColumn.MsComputed>(Assert.Single(createTable.Columns!));

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
}
