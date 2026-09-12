using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;

namespace TcfOss.DatabaseManager.Core.Parsing;

public class DataTypeParser
{
    protected static uint? ParseOptionalSimplePrecision(ParserState state)
    {
        if (!state.ConsumeTokenIs<ParenOpen>())
        {
            return null;
        }

        uint precision = ValueParser.ParseLiteralUInt(state);
        state.ExpectRightParen();
        return precision;
    }

    protected static uint ParseSimplePrecision(ParserState state)
    {
        state.ExpectLeftParen();
        uint value = ValueParser.ParseLiteralUInt(state);
        state.ExpectRightParen();
        return value;
    }

    protected static NumericLength? ParseOptionalNumericLength(ParserState state)
    {
        if (!state.ConsumeTokenIs<ParenOpen>())
        {
            return null;
        }
        uint precision = ValueParser.ParseLiteralUInt(state);

        if (!state.ConsumeTokenIs<Comma>())
        {
            return new NumericLength.Precision(precision);
        }

        uint scale = ValueParser.ParseLiteralUInt(state);
        state.ExpectRightParen();

        return new NumericLength.PrecisionScale(precision, scale);
    }

    protected static CharLength? ParseOptionalCharLength(ParserState state)
    {
        if (!state.ConsumeTokenIs<ParenOpen>())
        {
            return null;
        }
        if (state.ParseKeyword(Keyword.MAX))
        {
            var charLength = new CharLength.Max();
            state.ExpectRightParen();
            return charLength;
        }

        uint length = ValueParser.ParseLiteralUInt(state);
        state.ExpectRightParen();
        return new CharLength.Specified(length);
    }

    protected virtual DataType ParseBoolean()
    {
        return new DataType.Boolean();
    }

    protected virtual DataType ParseTinyInt(ParserState state)
    {
        return new DataType.TinyInt();
    }

    protected virtual DataType ParseInteger(ParserState state)
    {
        return new DataType.Int();
    }

    protected virtual DataType ParseBigInt(ParserState state)
    {
        return new DataType.BigInt();
    }

    protected virtual DataType ParseDecimal(ParserState state)
    {
        NumericLength? numLength = ParseOptionalNumericLength(state);
        return new DataType.Decimal(numLength);
    }

    protected virtual DataType ParseFloat(ParserState state)
    {
        NumericLength? numLength = ParseOptionalNumericLength(state);
        return new DataType.Float(numLength);
    }

    protected virtual DataType ParseDouble(ParserState state)
    {
        NumericLength? numLength = ParseOptionalNumericLength(state);
        return new DataType.Double(numLength);
    }

    protected virtual DataType ParseChar(ParserState state)
    {
        CharLength length = ParseOptionalCharLength(state) ?? throw state.ExpectedCategoryException("data_type_length");
        return new DataType.Char(length);
    }

    protected virtual DataType ParseVarchar(ParserState state)
    {
        CharLength length = ParseOptionalCharLength(state) ?? throw state.ExpectedCategoryException("data_type_length");
        return new DataType.Varchar(length);
    }

    protected virtual DataType ParseNationalVarchar(ParserState state)
    {
        CharLength length = ParseOptionalCharLength(state) ?? throw state.ExpectedCategoryException("data_type_length");
        return new DataType.NationalVarchar(length);
    }

    protected virtual DataType ParseText(ParserState state)
    {
        return new DataType.Text();
    }

    private static DataType.Enum ParseEnum(ParserState state)
    {
        return new DataType.Enum(state.ParseParenthesizedCommaSeparated(ValueParser.ParseLiteralString, false));
    }

    private static DataType.Set ParseSet(ParserState state)
    {
        return new DataType.Set(state.ParseParenthesizedCommaSeparated(ValueParser.ParseLiteralString, false));
    }

    public virtual DataType ParseDataType(ParserState state)
    {
        Token token = state.Next();
        Word word = (token as Word) ?? throw ParserState.ExpectedException("data_type".Italic(), token);

        return word.Keyword switch
        {
            Keyword.BOOLEAN or Keyword.BOOL => ParseBoolean(),
            Keyword.INT or Keyword.INTEGER => ParseInteger(state),
            Keyword.TINYINT => ParseTinyInt(state),
            Keyword.BIGINT => ParseBigInt(state),
            Keyword.DECIMAL or Keyword.NUMERIC => ParseDecimal(state),
            Keyword.CHAR => ParseChar(state),
            Keyword.VARCHAR => ParseVarchar(state),
            Keyword.NVARCHAR => ParseNationalVarchar(state),
            Keyword.TEXT => ParseText(state),
            Keyword.DOUBLE => ParseDouble(state),
            Keyword.FLOAT => ParseFloat(state),
            Keyword.ENUM => ParseEnum(state),
            Keyword.SET => ParseSet(state),
            _ => throw ParserState.ExpectedException("data_type".Italic(), word),
        };
    }

    // Returns true for any keyword that can open a typed-string expression (TYPE 'literal').
    public virtual bool IsDataTypeKeyword(Keyword? keyword) => keyword is
        Keyword.BOOLEAN or Keyword.BOOL or
        Keyword.INT or Keyword.INTEGER or
        Keyword.TINYINT or Keyword.BIGINT or
        Keyword.DECIMAL or Keyword.NUMERIC or
        Keyword.CHAR or Keyword.VARCHAR or Keyword.NVARCHAR or
        Keyword.TEXT or Keyword.DOUBLE or Keyword.FLOAT or
        Keyword.ENUM or Keyword.SET;

    public virtual DataType ParseDataTypeForConvert(ParserState state)
    {
        return ParseDataType(state);
    }
}
