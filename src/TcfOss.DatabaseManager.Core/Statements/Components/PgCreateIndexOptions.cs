using TcfOss.DatabaseManager.Core.Common;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public record PgCreateIndexOptions(bool Concurrently, bool Only)
{
    public bool? NullsDistinct { get; init; }
    public SqlValueList<PgStorageParameter>? StorageParameters { get; init; }
    public Identifier? Tablespace { get; init; }
}
