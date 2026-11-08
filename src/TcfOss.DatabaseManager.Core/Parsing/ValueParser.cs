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
            EscapeStringLiteral sl => ParseEscapeString(sl),
            UnicodeStringLiteral sl => ParseUnicodeString(sl),
            DollarQuotedStringLiteral sl => new Value.DollarQuotedString(sl.Value) { Tag = sl.Tag },
            NationalStringLiteral nsl => new Value.NationalStringLiteral(nsl.Value),
            HexStringLiteral hsl => new Value.HexString(hsl.Value),
            BitStringLiteral bit => ParseBitString(bit),
            _ => throw ParserState.ExpectedCategoryException("literal_value", token),
        };
    }

    public static string ParseLiteralString(ParserState state)
    {
        Token token = state.Next();

        return token switch
        {
            StringLiteral s => s.Value,
            EscapeStringLiteral s => ParseEscapeString(s).DecodedValue,
            UnicodeStringLiteral s => ParseUnicodeString(s).DecodedValue,
            DollarQuotedStringLiteral s => s.Value,
            _ => throw ParserState.ExpectedCategoryException("literal_string", token)
        };
    }

    public static string ParseLiteralStringOptionalQuotes(ParserState state)
    {
        Token token = state.Next();

        return token switch
        {
            Word { QuoteStyle: QuoteStyle.None, Sigil: SigilKind.None } word => word.Value,
            StringLiteral s => s.Value,
            EscapeStringLiteral s => ParseEscapeString(s).DecodedValue,
            UnicodeStringLiteral s => ParseUnicodeString(s).DecodedValue,
            DollarQuotedStringLiteral s => s.Value,
            _ => throw ParserState.ExpectedCategoryException("literal_string", token)
        };
    }

    private static Value.EscapeString ParseEscapeString(EscapeStringLiteral token)
    {
        var value = new Value.EscapeString(token.Value);
        try
        {
            _ = value.DecodedValue;
        }
        catch (FormatException)
        {
            throw ParserState.ExpectedCategoryException("escape_string", token);
        }
        return value;
    }

    private static Value.UnicodeString ParseUnicodeString(UnicodeStringLiteral token)
    {
        var value = new Value.UnicodeString(token.Value) { EscapeCharacter = token.EscapeCharacter };
        try
        {
            _ = value.DecodedValue;
        }
        catch (FormatException)
        {
            throw ParserState.ExpectedCategoryException("unicode_string", token);
        }
        return value;
    }

    private static Value.BitString ParseBitString(BitStringLiteral token)
    {
        foreach (char digit in token.Value)
        {
            if (token.IsHexadecimal ? !char.IsAsciiHexDigit(digit) : digit is not ('0' or '1'))
            {
                throw ParserState.ExpectedCategoryException("bit_string", token);
            }
        }
        return new Value.BitString(token.Value, token.IsHexadecimal);
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
        string text = number.GetDecimalText();
        return (sign.Length == 0 ? text : string.Concat(sign, text), start);
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
        string text = number.GetDecimalText();
        if (uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint value))
        {
            return value;
        }
        throw IntegerParseFailure(text, token, "literal_int", uint.MinValue, uint.MaxValue);
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
        if (token is NumericLiteral number && ulong.TryParse(number.GetDecimalText(), NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong value))
        {
            return value;
        }
        throw ParserState.ExpectedCategoryException("literal_long", token);
    }

    private static ParseException IntegerParseFailure(string text, Token token, string category, long minimum, long maximum)
    {
        return BigInteger.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            ? new ParseException.ValueOutOfRange("integer", text, minimum, maximum, token.Location)
            : ParserState.ExpectedCategoryException(category, token);
    }
}
