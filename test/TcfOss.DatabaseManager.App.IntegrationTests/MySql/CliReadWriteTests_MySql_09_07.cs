using TcfOss.DatabaseManager.App.IntegrationTests.MySqlFamily;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.IntegrationTests;
using TcfOss.DatabaseManager.MySql.IntegrationTests;

using Testcontainers.MySql;

namespace TcfOss.DatabaseManager.App.IntegrationTests.MySql;

// ReSharper disable once UnusedMember.Global
public class CliReadWriteTests_MySql_09_07(ITestOutputHelper testOutputHelper)
    : CliReadWriteTests<MySqlBuilder, MySqlContainer>(testOutputHelper)
{
    protected override SqlDialect Dialect => SqlDialect.MySql;

    protected override MySqlBuilder Configure()
    {
        return new MySqlBuilder(FixtureImages.MySql_09_07)
            .WithStandardOptions();
    }
}
