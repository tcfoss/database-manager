using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record PgAlterMaterializedViewOperation : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);
    public abstract void FormatSql(SqlTextWriter writer, FormatManager manager);

    public record RenameTo(Identifier Name) : PgAlterMaterializedViewOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"RENAME TO {Name}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write("RENAME TO ");
            Name.FormatSql(writer, manager);
        }
    }

    public record RenameColumn(Identifier OldName, Identifier NewName) : PgAlterMaterializedViewOperation
    {
        public bool UseColumnKeyword { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write(UseColumnKeyword ? "RENAME COLUMN " : "RENAME ");
            writer.WriteSql($"{OldName} TO {NewName}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(UseColumnKeyword ? "RENAME COLUMN " : "RENAME ");
            OldName.FormatSql(writer, manager);
            writer.Write(" TO ");
            NewName.FormatSql(writer, manager);
        }
    }

    public record SetSchema(Identifier Name) : PgAlterMaterializedViewOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"SET SCHEMA {Name}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write("SET SCHEMA ");
            Name.FormatSql(writer, manager);
        }
    }

    public record SetTablespace(Identifier Name) : PgAlterMaterializedViewOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"SET TABLESPACE {Name}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write("SET TABLESPACE ");
            Name.FormatSql(writer, manager);
        }
    }
}
