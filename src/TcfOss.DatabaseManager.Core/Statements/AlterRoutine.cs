using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Statements;

public record AlterRoutine(PgRoutineKind Kind, PgRoutineReference Routine, SqlValueList<PgAlterRoutineOperation> Operations) : Statement
{
    public bool Restrict { get; init; }

    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"ALTER {Kind} {Routine}");
        foreach (PgAlterRoutineOperation operation in Operations)
        {
            writer.Write(" ");
            operation.ToSql(writer);
        }
        if (Restrict)
        {
            writer.Write(" RESTRICT");
        }
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        writer.WriteSqlI($"ALTER {Kind} ");
        Routine.FormatSql(writer, manager);
        writer.WriteLine();
        manager.IncreaseIndent();
        for (int index = 0; index < Operations.Count; index++)
        {
            writer.Write(manager.Indent);
            Operations[index].FormatSql(writer, manager);
            if (index < Operations.Count - 1)
            {
                writer.WriteLine();
            }
        }
        if (Restrict)
        {
            writer.Write(" RESTRICT");
        }
        manager.DecreaseIndent();
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        using (context.Enter(ReferencedItemsContext.AlterRoutineTarget))
        {
            ItemRef? target = context.CreateObjectRef(Routine.Name);
            if (target != null)
            {
                yield return target;
            }
            if (Routine.Parameters == null)
            {
                yield break;
            }
            foreach (StatementRoutineParameter parameter in Routine.Parameters)
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
