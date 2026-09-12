using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Expressions;

public record Tuple(SqlValueList<Expression> Expressions) : Expression
{
    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"({Expressions})");
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        foreach (Expression expr in Expressions)
        {
            foreach (ItemRef item in expr.GetReferencedItems(context))
            {
                yield return item;
            }
        }
    }
}
