using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record StatementTableConstraint(Identifier? Name = null) : IWriteSql
{
    public Comment? Comment { get; init; }

    public abstract void ToSql(SqlTextWriter writer);
    public abstract void FormatSql(SqlTextWriter writer, FormatManager manager);

    private void WriteConstraintName(SqlTextWriter writer)
    {
        if (Name == null)
        {
            return;
        }
        writer.WriteSql($"CONSTRAINT {Name} ");
    }

    private void FormatConstraintName(SqlTextWriter writer, FormatManager manager)
    {
        if (Name == null)
        {
            return;
        }
        writer.WriteSql($"CONSTRAINT ");
        Name.FormatSql(writer, manager);
        writer.Write(" ");
    }

    private void WriteComment(SqlTextWriter writer)
    {
        if (Comment != null)
        {
            writer.WriteSql($" {Comment}");
        }
    }

    private static void WriteKeyLabel(SqlTextWriter writer, KeyLabel? label, bool prependSpace)
    {
        string initial = prependSpace ? " " : "";
        switch (label)
        {
            case KeyLabel.Key:
                writer.Write($"{initial}KEY");
                break;
            case KeyLabel.Index:
                writer.Write($"{initial}INDEX");
                break;
        }
    }

    public record PrimaryKey(SqlValueList<KeyPart> Columns, Identifier? Name = null) : StatementTableConstraint(Name)
    {
        public IndexMethod? IndexMethod { get; init; }
        public IndexOrganization? IndexOrganization { get; init; }
        public SqlValueList<StatementIndexOption>? Options { get; init; }
        public IndexStorageLocation? StorageLocation { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            WriteConstraintName(writer);
            writer.Write("PRIMARY KEY");
            if (IndexOrganization != null)
            {
                writer.WriteSql($" {IndexOrganization}");
            }
            if (IndexMethod != null)
            {
                writer.WriteSql($" USING {IndexMethod}");
            }
            writer.WriteSql($" ({Columns})");
            if (Options != null)
            {
                writer.WriteSql($" WITH ({Options})");
            }
            if (StorageLocation != null)
            {
                writer.WriteSql($" {StorageLocation}");
            }
            WriteComment(writer);
        }

        public void FormatSqlBody(SqlTextWriter writer, FormatManager manager)
        {
            FormatConstraintName(writer, manager);
            writer.Write("PRIMARY KEY ");
            if (IndexMethod != null)
            {
                writer.WriteSql($"USING {IndexMethod} ");
            }
            FormatKeyParts(writer, Columns, manager);
            if (Options != null)
            {
                writer.Write(" WITH (");
                writer.FormatDelimited(Options, manager);
                writer.Write(")");
            }
            if (StorageLocation != null)
            {
                writer.WriteSql($" {StorageLocation}");
            }
            WriteComment(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            FormatSqlBody(writer, manager);
        }
    }

    public record UniqueConstraint(SqlValueList<KeyPart> Columns, Identifier? Name = null) : StatementTableConstraint(Name)
    {
        public KeyLabel? KeyLabel { get; init; }
        public IndexMethod? IndexMethod { get; init; }
        public Identifier? IndexName { get; init; }
        public IndexOrganization? IndexOrganization { get; init; }
        public IncludedColumns? IncludedColumns { get; init; }
        public SqlValueList<StatementIndexOption>? Options { get; init; }
        public IndexStorageLocation? StorageLocation { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            WriteConstraintName(writer);
            writer.Write("UNIQUE");
            if (IndexOrganization != null)
            {
                writer.WriteSql($" {IndexOrganization}");
            }
            WriteKeyLabel(writer, KeyLabel, true);
            if (IndexName != null)
            {
                writer.WriteSql($" {IndexName}");
            }
            if (IndexMethod != null)
            {
                writer.WriteSql($" USING {IndexMethod}");
            }
            writer.WriteSql($" ({Columns})");
            if (IncludedColumns != null)
            {
                writer.WriteSql($" {IncludedColumns}");
            }
            if (Options != null)
            {
                writer.WriteSql($" WITH ({Options})");
            }
            if (StorageLocation != null)
            {
                writer.WriteSql($" {StorageLocation}");
            }
            WriteComment(writer);
        }

        public void FormatSqlBody(SqlTextWriter writer, FormatManager manager)
        {
            FormatConstraintName(writer, manager);
            writer.Write("UNIQUE");
            if (IndexOrganization != null)
            {
                writer.WriteSql($" {IndexOrganization}");
            }
            WriteKeyLabel(writer, KeyLabel, true);
            if (IndexName != null)
            {
                writer.Write(" ");
                IndexName.FormatSql(writer, manager);
            }
            if (IndexMethod != null)
            {
                writer.WriteSql($" USING {IndexMethod}");
            }
            writer.Write(" ");
            FormatKeyParts(writer, Columns, manager);
            if (IncludedColumns != null)
            {
                writer.Write(" ");
                IncludedColumns.FormatSql(writer, manager);
            }
            if (Options != null)
            {
                writer.Write(" ");
                writer.FormatDelimited(Options, manager);
            }
            if (StorageLocation != null)
            {
                writer.WriteSql($" {StorageLocation}");
            }
            WriteComment(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            FormatSqlBody(writer, manager);
        }
    }

    public record ForeignKey(SqlValueList<Identifier> Columns, ObjectName ForeignTable, SqlValueList<Identifier> ForeignColumns, Identifier? Name = null) : StatementTableConstraint(Name)
    {
        public ReferentialAction? OnDelete { get; init; }
        public ReferentialAction? OnUpdate { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            WriteConstraintName(writer);
            writer.WriteSql($"FOREIGN KEY ({Columns}) REFERENCES {ForeignTable} ({ForeignColumns})");
            if (OnDelete != null)
            {
                writer.WriteSql($" ON DELETE {OnDelete}");
            }
            if (OnUpdate != null)
            {
                writer.WriteSql($" ON UPDATE {OnUpdate}");
            }
            WriteComment(writer);
        }

        public void FormatSqlBody(SqlTextWriter writer, FormatManager manager)
        {
            FormatConstraintName(writer, manager);
            writer.Write("FOREIGN KEY (");
            writer.FormatDelimited(Columns, manager);
            writer.WriteSql($") REFERENCES ");
            ForeignTable.FormatSql(writer, manager);
            writer.Write(" (");
            writer.FormatDelimited(ForeignColumns, manager);
            writer.Write(")");
            if (OnDelete != null)
            {
                writer.WriteSql($" ON DELETE {OnDelete}");
            }
            if (OnUpdate != null)
            {
                writer.WriteSql($" ON UPDATE {OnUpdate}");
            }
            WriteComment(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            FormatSqlBody(writer, manager);
        }
    }

    public record Check(Expression Expression, Identifier? Name = null) : StatementTableConstraint(Name)
    {
        public override void ToSql(SqlTextWriter writer)
        {
            WriteConstraintName(writer);
            writer.WriteSql($"CHECK ({Expression})");
            WriteComment(writer);
        }

        public void FormatSqlBody(SqlTextWriter writer, FormatManager manager)
        {
            FormatConstraintName(writer, manager);
            writer.Write("CHECK (");
            Expression.FormatSql(writer, manager);
            writer.Write(")");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            FormatSqlBody(writer, manager);
        }
    }

    public abstract record NonConstraintKey(SqlValueList<KeyPart> Columns, Identifier? IndexName) : StatementTableConstraint
    {
        public KeyLabel? KeyLabel { get; init; }
    }

    public record Standard(SqlValueList<KeyPart> Columns, Identifier? IndexName) : NonConstraintKey(Columns, IndexName)
    {
        public IndexMethod? IndexMethod { get; init; }
        public IndexOrganization? IndexOrganization { get; init; }
        public IncludedColumns? IncludedColumns { get; init; }
        public Expression? Filter { get; init; }
        public SqlValueList<StatementIndexOption>? Options { get; init; }
        public IndexStorageLocation? StorageLocation { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {

            WriteKeyLabel(writer, KeyLabel, false);
            if (IndexName != null)
            {
                writer.WriteSql($" {IndexName}");
            }
            if (IndexOrganization != null)
            {
                writer.WriteSql($" {IndexOrganization}");
            }
            if (IndexMethod != null)
            {
                writer.WriteSql($" USING {IndexMethod}");
            }
            writer.WriteSql($" ({Columns})");
            if (IncludedColumns != null)
            {
                writer.WriteSql($" {IncludedColumns}");
            }

            if (Filter != null)
            {
                writer.WriteSql($" WHERE {Filter}");
            }

            if (Options != null)
            {
                writer.WriteSql($" WITH ({Options})");
            }

            if (StorageLocation != null)
            {
                writer.WriteSql($" {StorageLocation}");
            }

            WriteComment(writer);
        }

        public void FormatSqlBody(SqlTextWriter writer, FormatManager manager)
        {
            WriteKeyLabel(writer, KeyLabel, false);
            if (IndexName != null)
            {
                writer.Write(" ");
                IndexName.FormatSql(writer, manager);
            }
            if (IndexMethod != null)
            {
                writer.WriteSql($" USING {IndexMethod}");
            }
            writer.Write(" ");
            FormatKeyParts(writer, Columns, manager);
            if (IncludedColumns != null)
            {
                writer.Write(" ");
                IncludedColumns.FormatSql(writer, manager);
            }
            if (Filter != null)
            {
                writer.Write(" WHERE ");
                Filter.FormatSql(writer, manager);
            }
            if (Options != null)
            {
                writer.Write(" WITH (");
                writer.FormatDelimited(Options, manager);
                writer.Write(")");
            }
            if (StorageLocation != null)
            {
                writer.WriteSql($" {StorageLocation}");
            }
            WriteComment(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            FormatSqlBody(writer, manager);
        }
    }

    public record FullText(SqlValueList<KeyPart> Columns, Identifier? IndexName) : NonConstraintKey(Columns, IndexName)
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("FULLTEXT");
            WriteKeyLabel(writer, KeyLabel, true);
            if (IndexName != null)
            {
                writer.WriteSql($" {IndexName}");
            }
            writer.WriteSql($" ({Columns})");
            WriteComment(writer);
        }

        public void FormatSqlBody(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write("FULLTEXT");
            WriteKeyLabel(writer, KeyLabel, true);
            if (IndexName != null)
            {
                writer.Write(" ");
                IndexName.FormatSql(writer, manager);
            }
            writer.Write(" ");
            FormatKeyParts(writer, Columns, manager);
            WriteComment(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            FormatSqlBody(writer, manager);
        }
    }

    public record Spatial(SqlValueList<KeyPart> Columns, Identifier? IndexName) : NonConstraintKey(Columns, IndexName)
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("SPATIAL");
            WriteKeyLabel(writer, KeyLabel, true);
            if (IndexName != null)
            {
                writer.WriteSql($" {IndexName}");
            }
            writer.WriteSql($" ({Columns})");
            WriteComment(writer);
        }

        public void FormatSqlBody(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write("SPATIAL");
            WriteKeyLabel(writer, KeyLabel, true);
            if (IndexName != null)
            {
                writer.Write(" ");
                IndexName.FormatSql(writer, manager);
            }
            writer.Write(" ");
            FormatKeyParts(writer, Columns, manager);
            WriteComment(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            FormatSqlBody(writer, manager);
        }
    }


    public record UniqueIndex(SqlValueList<KeyPart> Columns, Identifier? IndexName) : NonConstraintKey(Columns, IndexName)
    {
        public IndexMethod? IndexMethod { get; init; }
        public IndexOrganization? IndexOrganization { get; init; }
        public IncludedColumns? IncludedColumns { get; init; }
        public Expression? Filter { get; init; }
        public SqlValueList<StatementIndexOption>? Options { get; init; }
        public IndexStorageLocation? StorageLocation { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {

            WriteKeyLabel(writer, KeyLabel, false);
            if (IndexName != null)
            {
                writer.WriteSql($" {IndexName}");
            }
            writer.Write(" UNIQUE");
            if (IndexOrganization != null)
            {
                writer.WriteSql($" {IndexOrganization}");
            }
            if (IndexMethod != null)
            {
                writer.WriteSql($" USING {IndexMethod}");
            }

            writer.WriteSql($" ({Columns})");
            if (IncludedColumns != null)
            {
                writer.WriteSql($" {IncludedColumns}");
            }


            if (Filter != null)
            {
                writer.WriteSql($" WHERE {Filter}");
            }

            if (Options != null)
            {
                writer.WriteSql($" WITH ({Options})");
            }

            if (StorageLocation != null)
            {
                writer.WriteSql($" {StorageLocation}");
            }

            WriteComment(writer);
        }

        public void FormatSqlBody(SqlTextWriter writer, FormatManager manager)
        {
            WriteKeyLabel(writer, KeyLabel, false);
            if (IndexName != null)
            {
                writer.Write(" ");
                IndexName.FormatSql(writer, manager);
            }
            if (IndexMethod != null)
            {
                writer.WriteSql($" USING {IndexMethod}");
            }
            writer.Write(" ");
            FormatKeyParts(writer, Columns, manager);
            if (IncludedColumns != null)
            {
                writer.Write(" ");
                IncludedColumns.FormatSql(writer, manager);
            }
            if (Filter != null)
            {
                writer.Write(" WHERE ");
                Filter.FormatSql(writer, manager);
            }
            if (Options != null)
            {
                writer.Write(" WITH (");
                writer.FormatDelimited(Options, manager);
                writer.Write(")");
            }
            if (StorageLocation != null)
            {
                writer.WriteSql($" {StorageLocation}");
            }
            WriteComment(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            FormatSqlBody(writer, manager);
        }
    }

    public static void FormatKeyParts(SqlTextWriter writer, SqlValueList<KeyPart> keyParts, FormatManager manager)
    {
        writer.Write("(");
        for (int i = 0; i < keyParts.Count; i++)
        {
            if (i > 0)
            {
                writer.Write(", ");
            }
            keyParts[i].FormatSql(writer, manager);
        }
        writer.Write(")");
    }
}
