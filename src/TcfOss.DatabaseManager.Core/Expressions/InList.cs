using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Expressions;

public record InList(Expression Expression, SqlValueList<Expression> Items, bool Negated) : Expression, INegated
{
    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"{Expression} {AsNegated.NegatedText}IN ({Items})");
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        foreach (ItemRef item in Expression.GetReferencedItems(context))
        {
            yield return item;
        }

        foreach (Expression expr in Items)
        {
            foreach (ItemRef item in expr.GetReferencedItems(context))
            {
                yield return item;
            }
        }
    }
}
