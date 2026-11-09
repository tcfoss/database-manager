using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.MsSql.Parsing;

public class MsDmlParser(Parser parser) : DmlParser(parser)
{
    protected override Identifier? ParseOptionalInsertAlias(ParserState state)
    {
        return null;
    }

    protected override PgIdentityOverride? ParseOptionalIdentityOverride(ParserState state)
    {
        return null;
    }

    protected override PgOnConflict? ParseOptionalOnConflict(ParserState state)
    {
        return null;
    }

    protected override SqlValueList<TableWithJoins>? ParseOptionalDeleteUsing(ParserState state)
    {
        return null;
    }

    protected override SqlValueList<Assignment>? ParseOptionalOnDuplicateKeyUpdate(ParserState state)
    {
        return null;
    }

    protected override SqlValueList<SimpleSelectItem>? ParseOptionalReturningClause(ParserState state)
    {
        return null;
    }
}
