using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

public record PgIndex(SqlValueList<KeyPart> Columns)
{
    public required Identifier Name { get; init; }
    public bool NameExplicit { get; init; } = true;
    public Identifier? Tablespace { get; init; }
    public bool Unique { get; init; }
    public IndexMethod Method { get; init; } = IndexMethod.Default;
    public Expression? Predicate { get; init; }
    public IncludedColumns? IncludedColumns { get; init; }

    public virtual bool Equals(PgIndex? other)
    {
        return other is not null && EqualityContract == other.EqualityContract
            && Name == other.Name && Tablespace == other.Tablespace
            && Unique == other.Unique && Method == other.Method && Predicate == other.Predicate
            && IncludedColumns == other.IncludedColumns && Columns.Equals(other.Columns);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(EqualityContract, Name, Tablespace, Unique, Method, Predicate, IncludedColumns, Columns);
    }

    public CreateIndex ToCreateStatement(ObjectIdentifier table, bool includeSchema)
    {
        ObjectName tableName = table.ToObjectName(includeSchema ? 2 : 1);
        return Unique
            ? new CreateIndex.Unique([.. Columns], Name, tableName)
            {
                IndexMethod = Method,
                IndexMethodAfterTableName = true,
                IncludedColumns = IncludedColumns,
                Filter = Predicate,
                PgOptions = Tablespace == null ? null : new PgCreateIndexOptions(false, false) { Tablespace = Tablespace }
            }
            : new CreateIndex.Standard([.. Columns], Name, tableName)
            {
                IndexMethod = Method,
                IndexMethodAfterTableName = true,
                IncludedColumns = IncludedColumns,
                Filter = Predicate,
                PgOptions = Tablespace == null ? null : new PgCreateIndexOptions(false, false) { Tablespace = Tablespace }
            };
    }
}
