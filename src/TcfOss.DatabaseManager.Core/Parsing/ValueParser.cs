using System.Globalization;
using System.Numerics;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Errors;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;

namespace TcfOss.DatabaseManager.Core.Parsing;

public class ValueParser
{
    public static Value ParseValue(ParserState state)
    {
        Token token = state.Next();

        return token switch
        {
            Word w => w.Keyword switch
            {
                Keyword.TRUE => new Value.Boolean(true),
                Keyword.FALSE => new Value.Boolean(false),
                Keyword.NULL => new Value.Null(),
                _ => throw ParserState.ExpectedCategoryException("literal_value", token)
            },
            NumericLiteral n => new Value.Number(n.Value, n.IsLong),
            StringLiteral sl => new Value.SingleQuotedString(sl.Value),
            NationalStringLiteral nsl => new Value.NationalStringLiteral(nsl.Value),
            HexStringLiteral hsl => new Value.HexString(hsl.Value),
            _ => throw ParserState.ExpectedCategoryException("literal_value", token),
        };
    }

    public static string ParseLiteralString(ParserState state)
    {
        Token token = state.Next();

        return token switch
        {
            Word { Keyword: Keyword.undefined } word => word.Value,
            StringLiteral s => s.Value,
            _ => throw ParserState.ExpectedCategoryException("literal_string", token)
        };
    }

    private static (string Text, Token Start) ParseIntegerText(ParserState state, string category)
    {
        Token token = state.Next();
        Token start = token;
        string sign = "";
        if (token is Plus or Minus)
        {
            sign = token.ToString()!;
            token = state.Next();
        }
        if (token is not NumericLiteral number)
        {
            throw ParserState.ExpectedCategoryException(category, token);
        }
        return (sign.Length == 0 ? number.Value : string.Concat(sign, number.Value), start);
    }

    private static ParseException IntegerParseFailure(string text, Token token, string category, long minimum, long maximum)
    {
        return BigInteger.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            ? new ParseException.ValueOutOfRange("integer", text, minimum, maximum, token.Location)
            : ParserState.ExpectedCategoryException(category, token);
    }

    public static int ParseLiteralInt(ParserState state)
    {
        (string text, Token token) = ParseIntegerText(state, "literal_int");
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
        {
            return value;
        }
        throw IntegerParseFailure(text, token, "literal_int", int.MinValue, int.MaxValue);
    }

    public static uint ParseLiteralUInt(ParserState state)
    {
        Token token = state.Next();
        if (token is not NumericLiteral number)
        {
            throw ParserState.ExpectedCategoryException("literal_int", token);
        }
        if (uint.TryParse(number.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint value))
        {
            return value;
        }
        throw IntegerParseFailure(number.Value, token, "literal_int", uint.MinValue, uint.MaxValue);
    }

    public static long ParseLiteralLong(ParserState state)
    {
        (string text, Token token) = ParseIntegerText(state, "literal_long");
        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value))
        {
            return value;
        }
        throw IntegerParseFailure(text, token, "literal_long", long.MinValue, long.MaxValue);
    }

    public static ulong ParseLiteralULong(ParserState state)
    {
        Token token = state.Next();
        if (token is NumericLiteral number && ulong.TryParse(number.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong value))
        {
            return value;
        }
        throw ParserState.ExpectedCategoryException("literal_long", token);
    }
}
