using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Expressions;

public record AtLocal(Expression Timestamp) : Expression
{
    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"{Timestamp} AT LOCAL");
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Timestamp.FormatSql(writer, manager);
        writer.Write(" AT LOCAL");
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        return Timestamp.GetReferencedItems(context);
    }
}
