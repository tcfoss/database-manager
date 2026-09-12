using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Statements;

/// <summary>
/// T-SQL-style <c>BREAK</c>: exits the enclosing <see cref="While"/> loop.
/// Unlike MySQL <see cref="Leave"/>, it has no label.
/// </summary>
public record Break : Statement
{
    public override void ToSql(SqlTextWriter writer)
    {
        writer.Write("BREAK");
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        writer.WriteSqlI("BREAK");
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        yield break;
    }
}
