using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;

namespace TcfOss.DatabaseManager.Core.Expressions;

public record InSubquery(SimpleSelect Subquery, bool Negated, Expression? Expression = null) : Expression, INegated
{
    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"{Expression} {AsNegated.NegatedText}IN ({Subquery})");
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        if (Expression != null)
        {
            foreach (ItemRef item in Expression.GetReferencedItems(context))
            {
                yield return item;
            }
        }

        foreach (ItemRef item in Subquery.GetReferencedItems(context))
        {
            yield return item;
        }
    }
}
