using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Expressions;

#pragma warning disable CA1716 // Identifiers should not match keywords

public record Like(Expression? Expression, bool Negated, Expression Pattern, string? EscapeChar = null, bool Any = false) : Expression, INegated
{
    public override void ToSql(SqlTextWriter writer)
    {
        string any = Any ? "ANY " : string.Empty;

        if (Expression != null)
        {
            writer.WriteSql($"{Expression} ");
        }

        writer.WriteSql($"{AsNegated.NegatedText}LIKE {any}{Pattern}");

        if (EscapeChar != null)
        {
            writer.WriteSql($" ESCAPE '{EscapeChar}'");
        }
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

        foreach (ItemRef item in Pattern.GetReferencedItems(context))
        {
            yield return item;
        }
    }
}
