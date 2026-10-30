using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO;
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
    [InlineData("Char(4)", "CHAR(4)")]
    [InlineData("CHAR(MAX)", "CHAR(MAX)")]
    [InlineData("Varchar(3)", "VARCHAR(3)")]
    [InlineData("varchar(MAX)", "VARCHAR(MAX)")]
    [InlineData("NVarchar(12)", "NVARCHAR(12)")]
    [InlineData("NVARCHAR(MAX)", "NVARCHAR(MAX)")]
    [InlineData("Text", "TEXT")]
    public void TestStringTypes(string givenText, string expectedSql)
    {
        var dt = GetDataType(givenText);

        Assert.NotNull(dt);
        Assert.Equal(DataTypeClass.String, dt.Class);
        Assert.Equal(expectedSql, dt.ToSql());
    }

    [Theory]
    [InlineData("Char")]
    [InlineData("Varchar")]
    [InlineData("NVarchar")]
    public void TestMissingStringLengthThrows(string givenText)
    {
        var ex = Assert.Throws<ParseException.ExpectedButFound>(() => GetDataType(givenText));
        var messageTemplate = "Expected {0}. Found {1}.";
        var expected = string.Format(messageTemplate, "data_type_length".Italic(), "end of input".Italic());
        Assert.EndsWith(expected, ex.Message);
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

        Assert.Equal(expectedClass, Assert.IsType<DataTypeReference.BuiltIn>(column.DataType).DataType.Class);
    }

    [Fact]
    public void DataType_TimestampToRowversion()
    {
        var createTableSql = "CREATE TABLE T (X TIMESTAMP)";
        var statements = ParseStatements(createTableSql);

        var createTable = Assert.IsType<CreateTable>(Assert.Single(statements));
        var column = Assert.IsType<StatementColumn.Typed>(Assert.Single(createTable.Columns));

        Assert.IsType<MsDataType.MsRowVersion>(Assert.IsType<DataTypeReference.BuiltIn>(column.DataType).DataType);
    }

    [Fact]
    public void DataTypeExpected_NonWordFound_Throws()
    {
        var ex = Assert.Throws<ParseException.ExpectedButFound>(() => ParseStatements("CREATE TABLE T (X !)"));
        Assert.Contains("data_type", ex.Message);
    }

    [Theory]
    [ClassData(typeof(IsDataTypeKeywordTests))]
    public void TestIsDataTypeKeyword(Keyword keyword, bool expected)
    {
        var actual = new MsParser().DataTypeParser.IsDataTypeKeyword(keyword);
        Assert.Equal(expected, actual);
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
    [InlineData("CHAR(0)")]
    [InlineData("VARCHAR(0)")]
    [InlineData("NVARCHAR(0)")]
    [InlineData("NCHAR(0)")]
    [InlineData("BINARY(0)")]
    [InlineData("VARBINARY(0)")]
    public void ZeroStringLengths_ReportValidationError(string sql)
    {
        var exception = Assert.Throws<ParseException.ValueOutOfRange>(() => ParseStatements($"CREATE TABLE sample (value {sql})"));

        Assert.Equal("length", exception.Label);
        Assert.Equal("0", exception.Value);
        Assert.Equal(1, exception.Minimum);
    }

    [Theory]
    [InlineData("mood")]
    [InlineData("app.mood")]
    [InlineData("\"mood\"")]
    [InlineData("[mood]")]
    public void TypeReferences_RejectNamedTypes(string sql)
    {
        var parser = new MsParser();

        Assert.ThrowsAny<ParseException>(() => parser.DataTypeParser.ParseDataTypeReference(GetState(sql)));
        Assert.ThrowsAny<ParseException>(() => parser.DataTypeParser.ParseDataTypeReferenceForConvert(GetState(sql)));
    }


    private static DataType GetDataType(string sql)
    {
        var parser = new MsParser();
        var state = GetState(sql);
        return parser.DataTypeParser.ParseDataType(state);
    }
}
