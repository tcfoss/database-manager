using TcfOss.DatabaseManager.Core.Common;

namespace TcfOss.DatabaseManager.Postgres.Configuration;

public record PgServerDefaults
{
    public required Identifier TableAccessMethod { get; init; }
    public required Identifier CatalogTablespace { get; init; }
    // Null represents an empty default_tablespace setting, retaining PostgreSQL's inheritance rules.
    public required Identifier? Tablespace { get; init; }
}
