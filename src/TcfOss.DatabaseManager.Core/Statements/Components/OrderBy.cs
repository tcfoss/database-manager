using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public record OrderBy(Expression Expression, Direction? Direction) : IWriteSql
{
    // TODO : NullsFirst and WithFill

    public void ToSql(SqlTextWriter writer)
    {
        Expression.ToSql(writer);

        if (Direction != null)
        {
            writer.WriteSql($" {Direction}");
        }
    }

    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        using (manager.Enter(FormatContext.OrderByClause))
        {
            writer.Write(manager.Indent);
            Expression.FormatSql(writer, manager);
            if (Direction != null)
            {
                writer.WriteSql($" {Direction}");
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
