using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Statements;

public record Use(ObjectName ObjectName) : Statement
{
    public UseObject? UseObject { get; init; }

    public override void ToSql(SqlTextWriter writer)
    {
        writer.Write("USE");
        if (UseObject != null)
        {
            writer.WriteSql($" {UseObject}");
        }
        writer.WriteSql($" {ObjectName}");
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        if (UseObject != null)
        {
            writer.WriteSqlI($"USE {UseObject} ");
        }
        else
        {
            writer.WriteSqlI("USE ");
        }
        ObjectName.FormatSql(writer, manager);
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        yield break;
    }
}
