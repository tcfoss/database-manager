using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Parsing;

namespace TcfOss.DatabaseManager.Postgres.Parsing;

public class PgPrecedenceManager : PrecedenceManager
{
    public override short GetPrecedence(Precedence precedence)
    {
        return precedence switch
        {
            Precedence.DoubleColon => 70,
            Precedence.MultiplyDivide => 35,
            Precedence.BitwiseAnd or Precedence.BitwiseOr or Precedence.BitwiseXor
                or Precedence.StringConcat or Precedence.PgOperator => 21,
            _ => base.GetPrecedence(precedence)
        };
    }

    public override Binding GetBinding(Precedence precedence)
    {
        return new Binding(GetPrecedence(precedence), precedence is Precedence.Between or Precedence.Like or Precedence.Comparison or Precedence.Is);
    }

    public override Binding GetCompletedBinding(Expression expression, Binding binding)
    {
        return expression is Like { Escape: not null } or ILike { Escape: not null }
            or SimilarTo { Escape: not null } ? GetBinding(Precedence.Escape) : binding;
    }

    protected override Precedence? GetOperatorPrecedence(ParserState state)
    {
        Token token = state.Peek();
        if (state.PeekNth(1) is Word { Keyword: Keyword.ANY or Keyword.ALL or Keyword.SOME }
            && token is Plus or Minus or Asterisk or Slash or Modulo or Caret
                or Equal or NotEqual or LessThan or LessThanOrEqual or GreaterThan or GreaterThanOrEqual
                or Word { Keyword: Keyword.LIKE or Keyword.ILIKE })
        {
            return state.Context.HasFlag(ParseContext.BetweenBound) ? null : Precedence.PgOperator;
        }
        return token switch
        {
            Word { Keyword: Keyword.XOR or Keyword.DIV or Keyword.REGEXP or Keyword.RLIKE } => null,
            Caret => Precedence.Exponentiation,
            _ => base.GetOperatorPrecedence(state)
        };
    }

    protected override Precedence? GetNotPrecedence(ParserState state)
    {
        if (state.PeekNth(1) is Word { Keyword: Keyword.LIKE or Keyword.ILIKE }
            && state.PeekNth(2) is Word { Keyword: Keyword.ANY or Keyword.ALL or Keyword.SOME })
        {
            return Precedence.PgOperator;
        }
        return state.PeekNth(1) is Word { Keyword: Keyword.REGEXP or Keyword.RLIKE }
            ? null : base.GetNotPrecedence(state);
    }
}
