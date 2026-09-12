using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Expressions;

public record IsTrue(Expression Expression, bool Negated) : Expression, INegated
{
    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"{Expression} IS {AsNegated.NegatedText}TRUE");
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        foreach (ItemRef item in Expression.GetReferencedItems(context))
        {
            yield return item;
        }
    }
}
