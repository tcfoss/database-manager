using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

/// <summary>
/// Inline window specification for a window function, i.e. the body of an
/// <c>OVER (...)</c> clause.
/// </summary>
public record WindowSpec : IWriteSql
{
    public SqlValueList<Expression>? PartitionBy { get; init; }
    public SqlValueList<OrderBy>? OrderBy { get; init; }

    public void ToSql(SqlTextWriter writer)
    {
        writer.Write("(");
        bool needSpace = false;
        if (PartitionBy != null)
        {
            writer.WriteSql($"PARTITION BY {PartitionBy.ToSqlDelimited()}");
            needSpace = true;
        }
        if (OrderBy != null)
        {
            if (needSpace)
            {
                writer.Write(" ");
            }
            writer.WriteSql($"ORDER BY {OrderBy.ToSqlDelimited()}");
        }
        writer.Write(")");
    }

    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        ToSql(writer);
    }

    public IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        if (PartitionBy != null)
        {
            foreach (Expression expr in PartitionBy)
            {
                foreach (ItemRef item in expr.GetReferencedItems(context))
                {
                    yield return item;
                }
            }
        }
        if (OrderBy != null)
        {
            foreach (OrderBy ob in OrderBy)
            {
                foreach (ItemRef item in ob.GetReferencedItems(context))
                {
                    yield return item;
                }
            }
        }
    }
}
