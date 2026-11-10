using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.MySql.Parsing;

public class MyDmlParser(Parser parser) : DmlParser(parser)
{
    protected override Identifier? ParseOptionalInsertAlias(ParserState state)
    {
        return null;
    }

    protected override PgIdentityOverride? ParseOptionalIdentityOverride(ParserState state)
    {
        return null;
    }

    protected override Select? ParseInsertSource(ParserState state)
    {
        if (state.PeekKeyword(Keyword.DEFAULT))
        {
            throw state.ExpectedException("SELECT", "VALUES");
        }
        return base.ParseInsertSource(state);
    }

    protected override PgOnConflict? ParseOptionalOnConflict(ParserState state)
    {
        return null;
    }

    protected override SqlValueList<SimpleSelectItem>? ParseOptionalOutputClause(ParserState state)
    {
        return null;
    }
}
