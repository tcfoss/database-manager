using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Expressions;

public record SimilarTo(Expression Expression, bool Negated, Expression Pattern, string? EscapeChar = null, bool Any = false) : Expression, INegated
{
    public override void ToSql(SqlTextWriter writer)
    {
        string any = Any ? "ANY " : string.Empty;
        writer.WriteSql($"{Expression} {AsNegated.NegatedText}SIMILAR TO {any}{Pattern}");
        if (EscapeChar != null)
        {
            writer.WriteSql($" ESCAPE '{EscapeChar}'");
        }
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
