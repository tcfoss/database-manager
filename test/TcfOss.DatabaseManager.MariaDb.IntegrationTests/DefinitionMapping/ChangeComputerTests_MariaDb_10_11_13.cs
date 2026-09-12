using TcfOss.DatabaseManager.MySql.IntegrationTests;
using Testcontainers.MariaDb;

namespace TcfOss.DatabaseManager.MariaDb.IntegrationTests.DefinitionMapping;

// ReSharper disable once UnusedType.Global
public class ChangeComputerTests_MariaDb_10_11_13(ITestOutputHelper testOutputHelper)
    : ChangeComputerTestsMaBase(testOutputHelper)
{
    protected override MariaDbBuilder Configure()
    {
        return new MariaDbBuilder("mariadb:10.11.13")
            .WithStandardOptions();
    }
}
