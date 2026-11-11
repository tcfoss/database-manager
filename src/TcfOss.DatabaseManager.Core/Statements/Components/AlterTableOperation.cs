using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements.Attributes;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record AlterTableOperation() : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);

    public virtual void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        writer.Write(manager.Indent);
        ToSql(writer);
    }

    public record AddColumn(StatementColumn Column, bool PrintColumn) : AlterTableOperation
    {
        public AlterTableColumnPosition? Position { get; init; }
        public bool IfNotExists { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("ADD");
            if (PrintColumn)
            {
                writer.WriteSql($" COLUMN");
            }
            if (IfNotExists)
            {
                writer.Write(" IF NOT EXISTS");
            }
            writer.WriteSql($" {Column}");
            if (Position != null)
            {
                writer.WriteSql($" {Position}");
            }
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            writer.Write("ADD ");
            if (PrintColumn)
            {
                writer.Write("COLUMN ");
            }
            if (IfNotExists)
            {
                writer.Write("IF NOT EXISTS ");
            }
            Column.FormatSqlBody(writer, manager);
            if (Position != null)
            {
                writer.Write(" ");
                Position.FormatSql(writer, manager);
            }
        }
    }

    public record DropColumn(Identifier Column) : AlterTableOperation
    {
        public bool PrintColumn { get; init; } = true;
        public bool IfExists { get; init; }
        public PgDropBehavior? PgBehavior { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write(PrintColumn ? "DROP COLUMN " : "DROP ");
            if (IfExists)
            {
                writer.Write("IF EXISTS ");
            }
            Column.ToSql(writer);
            if (PgBehavior != null)
            {
                writer.WriteSql($" {PgBehavior}");
            }
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            writer.Write(PrintColumn ? "DROP COLUMN " : "DROP ");
            if (IfExists)
            {
                writer.Write("IF EXISTS ");
            }
            Column.FormatSql(writer, manager);
            if (PgBehavior != null)
            {
                writer.WriteSql($" {PgBehavior}");
            }
        }
    }

    public record RenameColumn(Identifier OldName, Identifier NewName) : AlterTableOperation
    {
        public bool PrintColumn { get; init; } = true;

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write(PrintColumn ? "RENAME COLUMN " : "RENAME ");
            writer.WriteSql($"{OldName} TO {NewName}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            writer.Write(PrintColumn ? "RENAME COLUMN " : "RENAME ");
            OldName.FormatSql(writer, manager);
            writer.Write(" TO ");
            NewName.FormatSql(writer, manager);
        }
    }

    public record ChangeColumn(Identifier OldName, StatementColumn NewDefinition) : AlterTableOperation
    {
        public bool PrintColumn { get; init; } = true;
        public AlterTableColumnPosition? Position { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write(PrintColumn ? "CHANGE COLUMN " : "CHANGE ");
            writer.WriteSql($"{OldName} {NewDefinition}");
            if (Position != null)
            {
                writer.WriteSql($" {Position}");
            }
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            writer.Write(PrintColumn ? "CHANGE COLUMN " : "CHANGE ");
            OldName.FormatSql(writer, manager);
            writer.Write(" ");
            NewDefinition.FormatSqlBody(writer, manager);
            if (Position != null)
            {
                writer.Write(" ");
                Position.FormatSql(writer, manager);
            }
        }
    }

    public record ModifyColumn(StatementColumn NewDefinition) : AlterTableOperation
    {
        public bool PrintColumn { get; init; } = true;
        public AlterTableColumnPosition? Position { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write(PrintColumn ? "MODIFY COLUMN " : "MODIFY ");
            writer.WriteSql($"{NewDefinition}");
            if (Position != null)
            {
                writer.WriteSql($" {Position}");
            }
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            writer.Write(PrintColumn ? "MODIFY COLUMN " : "MODIFY ");
            NewDefinition.FormatSqlBody(writer, manager);
            if (Position != null)
            {
                writer.Write(" ");
                Position.FormatSql(writer, manager);
            }
        }
    }

    public record PgAttachPartition(ObjectName Partition, PgPartitionBound Bound) : AlterTableOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"ATTACH PARTITION {Partition} {Bound}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.WriteSqlI("ATTACH PARTITION ");
            Partition.FormatSql(writer, manager);
            writer.Write(" ");
            Bound.FormatSql(writer, manager);
        }
    }

    public record PgDetachPartition(ObjectName Partition, PgDetachPartitionMode? Mode = null) : AlterTableOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"DETACH PARTITION {Partition}");
            if (Mode != null)
            {
                writer.WriteSql($" {Mode}");
            }
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.WriteSqlI("DETACH PARTITION ");
            Partition.FormatSql(writer, manager);
            if (Mode != null)
            {
                writer.WriteSql($" {Mode}");
            }
        }
    }

    public record PgRelation(PgAlterRelationOperation Operation) : AlterTableOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            Operation.ToSql(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            Operation.FormatSql(writer, manager);
        }
    }

    public record PgRowSecurity(PgRowSecurityMode Mode) : AlterTableOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"{Mode} ROW LEVEL SECURITY");
        }
    }

    public record PgTriggerState(PgTriggerStateMode Mode, PgAlterTriggerTarget Target) : AlterTableOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"{Mode} TRIGGER {Target}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            writer.WriteSql($"{Mode} TRIGGER ");
            Target.FormatSql(writer, manager);
        }
    }

    public record PgReplicaIdentity(PgReplicaIdentityMode Mode) : AlterTableOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"REPLICA IDENTITY {Mode}");
        }
    }

    public record PgReplicaIdentityUsingIndex(Identifier Index) : AlterTableOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"REPLICA IDENTITY USING INDEX {Index}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            writer.Write("REPLICA IDENTITY USING INDEX ");
            Index.FormatSql(writer, manager);
        }
    }

    public record PgPersistence(bool Logged) : AlterTableOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write(Logged ? "SET LOGGED" : "SET UNLOGGED");
        }
    }

    public record PgInherit(ObjectName Parent) : AlterTableOperation
    {
        public bool No { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write(No ? "NO INHERIT " : "INHERIT ");
            Parent.ToSql(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            writer.Write(No ? "NO INHERIT " : "INHERIT ");
            Parent.FormatSql(writer, manager);
        }
    }

    public record PgOf(ObjectName Type) : AlterTableOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"OF {Type}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            writer.Write("OF ");
            Type.FormatSql(writer, manager);
        }
    }

    public record PgNotOf : AlterTableOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("NOT OF");
        }
    }

    public record PgIdentity(Identifier Column, SqlValueList<PgAlterIdentityOperation> Operations) : AlterTableOperation
    {
        public bool UseColumnKeyword { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write(UseColumnKeyword ? "ALTER COLUMN " : "ALTER ");
            writer.WriteSql($"{Column} ");
            writer.WriteDelimited(Operations, " ");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            writer.Write(UseColumnKeyword ? "ALTER COLUMN " : "ALTER ");
            Column.FormatSql(writer, manager);
            foreach (PgAlterIdentityOperation operation in Operations)
            {
                writer.Write(" ");
                operation.FormatSql(writer, manager);
            }
        }
    }

    public record PgColumn(Identifier Column, PgAlterColumnOperation Operation) : AlterTableOperation
    {
        public bool PrintColumn { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write(PrintColumn ? "ALTER COLUMN " : "ALTER ");
            writer.WriteSql($"{Column} {Operation}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            writer.Write(PrintColumn ? "ALTER COLUMN " : "ALTER ");
            Column.FormatSql(writer, manager);
            writer.Write(" ");
            Operation.FormatSql(writer, manager);
        }
    }

    public record SetDefault(Identifier Column, StatementColumnOption.Default NewDefault) : AlterTableOperation
    {
        public bool PrintColumn { get; init; } = true;

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write(PrintColumn ? "ALTER COLUMN " : "ALTER ");
            writer.WriteSql($"{Column} SET {NewDefault}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            writer.Write(PrintColumn ? "ALTER COLUMN " : "ALTER ");
            Column.FormatSql(writer, manager);
            writer.Write(" SET ");
            if (NewDefault is StatementColumnOption.Default.DefaultExpression expression)
            {
                writer.Write("DEFAULT (");
                expression.Expression.FormatSql(writer, manager);
                writer.Write(")");
            }
            else
            {
                NewDefault.FormatSql(writer, manager);
            }
        }
    }

    public record DropDefault(Identifier Column) : AlterTableOperation
    {
        public bool PrintColumn { get; init; } = true;

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write(PrintColumn ? "ALTER COLUMN " : "ALTER ");
            writer.WriteSql($"{Column} DROP DEFAULT");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            writer.Write(PrintColumn ? "ALTER COLUMN " : "ALTER ");
            Column.FormatSql(writer, manager);
            writer.Write(" DROP DEFAULT");
        }
    }

    public record Rename(ObjectName NewName) : AlterTableOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"RENAME TO {NewName}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            writer.Write("RENAME TO ");
            NewName.FormatSql(writer, manager);
        }
    }

    public record SetDefaultCharacterSet(string CharacterSet) : AlterTableOperation
    {
        public bool IncludeDefault { get; init; }
        public bool IncludeEquals { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            if (IncludeDefault)
            {
                writer.Write("DEFAULT ");
            }
            writer.Write("CHARACTER SET ");
            if (IncludeEquals)
            {
                writer.Write("= ");
            }
            writer.Write(CharacterSet);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            ToSql(writer);
        }
    }

    public record SetDefaultCollation(string Collation) : AlterTableOperation
    {
        public bool IncludeDefault { get; init; }
        public bool IncludeEquals { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            if (IncludeDefault)
            {
                writer.Write("DEFAULT ");
            }
            writer.Write("COLLATE ");
            if (IncludeEquals)
            {
                writer.Write("= ");
            }
            writer.Write(Collation);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            ToSql(writer);
        }
    }

    public record AddPrimaryKey(StatementTableConstraint.PrimaryKey PrimaryKey) : AlterTableOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"ADD {PrimaryKey}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            writer.Write("ADD ");
            PrimaryKey.FormatSqlBody(writer, manager);
        }
    }

    public record DropPrimaryKey(Identifier? Name = null) : AlterTableOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("DROP PRIMARY KEY");
            if (Name != null)
            {
                writer.WriteSql($" {Name}");
            }
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            writer.Write("DROP PRIMARY KEY");
            if (Name != null)
            {
                writer.Write(" ");
                Name.FormatSql(writer, manager);
            }
        }
    }

    public record AddUniqueKey(StatementTableConstraint.UniqueConstraint UniqueKey) : AlterTableOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"ADD {UniqueKey}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            writer.Write("ADD ");
            UniqueKey.FormatSqlBody(writer, manager);
        }
    }

    public record DropUniqueKey(Identifier Name) : AlterTableOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"DROP CONSTRAINT {Name}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            writer.Write("DROP CONSTRAINT ");
            Name.FormatSql(writer, manager);
        }
    }

    public record AddForeignKey(StatementTableConstraint.ForeignKey ForeignKey) : AlterTableOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"ADD {ForeignKey}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            writer.Write("ADD ");
            ForeignKey.FormatSqlBody(writer, manager);
        }
    }

    public record DropForeignKey(Identifier Name) : AlterTableOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"DROP FOREIGN KEY {Name}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            writer.Write("DROP FOREIGN KEY ");
            Name.FormatSql(writer, manager);
        }
    }

    public record AddCheck(StatementTableConstraint.Check Check) : AlterTableOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"ADD {Check}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            writer.Write("ADD ");
            Check.FormatSqlBody(writer, manager);
        }
    }

    public record DropCheck(Identifier Name) : AlterTableOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"DROP CONSTRAINT {Name}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            writer.Write("DROP CONSTRAINT ");
            Name.FormatSql(writer, manager);
        }
    }

    public record DropConstraint(Identifier Name) : AlterTableOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"DROP CONSTRAINT {Name}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            writer.Write("DROP CONSTRAINT ");
            Name.FormatSql(writer, manager);
        }
    }

    public record AddStandardKey(StatementTableConstraint.Standard Key) : AlterTableOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"ADD {Key}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            writer.Write("ADD ");
            Key.FormatSqlBody(writer, manager);
        }
    }

    public record AddFullTextKey(StatementTableConstraint.FullText Key) : AlterTableOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"ADD {Key}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            writer.Write("ADD ");
            Key.FormatSqlBody(writer, manager);
        }
    }

    public record AddSpatialKey(StatementTableConstraint.Spatial Key) : AlterTableOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"ADD {Key}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            writer.Write("ADD ");
            Key.FormatSqlBody(writer, manager);
        }
    }

    public record DropKey(Identifier Name) : AlterTableOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"DROP KEY {Name}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            writer.Write("DROP KEY ");
            Name.FormatSql(writer, manager);
        }
    }

    public record AutoIncrement(ulong NewValue) : AlterTableOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"AUTO_INCREMENT = {NewValue}");
        }
    }

    public record TableComment(string? NewComment) : AlterTableOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            string newComment = NewComment ?? "";
            writer.WriteSql($"COMMENT = '{newComment}'");
        }
    }
}
