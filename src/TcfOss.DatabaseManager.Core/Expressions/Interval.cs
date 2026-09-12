using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Expressions;

public record Interval(Expression Value, DateTimeUnit Unit) : Expression
{
    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"INTERVAL {Value} {Unit}");
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        foreach (ItemRef item in Value.GetReferencedItems(context))
        {
            yield return item;
        }
    }
}
