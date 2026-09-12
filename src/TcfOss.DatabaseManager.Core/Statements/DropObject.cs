using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Statements;

public record DropObject(ObjectName Name, DroppableObject ObjectType) : Statement
{
    public bool Temporary { get; init; }
    public bool IfExists { get; init; }

    public override void ToSql(SqlTextWriter writer)
    {
        string? ifNotExists = IfExists ? " IF EXISTS" : null;
        string? temporary = Temporary ? " TEMPORARY" : null;
        writer.WriteSql($"DROP{temporary} {ObjectType}{ifNotExists} {Name}");
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        string? temporary = Temporary ? " TEMPORARY" : null;
        writer.WriteSqlI($"DROP{temporary} {ObjectType}");
        if (IfExists)
        {
            writer.Write(" IF EXISTS");
        }
        writer.Write(" ");
        Name.FormatSql(writer, manager);
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        using (context.Enter(ReferencedItemsContext.DropObjectStatement))
        {
            ItemRef? item = context.CreateObjectRef(Name);
            if (item != null)
            {
                yield return item;
            }
        }
    }
}
