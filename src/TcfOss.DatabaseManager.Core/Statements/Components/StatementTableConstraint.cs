using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record StatementTableConstraint(Identifier? Name = null) : IWriteSql
{
    public Comment? Comment { get; init; }
    public SqlValueList<PgConstraintOption>? PgOptions { get; init; }

    public abstract void ToSql(SqlTextWriter writer);
    public abstract void FormatSql(SqlTextWriter writer, FormatManager manager);

    public IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        if (this is ForeignKey foreignKey)
        {
            using (context.Enter(ReferencedItemsContext.ConstraintTable))
            {
                ItemRef? item = context.CreateObjectRef(foreignKey.ForeignTable);
                if (item != null)
                {
                    yield return item;
                }
            }
        }
        using (context.Enter(ReferencedItemsContext.ConstraintExpression))
        {
            IEnumerable<Expression> expressions = this switch
            {
                Check check => [check.Expression],
                PgExclusion exclusion => exclusion.Elements.Select(element => element.Key)
                    .OfType<KeyPart.IndexExpression>().Select(key => key.Expression),
                _ => []
            };
            if (this is PgExclusion { Predicate: { } predicate })
            {
                expressions = expressions.Append(predicate);
            }
            foreach (Expression expression in expressions)
            {
                foreach (ItemRef item in expression.GetReferencedItems(context).OfType<ItemRef.ObjectNameRef>())
                {
                    yield return item;
                }
            }
        }
    }

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

    private void WriteTail(SqlTextWriter writer)
    {
        if (Comment != null)
        {
            writer.WriteSql($" {Comment}");
        }
        foreach (PgConstraintOption option in PgOptions ?? [])
        {
            writer.Write(" ");
            option.ToSql(writer);
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
        public PgConstraintIndexParameters? IndexParameters { get; init; }
        public IncludedColumns? IncludedColumns { get; init; }
        public BuiltInIndexMethod? IndexMethod { get; init; }
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
            if (IncludedColumns != null)
            {
                writer.WriteSql($" {IncludedColumns}");
            }
            if (Options != null)
            {
                writer.WriteSql($" WITH ({Options})");
            }
            if (IndexParameters != null)
            {
                writer.WriteSql($" {IndexParameters}");
            }
            if (StorageLocation != null)
            {
                writer.WriteSql($" {StorageLocation}");
            }
            WriteTail(writer);
        }

        public void FormatSqlBody(SqlTextWriter writer, FormatManager manager)
        {
            FormatConstraintName(writer, manager);
            writer.Write("PRIMARY KEY ");
            if (IndexOrganization != null)
            {
                writer.WriteSql($"{IndexOrganization} ");
            }
            if (IndexMethod != null)
            {
                writer.WriteSql($"USING {IndexMethod} ");
            }
            FormatKeyParts(writer, Columns, manager);
            if (IncludedColumns != null)
            {
                writer.Write(" ");
                IncludedColumns.FormatSql(writer, manager);
            }
            if (Options != null)
            {
                writer.Write(" WITH (");
                writer.FormatDelimited(Options, manager);
                writer.Write(")");
            }
            if (IndexParameters != null)
            {
                writer.Write(" ");
                IndexParameters.FormatSql(writer, manager);
            }
            if (StorageLocation != null)
            {
                writer.WriteSql($" {StorageLocation}");
            }
            WriteTail(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            FormatSqlBody(writer, manager);
        }
    }

    public record UniqueConstraint(SqlValueList<KeyPart> Columns, Identifier? Name = null) : StatementTableConstraint(Name)
    {
        public bool? NullsDistinct { get; init; }
        public PgConstraintIndexParameters? IndexParameters { get; init; }
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
            if (NullsDistinct != null)
            {
                writer.Write(NullsDistinct.Value ? " NULLS DISTINCT" : " NULLS NOT DISTINCT");
            }
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
            if (IndexParameters != null)
            {
                writer.WriteSql($" {IndexParameters}");
            }
            if (StorageLocation != null)
            {
                writer.WriteSql($" {StorageLocation}");
            }
            WriteTail(writer);
        }

        public void FormatSqlBody(SqlTextWriter writer, FormatManager manager)
        {
            FormatConstraintName(writer, manager);
            writer.Write("UNIQUE");
            if (NullsDistinct != null)
            {
                writer.Write(NullsDistinct.Value ? " NULLS DISTINCT" : " NULLS NOT DISTINCT");
            }
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
                writer.Write(" WITH (");
                writer.FormatDelimited(Options, manager);
                writer.Write(")");
            }
            if (IndexParameters != null)
            {
                writer.Write(" ");
                IndexParameters.FormatSql(writer, manager);
            }
            if (StorageLocation != null)
            {
                writer.WriteSql($" {StorageLocation}");
            }
            WriteTail(writer);
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
            WriteTail(writer);
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
            WriteTail(writer);
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
            WriteTail(writer);
        }

        public void FormatSqlBody(SqlTextWriter writer, FormatManager manager)
        {
            FormatConstraintName(writer, manager);
            writer.Write("CHECK (");
            Expression.FormatSql(writer, manager);
            writer.Write(")");
            WriteTail(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            FormatSqlBody(writer, manager);
        }
    }

    public record PgExclusion(SqlValueList<PgExclusionElement> Elements, Identifier? Name = null) : StatementTableConstraint(Name)
    {
        public PgConstraintIndexParameters? IndexParameters { get; init; }
        public IndexMethod? Method { get; init; }
        public IncludedColumns? IncludedColumns { get; init; }
        public Expression? Predicate { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            WriteConstraintName(writer);
            writer.Write("EXCLUDE");
            if (Method != null)
            {
                writer.WriteSql($" USING {Method}");
            }
            writer.WriteSql($" ({Elements})");
            if (IncludedColumns != null)
            {
                writer.WriteSql($" {IncludedColumns}");
            }
            if (IndexParameters != null)
            {
                writer.WriteSql($" {IndexParameters}");
            }
            if (Predicate != null)
            {
                writer.WriteSql($" WHERE ({Predicate})");
            }
            WriteTail(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            FormatSqlBody(writer, manager);
        }

        public void FormatSqlBody(SqlTextWriter writer, FormatManager manager)
        {
            FormatConstraintName(writer, manager);
            writer.Write("EXCLUDE");
            if (Method != null)
            {
                writer.Write(" USING ");
                Method.FormatSql(writer, manager);
            }
            writer.Write(" (");
            writer.FormatDelimited(Elements, manager);
            writer.Write(")");
            if (IncludedColumns != null)
            {
                writer.Write(" ");
                IncludedColumns.FormatSql(writer, manager);
            }
            if (IndexParameters != null)
            {
                writer.Write(" ");
                IndexParameters.FormatSql(writer, manager);
            }
            if (Predicate != null)
            {
                writer.Write(" WHERE (");
                Predicate.FormatSql(writer, manager);
                writer.Write(")");
            }
            WriteTail(writer);
        }
    }

    public record PgUsingIndex(Identifier Index, bool IsPrimaryKey, Identifier? Name = null) : StatementTableConstraint(Name)
    {
        public override void ToSql(SqlTextWriter writer)
        {
            WriteConstraintName(writer);
            writer.WriteSql($"{(IsPrimaryKey ? "PRIMARY KEY" : "UNIQUE")} USING INDEX {Index}");
            WriteTail(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            FormatSqlBody(writer, manager);
        }

        public void FormatSqlBody(SqlTextWriter writer, FormatManager manager)
        {
            FormatConstraintName(writer, manager);
            writer.Write(IsPrimaryKey ? "PRIMARY KEY USING INDEX " : "UNIQUE USING INDEX ");
            Index.FormatSql(writer, manager);
            WriteTail(writer);
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

            WriteTail(writer);
        }

        public void FormatSqlBody(SqlTextWriter writer, FormatManager manager)
        {
            WriteKeyLabel(writer, KeyLabel, false);
            if (IndexName != null)
            {
                writer.Write(" ");
                IndexName.FormatSql(writer, manager);
            }
            if (IndexOrganization != null)
            {
                writer.WriteSql($" {IndexOrganization}");
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
            WriteTail(writer);
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
            WriteTail(writer);
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
            WriteTail(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            FormatSqlBody(writer, manager);
        }
    }

    public record Spatial(SqlValueList<KeyPart> Columns, Identifier? IndexName) : NonConstraintKey(Columns, IndexName)
    {
        public BuiltInIndexMethod? IndexMethod { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("SPATIAL");
            WriteKeyLabel(writer, KeyLabel, true);
            if (IndexName != null)
            {
                writer.WriteSql($" {IndexName}");
            }
            writer.WriteSql($" ({Columns})");
            if (IndexMethod != null)
            {
                writer.WriteSql($" USING {IndexMethod}");
            }
            WriteTail(writer);
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
            if (IndexMethod != null)
            {
                writer.WriteSql($" USING {IndexMethod}");
            }
            WriteTail(writer);
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

            WriteTail(writer);
        }

        public void FormatSqlBody(SqlTextWriter writer, FormatManager manager)
        {
            WriteKeyLabel(writer, KeyLabel, false);
            if (IndexName != null)
            {
                writer.Write(" ");
                IndexName.FormatSql(writer, manager);
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
            WriteTail(writer);
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
