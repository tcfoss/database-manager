using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using Operator = TcfOss.DatabaseManager.Core.BuiltIn.BinaryOperator;

namespace TcfOss.DatabaseManager.Core.Expressions;

public record BinaryOperator(Expression Left, Operator Operator, Expression Right) : Expression
{
    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"{Left} {Operator} {Right}");
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Left.FormatSql(writer, manager);
        writer.WriteSql($" {Operator} ");
        Right.FormatSql(writer, manager);
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
