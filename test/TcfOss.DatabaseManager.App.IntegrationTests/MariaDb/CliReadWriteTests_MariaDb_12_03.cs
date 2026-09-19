using TcfOss.DatabaseManager.App.IntegrationTests.MySqlFamily;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.IntegrationTests;
using TcfOss.DatabaseManager.MySql.IntegrationTests;
using Testcontainers.MariaDb;

namespace TcfOss.DatabaseManager.App.IntegrationTests.MariaDb;

// ReSharper disable once UnusedMember.Global
public class CliReadWriteTests_MariaDb_12_03(ITestOutputHelper testOutputHelper)
    : CliReadWriteTests<MariaDbBuilder, MariaDbContainer>(testOutputHelper)
{
    protected override SqlDialect Dialect => SqlDialect.MariaDb;

    protected override MariaDbBuilder Configure()
    {
        return new MariaDbBuilder(FixtureImages.MariaDb_12_03)
            .WithStandardOptions();
    }
}
