using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.MsSql.Parsing;

public class MsComponentParser(MsParser parser) : ComponentParser(parser)
{
    public override Limit? ParseOptionalSelectLimit(ParserState state)
    {
        if (state.PeekKeyword(Keyword.LIMIT))
        {
            return ParseLimit(state);
        }
        if (state.PeekKeyword(Keyword.OFFSET))
        {
            return ParseOffsetFetch(state);
        }
        return null;
    }

    public override RowLockingClause? ParseOptionalRowLock(ParserState state)
    {
        return null;
    }

    protected override NullsOrder? ParseOptionalNullsOrder(ParserState state)
    {
        return null;
    }

    public override CreateOrLabel? ParseCreateOrLabel(ParserState state)
    {
        if (state.ParseKeywordsAll(Keyword.OR, Keyword.ALTER))
        {
            return CreateOrLabel.OrAlter;
        }
        return null;
    }
}
