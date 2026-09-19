using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.IntegrationTests;
using Testcontainers.MySql;

namespace TcfOss.DatabaseManager.MySql.IntegrationTests.DefinitionMapping;

// ReSharper disable once UnusedMember.Global
public class ChangeComputerTests_MySql_09_07(ITestOutputHelper testOutputHelper)
    : ChangeComputerTests<MySqlBuilder, MySqlContainer>(testOutputHelper)
{
    protected override SqlDialect Dialect => SqlDialect.MySql;

    protected override MySqlBuilder Configure()
    {
        return new MySqlBuilder(FixtureImages.MySql_09_07)
            .WithStandardOptions();
    }
}
