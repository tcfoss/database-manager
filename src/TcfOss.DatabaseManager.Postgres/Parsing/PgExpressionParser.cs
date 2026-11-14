using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Parsing;

namespace TcfOss.DatabaseManager.Postgres.Parsing;

public class PgExpressionParser(PgParser parser) : ExpressionParser(parser)
{
    private readonly PrecedenceManager _pm = parser.PrecedenceManager;

    protected override Core.Expressions.Attributes.FunctionArgumentOperator? PeekOptionalFunctionArgumentOperator(ParserState state)
    {
        return state.PeekNth(1) is Equal ? null : base.PeekOptionalFunctionArgumentOperator(state);
    }

    protected override bool IsGenericOperatorToken(Token token)
    {
        return token is Caret || base.IsGenericOperatorToken(token);
    }

    protected override BinaryOperator? GetBinaryOperator(ParserState state, Token token)
    {
        return token is Caret ? null : base.GetBinaryOperator(state, token);
    }
}
