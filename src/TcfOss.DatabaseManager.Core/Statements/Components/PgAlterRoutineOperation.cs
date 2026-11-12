using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record PgAlterRoutineOperation : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);

    public virtual void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        ToSql(writer);
    }

    public record SetOption(PgStatementRoutineOption Value) : PgAlterRoutineOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            Value.ToSql(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Value.FormatSql(writer, manager);
        }
    }

    public record Relation(PgAlterRelationOperation Value) : PgAlterRoutineOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            Value.ToSql(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Value.FormatSql(writer, manager);
        }
    }

    public record Reset(ObjectName Name) : PgAlterRoutineOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"RESET {Name}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write("RESET ");
            Name.FormatSql(writer, manager);
        }
    }

    public record ResetAll : PgAlterRoutineOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("RESET ALL");
        }
    }
}
