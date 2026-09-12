using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Parsing;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.MySql.Parsing;

/// <summary>
/// MySQL <see cref="SelectParser"/>. Overrides the kitchen-sink core hooks
/// for T-SQL-only syntax (<c>SELECT TOP</c>, <c>WITH (...)</c> table hints)
/// to return <c>null</c> unconditionally, documenting that these constructs
/// are not part of the MySQL dialect.
/// </summary>
public class MySelectParser(Parser parser) : SelectParser(parser)
{
    /// <summary>
    /// MySQL does not support <c>SELECT TOP</c>; row limiting is expressed
    /// via the trailing <c>LIMIT</c> clause instead.
    /// </summary>
    protected override Top? ParseOptionalTop(ParserState state)
    {
        return null;
    }

    /// <summary>
    /// MySQL does not support T-SQL-style <c>WITH (NOLOCK, ...)</c> table
    /// hints. Index hints in MySQL use a different syntax
    /// (<c>USE INDEX (...)</c>, <c>FORCE INDEX (...)</c>) and are parsed
    /// elsewhere if at all.
    /// </summary>
    protected override SqlValueList<TableHint>? ParseOptionalTableHints(ParserState state)
    {
        return null;
    }
}
