using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Statements;

namespace TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

public record PgIndex(SqlValueList<KeyPart> Columns)
{
    public required Identifier Name { get; init; }
    public bool Unique { get; init; }
    public IndexMethod Method { get; init; } = IndexMethod.Btree;
    public Expression? Predicate { get; init; }
    public IncludedColumns? IncludedColumns { get; init; }

    public CreateIndex ToCreateStatement(ObjectIdentifier table, bool includeSchema)
    {
        ObjectName tableName = table.ToObjectName(includeSchema ? 2 : 1);
        return Unique
            ? new CreateIndex.Unique([.. Columns], Name, tableName)
            {
                IndexMethod = Method,
                IndexMethodAfterTableName = true,
                IncludedColumns = IncludedColumns,
                Filter = Predicate
            }
            : new CreateIndex.Standard([.. Columns], Name, tableName)
            {
                IndexMethod = Method,
                IndexMethodAfterTableName = true,
                IncludedColumns = IncludedColumns,
                Filter = Predicate
            };
    }
}
