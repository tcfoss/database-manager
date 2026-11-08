using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Expressions;

public record SimilarTo(Expression Expression, bool Negated, Expression Pattern, PatternEscape? Escape = null, bool Any = false) : Expression, INegated
{
    public override void ToSql(SqlTextWriter writer)
    {
        string any = Any ? "ANY " : string.Empty;
        writer.WriteSql($"{Expression} {AsNegated.NegatedText}SIMILAR TO {any}{Pattern}");
        Escape?.ToSql(writer);
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Expression.FormatSql(writer, manager);
        writer.WriteSql($" {AsNegated.NegatedText}SIMILAR TO {(Any ? "ANY " : string.Empty)}");
        Pattern.FormatSql(writer, manager);
        Escape?.FormatSql(writer, manager);
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
        if (Escape != null)
        {
            foreach (ItemRef item in Escape.GetReferencedItems(context))
            {
                yield return item;
            }
        }
    }
}
