using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Expressions;

public record IsFalse(Expression Expression, bool Negated) : Expression, INegated
{
    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"{Expression} IS {AsNegated.NegatedText}FALSE");
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        foreach (ItemRef item in Expression.GetReferencedItems(context))
        {
            yield return item;
        }
    }
}
