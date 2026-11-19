using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

public record PgIndex(SqlValueList<PgKeyPart> Columns)
{
    public required Identifier Name { get; init; }
    public bool NameExplicit { get; init; } = true;
    public Identifier? Tablespace { get; init; }
    public bool Unique { get; init; }
    public IndexMethod Method { get; init; } = IndexMethod.Default;
    public Expression? Predicate { get; init; }
    public IncludedColumns? IncludedColumns { get; init; }
    public uint? FillFactor { get; init; }
    public PgIndexBufferingMode? Buffering { get; init; }
    public bool? DeduplicateItems { get; init; }

    public virtual bool Equals(PgIndex? other)
    {
        return other is not null && EqualityContract == other.EqualityContract
            && Name == other.Name && Tablespace == other.Tablespace
            && Unique == other.Unique && Method == other.Method && Predicate == other.Predicate
            && IncludedColumns == other.IncludedColumns && Columns.Equals(other.Columns)
            && FillFactor == other.FillFactor && Buffering == other.Buffering
            && DeduplicateItems == other.DeduplicateItems;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(
            HashCode.Combine(EqualityContract, Name, Tablespace, Unique, Method, Predicate, IncludedColumns, Columns),
            FillFactor, Buffering, DeduplicateItems);
    }

    public SqlValueList<StatementIndexOption>? ToStatementIndexOptions()
    {
        SqlValueList<StatementIndexOption> options = [];
        if (FillFactor is { } fillFactor)
        {
            options.Add(new StatementIndexOption.FillFactor(fillFactor));
        }
        if (Buffering is { } buffering)
        {
            options.Add(new StatementIndexOption.PgBuffering(buffering));
        }
        if (DeduplicateItems is { } deduplicateItems)
        {
            options.Add(new StatementIndexOption.PgDeduplicateItems(deduplicateItems));
        }
        return options.Count == 0 ? null : options;
    }

    public CreateIndex ToCreateStatement(ObjectIdentifier table, bool includeSchema)
    {
        ObjectName tableName = table.ToObjectName(includeSchema ? 2 : 1);
        return Unique
            ? new CreateIndex.Unique(Columns.Map(column => column.ToStatementKeyPart()), Name, tableName)
            {
                IndexMethod = Method,
                IndexMethodAfterTableName = true,
                IncludedColumns = IncludedColumns,
                Filter = Predicate,
                Options = ToStatementIndexOptions(),
                PgOptions = Tablespace == null ? null : new PgCreateIndexOptions(false, false) { Tablespace = Tablespace }
            }
            : new CreateIndex.Standard(Columns.Map(column => column.ToStatementKeyPart()), Name, tableName)
            {
                IndexMethod = Method,
                IndexMethodAfterTableName = true,
                IncludedColumns = IncludedColumns,
                Filter = Predicate,
                Options = ToStatementIndexOptions(),
                PgOptions = Tablespace == null ? null : new PgCreateIndexOptions(false, false) { Tablespace = Tablespace }
            };
    }
}
