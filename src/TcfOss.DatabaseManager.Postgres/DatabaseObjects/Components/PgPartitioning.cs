using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

public record PgPartitioning(PgPartitionStrategy Strategy, SqlValueList<PgPartitioning.Key> Keys)
{
    public PgPartitionBy ToStatementPartitionBy()
    {
        return new PgPartitionBy(Strategy, Keys.Map(key => new PgPartitionKey(key.Expression, key.OperatorClass)));
    }

    public record Key(Expression Expression, ObjectName? OperatorClass = null);
}
