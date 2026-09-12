using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

/// <summary>
/// Microsoft SQL Server <c>TOP (n) [PERCENT] [WITH TIES]</c> clause, appearing
/// immediately after the <c>SELECT [ALL|DISTINCT]</c> keywords.
/// </summary>
public record Top(Expression Expression, bool Percent = false, bool WithTies = false) : IWriteSql
{
    public void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"TOP ({Expression})");
        if (Percent)
        {
            writer.Write(" PERCENT");
        }
        if (WithTies)
        {
            writer.Write(" WITH TIES");
        }
    }

    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        ToSql(writer);
    }

    public IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        return Expression.GetReferencedItems(context);
    }
}
