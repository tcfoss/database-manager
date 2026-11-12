using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Statements;

public record DropRoutine(SqlValueList<PgRoutineReference> Routines, DroppableObject ObjectType) : Statement
{
    public bool IfExists { get; init; }
    public PgDropBehavior? PgBehavior { get; init; }

    public override void ToSql(SqlTextWriter writer)
    {
        string? ifExists = IfExists ? " IF EXISTS" : null;
        writer.WriteSql($"DROP {ObjectType}{ifExists} {Routines}");
        if (PgBehavior != null)
        {
            writer.WriteSql($" {PgBehavior}");
        }
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        writer.WriteSqlI($"DROP {ObjectType}");
        if (IfExists)
        {
            writer.Write(" IF EXISTS");
        }
        writer.Write(" ");
        for (int index = 0; index < Routines.Count; index++)
        {
            if (index > 0)
            {
                writer.Write(", ");
            }
            Routines[index].FormatSql(writer, manager);
        }
        if (PgBehavior != null)
        {
            writer.WriteSql($" {PgBehavior}");
        }
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        using (context.Enter(ReferencedItemsContext.DropRoutineTarget))
        {
            foreach (PgRoutineReference routine in Routines)
            {
                ItemRef? target = context.CreateObjectRef(routine.Name);
                if (target != null)
                {
                    yield return target;
                }
                if (routine.Parameters == null)
                {
                    continue;
                }
                foreach (StatementRoutineParameter parameter in routine.Parameters)
                {
                    DataTypeReference dataType = parameter.DataType;
                    while (dataType is DataTypeReference.Array array)
                    {
                        dataType = array.ElementType;
                    }
                    if (dataType is DataTypeReference.Named named)
                    {
                        ItemRef? type = context.CreateObjectRef(named.Name);
                        if (type != null)
                        {
                            yield return type;
                        }
                    }
                }
            }
        }
    }
}
