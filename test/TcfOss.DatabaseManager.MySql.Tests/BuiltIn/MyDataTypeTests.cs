using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Tests;
using TcfOss.DatabaseManager.MySql.BuiltIn;
using TcfOss.DatabaseManager.MySql.Lexing;
using TcfOss.DatabaseManager.MySql.Parsing;
using Xunit.Sdk;

[assembly: RegisterXunitSerializer(typeof(StringEnumTestHelpers.StringEnumSerializer<MySqlNumericAttribute>), typeof(MySqlNumericAttribute))]

namespace TcfOss.DatabaseManager.MySql.Tests.BuiltIn;

public class MyDataTypeTests
{
    [Theory]
    [ClassData(typeof(StringEnumTestHelpers.StringEnumTestData<MySqlNumericAttribute>))]
    public void NumericAttributeSerializerRoundTrips(string text, MySqlNumericAttribute attribute)
    {
        var serializer = new StringEnumTestHelpers.StringEnumSerializer<MySqlNumericAttribute>();

        Assert.True(serializer.IsSerializable(typeof(MySqlNumericAttribute), attribute, out var failureReason), failureReason);
        Assert.Equal(text, serializer.Serialize(attribute));
        Assert.Equal(attribute, serializer.Deserialize(typeof(MySqlNumericAttribute), text));
    }

    [Theory]
    [InlineData("tinyint", typeof(MyDataType.MyTinyInt), null, null, "TINYINT", Label = "tinyint")]
    [InlineData("tinyint(3)", typeof(MyDataType.MyTinyInt), null, 3u, "TINYINT(3)", Label = "tinyint width")]
    [InlineData("tinyint unsigned", typeof(MyDataType.MyTinyInt), MySqlNumericAttributeOption.Unsigned, null, "TINYINT UNSIGNED", Label = "tinyint unsigned")]
    [InlineData("tinyint(4) zerofill", typeof(MyDataType.MyTinyInt), MySqlNumericAttributeOption.Zerofill, 4u, "TINYINT(4) ZEROFILL", Label = "tinyint zerofill")]
    [InlineData("smallint", typeof(MyDataType.MySmallInt), null, null, "SMALLINT", Label = "smallint")]
    [InlineData("smallint(5) unsigned", typeof(MyDataType.MySmallInt), MySqlNumericAttributeOption.Unsigned, 5u, "SMALLINT(5) UNSIGNED", Label = "smallint unsigned")]
    [InlineData("mediumint", typeof(MyDataType.MyMediumInt), null, null, "MEDIUMINT", Label = "mediumint")]
    [InlineData("mediumint(6) zerofill", typeof(MyDataType.MyMediumInt), MySqlNumericAttributeOption.Zerofill, 6u, "MEDIUMINT(6) ZEROFILL", Label = "mediumint zerofill")]
    [InlineData("int", typeof(MyDataType.MyInt), null, null, "INT", Label = "int")]
    [InlineData("int(8) signed", typeof(MyDataType.MyInt), MySqlNumericAttributeOption.Signed, 8u, "INT(8) SIGNED", Label = "int signed")]
    [InlineData("bigint", typeof(MyDataType.MyBigInt), null, null, "BIGINT", Label = "bigint")]
    [InlineData("bigint(12) unsigned", typeof(MyDataType.MyBigInt), MySqlNumericAttributeOption.Unsigned, 12u, "BIGINT(12) UNSIGNED", Label = "bigint unsigned")]
    public void Integer_Types(string givenText, Type expectedType, MySqlNumericAttributeOption? expectedAttribute, uint? width, string expectedText)
    {
        var dt = GetDataType(givenText);

        Assert.IsType(expectedType, dt);

        var asInteger = dt as MyDataType.BaseMyIntegerType;
        Assert.NotNull(asInteger);

        Assert.Equal(DataTypeClass.Integral, asInteger.Class);

        Assert.Equal(width, asInteger.Width);
        Assert.Equal(FromOption(expectedAttribute), asInteger.NumericAttribute);

        Assert.Equal(expectedText, dt.ToSql());
    }

    [Theory]
    [InlineData("decimal", typeof(MyDataType.MyDecimal), null, "DECIMAL")]
    [InlineData("decimal(10,2)", typeof(MyDataType.MyDecimal), null, "DECIMAL(10,2)")]
    [InlineData("decimal(10,0) unsigned", typeof(MyDataType.MyDecimal), MySqlNumericAttributeOption.Unsigned, "DECIMAL(10,0) UNSIGNED")]
    public void Decimal_Types(string givenText, Type expectedType, MySqlNumericAttributeOption? expectedAttribute, string expectedText)
    {
        var dt = GetDataType(givenText);

        Assert.IsType(expectedType, dt);

        var asDec = dt as MyDataType.MyDecimal;
        Assert.NotNull(asDec);

        Assert.Equal(DataTypeClass.ExactDecimal, asDec.Class);
        Assert.Equal(FromOption(expectedAttribute), asDec.NumericAttribute);
        Assert.Equal(expectedText, dt.ToSql());
    }

    [Theory]
    [InlineData("double", typeof(MyDataType.MyDouble), null, "DOUBLE")]
    [InlineData("double(8,4)", typeof(MyDataType.MyDouble), null, "DOUBLE(8,4)")]
    [InlineData("double unsigned", typeof(MyDataType.MyDouble), MySqlNumericAttributeOption.Unsigned, "DOUBLE UNSIGNED")]
    [InlineData("float", typeof(MyDataType.MyFloat), null, "FLOAT")]
    [InlineData("float(6,3) signed", typeof(MyDataType.MyFloat), MySqlNumericAttributeOption.Signed, "FLOAT(6,3) SIGNED")]
    public void ApproximateDecimal_Types(string givenText, Type expectedType, MySqlNumericAttributeOption? expectedAttribute, string expectedText)
    {
        var dt = GetDataType(givenText);

        Assert.IsType(expectedType, dt);

        if (expectedType == typeof(MyDataType.MyDouble))
        {
            var asD = dt as MyDataType.MyDouble;
            Assert.NotNull(asD);
            Assert.Equal(DataTypeClass.ApproximateDecimal, asD.Class);
            Assert.Equal(FromOption(expectedAttribute), asD.NumericAttribute);
        }
        else
        {
            var asF = dt as MyDataType.MyFloat;
            Assert.NotNull(asF);
            Assert.Equal(DataTypeClass.ApproximateDecimal, asF.Class);
            Assert.Equal(FromOption(expectedAttribute), asF.NumericAttribute);
        }

        Assert.Equal(expectedText, dt.ToSql());
    }

    [Theory]
    [InlineData("char(4)", typeof(MyDataType.MyChar), null, null, "CHAR(4)", Label = "char")]
    [InlineData("char(10) character set utf8 collate utf8_general_ci", typeof(MyDataType.MyChar), "utf8", "utf8_general_ci", "CHAR(10) CHARACTER SET utf8 COLLATE utf8_general_ci", Label = "char charset and collation")]
    [InlineData("Char(3) Character Set utf8", typeof(MyDataType.MyChar), "utf8", null, "CHAR(3) CHARACTER SET utf8", Label = "char charset")]
    [InlineData("CHAR(5) COLLATE utf8mb4_unicode_ci", typeof(MyDataType.MyChar), null, "utf8mb4_unicode_ci", "CHAR(5) COLLATE utf8mb4_unicode_ci", Label = "char collation")]
    [InlineData("varchar(10)", typeof(MyDataType.MyVarchar), null, null, "VARCHAR(10)", Label = "varchar")]
    [InlineData("varchar(20) character set latin1 collate latin1_swedish_ci", typeof(MyDataType.MyVarchar), "latin1", "latin1_swedish_ci", "VARCHAR(20) CHARACTER SET latin1 COLLATE latin1_swedish_ci", Label = "varchar charset and collation")]
    [InlineData("VARCHAR(15) CHARACTER SET utf8mb4", typeof(MyDataType.MyVarchar), "utf8mb4", null, "VARCHAR(15) CHARACTER SET utf8mb4", Label = "varchar charset")]
    [InlineData("varchar(25) COLLATE utf8_general_ci", typeof(MyDataType.MyVarchar), null, "utf8_general_ci", "VARCHAR(25) COLLATE utf8_general_ci", Label = "varchar collation")]
    [InlineData("national varchar(20)", typeof(MyDataType.MyNationalVarchar), null, null, "NATIONAL VARCHAR(20)", Label = "national varchar")]
    [InlineData("nvarchar(30) character set utf8mb4 collate utf8mb4_general_ci", typeof(MyDataType.MyNationalVarchar), "utf8mb4", "utf8mb4_general_ci", "NATIONAL VARCHAR(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci", Label = "nvarchar charset and collation")]
    [InlineData("NVARCHAR(12) CHARACTER SET latin1", typeof(MyDataType.MyNationalVarchar), "latin1", null, "NATIONAL VARCHAR(12) CHARACTER SET latin1", Label = "nvarchar charset")]
    [InlineData("nvarchar(40) COLLATE latin1_swedish_ci", typeof(MyDataType.MyNationalVarchar), null, "latin1_swedish_ci", "NATIONAL VARCHAR(40) COLLATE latin1_swedish_ci", Label = "nvarchar collation")]
    [InlineData("NATIONAL VARCHAR(50)", typeof(MyDataType.MyNationalVarchar), null, null, "NATIONAL VARCHAR(50)", Label = "national varchar keyword")]
    [InlineData("national char(10)", typeof(MyDataType.MyNationalChar), null, null, "NATIONAL CHAR(10)", Label = "national char")]
    [InlineData("nchar(8) character set utf8 collate utf8_general_ci", typeof(MyDataType.MyNationalChar), "utf8", "utf8_general_ci", "NATIONAL CHAR(8) CHARACTER SET utf8 COLLATE utf8_general_ci", Label = "nchar charset and collation")]
    [InlineData("NCHAR(6) CHARACTER SET latin1", typeof(MyDataType.MyNationalChar), "latin1", null, "NATIONAL CHAR(6) CHARACTER SET latin1", Label = "nchar charset")]
    [InlineData("nchar(12) COLLATE latin1_swedish_ci", typeof(MyDataType.MyNationalChar), null, "latin1_swedish_ci", "NATIONAL CHAR(12) COLLATE latin1_swedish_ci", Label = "nchar collation")]
    [InlineData("text", typeof(MyDataType.MyText), null, null, "TEXT", Label = "text")]
    [InlineData("text character set utf8 collate utf8_general_ci", typeof(MyDataType.MyText), "utf8", "utf8_general_ci", "TEXT CHARACTER SET utf8 COLLATE utf8_general_ci", Label = "text charset and collation")]
    [InlineData("TEXT CHARACTER SET latin1", typeof(MyDataType.MyText), "latin1", null, "TEXT CHARACTER SET latin1", Label = "text charset")]
    [InlineData("text COLLATE utf8mb4_unicode_ci", typeof(MyDataType.MyText), null, "utf8mb4_unicode_ci", "TEXT COLLATE utf8mb4_unicode_ci", Label = "text collation")]
    [InlineData("tinytext", typeof(MyDataType.MyTinyText), null, null, "TINYTEXT", Label = "tinytext")]
    [InlineData("tinytext character set utf8 collate utf8_general_ci", typeof(MyDataType.MyTinyText), "utf8", "utf8_general_ci", "TINYTEXT CHARACTER SET utf8 COLLATE utf8_general_ci", Label = "tinytext charset and collation")]
    [InlineData("TINYTEXT CHARACTER SET latin1", typeof(MyDataType.MyTinyText), "latin1", null, "TINYTEXT CHARACTER SET latin1", Label = "tinytext charset")]
    [InlineData("mediumtext", typeof(MyDataType.MyMediumText), null, null, "MEDIUMTEXT", Label = "mediumtext")]
    [InlineData("mediumtext character set utf8 collate utf8_general_ci", typeof(MyDataType.MyMediumText), "utf8", "utf8_general_ci", "MEDIUMTEXT CHARACTER SET utf8 COLLATE utf8_general_ci", Label = "mediumtext charset and collation")]
    [InlineData("MEDIUMTEXT CHARACTER SET latin1", typeof(MyDataType.MyMediumText), "latin1", null, "MEDIUMTEXT CHARACTER SET latin1", Label = "mediumtext charset")]
    [InlineData("longtext", typeof(MyDataType.MyLongText), null, null, "LONGTEXT", Label = "longtext")]
    [InlineData("longtext character set utf8 collate utf8_general_ci", typeof(MyDataType.MyLongText), "utf8", "utf8_general_ci", "LONGTEXT CHARACTER SET utf8 COLLATE utf8_general_ci", Label = "longtext charset and collation")]
    [InlineData("LONGTEXT CHARACTER SET latin1", typeof(MyDataType.MyLongText), "latin1", null, "LONGTEXT CHARACTER SET latin1", Label = "longtext charset")]
    public void String_Types(string givenText, Type expectedType, string? characterSet, string? collation, string expectedText)
    {
        var dt = GetDataType(givenText);

        Assert.IsType(expectedType, dt);
        Assert.Equal(DataTypeClass.String, dt.Class);

        var asString = dt as MyDataType.BaseMyStringType;
        Assert.NotNull(asString);

        StringAttribute? expectedAttributes = null;
        if (characterSet != null || collation != null)
        {
            expectedAttributes = new StringAttribute(characterSet, collation);
        }

        Assert.Equal(expectedAttributes, asString.StringAttribute);

        Assert.Equal(expectedText, dt.ToSql());
    }

    [Theory]
    [InlineData("binary(16)", typeof(MyDataType.Binary), "BINARY(16)")]
    [InlineData("varbinary(32)", typeof(MyDataType.Varbinary), "VARBINARY(32)")]
    [InlineData("tinyblob", typeof(MyDataType.TinyBlob), "TINYBLOB")]
    [InlineData("mediumblob", typeof(MyDataType.MediumBlob), "MEDIUMBLOB")]
    [InlineData("longblob", typeof(MyDataType.LongBlob), "LONGBLOB")]
    public void BinaryTypes(string givenText, Type expectedType, string expectedText)
    {
        var dt = GetDataType(givenText);

        Assert.IsType(expectedType, dt);
        Assert.Equal(DataTypeClass.Binary, dt.Class);
        Assert.Equal(expectedText, dt.ToSql());
    }

    [Theory]
    [InlineData("date", typeof(MyDataType.Date), DataTypeClass.Date, "DATE")]
    [InlineData("datetime", typeof(MyDataType.DateTime), DataTypeClass.DateTime, "DATETIME")]
    [InlineData("datetime(3)", typeof(MyDataType.DateTime), DataTypeClass.DateTime, "DATETIME(3)")]
    [InlineData("time", typeof(MyDataType.Time), DataTypeClass.Time, "TIME")]
    [InlineData("time(2)", typeof(MyDataType.Time), DataTypeClass.Time, "TIME(2)")]
    [InlineData("timestamp", typeof(MyDataType.TimeStamp), DataTypeClass.DateTime, "TIMESTAMP")]
    [InlineData("year", typeof(MyDataType.Year), DataTypeClass.Year, "YEAR")]
    public void DateTime_Types(string givenText, Type expectedType, DataTypeClass expectedClass, string expectedText)
    {
        var dt = GetDataType(givenText);

        Assert.IsType(expectedType, dt);

        Assert.Equal(expectedClass, dt.Class);

        Assert.Equal(expectedText, dt.ToSql());
    }

    [Fact]
    public void Blob()
    {
        var dt = GetDataType("blob");
        Assert.IsType<MyDataType.Blob>(dt);
        Assert.Equal(DataTypeClass.Binary, dt.Class);
        Assert.Equal("BLOB", dt.ToSql());
    }

    [Theory]
    [InlineData("geometry", typeof(MyDataType.Geometry), "GEOMETRY", Label = "geometry")]
    [InlineData("point", typeof(MyDataType.Geometry.Point), "POINT", Label = "point")]
    [InlineData("curve", typeof(MyDataType.Geometry.Curve), "CURVE", Label = "curve")]
    [InlineData("linestring", typeof(MyDataType.Geometry.Curve.LineString), "LINESTRING", Label = "linestring")]
    [InlineData("line", typeof(MyDataType.Geometry.Curve.LineString.Line), "LINE", Label = "line")]
    [InlineData("LinearRing", typeof(MyDataType.Geometry.Curve.LineString.LinearRing), "LINEARRING", Label = "linear ring")]
    [InlineData("SuRfACE", typeof(MyDataType.Geometry.Surface), "SURFACE", Label = "surface")]
    [InlineData("polygon", typeof(MyDataType.Geometry.Surface.Polygon), "POLYGON", Label = "polygon")]
    [InlineData("GeoMETryCOllECTion", typeof(MyDataType.Geometry.GeometryCollection), "GEOMETRYCOLLECTION", Label = "geometry collection")]
    [InlineData("multipoint", typeof(MyDataType.Geometry.GeometryCollection.MultiPoint), "MULTIPOINT", Label = "multipoint")]
    [InlineData("MULTICURVE", typeof(MyDataType.Geometry.GeometryCollection.MultiCurve), "MULTICURVE", Label = "multicurve")]
    [InlineData("multilinestring", typeof(MyDataType.Geometry.GeometryCollection.MultiCurve.MultiLineString), "MULTILINESTRING", Label = "multilinestring")]
    [InlineData("multiSURFACE", typeof(MyDataType.Geometry.GeometryCollection.MultiSurface), "MULTISURFACE", Label = "multisurface")]
    [InlineData("multipolygon", typeof(MyDataType.Geometry.GeometryCollection.MultiSurface.MultiPolygon), "MULTIPOLYGON", Label = "multipolygon")]
    public void Geometry_Types(string givenText, Type expectedType, string expectedText)
    {
        var dt = GetDataType(givenText);

        Assert.IsType(expectedType, dt);
        Assert.Equal(DataTypeClass.Geometry, dt.Class);
        Assert.Equal(expectedText, dt.ToSql());
    }

    private static DataType GetDataType(string text)
    {
        var lexer = new MyLexer();
        var parser = new MyParser();
        var state = new Core.Parsing.ParserState([.. lexer.Tokenize(text)]);
        return parser.DataTypeParser.ParseDataType(state);
    }

    private static MySqlNumericAttribute? FromOption(MySqlNumericAttributeOption? option)
    {
        return option switch
        {
            MySqlNumericAttributeOption.Signed => MySqlNumericAttribute.Signed,
            MySqlNumericAttributeOption.Unsigned => MySqlNumericAttribute.Unsigned,
            MySqlNumericAttributeOption.Zerofill => MySqlNumericAttribute.Zerofill,
            MySqlNumericAttributeOption.None => null,
            null => null,
            _ => throw new ArgumentOutOfRangeException(nameof(option), option, null),
        };
    }

    public enum MySqlNumericAttributeOption
    {
        None,
        Signed,
        Unsigned,
        Zerofill
    }
}
