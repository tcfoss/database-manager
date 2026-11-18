using TcfOss.DatabaseManager.Core.Common;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public record PgCreateTableOptions(bool Unlogged, PgOnCommit? OnCommit)
{
    public PgPartitionBy? PartitionBy { get; init; }
    public PgPartitionOf? PartitionOf { get; init; }
    public Identifier? AccessMethod { get; init; }
    public Identifier? Tablespace { get; init; }
}
