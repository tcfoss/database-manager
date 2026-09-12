using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.MsSql.Parsing;

public class MsDmlParser(Parser parser) : DmlParser(parser)
{
    protected override SqlValueList<Assignment>? ParseOptionalOnDuplicateKeyUpdate(ParserState state)
    {
        return null;
    }

    protected override SqlValueList<SimpleSelectItem>? ParseOptionalReturningClause(ParserState state)
    {
        return null;
    }
}
