using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Expressions;

public record PgBinaryOperator(Expression Left, PgOperatorReference Operator, Expression Right) : Expression
{
    public AggregateQuantifier? Quantifier { get; init; }

    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"{Left} {Operator} ");
        if (Quantifier != null)
        {
            writer.WriteSql($"{Quantifier}");
        }
        bool wrapRight = Quantifier != null && Right is not Subquery;
        if (wrapRight)
        {
            writer.Write("(");
        }
        Right.ToSql(writer);
        if (wrapRight)
        {
            writer.Write(")");
        }
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Left.FormatSql(writer, manager);
        writer.Write(" ");
        Operator.FormatSql(writer, manager);
        writer.Write(" ");
        if (Quantifier != null)
        {
            writer.WriteSql($"{Quantifier}");
        }
        bool wrapRight = Quantifier != null && Right is not Subquery;
        if (wrapRight)
        {
            writer.Write("(");
        }
        Right.FormatSql(writer, manager);
        if (wrapRight)
        {
            writer.Write(")");
        }
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        foreach (ItemRef item in Left.GetReferencedItems(context))
        {
            yield return item;
        }
        foreach (ItemRef item in Right.GetReferencedItems(context))
        {
            yield return item;
        }
    }
}
