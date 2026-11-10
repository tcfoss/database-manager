using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Statements;

public record AlterTable(ObjectName Table, SqlValueList<AlterTableOperation> Operations) : Statement
{
    public bool IfExists { get; init; }
    public bool Only { get; init; }

    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"ALTER TABLE{(IfExists ? " IF EXISTS" : "")}{(Only ? " ONLY" : "")} {Table} {Operations.ToSqlDelimited()}");
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        writer.WriteSqlI("ALTER TABLE ");
        if (IfExists)
        {
            writer.Write("IF EXISTS ");
        }
        if (Only)
        {
            writer.Write("ONLY ");
        }
        Table.FormatSql(writer, manager);
        writer.WriteLine();
        manager.IncreaseIndent();
        for (int i = 0; i < Operations.Count; i++)
        {
            Operations[i].FormatSql(writer, manager);
            if (i < Operations.Count - 1)
            {
                writer.WriteLine(",");
            }
        }
        manager.DecreaseIndent();
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        using (context.Enter(ReferencedItemsContext.AlterTableTarget))
        {
            ItemRef? item = context.CreateObjectRef(Table);
            if (item != null)
            {
                yield return item;
            }
        }
    }
}
