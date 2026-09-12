using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Statements;

public record Truncate(ObjectName Name) : Statement
{
    public bool TableSpecified { get; init; }

    public override void ToSql(SqlTextWriter writer)
    {
        string tableKeyword = TableSpecified ? "TABLE " : string.Empty;
        writer.WriteSql($"TRUNCATE {tableKeyword}{Name}");
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        writer.WriteSqlI(TableSpecified ? "TRUNCATE TABLE " : "TRUNCATE ");
        Name.FormatSql(writer, manager);
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        using (context.Enter(ReferencedItemsContext.TruncateTarget))
        {
            ItemRef? item = context.CreateObjectRef(Name);
            if (item != null)
            {
                yield return item;
            }
        }
    }
}
