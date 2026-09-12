using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using Operator = TcfOss.DatabaseManager.Core.BuiltIn.UnaryOperator;

namespace TcfOss.DatabaseManager.Core.Expressions;

public record UnaryOperator(Expression Expression, Operator Operator) : Expression
{
    public override void ToSql(SqlTextWriter writer)
    {
        if (Operator == Operator.Not)
        {
            writer.WriteSql($"{Operator} {Expression}");
        }
        else
        {
            writer.WriteSql($"{Operator}{Expression}");
        }
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        foreach (ItemRef item in Expression.GetReferencedItems(context))
        {
            yield return item;
        }
    }
}
