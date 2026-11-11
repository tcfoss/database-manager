using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.Formatting;
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
    public bool? IndexMethodAfterTableName { get; init; }
    public PgCreateIndexOptions? PgOptions { get; init; }

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
        if (PgOptions?.Concurrently == true)
        {
            writer.Write(" CONCURRENTLY");
        }
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
        using (context.Enter(ReferencedItemsContext.CreateIndexExpression))
        {
            IEnumerable<Expression> expressions = Columns.OfType<KeyPart.IndexExpression>().Select(key => key.Expression);
            Expression? filter = this switch
            {
                Standard standard => standard.Filter,
                Unique unique => unique.Filter,
                _ => null
            };
            if (filter != null)
            {
                expressions = expressions.Append(filter);
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

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        var standard = this as Standard;
        var unique = this as Unique;
        IndexMethod? method = standard?.IndexMethod ?? unique?.IndexMethod;
        IndexOrganization? organization = standard?.IndexOrganization ?? unique?.IndexOrganization;
        IncludedColumns? included = standard?.IncludedColumns ?? unique?.IncludedColumns;
        Expression? filter = standard?.Filter ?? unique?.Filter;
        SqlValueList<StatementIndexOption>? options = standard?.Options ?? unique?.Options;
        IndexStorageLocation? storage = standard?.StorageLocation ?? unique?.StorageLocation;
        Comment? comment = standard?.Comment ?? unique?.Comment ?? (this as FullText)?.Comment ?? (this as Spatial)?.Comment;

        Meta.FormatPreNonSql(writer, manager);
        writer.Write(manager.Indent);
        WritePrefix(writer, this switch { Unique => "UNIQUE", FullText => "FULLTEXT", Spatial => "SPATIAL", _ => null }, organization);
        if (Name != null)
        {
            writer.Write(" ");
            Name.FormatSql(writer, manager);
        }
        if (method != null && IndexMethodAfterTableName != true)
        {
            writer.Write(" USING ");
            method.FormatSql(writer, manager);
        }
        writer.Write(PgOptions?.Only == true ? " ON ONLY " : " ON ");
        TableName.FormatSql(writer, manager);
        if (method != null && IndexMethodAfterTableName == true)
        {
            writer.Write(" USING ");
            method.FormatSql(writer, manager);
        }
        writer.Write(" (");
        writer.FormatDelimited(Columns, manager);
        writer.Write(")");
        if (included != null)
        {
            writer.Write(" ");
            included.FormatSql(writer, manager);
        }
        WriteNullsDistinct(writer);
        if (filter != null && PgOptions == null)
        {
            writer.Write(" WHERE ");
            filter.FormatSql(writer, manager);
        }
        if (options.SafeAny())
        {
            writer.WriteSql($" WITH ({options})");
        }
        if (storage != null)
        {
            writer.WriteSql($" {storage}");
        }
        if (PgOptions?.StorageParameters != null)
        {
            writer.Write(" WITH (");
            writer.FormatDelimited(PgOptions.StorageParameters, manager);
            writer.Write(")");
        }
        if (PgOptions?.Tablespace != null)
        {
            writer.Write(" TABLESPACE ");
            PgOptions.Tablespace.FormatSql(writer, manager);
        }
        if (filter != null && PgOptions != null)
        {
            writer.Write(" WHERE ");
            filter.FormatSql(writer, manager);
        }
        if (comment != null)
        {
            writer.WriteSql($" {comment}");
        }
        Meta.FormatPostNonSql(writer, manager);
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
            if (Name != null)
            {
                writer.WriteSql($" {Name}");
            }
            if (IndexMethod != null && IndexMethodAfterTableName != true)
            {
                writer.WriteSql($" USING {IndexMethod}");
            }
            writer.Write(" ON ");
            if (PgOptions?.Only == true)
            {
                writer.Write("ONLY ");
            }
            TableName.ToSql(writer);
            if (IndexMethod != null && IndexMethodAfterTableName == true)
            {
                writer.WriteSql($" USING {IndexMethod}");
            }
            writer.WriteSql($" ({Columns})");
            if (IncludedColumns != null)
            {
                writer.WriteSql($" {IncludedColumns}");
            }
            WriteNullsDistinct(writer);
            if (Filter != null && PgOptions == null)
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
            WritePgTail(writer);
            if (Filter != null && PgOptions != null)
            {
                writer.WriteSql($" WHERE {Filter}");
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
            if (Name != null)
            {
                writer.WriteSql($" {Name}");
            }
            if (IndexMethod != null && IndexMethodAfterTableName != true)
            {
                writer.WriteSql($" USING {IndexMethod}");
            }
            writer.Write(" ON ");
            if (PgOptions?.Only == true)
            {
                writer.Write("ONLY ");
            }
            TableName.ToSql(writer);
            if (IndexMethod != null && IndexMethodAfterTableName == true)
            {
                writer.WriteSql($" USING {IndexMethod}");
            }
            writer.WriteSql($" ({Columns})");
            if (IncludedColumns != null)
            {
                writer.WriteSql($" {IncludedColumns}");
            }
            WriteNullsDistinct(writer);
            if (Filter != null && PgOptions == null)
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
            WritePgTail(writer);
            if (Filter != null && PgOptions != null)
            {
                writer.WriteSql($" WHERE {Filter}");
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

    private void WriteNullsDistinct(SqlTextWriter writer)
    {
        if (PgOptions?.NullsDistinct != null)
        {
            writer.Write(PgOptions.NullsDistinct.Value ? " NULLS DISTINCT" : " NULLS NOT DISTINCT");
        }
    }

    private void WritePgTail(SqlTextWriter writer)
    {
        if (PgOptions?.StorageParameters != null)
        {
            writer.WriteSql($" WITH ({PgOptions.StorageParameters})");
        }
        if (PgOptions?.Tablespace != null)
        {
            writer.WriteSql($" TABLESPACE {PgOptions.Tablespace}");
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
