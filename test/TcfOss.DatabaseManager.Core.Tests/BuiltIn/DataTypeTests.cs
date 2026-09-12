using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Tests.ParserTests;

namespace TcfOss.DatabaseManager.Core.Tests.BuiltIn;

public class DataTypeTests : ParserTestsBase<GenericLexer, Parser>
{
    [Theory]
    [InlineData("BOOLEAN", "BOOLEAN")]
    [InlineData("bool", "BOOLEAN")]
    [InlineData("INT", "INT")]
    [InlineData("INTEGER", "INT")]
    [InlineData("bigint", "BIGINT")]
    [InlineData("BigInt", "BIGINT")]
    [InlineData("tinyint", "TINYINT")]
    [InlineData("TinyInt", "TINYINT")]
    [InlineData("NUMERIC", "DECIMAL")]
    [InlineData("Decimal", "DECIMAL")]
    [InlineData("DECIMAL(3)", "DECIMAL(3)")]
    [InlineData("NUMERIC(10,2)", "DECIMAL(10,2)")]
    [InlineData("char(4)", "CHAR(4)")]
    [InlineData("VarChar(3)", "VARCHAR(3)")]
    [InlineData("NVarchar(12)", "NVARCHAR(12)")]
    [InlineData("text", "TEXT")]
    public static void Simple_Data_Types_To_Canonical(string given, string canonical)
    {
        var parser = new Parser();

        var (expected, actual) = GetExpectedActual(s => parser.DataTypeParser.ParseDataType(s), given, canonical);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TestEnum()
    {
        var text = "ENUM('a', 'b', 'c')";

        var enumDataType = GetDataType(text) as DataType.Enum;

        Assert.NotNull(enumDataType);

        Assert.Equal(DataTypeClass.Enum, enumDataType.Class);

        Assert.Equal(3, enumDataType.Values.Count);
        Assert.Equal("a", enumDataType.Values[0]);
        Assert.Equal("b", enumDataType.Values[1]);
        Assert.Equal("c", enumDataType.Values[2]);

        Assert.Equal(text, enumDataType.ToSql());
    }

    [Fact]
    public void TestSet()
    {
        var text = "SET('x', 'y', 'z')";
        var setDataType = GetDataType(text) as DataType.Set;

        Assert.NotNull(setDataType);

        Assert.Equal(DataTypeClass.Set, setDataType.Class);

        Assert.Equal(3, setDataType.Values.Count);
        Assert.Equal("x", setDataType.Values[0]);
        Assert.Equal("y", setDataType.Values[1]);
        Assert.Equal("z", setDataType.Values[2]);

        Assert.Equal(text, setDataType.ToSql());
    }

    [Theory]
    [InlineData("BOOLEAN")]
    [InlineData("bool")]
    public void TestBoolean(string givenText)
    {
        var boolDataType = GetDataType(givenText);

        Assert.NotNull(boolDataType);

        Assert.Equal(DataTypeClass.Boolean, boolDataType.Class);
        Assert.Equal("BOOLEAN", boolDataType.ToSql());
    }

    [Theory]
    [InlineData("INT")]
    [InlineData("INTEGER")]
    public void TestIntegerTypes(string givenText)
    {
        var intDataType = GetDataType(givenText);

        Assert.NotNull(intDataType);

        Assert.Equal(DataTypeClass.Integral, intDataType.Class);
        Assert.Equal("INT", intDataType.ToSql());
    }

    [Theory]
    [InlineData("DECIMAL", "DECIMAL")]
    [InlineData("Decimal(3)", "DECIMAL(3)")]
    [InlineData("NUMERIC(10,2)", "DECIMAL(10,2)")]
    public void TestDecimalTypes(string givenText, string expectedSql)
    {
        var dec = GetDataType(givenText);

        Assert.NotNull(dec);
        Assert.Equal(DataTypeClass.ExactDecimal, dec.Class);
        Assert.Equal(expectedSql, dec.ToSql());
    }

    [Theory]
    [InlineData("DOUBLE", "DOUBLE")]
    [InlineData("double(4)", "DOUBLE(4)")]
    [InlineData("Double(5,2)", "DOUBLE(5,2)")]
    [InlineData("FLOAT", "FLOAT")]
    [InlineData("float(7)", "FLOAT(7)")]
    [InlineData("Float(6,3)", "FLOAT(6,3)")]
    public void TestApproximateDecimalTypes(string givenText, string expectedSql)
    {
        var dt = GetDataType(givenText);

        Assert.NotNull(dt);
        Assert.Equal(DataTypeClass.ApproximateDecimal, dt.Class);
        Assert.Equal(expectedSql, dt.ToSql());
    }

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

    private static DataType GetDataType(string sql)
    {
        var parser = new Parser();
        var state = GetState(sql);
        return parser.DataTypeParser.ParseDataType(state);
    }
}
