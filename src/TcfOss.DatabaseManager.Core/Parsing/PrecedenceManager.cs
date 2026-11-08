using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;

namespace TcfOss.DatabaseManager.Core.Parsing;

#pragma warning disable CA1822 // Mark members as static

public class PrecedenceManager
{
    public readonly record struct Binding(short Precedence, bool IsNonAssociative = false);

    protected virtual bool SupportsPostgresPredicateSyntax => true;

    public virtual short GetPrecedence(Precedence precedence)
    {
        return precedence switch
        {
            Precedence.ArraySubscript => 60,
            Precedence.DoubleColon => 50,
            Precedence.UnarySign => 45,
            Precedence.Collate => 43,
            Precedence.AtTimezone => 41,
            Precedence.MultiplyDivide => 40,
            Precedence.AddSubtract => 30,
            Precedence.LogicalXor => 24,
            Precedence.BitwiseAnd => 23,
            Precedence.BitwiseXor => 22,
            Precedence.Exponentiation => 40,
            Precedence.BitwiseOr => 21,
            Precedence.StringConcat => 20,
            Precedence.PgOperator => 20,
            Precedence.Between => 19,
            Precedence.Comparison => 18,
            Precedence.Like => 19,
            Precedence.Escape => 20,
            Precedence.Is => 17,
            Precedence.UnaryNot => 15,
            Precedence.LogicalAnd => 10,
            Precedence.LogicalOr => 5,
            _ => 0
        };
    }

    public short GetNextPrecedence(ParserState state)
    {
        return GetNextBinding(state).Precedence;
    }

    public virtual Binding GetBinding(Precedence precedence)
    {
        return new Binding(GetPrecedence(precedence));
    }

    public virtual Binding GetCompletedBinding(Expression expression, Binding binding)
    {
        return binding;
    }

    public Binding GetNextBinding(ParserState state)
    {
        Token token = state.Peek();
        if (state.Context.HasFlag(ParseContext.BetweenBound) && token is Word word)
        {
            if (word.Keyword is Keyword.AND or Keyword.OR or Keyword.NOT or Keyword.IN or Keyword.BETWEEN
                or Keyword.LIKE or Keyword.ILIKE or Keyword.SIMILAR or Keyword.COLLATE or Keyword.AT
                or Keyword.ISNULL or Keyword.NOTNULL)
            {
                return default;
            }
            if (word.Keyword == Keyword.IS
                && !state.PeekKeywordsAllEqual(Keyword.IS, Keyword.DISTINCT, Keyword.FROM)
                && !state.PeekKeywordsAllEqual(Keyword.IS, Keyword.NOT, Keyword.DISTINCT, Keyword.FROM))
            {
                return default;
            }
        }

        Precedence? precedence = GetOperatorPrecedence(state);
        return precedence == null ? default : GetBinding(precedence.Value);
    }

    protected virtual Precedence? GetOperatorPrecedence(ParserState state)
    {
        return state.Peek() switch
        {
            Word { Keyword: Keyword.OR } => Precedence.LogicalOr,
            Word { Keyword: Keyword.AND } => Precedence.LogicalAnd,
            Word { Keyword: Keyword.XOR } => Precedence.LogicalXor,
            Word { Keyword: Keyword.AT } => GetAtPrecedence(state),
            Word { Keyword: Keyword.COLLATE } => Precedence.Collate,
            Word { Keyword: Keyword.NOT } => GetNotPrecedence(state),
            Word { Keyword: Keyword.IS } => Precedence.Is,
            Word { Keyword: Keyword.ISNULL or Keyword.NOTNULL } when SupportsPostgresPredicateSyntax => Precedence.Is,
            Word { Keyword: Keyword.IN or Keyword.BETWEEN } => Precedence.Between,
            Word { Keyword: Keyword.OPERATOR } or OperatorToken => Precedence.PgOperator,
            Word { Keyword: Keyword.LIKE or Keyword.ILIKE or Keyword.REGEXP or Keyword.RLIKE } => Precedence.Like,
            Word { Keyword: Keyword.SIMILAR } when state.PeekNth(1) is Word { Keyword: Keyword.TO } => Precedence.Like,
            Word { Keyword: Keyword.DIV } => Precedence.MultiplyDivide,
            BracketOpen => Precedence.ArraySubscript,
            DoubleColon => Precedence.DoubleColon,
            DoublePipe => Precedence.StringConcat,
            Pipe => Precedence.BitwiseOr,
            Caret => Precedence.BitwiseXor,
            Ampersand => Precedence.BitwiseAnd,
            Tilde or TildeAsterisk or NotTilde or NotTildeAsterisk => Precedence.PgOperator,
            Equal
                or LessThan
                or LessThanOrEqual
                or GreaterThan
                or GreaterThanOrEqual
                or NotEqual
                => Precedence.Comparison,
            Plus or Minus => Precedence.AddSubtract,
            Asterisk or Slash or Modulo => Precedence.MultiplyDivide,
            _ => null
        };
    }

    private Precedence? GetAtPrecedence(ParserState state)
    {
        if (SupportsPostgresPredicateSyntax && state.PeekKeywordsAllEqual(Keyword.AT, Keyword.LOCAL))
        {
            return Precedence.AtTimezone;
        }
        if (state.PeekKeywordsAllEqual(Keyword.AT, Keyword.TIME, Keyword.ZONE))
        {
            return Precedence.AtTimezone;
        }
        return null;
    }

    protected virtual Precedence? GetNotPrecedence(ParserState state)
    {
        return state.PeekNth(1) switch
        {
            Word { Keyword: Keyword.IN or Keyword.BETWEEN } => Precedence.Between,
            Word { Keyword: Keyword.LIKE or Keyword.ILIKE or Keyword.REGEXP or Keyword.RLIKE } => Precedence.Like,
            Word { Keyword: Keyword.SIMILAR } when state.PeekNth(2) is Word { Keyword: Keyword.TO } => Precedence.Like,
            _ => null
        };
    }
}
