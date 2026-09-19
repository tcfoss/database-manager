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

    public record AddColumn(StatementColumn Column) : AlterTableOperation
    {
        public AlterTableColumnPosition? Position { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"ADD COLUMN {Column}");
            if (Position != null)
            {
                writer.WriteSql($" {Position}");
            }
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            writer.Write("ADD COLUMN ");
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
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"DROP COLUMN {Column}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            writer.Write("DROP COLUMN ");
            Column.FormatSql(writer, manager);
        }
    }

    public record RenameColumn(Identifier OldName, Identifier NewName) : AlterTableOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"RENAME COLUMN {OldName} TO {NewName}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            writer.Write("RENAME COLUMN ");
            OldName.FormatSql(writer, manager);
            writer.Write(" TO ");
            NewName.FormatSql(writer, manager);
        }
    }

    public record ChangeColumn(Identifier OldName, StatementColumn NewDefinition) : AlterTableOperation
    {
        public AlterTableColumnPosition? Position { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"CHANGE COLUMN {OldName} {NewDefinition}");
            if (Position != null)
            {
                writer.WriteSql($" {Position}");
            }
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            writer.Write("CHANGE COLUMN ");
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
        public AlterTableColumnPosition? Position { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"MODIFY COLUMN {NewDefinition}");
            if (Position != null)
            {
                writer.WriteSql($" {Position}");
            }
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            writer.Write("MODIFY COLUMN ");
            NewDefinition.FormatSqlBody(writer, manager);
            if (Position != null)
            {
                writer.Write(" ");
                Position.FormatSql(writer, manager);
            }
        }
    }

    public record SetDefault(Identifier Column, StatementColumnOption.Default NewDefault) : AlterTableOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"ALTER COLUMN {Column} SET {NewDefault}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            writer.Write("ALTER COLUMN ");
            Column.FormatSql(writer, manager);
            writer.Write(" SET ");
            NewDefault.ToSql(writer);
        }
    }

    public record DropDefault(Identifier Column) : AlterTableOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"ALTER COLUMN {Column} DROP DEFAULT");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            writer.Write("ALTER COLUMN ");
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

    public record SetCharacterSetCollation(string? CharacterSet, string? Collation) : AlterTableOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            string? charset = CharacterSet == null ? null : $" CHARACTER SET {CharacterSet}";
            string? collation = Collation == null ? null : $" COLLATE {Collation}";
            writer.WriteSql($"DEFAULT{charset}{collation}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            writer.Write("DEFAULT");
            if (CharacterSet != null)
            {
                writer.Write($" CHARACTER SET {CharacterSet}");
            }
            if (Collation != null)
            {
                writer.Write($" COLLATE {Collation}");
            }
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


}
