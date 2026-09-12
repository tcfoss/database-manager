using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.MySql.BuiltIn;

namespace TcfOss.DatabaseManager.MySql.Parsing;

public class MyDataTypeParser : DataTypeParser
{
    private static MySqlNumericAttribute? ParseOptionalNumericAttribute(ParserState state)
    {
        if (state.ParseKeywordSequence(KeywordSearchCondition.OneOf(Keyword.UNSIGNED, Keyword.ZEROFILL), KeywordSearchCondition.OneOf(Keyword.ZEROFILL, Keyword.UNSIGNED)).Count > 0)
        {
            return MySqlNumericAttribute.Zerofill;
        }
        if (state.ParseKeyword(Keyword.ZEROFILL))
        {
            return MySqlNumericAttribute.Zerofill;
        }
        if (state.ParseKeyword(Keyword.SIGNED))
        {
            return MySqlNumericAttribute.Signed;
        }
        if (state.ParseKeyword(Keyword.UNSIGNED))
        {
            return MySqlNumericAttribute.Unsigned;
        }
        return null;
    }

    protected override DataType ParseTinyInt(ParserState state)
    {
        uint? width = ParseOptionalSimplePrecision(state);
        MySqlNumericAttribute? numericAttribute = ParseOptionalNumericAttribute(state);
        return new MyDataType.MyTinyInt()
        {
            Width = width,
            NumericAttribute = numericAttribute,
        };
    }

    private static MyDataType.MySmallInt ParseSmallInt(ParserState state)
    {
        uint? width = ParseOptionalSimplePrecision(state);
        MySqlNumericAttribute? numericAttribute = ParseOptionalNumericAttribute(state);
        return new MyDataType.MySmallInt()
        {
            Width = width,
            NumericAttribute = numericAttribute,
        };
    }

    private static MyDataType.MyMediumInt ParseMediumInt(ParserState state)
    {
        uint? width = ParseOptionalSimplePrecision(state);
        MySqlNumericAttribute? numericAttribute = ParseOptionalNumericAttribute(state);
        return new MyDataType.MyMediumInt()
        {
            Width = width,
            NumericAttribute = numericAttribute,
        };
    }

    protected override DataType ParseInteger(ParserState state)
    {
        uint? width = ParseOptionalSimplePrecision(state);
        MySqlNumericAttribute? numericAttribute = ParseOptionalNumericAttribute(state);
        return new MyDataType.MyInt()
        {
            Width = width,
            NumericAttribute = numericAttribute
        };
    }

    protected override DataType ParseBigInt(ParserState state)
    {
        uint? width = ParseOptionalSimplePrecision(state);
        MySqlNumericAttribute? numericAttribute = ParseOptionalNumericAttribute(state);
        return new MyDataType.MyBigInt()
        {
            Width = width,
            NumericAttribute = numericAttribute
        };
    }

    protected override DataType ParseBoolean()
    {
        return new MyDataType.MyTinyInt()
        {
            Width = 1,
        };
    }

    protected override DataType ParseDecimal(ParserState state)
    {
        NumericLength? numericLength = ParseOptionalNumericLength(state);
        MySqlNumericAttribute? numericAttribute = ParseOptionalNumericAttribute(state);
        return new MyDataType.MyDecimal(numericLength)
        {
            NumericAttribute = numericAttribute
        };
    }

    protected override DataType ParseFloat(ParserState state)
    {
        NumericLength? numericLength = ParseOptionalNumericLength(state);
        MySqlNumericAttribute? numericAttribute = ParseOptionalNumericAttribute(state);
        return new MyDataType.MyFloat(numericLength)
        {
            NumericAttribute = numericAttribute
        };
    }

    protected override DataType ParseDouble(ParserState state)
    {
        NumericLength? numericLength = ParseOptionalNumericLength(state);
        MySqlNumericAttribute? numericAttribute = ParseOptionalNumericAttribute(state);
        return new MyDataType.MyDouble(numericLength)
        {
            NumericAttribute = numericAttribute
        };
    }

    private static MyDataType.DateTime ParseDateTime(ParserState state)
    {
        uint? precision = ParseOptionalSimplePrecision(state);
        return new MyDataType.DateTime() { Precision = precision };
    }

    private static MyDataType.TimeStamp ParseTimestamp(ParserState state)
    {
        uint? precision = ParseOptionalSimplePrecision(state);
        return new MyDataType.TimeStamp() { Precision = precision };
    }

    private static MyDataType.Time ParseTime(ParserState state)
    {
        uint? precision = ParseOptionalSimplePrecision(state);
        return new MyDataType.Time() { Precision = precision };
    }

    private static MyDataType.Year ParseYear(ParserState state)
    {
        uint? precision = ParseOptionalSimplePrecision(state);
        return new MyDataType.Year() { Precision = precision };
    }

    private static StringAttribute? ParseOptionalStringAttribute(ParserState state)
    {
        string? characterSet = null;
        string? collation = null;

        while (true)
        {
            if (state.ParseKeywordsAll(Keyword.CHARACTER, Keyword.SET))
            {
                characterSet = ValueParser.ParseLiteralString(state);
            }
            else if (state.ParseKeyword(Keyword.CHARSET))
            {
                characterSet = ValueParser.ParseLiteralString(state);
            }
            else if (state.ParseKeyword(Keyword.COLLATE))
            {
                collation = ValueParser.ParseLiteralString(state);
            }
            else
            {
                break;
            }
        }

        if (characterSet != null || collation != null)
        {
            return new StringAttribute(characterSet, collation);
        }
        return null;
    }

    protected override DataType ParseChar(ParserState state)
    {
        uint length = ParseSimplePrecision(state);
        StringAttribute? stringOptions = ParseOptionalStringAttribute(state);

        return new MyDataType.MyChar(length)
        {
            StringAttribute = stringOptions
        };
    }

    private static MyDataType.MyCharOptionalLength ParseCharOptionalLength(ParserState state)
    {
        uint? length = ParseOptionalSimplePrecision(state);

        StringAttribute? stringOptions = ParseOptionalStringAttribute(state);

        return new MyDataType.MyCharOptionalLength(length)
        {
            StringAttribute = stringOptions
        };
    }

    protected override DataType ParseVarchar(ParserState state)
    {
        uint length = ParseSimplePrecision(state);
        StringAttribute? stringOptions = ParseOptionalStringAttribute(state);

        return new MyDataType.MyVarchar(length)
        {
            StringAttribute = stringOptions
        };
    }

    private static MyDataType.MyVarcharOptionalLength ParseVarcharOptionalLength(ParserState state)
    {
        uint? length = ParseOptionalSimplePrecision(state);

        StringAttribute? stringOptions = ParseOptionalStringAttribute(state);

        return new MyDataType.MyVarcharOptionalLength(length)
        {
            StringAttribute = stringOptions
        };
    }

    protected override DataType ParseNationalVarchar(ParserState state)
    {
        uint length = ParseSimplePrecision(state);
        StringAttribute? stringOptions = ParseOptionalStringAttribute(state);

        return new MyDataType.MyNationalVarchar(length)
        {
            StringAttribute = stringOptions
        };
    }

    private static MyDataType.MyNationalVarcharOptionalLength ParseNationalVarcharOptionalLength(ParserState state)
    {
        uint? length = ParseOptionalSimplePrecision(state);

        StringAttribute? stringOptions = ParseOptionalStringAttribute(state);

        return new MyDataType.MyNationalVarcharOptionalLength(length)
        {
            StringAttribute = stringOptions
        };
    }

    private static MyDataType.MyTinyText ParseTinyText(ParserState state)
    {
        uint? length = ParseOptionalSimplePrecision(state);
        StringAttribute? stringOptions = ParseOptionalStringAttribute(state);

        return new MyDataType.MyTinyText()
        {
            StringAttribute = stringOptions,
            Length = length,
        };
    }

    protected override DataType ParseText(ParserState state)
    {
        uint? length = ParseOptionalSimplePrecision(state);
        StringAttribute? stringOptions = ParseOptionalStringAttribute(state);

        return new MyDataType.MyText()
        {
            StringAttribute = stringOptions,
            Length = length,
        };
    }

    private static MyDataType.MyMediumText ParseMediumText(ParserState state)
    {
        uint? length = ParseOptionalSimplePrecision(state);
        StringAttribute? stringOptions = ParseOptionalStringAttribute(state);
        return new MyDataType.MyMediumText()
        {
            StringAttribute = stringOptions,
            Length = length,
        };
    }

    private static MyDataType.MyLongText ParseLongText(ParserState state)
    {
        uint? length = ParseOptionalSimplePrecision(state);
        StringAttribute? stringOptions = ParseOptionalStringAttribute(state);
        return new MyDataType.MyLongText()
        {
            StringAttribute = stringOptions,
            Length = length,
        };
    }

    private static MyDataType.Binary ParseBinary(ParserState state)
    {
        uint length = ParseSimplePrecision(state);
        return new MyDataType.Binary(length);
    }

    private static MyDataType.Varbinary ParseVarbinary(ParserState state)
    {
        uint length = ParseSimplePrecision(state);
        return new MyDataType.Varbinary(length);
    }

    public override bool IsDataTypeKeyword(Keyword? keyword) => keyword is
        Keyword.MEDIUMINT or Keyword.SMALLINT or
        Keyword.DATETIME or Keyword.TIMESTAMP or Keyword.TIME or Keyword.DATE or Keyword.YEAR or
        Keyword.TINYTEXT or Keyword.MEDIUMTEXT or Keyword.LONGTEXT or
        Keyword.TINYBLOB or Keyword.BLOB or Keyword.MEDIUMBLOB or Keyword.LONGBLOB or
        Keyword.BINARY or Keyword.VARBINARY or
        Keyword.GEOMETRY or Keyword.POINT or Keyword.CURVE or Keyword.LINESTRING or
        Keyword.LINE or Keyword.LINEARRING or Keyword.SURFACE or Keyword.POLYGON or
        Keyword.GEOMETRYCOLLECTION or Keyword.MULTIPOINT or Keyword.MULTICURVE or
        Keyword.MULTILINESTRING or Keyword.MULTISURFACE or Keyword.MULTIPOLYGON or
        Keyword.NATIONAL
        || base.IsDataTypeKeyword(keyword);

    public override DataType ParseDataType(ParserState state)
    {
        Token token = state.Next();
        Keyword? keyword = (token as Word)?.Keyword;

        DataType? dataType = keyword switch
        {
            Keyword.MEDIUMINT => ParseMediumInt(state),
            Keyword.SMALLINT => ParseSmallInt(state),
            Keyword.DATETIME => ParseDateTime(state),
            Keyword.TIMESTAMP => ParseTimestamp(state),
            Keyword.TIME => ParseTime(state),
            Keyword.DATE => new MyDataType.Date(),
            Keyword.YEAR => ParseYear(state),
            Keyword.NATIONAL => state.ParseKeyword(Keyword.VARCHAR) ? ParseNationalVarchar(state) : null,
            Keyword.TINYTEXT => ParseTinyText(state),
            Keyword.MEDIUMTEXT => ParseMediumText(state),
            Keyword.LONGTEXT => ParseLongText(state),
            Keyword.TINYBLOB => new MyDataType.TinyBlob(),
            Keyword.BLOB => new MyDataType.Blob(),
            Keyword.MEDIUMBLOB => new MyDataType.MediumBlob(),
            Keyword.LONGBLOB => new MyDataType.LongBlob(),
            Keyword.BINARY => ParseBinary(state),
            Keyword.VARBINARY => ParseVarbinary(state),
            Keyword.GEOMETRY => new MyDataType.Geometry(),
            Keyword.POINT => new MyDataType.Geometry.Point(),
            Keyword.CURVE => new MyDataType.Geometry.Curve(),
            Keyword.LINESTRING => new MyDataType.Geometry.Curve.LineString(),
            Keyword.LINE => new MyDataType.Geometry.Curve.LineString.Line(),
            Keyword.LINEARRING => new MyDataType.Geometry.Curve.LineString.LinearRing(),
            Keyword.SURFACE => new MyDataType.Geometry.Surface(),
            Keyword.POLYGON => new MyDataType.Geometry.Surface.Polygon(),
            Keyword.GEOMETRYCOLLECTION => new MyDataType.Geometry.GeometryCollection(),
            Keyword.MULTIPOINT => new MyDataType.Geometry.GeometryCollection.MultiPoint(),
            Keyword.MULTICURVE => new MyDataType.Geometry.GeometryCollection.MultiCurve(),
            Keyword.MULTILINESTRING => new MyDataType.Geometry.GeometryCollection.MultiCurve.MultiLineString(),
            Keyword.MULTISURFACE => new MyDataType.Geometry.GeometryCollection.MultiSurface(),
            Keyword.MULTIPOLYGON => new MyDataType.Geometry.GeometryCollection.MultiSurface.MultiPolygon(),
            _ => null
        };

        if (dataType != null)
        {
            return dataType;
        }

        state.Rewind();
        return base.ParseDataType(state);
    }

    public override DataType ParseDataTypeForConvert(ParserState state)
    {
        Keyword? keyword = (state.Peek() as Word)?.Keyword;

        if (keyword == Keyword.VARCHAR)
        {
            state.Next();
            return ParseVarcharOptionalLength(state);
        }
        if (keyword == Keyword.CHAR)
        {
            state.Next();
            return ParseCharOptionalLength(state);
        }
        if (keyword == Keyword.NATIONAL && state.PeekNth(1) is Word { Keyword: Keyword.VARCHAR })
        {
            state.Next();
            state.Next();
            return ParseNationalVarcharOptionalLength(state);
        }

        return ParseDataType(state);
    }
}
