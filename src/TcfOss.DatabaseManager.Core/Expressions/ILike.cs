using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Expressions;

public record ILike(Expression Expression, bool Negated, Expression Pattern, PatternEscape? Escape = null, bool Any = false) : Expression, INegated
{
    public AggregateQuantifier? Quantifier { get; init; }

    public override void ToSql(SqlTextWriter writer)
    {
        string any = Any ? "ANY " : string.Empty;
        writer.WriteSql($"{Expression} {AsNegated.NegatedText}ILIKE {any}");
        if (Quantifier != null)
        {
            writer.WriteSql($"{Quantifier}");
        }
        bool wrapPattern = Quantifier != null && Pattern is not Subquery;
        if (wrapPattern)
        {
            writer.Write("(");
        }
        Pattern.ToSql(writer);
        if (wrapPattern)
        {
            writer.Write(")");
        }
        Escape?.ToSql(writer);
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Expression.FormatSql(writer, manager);
        writer.WriteSql($" {AsNegated.NegatedText}ILIKE {(Any ? "ANY " : string.Empty)}");
        if (Quantifier != null)
        {
            writer.WriteSql($"{Quantifier}");
        }
        bool wrapPattern = Quantifier != null && Pattern is not Subquery;
        if (wrapPattern)
        {
            writer.Write("(");
        }
        Pattern.FormatSql(writer, manager);
        if (wrapPattern)
        {
            writer.Write(")");
        }
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
