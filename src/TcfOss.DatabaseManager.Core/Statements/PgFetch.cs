using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Statements;

public record PgFetch(
    PgCursorDirection? Direction,
    PgCursorNameConnector? Connector,
    Identifier CursorName,
    SqlValueList<ObjectName> Targets) : Statement
{
    public override void ToSql(SqlTextWriter writer)
    {
        writer.Write("FETCH ");
        if (Direction != null)
        {
            writer.WriteSql($"{Direction} {Connector} ");
        }
        writer.WriteSql($"{CursorName} INTO {Targets.ToSqlDelimited()}");
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        writer.WriteSqlI("FETCH ");
        if (Direction != null)
        {
            Direction.FormatSql(writer, manager);
            writer.Write(" ");
            Connector!.ToSql(writer);
            writer.Write(" ");
        }
        CursorName.FormatSql(writer, manager);
        writer.Write(" INTO ");
        for (int i = 0; i < Targets.Count; i++)
        {
            if (i > 0)
            {
                writer.Write(", ");
            }
            Targets[i].FormatSql(writer, manager);
        }
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
