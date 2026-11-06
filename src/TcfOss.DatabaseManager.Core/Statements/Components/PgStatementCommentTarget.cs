using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record PgStatementCommentTarget : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);
    public abstract void FormatSql(SqlTextWriter writer, FormatManager manager);

    public record DatabaseObject(PgCommentObjectType ObjectType, ObjectName Name) : PgStatementCommentTarget
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"{ObjectType} {Name}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.WriteSqlI($"{ObjectType} ");
            Name.FormatSql(writer, manager);
        }
    }

    public record Column(ObjectName Name) : PgStatementCommentTarget
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"COLUMN {Name}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write("COLUMN ");
            Name.FormatSql(writer, manager);
        }
    }

    public record Routine(PgCommentObjectType ObjectType, ObjectName Name,
        SqlValueList<StatementRoutineParameter>? Parameters) : PgStatementCommentTarget
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"{ObjectType} {Name}");
            if (Parameters != null)
            {
                writer.WriteSql($"({Parameters})");
            }
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.WriteSqlI($"{ObjectType} ");
            Name.FormatSql(writer, manager);
            if (Parameters != null)
            {
                writer.Write("(");
                for (int index = 0; index < Parameters.Count; index++)
                {
                    if (index > 0)
                    {
                        writer.Write(", ");
                    }
                    Parameters[index].FormatSql(writer, manager);
                }
                writer.Write(")");
            }
        }
    }

    public record Trigger(Identifier Name, ObjectName OnTable) : PgStatementCommentTarget
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"TRIGGER {Name} ON {OnTable}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write("TRIGGER ");
            Name.FormatSql(writer, manager);
            writer.Write(" ON ");
            OnTable.FormatSql(writer, manager);
        }
    }
}
