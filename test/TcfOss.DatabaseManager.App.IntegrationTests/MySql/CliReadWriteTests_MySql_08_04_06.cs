using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.MySql.IntegrationTests;

using Testcontainers.MySql;

namespace TcfOss.DatabaseManager.App.IntegrationTests.MySql;

// ReSharper disable once UnusedMember.Global
public class CliReadWriteTests_MySql_08_04_06(ITestOutputHelper testOutputHelper)
    : CliReadWriteTests<MySqlBuilder, MySqlContainer>(testOutputHelper)
{
    protected override SqlDialect Dialect => SqlDialect.MySql;

    protected override MySqlBuilder Configure()
    {
        return new MySqlBuilder("mysql:8.4.6")
            .WithStandardOptions();
    }
}
