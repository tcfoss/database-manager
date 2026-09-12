using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.MySql.IntegrationTests;
using Testcontainers.MariaDb;

namespace TcfOss.DatabaseManager.App.IntegrationTests.MariaDb;

// ReSharper disable once UnusedMember.Global
public class CliReadWriteTests_MariaDb_11_08_02(ITestOutputHelper testOutputHelper)
    : CliReadWriteTests<MariaDbBuilder, MariaDbContainer>(testOutputHelper)
{
    protected override SqlDialect Dialect => SqlDialect.MariaDb;

    protected override MariaDbBuilder Configure()
    {
        TestOutputHelper.WriteLine("Configuring MariaDbBuilder for mariadb:11.8.2");
        return new MariaDbBuilder("mariadb:11.8.2")
            .WithStandardOptions();
    }
}
