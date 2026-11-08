using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Expressions;

#pragma warning disable CA1716 // Identifiers should not match keywords

public record Like(Expression Expression, bool Negated, Expression Pattern, string? EscapeChar = null, bool Any = false) : Expression, INegated
{
    public AggregateQuantifier? Quantifier { get; init; }
    public Expression? EscapeExpression { get; init; }

    public override void ToSql(SqlTextWriter writer)
    {
        string any = Any ? "ANY " : string.Empty;

        writer.WriteSql($"{Expression} ");

        writer.WriteSql($"{AsNegated.NegatedText}LIKE {any}");
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
        writer.WriteSql($" {AsNegated.NegatedText}LIKE {(Any ? "ANY " : string.Empty)}");
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
