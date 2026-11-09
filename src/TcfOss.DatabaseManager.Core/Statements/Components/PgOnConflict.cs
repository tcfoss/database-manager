using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record PgOnConflict : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);
    public abstract void FormatSql(SqlTextWriter writer, FormatManager manager);
    public abstract IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context);

    public record DoNothing(PgConflictTarget? Target = null) : PgOnConflict
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("ON CONFLICT");
            if (Target != null)
            {
                writer.WriteSql($" {Target}");
            }
            writer.Write(" DO NOTHING");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write("ON CONFLICT");
            if (Target != null)
            {
                writer.Write(" ");
                Target.FormatSql(writer, manager);
            }
            writer.Write(" DO NOTHING");
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            return Target?.GetReferencedItems(context) ?? [];
        }
    }

    public record DoUpdate(PgConflictTarget Target, SqlValueList<Assignment> Assignments, Expression? Where = null) : PgOnConflict
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"ON CONFLICT {Target} DO UPDATE SET {Assignments}");
            if (Where != null)
            {
                writer.WriteSql($" WHERE {Where}");
            }
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write("ON CONFLICT ");
            Target.FormatSql(writer, manager);
            writer.Write(" DO UPDATE");
            DmlFormatter.FormatAssignmentsClause(writer, manager, Assignments);
            if (Where != null)
            {
                DmlFormatter.FormatWhereClause(writer, manager, Where);
            }
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            foreach (ItemRef item in Target.GetReferencedItems(context))
            {
                yield return item;
            }
            foreach (Assignment assignment in Assignments)
            {
                foreach (ItemRef item in assignment.Value.GetReferencedItems(context))
                {
                    yield return item;
                }
            }
            foreach (ItemRef item in Where?.GetReferencedItems(context) ?? [])
            {
                yield return item;
            }
        }
    }
}
