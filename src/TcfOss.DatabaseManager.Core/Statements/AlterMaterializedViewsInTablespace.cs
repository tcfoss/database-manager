using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Statements;

public record AlterMaterializedViewsInTablespace(Identifier Tablespace, Identifier NewTablespace) : Statement
{
    public SqlValueList<Account>? OwnedBy { get; init; }
    public bool Nowait { get; init; }

    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"ALTER MATERIALIZED VIEW ALL IN TABLESPACE {Tablespace}");
        if (OwnedBy != null)
        {
            writer.WriteSql($" OWNED BY {OwnedBy}");
        }
        writer.WriteSql($" SET TABLESPACE {NewTablespace}");
        if (Nowait)
        {
            writer.Write(" NOWAIT");
        }
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        writer.WriteSqlI("ALTER MATERIALIZED VIEW ALL IN TABLESPACE ");
        Tablespace.FormatSql(writer, manager);
        if (OwnedBy != null)
        {
            writer.Write(" OWNED BY ");
            writer.FormatDelimited(OwnedBy, manager);
        }
        writer.Write(" SET TABLESPACE ");
        NewTablespace.FormatSql(writer, manager);
        if (Nowait)
        {
            writer.Write(" NOWAIT");
        }
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        return [];
    }
}
