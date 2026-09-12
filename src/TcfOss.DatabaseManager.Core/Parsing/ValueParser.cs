using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
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

    public static uint ParseLiteralUInt(ParserState state)
    {
        Token token = state.Next();

        if (token is NumericLiteral n && uint.TryParse(n.Value, out uint value))
        {
            return value;
        }
        throw ParserState.ExpectedCategoryException("literal_int", token);
    }

    public static ulong ParseLiteralULong(ParserState state)
    {
        Token token = state.Next();

        if (token is NumericLiteral n && ulong.TryParse(n.Value, out ulong value))
        {
            return value;
        }
        throw ParserState.ExpectedCategoryException("literal_long", token);
    }
}
