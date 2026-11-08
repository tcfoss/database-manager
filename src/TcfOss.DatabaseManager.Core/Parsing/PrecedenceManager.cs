using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;

namespace TcfOss.DatabaseManager.Core.Parsing;

#pragma warning disable CA1822 // Mark members as static

public class PrecedenceManager(bool treatDoublePipeAsOr = false, bool usePostgresUnaryPrecedence = true, bool usePostgresOperatorPrecedence = false, bool supportPostgresPredicateSyntax = true)
{
    public bool UsePostgresOperatorPrecedence => usePostgresOperatorPrecedence;

    public short GetPrecedence(Precedence precedence)
    {
        return precedence switch
        {
            Precedence.ArraySubscript => 60,
            Precedence.DoubleColon => usePostgresOperatorPrecedence ? (short)70 : (short)50,
            Precedence.UnarySign => usePostgresUnaryPrecedence ? (short)45 : (short)40,
            Precedence.Collate => 43,
            Precedence.AtTimezone => 41,
            Precedence.MultiplyDivide => usePostgresOperatorPrecedence ? (short)35 : (short)40,
            Precedence.AddSubtract => 30,
            Precedence.Xor => 24,
            Precedence.Ampersand => usePostgresOperatorPrecedence ? (short)21 : (short)23,
            Precedence.Caret => usePostgresOperatorPrecedence ? (short)40 : (short)22,
            Precedence.Pipe => 21,
            Precedence.PgConcat => usePostgresOperatorPrecedence ? (short)21 : (short)20,
            Precedence.PgOperator => usePostgresOperatorPrecedence ? (short)21 : (short)20,
            Precedence.Between => 19,
            Precedence.Equals => usePostgresOperatorPrecedence ? (short)18 : (short)19,
            Precedence.Like => usePostgresOperatorPrecedence ? (short)19 : (short)18,
            Precedence.Escape => 20,
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
        if (usePostgresOperatorPrecedence
            && state.PeekNth(1) is Word { Keyword: Keyword.ANY or Keyword.ALL or Keyword.SOME }
            && token is Plus or Minus or Asterisk or Slash or Modulo or Caret
                or Equal or NotEqual or LessThan or LessThanOrEqual or GreaterThan or GreaterThanOrEqual
                or Word { Keyword: Keyword.LIKE or Keyword.ILIKE })
        {
            return state.Context.HasFlag(ParseContext.BetweenBound) ? (short)0 : GetPrecedence(Precedence.PgOperator);
        }
        if (state.Context.HasFlag(ParseContext.BetweenBound) && token is Word word)
        {
            if (word.Keyword is Keyword.AND or Keyword.OR or Keyword.NOT or Keyword.IN or Keyword.BETWEEN
                or Keyword.LIKE or Keyword.ILIKE or Keyword.SIMILAR or Keyword.COLLATE or Keyword.AT
                or Keyword.ISNULL or Keyword.NOTNULL)
            {
                return 0;
            }
            if (word.Keyword == Keyword.IS
                && !state.PeekKeywordsAllEqual(Keyword.IS, Keyword.DISTINCT, Keyword.FROM)
                && !state.PeekKeywordsAllEqual(Keyword.IS, Keyword.NOT, Keyword.DISTINCT, Keyword.FROM))
            {
                return 0;
            }
        }

        return token switch
        {
            Word { Keyword: Keyword.OR } => GetPrecedence(Precedence.Or),
            Word { Keyword: Keyword.AND } => GetPrecedence(Precedence.And),
            Word { Keyword: Keyword.XOR } when !usePostgresOperatorPrecedence => GetPrecedence(Precedence.Xor),
            Word { Keyword: Keyword.AT } => GetAtPrecedence(state),
            Word { Keyword: Keyword.COLLATE } => GetPrecedence(Precedence.Collate),
            Word { Keyword: Keyword.NOT } => GetNotPrecedence(state),
            Word { Keyword: Keyword.IS } => GetPrecedence(Precedence.Is),
            Word { Keyword: Keyword.ISNULL or Keyword.NOTNULL } when supportPostgresPredicateSyntax => GetPrecedence(Precedence.Is),
            Word { Keyword: Keyword.IN or Keyword.BETWEEN } => GetPrecedence(Precedence.Between),
            Word { Keyword: Keyword.OPERATOR } or OperatorToken => GetPrecedence(Precedence.PgOperator),
            Word { Keyword: Keyword.LIKE or Keyword.ILIKE } => GetPrecedence(Precedence.Like),
            Word { Keyword: Keyword.REGEXP or Keyword.RLIKE } when !usePostgresOperatorPrecedence => GetPrecedence(Precedence.Like),
            Word { Keyword: Keyword.SIMILAR } when state.PeekNth(1) is Word { Keyword: Keyword.TO } => GetPrecedence(Precedence.Like),
            Word { Keyword: Keyword.DIV } when !usePostgresOperatorPrecedence => GetPrecedence(Precedence.MultiplyDivide),
            BracketOpen => GetPrecedence(Precedence.ArraySubscript),
            DoubleColon => GetPrecedence(Precedence.DoubleColon),
            DoublePipe => GetPrecedence(treatDoublePipeAsOr ? Precedence.Or : Precedence.PgConcat),
            Pipe => GetPrecedence(Precedence.Pipe),
            Caret => GetPrecedence(Precedence.Caret),
            Ampersand => GetPrecedence(Precedence.Ampersand),
            Tilde or TildeAsterisk or NotTilde or NotTildeAsterisk => GetPrecedence(Precedence.PgConcat),
            Equal
                or LessThan
                or LessThanOrEqual
                or GreaterThan
                or GreaterThanOrEqual
                or NotEqual
                => GetPrecedence(Precedence.Equals),
            Plus or Minus => GetPrecedence(Precedence.AddSubtract),
            Asterisk or Slash or Modulo => GetPrecedence(Precedence.MultiplyDivide),
            _ => 0
        };
    }

    public bool IsNonAssociative(short precedence)
    {
        return usePostgresOperatorPrecedence
            && (precedence == GetPrecedence(Precedence.Between)
                || precedence == GetPrecedence(Precedence.Equals)
                || precedence == GetPrecedence(Precedence.Is));
    }

    private short GetAtPrecedence(ParserState state)
    {
        if (supportPostgresPredicateSyntax && state.PeekKeywordsAllEqual(Keyword.AT, Keyword.LOCAL))
        {
            return GetPrecedence(Precedence.AtTimezone);
        }
        if (state.PeekKeywordsAllEqual(Keyword.AT, Keyword.TIME, Keyword.ZONE))
        {
            return GetPrecedence(Precedence.AtTimezone);
        }
        return 0;
    }

    private short GetNotPrecedence(ParserState state)
    {
        if (usePostgresOperatorPrecedence
            && state.PeekNth(1) is Word { Keyword: Keyword.LIKE or Keyword.ILIKE }
            && state.PeekNth(2) is Word { Keyword: Keyword.ANY or Keyword.ALL or Keyword.SOME })
        {
            return GetPrecedence(Precedence.PgOperator);
        }
        return state.PeekNth(1) switch
        {
            Word { Keyword: Keyword.IN or Keyword.BETWEEN } => GetPrecedence(Precedence.Between),
            Word { Keyword: Keyword.LIKE or Keyword.ILIKE } => GetPrecedence(Precedence.Like),
            Word { Keyword: Keyword.REGEXP or Keyword.RLIKE } when !usePostgresOperatorPrecedence => GetPrecedence(Precedence.Like),
            Word { Keyword: Keyword.SIMILAR } when state.PeekNth(2) is Word { Keyword: Keyword.TO } => GetPrecedence(Precedence.Like),
            _ => 0
        };
    }
}
