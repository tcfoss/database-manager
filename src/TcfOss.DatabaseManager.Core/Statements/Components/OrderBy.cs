using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public record OrderBy(Expression Expression, Direction? Direction) : IWriteSql
{
    public NullsOrder? NullsOrder { get; init; }

    public void ToSql(SqlTextWriter writer)
    {
        Expression.ToSql(writer);

        if (Direction != null)
        {
            writer.WriteSql($" {Direction}");
        }
        if (NullsOrder != null)
        {
            writer.WriteSql($" NULLS {NullsOrder}");
        }
    }

    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        writer.Write(manager.Indent);
        FormatInline(writer, manager);
    }

    public void FormatInline(SqlTextWriter writer, FormatManager manager)
    {
        using (manager.Enter(FormatContext.OrderByClause))
        {
            Expression.FormatSql(writer, manager);
            if (Direction != null)
            {
                writer.WriteSql($" {Direction}");
            }
            if (NullsOrder != null)
            {
                writer.WriteSql($" NULLS {NullsOrder}");
            }
        }
    }

    public IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        foreach (ItemRef item in Expression.GetReferencedItems(context))
        {
            yield return item;
        }
    }
}
