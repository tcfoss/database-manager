using TcfOss.DatabaseManager.MySql.IntegrationTests;
using Testcontainers.MariaDb;

namespace TcfOss.DatabaseManager.MariaDb.IntegrationTests.DefinitionMapping;

// ReSharper disable once UnusedType.Global
public class ChangeComputerTests_MariaDb_11_08_02(ITestOutputHelper testOutputHelper)
    : ChangeComputerTestsMaBase(testOutputHelper)
{
    protected override MariaDbBuilder Configure()
    {
        return new MariaDbBuilder("mariadb:11.8.2")
            .WithStandardOptions();
    }
}
