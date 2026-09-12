using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Expressions;

public record QuantifiedBinaryOperator(Expression Left, BuiltIn.BinaryOperator Operator, Expression Right, AggregateQuantifier Quantifier) : Expression
{
    public override void ToSql(SqlTextWriter writer)
    {
        string leftParen = "";
        string rightParen = "";
        if (Right is not Subquery)
        {
            leftParen = "(";
            rightParen = ")";
        }
        writer.WriteSql($"{Left} {Operator} {Quantifier}{leftParen}{Right}{rightParen}");
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
