using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.MySql.Parsing;

public class MyDmlParser(Parser parser) : DmlParser(parser)
{
    /// <summary>
    /// MySQL does not support the T-SQL <c>OUTPUT</c> clause on
    /// <c>INSERT</c>/<c>UPDATE</c>/<c>DELETE</c>; equivalent functionality
    /// is provided by <c>RETURNING</c> (MariaDB) or by issuing a separate
    /// <c>SELECT</c>.
    /// </summary>
    protected override SqlValueList<SimpleSelectItem>? ParseOptionalOutputClause(ParserState state)
    {
        return null;
    }
}
