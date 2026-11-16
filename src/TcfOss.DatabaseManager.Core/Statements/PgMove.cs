using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Statements;

public record PgMove(
    PgCursorDirection? Direction,
    PgCursorNameConnector? Connector,
    Identifier CursorName) : Statement
{
    public override void ToSql(SqlTextWriter writer)
    {
        writer.Write("MOVE ");
        if (Direction != null)
        {
            writer.WriteSql($"{Direction} ");
        }
        if (Connector != null)
        {
            writer.WriteSql($"{Connector} ");
        }
        writer.WriteSql($"{CursorName}");
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        writer.WriteSqlI("MOVE ");
        if (Direction != null)
        {
            Direction.FormatSql(writer, manager);
            writer.Write(" ");
        }
        if (Connector != null)
        {
            writer.WriteSql($"{Connector} ");
        }
        CursorName.FormatSql(writer, manager);
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        if (Direction is not null)
        {
            foreach (ItemRef item in Direction.GetReferencedItems(context))
            {
                yield return item;
            }
        }
    }
}
