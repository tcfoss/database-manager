using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;
using CreateOrLabel = TcfOss.DatabaseManager.Core.Statements.LabelAttributes.CreateOrLabel;

namespace TcfOss.DatabaseManager.Core.Statements;

public abstract record CreateIndex(ObjectName TableName, SqlValueList<KeyPart> Columns, Identifier? Name) : Statement
{
    public bool IfNotExists { get; init; }
    public CreateOrLabel? CreateOrLabel { get; init; }

    private void WritePrefix(SqlTextWriter writer, string? kindKeyword, IndexOrganization? indexOrganization)
    {
        writer.Write("CREATE");
        if (CreateOrLabel != null)
        {
            writer.WriteSql($" {CreateOrLabel}");
        }
        if (kindKeyword != null)
        {
            writer.Write($" {kindKeyword}");
        }
        if (indexOrganization != null)
        {
            writer.WriteSql($" {indexOrganization}");
        }
        writer.Write(" INDEX");
        if (IfNotExists)
        {
            writer.Write(" IF NOT EXISTS");
        }
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        using (context.Enter(ReferencedItemsContext.CreateIndexTable))
        {
            ItemRef? item = context.CreateObjectRef(TableName);
            if (item != null)
            {
                yield return item;
            }
        }
    }

    public record Standard(SqlValueList<KeyPart> Columns, Identifier? Name, ObjectName TableName) : CreateIndex(TableName, Columns, Name)
    {
        public IndexMethod? IndexMethod { get; init; }
        public IndexOrganization? IndexOrganization { get; init; }
        public IncludedColumns? IncludedColumns { get; init; }
        public Expression? Filter { get; init; }
        public SqlValueList<StatementIndexOption>? Options { get; init; }
        public IndexStorageLocation? StorageLocation { get; init; }
        public Comment? Comment { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            WritePrefix(writer, null, IndexOrganization);
            writer.WriteSql($" {Name}");
            if (IndexMethod != null)
            {
                writer.WriteSql($" USING {IndexMethod}");
            }
            writer.WriteSql($" ON {TableName} ({Columns})");
            if (IncludedColumns != null)
            {
                writer.WriteSql($" {IncludedColumns}");
            }
            if (Filter != null)
            {
                writer.WriteSql($" WHERE {Filter}");
            }

            if (Options.SafeAny())
            {
                writer.WriteSql($" WITH ({Options})");
            }
            if (StorageLocation != null)
            {
                writer.WriteSql($" {StorageLocation}");
            }
            if (Comment != null)
            {
                writer.WriteSql($" {Comment}");
            }
        }

        public StatementTableConstraint.Standard ToStatementConstraint()
        {
            return new StatementTableConstraint.Standard(Columns, Name)
            {
                IndexMethod = IndexMethod,
                IndexOrganization = IndexOrganization,
                Comment = Comment,
                KeyLabel = KeyLabel.Key,
                IncludedColumns = IncludedColumns,
                Filter = Filter,
                Options = Options,
                StorageLocation = StorageLocation,
            };
        }
    }

    public record Unique(SqlValueList<KeyPart> Columns, Identifier? Name, ObjectName TableName) : CreateIndex(TableName, Columns, Name)
    {
        public IndexMethod? IndexMethod { get; init; }
        public IndexOrganization? IndexOrganization { get; init; }
        public IncludedColumns? IncludedColumns { get; init; }
        public Expression? Filter { get; init; }
        public SqlValueList<StatementIndexOption>? Options { get; init; }
        public IndexStorageLocation? StorageLocation { get; init; }
        public Comment? Comment { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            WritePrefix(writer, "UNIQUE", IndexOrganization);
            writer.WriteSql($" {Name}");
            if (IndexMethod != null)
            {
                writer.WriteSql($" USING {IndexMethod}");
            }
            writer.WriteSql($" ON {TableName} ({Columns})");
            if (IncludedColumns != null)
            {
                writer.WriteSql($" {IncludedColumns}");
            }
            if (Filter != null)
            {
                writer.WriteSql($" WHERE {Filter}");
            }
            if (Options.SafeAny())
            {
                writer.WriteSql($" WITH ({Options})");
            }
            if (StorageLocation != null)
            {
                writer.WriteSql($" {StorageLocation}");
            }
            if (Comment != null)
            {
                writer.WriteSql($" {Comment}");
            }
        }

        public StatementTableConstraint.UniqueConstraint ToStatementConstraint()
        {
            if (Filter != null)
            {
                throw new InvalidOperationException("Unique indexes with filters cannot be represented as unique constraints.");
            }
            return new StatementTableConstraint.UniqueConstraint(Columns, Name)
            {
                IndexMethod = IndexMethod,
                Comment = Comment,
                IndexOrganization = IndexOrganization,
                KeyLabel = KeyLabel.Key,
                IncludedColumns = IncludedColumns,
                Options = Options,
                StorageLocation = StorageLocation
            };
        }

        public StatementTableConstraint.UniqueIndex ToStatementIndex()
        {
            if (Filter != null)
            {
                throw new InvalidOperationException("Unique indexes with filters cannot be represented as unique constraints.");
            }
            return new StatementTableConstraint.UniqueIndex(Columns, Name)
            {
                IndexMethod = IndexMethod,
                Comment = Comment,
                IndexOrganization = IndexOrganization,
                KeyLabel = KeyLabel.Key,
                IncludedColumns = IncludedColumns,
                Filter = Filter,
                Options = Options,
                StorageLocation = StorageLocation
            };
        }
    }

    public record FullText(SqlValueList<KeyPart> Columns, Identifier? Name, ObjectName TableName) : CreateIndex(TableName, Columns, Name)
    {
        public Comment? Comment { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            WritePrefix(writer, "FULLTEXT", null);
            writer.WriteSql($" {Name} ON {TableName} ({Columns})");
            if (Comment != null)
            {
                writer.WriteSql($" {Comment}");
            }
        }

        public StatementTableConstraint.FullText ToStatementConstraint()
        {
            return new StatementTableConstraint.FullText(Columns, Name)
            {
                Comment = Comment,
                KeyLabel = KeyLabel.Key
            };
        }
    }

    public record Spatial(SqlValueList<KeyPart> Columns, Identifier? Name, ObjectName TableName) : CreateIndex(TableName, Columns, Name)
    {
        public Comment? Comment { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            WritePrefix(writer, "SPATIAL", null);
            writer.WriteSql($" {Name} ON {TableName} ({Columns})");
            if (Comment != null)
            {
                writer.WriteSql($" {Comment}");
            }
        }

        public StatementTableConstraint.Spatial ToStatementConstraint()
        {
            return new StatementTableConstraint.Spatial(Columns, Name)
            {
                Comment = Comment,
                KeyLabel = KeyLabel.Key
            };
        }
    }
}
