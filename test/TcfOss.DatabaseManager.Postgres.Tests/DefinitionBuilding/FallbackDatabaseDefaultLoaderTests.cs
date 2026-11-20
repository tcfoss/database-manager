using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.Tests;
using TcfOss.DatabaseManager.Postgres.Configuration;
using TcfOss.DatabaseManager.Postgres.DefinitionBuilding;

namespace TcfOss.DatabaseManager.Postgres.Tests.DefinitionBuilding;

public class FallbackDatabaseDefaultLoaderTests
{
    [Fact]
    public void GetServerDefaults_ReportsOfflineAssumptions()
    {
        var logger = new TestLogger();
        var loader = new FallbackDatabaseDefaultLoader(logger);

        PgServerDefaults defaults = loader.GetServerDefaults();

        Assert.Equal("heap", defaults.TableAccessMethod.Name);
        Assert.Equal("pg_default", defaults.CatalogTablespace.Name);
        Assert.Null(defaults.Tablespace);
        TestLogger.LogEntry warning = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.Contains("may not match", warning.Message);
        Assert.Contains(defaults.TableAccessMethod.Name, warning.Message);
        Assert.Contains(defaults.CatalogTablespace.Name, warning.Message);
        Assert.Contains("no default_tablespace override", warning.Message);
    }
}
