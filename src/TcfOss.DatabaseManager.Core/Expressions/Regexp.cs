using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Expressions;

public record Regexp(Expression Expression, bool Negated, Expression Pattern, bool Rlike) : Expression, INegated
{
    public override void ToSql(SqlTextWriter writer)
    {
        string operatorText = Rlike ? "RLIKE" : "REGEXP";
        writer.WriteSql($"{Expression} {AsNegated.NegatedText}{operatorText} {Pattern}");
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        foreach (ItemRef item in Expression.GetReferencedItems(context))
        {
            yield return item;
        }

        foreach (ItemRef item in Pattern.GetReferencedItems(context))
        {
            yield return item;
        }
    }
}
