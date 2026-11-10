using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Statements;

public record AlterMaterializedView(ObjectName Name, SqlValueList<PgAlterMaterializedViewOperation> Operations) : Statement
{
    public bool IfExists { get; init; }

    public override void ToSql(SqlTextWriter writer)
    {
        writer.Write("ALTER MATERIALIZED VIEW ");
        if (IfExists)
        {
            writer.Write("IF EXISTS ");
        }
        writer.WriteSql($"{Name} {Operations}");
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        writer.WriteSqlI("ALTER MATERIALIZED VIEW ");
        if (IfExists)
        {
            writer.Write("IF EXISTS ");
        }
        Name.FormatSql(writer, manager);
        writer.Write(" ");
        writer.FormatDelimited(Operations, manager);
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        using ReferencedItemsManager.ContextScope scope = context.Enter(ReferencedItemsContext.AlterMaterializedViewTarget);
        ItemRef? item = context.CreateObjectRef(Name);
        if (item != null)
        {
            yield return item;
        }
    }
}
