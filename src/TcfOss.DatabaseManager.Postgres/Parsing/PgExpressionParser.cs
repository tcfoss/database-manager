using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Postgres.Parsing;

public class PgExpressionParser(PgParser parser) : ExpressionParser(parser)
{
    private readonly PrecedenceManager _pm = parser.PrecedenceManager;

    protected override bool UsesRestrictedBetweenBounds => true;
    protected override bool SupportsLegacyPatternAny => false;

    protected override bool IsGenericOperatorToken(Token token)
    {
        return token is Caret || base.IsGenericOperatorToken(token);
    }

    protected override BinaryOperator? GetBinaryOperator(Token token)
    {
        return token is Caret ? null : base.GetBinaryOperator(token);
    }

    protected override PatternEscape ParsePatternEscape(ParserState state)
    {
        return new PatternEscape.Expression(ParseSubExpression(state, _pm.GetPrecedence(Precedence.Escape)));
    }
}
