using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;

namespace TcfOss.DatabaseManager.MsSql.Statements;

/// <summary>
/// T-SQL <c>GO [count]</c> batch separator. Not a true SQL statement—<c>GO</c>
/// is a client-side directive interpreted by sqlcmd/SSMS—but
/// modeled here as a top-level Statement so that input scripts can be
/// round-tripped without losing batch boundaries.
/// </summary>
public record MsBatchSeparator(int Count = 1) : Statement
{
    public override void ToSql(SqlTextWriter writer)
    {
        if (Count == 1)
        {
            writer.Write("GO");
        }
        else
        {
            writer.WriteSql($"GO {Count}");
        }
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        yield break;
    }
}
