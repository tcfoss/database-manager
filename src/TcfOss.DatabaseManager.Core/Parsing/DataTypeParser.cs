using System.Globalization;
using System.Numerics;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;

namespace TcfOss.DatabaseManager.Core.Parsing;

public class DataTypeParser
{
    protected static int ParseBoundedInt(ParserState state, int min = 0, int max = int.MaxValue, string label = "precision")
        => ParseBoundedInteger(state, ValueParser.ParseLiteralInt, min, max, label);

    protected static uint ParseBoundedUInt(ParserState state, uint min = 0, uint max = uint.MaxValue, string label = "precision")
        => ParseBoundedInteger(state, ValueParser.ParseLiteralUInt, min, max, label);

    protected static long ParseBoundedLong(ParserState state, long min = 0, long max = long.MaxValue, string label = "precision")
        => ParseBoundedInteger(state, ValueParser.ParseLiteralLong, min, max, label);

    private static T ParseBoundedInteger<T>(ParserState state, Func<ParserState, T> parse, T min, T max, string label)
        where T : struct, IBinaryInteger<T>
    {
        Token first = state.Peek();
        T value;
        try
        {
            value = parse(state);
        }
        catch (ParseException.ValueOutOfRange exception)
        {
            throw new ParseException.ValueOutOfRange(label, exception.Value, long.CreateChecked(min), long.CreateChecked(max), first.Location);
        }
        if (value < min || value > max)
        {
            throw new ParseException.ValueOutOfRange(label, value.ToString(null, CultureInfo.InvariantCulture), long.CreateChecked(min), long.CreateChecked(max), first.Location);
        }
        return value;
    }

    protected static uint? ParseOptionalSimplePrecision(ParserState state, uint min = 0, uint max = uint.MaxValue, string label = "precision")
    {
        if (!state.ConsumeTokenIs<ParenOpen>())
        {
            return null;
        }

        uint precision = ParseBoundedUInt(state, min, max, label);
        state.ExpectRightParen();
        return precision;
    }

    protected static uint ParseSimplePrecision(ParserState state, uint min = 0, uint max = uint.MaxValue, string label = "precision")
    {
        state.ExpectLeftParen();
        uint value = ParseBoundedUInt(state, min, max, label);
        state.ExpectRightParen();
        return value;
    }

    protected static uint? ParseOptionalLength(ParserState state, uint min = 0, uint max = uint.MaxValue)
        => ParseOptionalSimplePrecision(state, min, max, "length");

    protected static uint ParseLength(ParserState state, uint min = 0, uint max = uint.MaxValue)
        => ParseSimplePrecision(state, min, max, "length");

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

    protected virtual DataType ParseCharacter(ParserState state)
    {
        return state.ParseKeyword(Keyword.VARYING) ? ParseVarchar(state) : ParseChar(state);
    }

    protected virtual DataType ParseChar(ParserState state)
    {
        uint? length = ParseOptionalLength(state);
        return new DataType.Char(length);
    }

    protected virtual DataType ParseVarchar(ParserState state)
    {
        uint? length = ParseOptionalLength(state);
        return new DataType.Varchar(length);
    }

    protected virtual DataType ParseNationalChar(ParserState state)
    {
        uint? length = ParseOptionalLength(state);
        return new DataType.NationalChar(length);
    }

    protected virtual DataType ParseNationalVarchar(ParserState state)
    {
        uint? length = ParseOptionalLength(state);
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
            Keyword.NCHAR => ParseNationalChar(state),
            Keyword.CHARACTER => ParseCharacter(state),
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
        Keyword.CHAR or Keyword.CHARACTER or Keyword.NCHAR or Keyword.VARCHAR or Keyword.NVARCHAR or
        Keyword.TEXT or Keyword.DOUBLE or Keyword.FLOAT or
        Keyword.ENUM or Keyword.SET;

    public virtual bool IsDataTypeStart(Token token) => token is Word word && IsDataTypeKeyword(word.Keyword);

    public virtual DataType ParseDataTypeForConvert(ParserState state)
    {
        return ParseDataType(state);
    }
}
