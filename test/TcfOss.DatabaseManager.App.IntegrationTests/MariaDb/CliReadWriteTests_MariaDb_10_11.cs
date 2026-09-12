using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.MySql.IntegrationTests;
using Testcontainers.MariaDb;

namespace TcfOss.DatabaseManager.App.IntegrationTests.MariaDb;

// ReSharper disable once UnusedMember.Global
public class CliReadWriteTests_MariaDb_10_11(ITestOutputHelper testOutputHelper)
    : CliReadWriteTests<MariaDbBuilder, MariaDbContainer>(testOutputHelper)
{
    protected override SqlDialect Dialect => SqlDialect.MariaDb;

    protected override MariaDbBuilder Configure()
    {
        return new MariaDbBuilder("mariadb:10.11.13")
            .WithStandardOptions();
    }
}
