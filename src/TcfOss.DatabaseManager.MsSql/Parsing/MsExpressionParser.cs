using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Parsing;

namespace TcfOss.DatabaseManager.MsSql.Parsing;

public class MsExpressionParser(MsParser parser) : ExpressionParser(parser)
{
    protected override WindowFrameUnit? ParseOptionalWindowFrameUnit(ParserState state)
    {
        return state.PeekKeyword(Keyword.GROUPS) ? null : base.ParseOptionalWindowFrameUnit(state);
    }

    protected override WindowFrameExclusion? ParseOptionalWindowExclusion(ParserState state)
    {
        return null;
    }

    protected override Expression? ParseOptionalFilter(ParserState state)
    {
        return null;
    }

    protected override Expression ParseQuantifiedOperand(ParserState state, short precedence)
    {
        return ParseLegacyQuantifiedOperand(state, precedence);
    }

    protected override bool SupportsQuantifiedOperator(Core.BuiltIn.BinaryOperator op)
    {
        return IsComparisonOperator(op);
    }

    protected override Core.Expressions.Components.FunctionArgument? ParseOptionalVariadicArgument(ParserState state)
    {
        return null;
    }
}
