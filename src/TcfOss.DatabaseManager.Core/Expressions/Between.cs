using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Expressions;

public record Between(Expression Expression, Expression Low, Expression High, bool Negated) : Expression, INegated
{
    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"{Expression} {AsNegated.NegatedText}BETWEEN {Low} AND {High}");
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        foreach (ItemRef item in Expression.GetReferencedItems(context))
        {
            yield return item;
        }

        foreach (ItemRef item in Low.GetReferencedItems(context))
        {
            yield return item;
        }

        foreach (ItemRef item in High.GetReferencedItems(context))
        {
            yield return item;
        }
    }
}
