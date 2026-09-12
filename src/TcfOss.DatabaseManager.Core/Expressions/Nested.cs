using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Expressions;

public record Nested(Expression Expression) : Expression
{
    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"({Expression})");
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        writer.Write("(");
        Expression.FormatSql(writer, manager);
        writer.Write(")");
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        foreach (ItemRef item in Expression.GetReferencedItems(context))
        {
            yield return item;
        }
    }
}
