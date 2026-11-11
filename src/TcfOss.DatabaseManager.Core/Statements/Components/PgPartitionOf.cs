using TcfOss.DatabaseManager.Core.Common;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public record PgPartitionOf(ObjectName Parent, PgPartitionBound Bound);
