using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Postgres.Parsing;

public class PgDmlParser(Parser parser) : DmlParser(parser)
{
    protected override SqlValueList<SimpleSelectItem>? ParseOptionalOutputClause(ParserState state)
    {
        return null;
    }

    protected override SqlValueList<Assignment>? ParseOptionalOnDuplicateKeyUpdate(ParserState state)
    {
        return null;
    }
}
