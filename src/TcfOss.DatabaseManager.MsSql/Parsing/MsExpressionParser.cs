using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Parsing;

namespace TcfOss.DatabaseManager.MsSql.Parsing;

public class MsExpressionParser(MsParser parser) : ExpressionParser(parser)
{
    protected override Expression ParseQuantifiedOperand(ParserState state, short precedence)
    {
        return ParseLegacyQuantifiedOperand(state, precedence);
    }

    protected override bool SupportsQuantifiedOperator(Core.BuiltIn.BinaryOperator op)
    {
        return IsComparisonOperator(op);
    }
}
