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

    public record SetStorageParameters(SqlValueList<PgStorageParameter> Parameters) : PgAlterMaterializedViewOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"SET ({Parameters})");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write("SET (");
            writer.FormatDelimited(Parameters, manager);
            writer.Write(")");
        }
    }

    public record ResetStorageParameters(SqlValueList<ObjectName> Parameters) : PgAlterMaterializedViewOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"RESET ({Parameters})");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write("RESET (");
            writer.FormatDelimited(Parameters, manager);
            writer.Write(")");
        }
    }

    public record OwnerTo(Account Owner) : PgAlterMaterializedViewOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"OWNER TO {Owner}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write("OWNER TO ");
            Owner.FormatSql(writer, manager);
        }
    }

    public record SetAccessMethod(Identifier Name) : PgAlterMaterializedViewOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"SET ACCESS METHOD {Name}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write("SET ACCESS METHOD ");
            Name.FormatSql(writer, manager);
        }
    }

    public record SetDefaultAccessMethod : PgAlterMaterializedViewOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("SET ACCESS METHOD DEFAULT");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            ToSql(writer);
        }
    }

    public record ClusterOn(Identifier Name) : PgAlterMaterializedViewOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"CLUSTER ON {Name}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write("CLUSTER ON ");
            Name.FormatSql(writer, manager);
        }
    }

    public record SetWithoutCluster : PgAlterMaterializedViewOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("SET WITHOUT CLUSTER");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            ToSql(writer);
        }
    }

    public record DependsOnExtension(Identifier Name) : PgAlterMaterializedViewOperation
    {
        public bool No { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write(No ? "NO DEPENDS ON EXTENSION " : "DEPENDS ON EXTENSION ");
            Name.ToSql(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(No ? "NO DEPENDS ON EXTENSION " : "DEPENDS ON EXTENSION ");
            Name.FormatSql(writer, manager);
        }
    }

    public record AlterColumn(Identifier Name, PgColumnConfigurationOperation Operation) : PgAlterMaterializedViewOperation
    {
        public bool UseColumnKeyword { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write(UseColumnKeyword ? "ALTER COLUMN " : "ALTER ");
            writer.WriteSql($"{Name} {Operation}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(UseColumnKeyword ? "ALTER COLUMN " : "ALTER ");
            Name.FormatSql(writer, manager);
            writer.Write(" ");
            Operation.FormatSql(writer, manager);
        }
    }
}
