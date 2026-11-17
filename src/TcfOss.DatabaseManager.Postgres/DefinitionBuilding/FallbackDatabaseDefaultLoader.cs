using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Postgres.Configuration;

namespace TcfOss.DatabaseManager.Postgres.DefinitionBuilding;

public partial class FallbackDatabaseDefaultLoader(ILogger logger)
{
    public PgServerDefaults GetServerDefaults()
    {
        s_logUsingFallbackDatabaseDefaults(logger);
        return new PgServerDefaults
        {
            TableAccessMethod = new Identifier("heap"),
            CatalogTablespace = new Identifier("pg_default"),
            Tablespace = null
        };
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Using fallback PostgreSQL defaults (heap table access method, pg_default catalog tablespace, no default_tablespace override). These may not match the actual server defaults.")]
    private static partial void s_logUsingFallbackDatabaseDefaults(ILogger logger);
}
