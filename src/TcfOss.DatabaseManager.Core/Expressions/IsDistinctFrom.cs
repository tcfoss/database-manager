using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Expressions;

public record IsDistinctFrom(Expression Left, Expression Right, bool Negated) : Expression, INegated
{
    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"{Left} IS {AsNegated.NegatedText}DISTINCT FROM {Right}");
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        foreach (ItemRef item in Left.GetReferencedItems(context))
        {
            yield return item;
        }

        foreach (ItemRef item in Right.GetReferencedItems(context))
        {
            yield return item;
        }
    }
}
