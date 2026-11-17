using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements.Attributes;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record PgDiagnosticAssignment(ObjectName Target, PgAssignmentOperator Op) : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);
    public abstract void FormatSql(SqlTextWriter writer, FormatManager manager);

    public record Current(ObjectName Target, PgAssignmentOperator Op, PgDiagnosticItem Item) : PgDiagnosticAssignment(Target, Op)
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"{Target} {Op} {Item}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Target.FormatSql(writer, manager);
            writer.WriteSql($" {Op} {Item}");
        }
    }

    public record Stacked(ObjectName Target, PgAssignmentOperator Op, PgStackedDiagnosticItem Item) : PgDiagnosticAssignment(Target, Op)
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"{Target} {Op} {Item}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Target.FormatSql(writer, manager);
            writer.WriteSql($" {Op} {Item}");
        }
    }
}
