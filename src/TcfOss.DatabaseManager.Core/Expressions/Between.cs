using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Expressions;

public record Between(Expression Expression, Expression Low, Expression High, bool Negated) : Expression, INegated
{
    public bool? Symmetric { get; init; }

    public override void ToSql(SqlTextWriter writer)
    {
        string symmetry = Symmetric switch { true => "SYMMETRIC ", false => "ASYMMETRIC ", _ => "" };
        writer.WriteSql($"{Expression} {AsNegated.NegatedText}BETWEEN {symmetry}{Low} AND {High}");
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        string symmetry = Symmetric switch { true => "SYMMETRIC ", false => "ASYMMETRIC ", _ => "" };
        Expression.FormatSql(writer, manager);
        writer.WriteSql($" {AsNegated.NegatedText}BETWEEN {symmetry}");
        Low.FormatSql(writer, manager);
        writer.Write(" AND ");
        High.FormatSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        foreach (ItemRef item in Expression.GetReferencedItems(context))
        {
            yield return item;
        }

        foreach (ItemRef item in Low.GetReferencedItems(context))
        {
            yield return item;
        }

        foreach (ItemRef item in High.GetReferencedItems(context))
        {
            yield return item;
        }
    }
}
