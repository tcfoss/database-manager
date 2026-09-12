using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Expressions;

public record AtTimeZone(Expression Timestamp, Expression Timezone) : Expression
{
    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"{Timestamp} AT TIME ZONE {Timezone}");
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        foreach (ItemRef item in Timestamp.GetReferencedItems(context))
        {
            yield return item;
        }

        foreach (ItemRef item in Timezone.GetReferencedItems(context))
        {
            yield return item;
        }
    }
}
