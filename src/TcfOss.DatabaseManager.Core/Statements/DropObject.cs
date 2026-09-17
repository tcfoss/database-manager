using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Statements;

public record DropObject(SqlValueList<ObjectName> Names, DroppableObject ObjectType) : Statement
{
    public bool Temporary { get; init; }
    public bool IfExists { get; init; }
    public ObjectName? OnObject { get; init; }

    public override void ToSql(SqlTextWriter writer)
    {
        string? ifNotExists = IfExists ? " IF EXISTS" : null;
        string? temporary = Temporary ? " TEMPORARY" : null;
        string? onObject = OnObject != null ? $" ON {OnObject}" : null;
        writer.WriteSql($"DROP{temporary} {ObjectType}{ifNotExists} {Names}{onObject}");
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
        for (int i = 0; i < Names.Count; i++)
        {
            if (i > 0)
            {
                writer.Write(", ");
            }
            Names[i].FormatSql(writer, manager);
        }
        if (OnObject != null)
        {
            writer.Write($" ON {OnObject}");
        }
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        using (context.Enter(ReferencedItemsContext.DropObjectStatement))
        {
            foreach (ObjectName name in Names)
            {
                ItemRef? item = context.CreateObjectRef(name);
                if (item != null)
                {
                    yield return item;
                }
            }
            if (OnObject != null)
            {
                ItemRef? item = context.CreateObjectRef(OnObject);
                if (item != null)
                {
                    yield return item;
                }
            }
        }
    }
}
