using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Expressions;

public record SimilarTo(Expression Expression, bool Negated, Expression Pattern, string? EscapeChar = null, bool Any = false) : Expression, INegated
{
    public Expression? EscapeExpression { get; init; }

    public override void ToSql(SqlTextWriter writer)
    {
        string any = Any ? "ANY " : string.Empty;
        writer.WriteSql($"{Expression} {AsNegated.NegatedText}SIMILAR TO {any}{Pattern}");
        if (EscapeExpression != null)
        {
            writer.WriteSql($" ESCAPE {EscapeExpression}");
        }
        if (EscapeChar != null)
        {
            writer.WriteSql($" ESCAPE '{EscapeChar}'");
        }
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Expression.FormatSql(writer, manager);
        writer.WriteSql($" {AsNegated.NegatedText}SIMILAR TO {(Any ? "ANY " : string.Empty)}");
        Pattern.FormatSql(writer, manager);
        if (EscapeExpression != null)
        {
            writer.Write(" ESCAPE ");
            EscapeExpression.FormatSql(writer, manager);
        }
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
        if (EscapeExpression != null)
        {
            foreach (ItemRef item in EscapeExpression.GetReferencedItems(context))
            {
                yield return item;
            }
        }
    }
}
