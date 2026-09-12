using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using Testcontainers.MySql;

namespace TcfOss.DatabaseManager.MySql.IntegrationTests.DefinitionMapping;

// ReSharper disable once UnusedMember.Global
public class ChangeComputerTests_MySql_08_04_06(ITestOutputHelper testOutputHelper)
    : ChangeComputerTests<MySqlBuilder, MySqlContainer>(testOutputHelper)
{
    protected override SqlDialect Dialect => SqlDialect.MySql;

    protected override MySqlBuilder Configure()
    {
        return new MySqlBuilder("mysql:8.4.6")
            .WithStandardOptions();
    }
}
