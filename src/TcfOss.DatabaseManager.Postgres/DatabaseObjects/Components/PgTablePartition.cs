using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

public record PgTablePartition(ObjectIdentifier Parent, PgPartitionBounds Bound)
{
    public PgPartitionOf ToStatementPartitionOf(bool includeSchema)
    {
        return new PgPartitionOf(Parent.ToObjectName(includeSchema ? 2 : 1), Bound.ToStatementBound());
    }
}
