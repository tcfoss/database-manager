using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;

namespace TcfOss.DatabaseManager.Core.Parsing;

#pragma warning disable CA1822 // Mark members as static

public class PrecedenceManager
{
    public short GetPrecedence(Precedence precedence)
    {
        return precedence switch
        {
            Precedence.DoubleColon => 50,
            Precedence.AtTimezone => 41,
            Precedence.MultiplyDivide => 40,
            Precedence.AddSubtract => 30,
            Precedence.Xor => 24,
            Precedence.Ampersand => 23,
            Precedence.Caret => 22,
            Precedence.Pipe => 21,
            Precedence.Between => 20,
            Precedence.Equals => 20,
            Precedence.Like => 19,
            Precedence.Is => 17,
            Precedence.UnaryNot => 15,
            Precedence.And => 10,
            Precedence.Or => 5,
            _ => 0
        };
    }

    public short GetNextPrecedence(ParserState state)
    {
        Token token = state.Peek();

        return token switch
        {
            Word { Keyword: Keyword.OR } => GetPrecedence(Precedence.Or),
            Word { Keyword: Keyword.AND } => GetPrecedence(Precedence.And),
            Word { Keyword: Keyword.XOR } => GetPrecedence(Precedence.Xor),
            Word { Keyword: Keyword.AT } => GetAtPrecedence(state),
            Word { Keyword: Keyword.NOT } => GetNotPrecedence(state),
            Word { Keyword: Keyword.IS } => GetPrecedence(Precedence.Is),
            Word { Keyword: Keyword.IN or Keyword.BETWEEN or Keyword.OPERATOR } => GetPrecedence(Precedence.Between),
            Word { Keyword: Keyword.LIKE or Keyword.ILIKE or Keyword.SIMILAR or Keyword.REGEXP or Keyword.RLIKE } => GetPrecedence(Precedence.Like),
            Word { Keyword: Keyword.DIV } => GetPrecedence(Precedence.MultiplyDivide),
            Equal
                or LessThan
                or LessThanOrEqual
                or GreaterThan
                or GreaterThanOrEqual
                or NotEqual
                => GetPrecedence(Precedence.Between),
            Plus or Minus => GetPrecedence(Precedence.AddSubtract),
            Asterisk or Slash or Modulo => GetPrecedence(Precedence.MultiplyDivide),
            _ => 0
        };
    }

    private short GetAtPrecedence(ParserState state)
    {
        if (state.PeekKeywordsAllEqual(Keyword.AT, Keyword.TIME, Keyword.ZONE))
        {
            return GetPrecedence(Precedence.AtTimezone);
        }
        return 0;
    }

    private short GetNotPrecedence(ParserState state)
    {
        return state.PeekNth(1) switch
        {
            Word { Keyword: Keyword.IN or Keyword.BETWEEN } => GetPrecedence(Precedence.Between),
            Word { Keyword: Keyword.LIKE or Keyword.ILIKE or Keyword.SIMILAR or Keyword.REGEXP or Keyword.RLIKE } => GetPrecedence(Precedence.Like),
            _ => 0
        };
    }
}
