using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Statements;

public record RefreshMaterializedView(ObjectName Name) : Statement
{
    public bool Concurrently { get; init; }
    public bool? WithData { get; init; }

    public override void ToSql(SqlTextWriter writer)
    {
        writer.Write("REFRESH MATERIALIZED VIEW ");
        if (Concurrently)
        {
            writer.Write("CONCURRENTLY ");
        }
        Name.ToSql(writer);
        if (WithData != null)
        {
            writer.Write(WithData.Value ? " WITH DATA" : " WITH NO DATA");
        }
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        writer.WriteSqlI("REFRESH MATERIALIZED VIEW ");
        if (Concurrently)
        {
            writer.Write("CONCURRENTLY ");
        }
        Name.FormatSql(writer, manager);
        if (WithData != null)
        {
            writer.Write(WithData.Value ? " WITH DATA" : " WITH NO DATA");
        }
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        using var scope = context.Enter(ReferencedItemsContext.RefreshMaterializedViewTarget);
        ItemRef? item = context.CreateObjectRef(Name);
        if (item != null)
        {
            yield return item;
        }
    }
}
