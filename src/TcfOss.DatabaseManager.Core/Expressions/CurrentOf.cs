using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Expressions;

public record CurrentOf(Identifier CursorName) : Expression
{
    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"CURRENT OF {CursorName}");
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        writer.Write("CURRENT OF ");
        CursorName.FormatSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        yield break;
    }
}
