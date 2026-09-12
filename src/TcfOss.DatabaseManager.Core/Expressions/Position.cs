using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Expressions;

public record Position(Expression Expression, Expression In) : Expression
{
    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"POSITION({Expression} IN {In})");
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        writer.Write("POSITION(");
        Expression.FormatSql(writer, manager);
        writer.Write(" IN ");
        In.FormatSql(writer, manager);
        writer.Write(")");
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        foreach (ItemRef item in Expression.GetReferencedItems(context))
        {
            yield return item;
        }

        foreach (ItemRef item in In.GetReferencedItems(context))
        {
            yield return item;
        }
    }
}
