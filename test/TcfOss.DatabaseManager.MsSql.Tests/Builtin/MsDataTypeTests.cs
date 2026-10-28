using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Tests;
using TcfOss.DatabaseManager.Core.Tests.BuiltIn;
using TcfOss.DatabaseManager.Core.Tests.ParserTests;
using TcfOss.DatabaseManager.MsSql.BuiltIn;
using TcfOss.DatabaseManager.MsSql.Lexing;
using TcfOss.DatabaseManager.MsSql.Parsing;

namespace TcfOss.DatabaseManager.MsSql.Tests.Builtin;

public class MsDataTypeTests : ParserTestsBase<MsLexer, MsParser>
{
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
    [InlineData("TIMESTAMP", DataTypeClass.Binary)]
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
        var column = Assert.IsType<StatementColumn.Typed>(Assert.Single(createTable.Columns));

        Assert.Equal(expectedClass, column.DataType.Class);
    }

    [Fact]
    public void DataType_TimestampToRowversion()
    {
        var createTableSql = "CREATE TABLE T (X TIMESTAMP)";
        var statements = ParseStatements(createTableSql);

        var createTable = Assert.IsType<CreateTable>(Assert.Single(statements));
        var column = Assert.IsType<StatementColumn.Typed>(Assert.Single(createTable.Columns));

        Assert.IsType<MsDataType.MsRowVersion>(column.DataType);
    }

    [Fact]
    public void DataTypeExpected_NonWordFound_Throws()
    {
        var ex = Assert.Throws<ParseException.ExpectedButFound>(() => ParseStatements("CREATE TABLE T (X !)"));
        Assert.Contains("data_type", ex.Message);
    }


    public class IsDataTypeKeywordTests : ExhaustiveKeywordTestData<bool>
    {
        protected override IEnumerable<TheoryDataRow<Keyword, bool>> GetSupportedData()
        {
            foreach (var keyword in MsDataTypeKeywords.All)
            {
                yield return new TheoryDataRow<Keyword, bool>(keyword, true);
            }
            foreach (var keyword in DataTypeKeywords.All)
            {
                yield return new TheoryDataRow<Keyword, bool>(keyword, true);
            }
        }

        protected override bool GetDefaultValue() => false;
    }

    [Theory]
    [ClassData(typeof(IsDataTypeKeywordTests))]
    public void TestIsDataTypeKeyword(Keyword keyword, bool expected)
    {
        var actual = new MsParser().DataTypeParser.IsDataTypeKeyword(keyword);
        Assert.Equal(expected, actual);
    }
}
