using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.MsSql.Parsing;

public class MsComponentParser(MsParser parser) : ComponentParser(parser)
{
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
