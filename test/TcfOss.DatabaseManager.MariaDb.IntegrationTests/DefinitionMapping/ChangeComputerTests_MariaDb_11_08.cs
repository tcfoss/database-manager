using TcfOss.DatabaseManager.Core.IntegrationTests;
using TcfOss.DatabaseManager.MySql.IntegrationTests;
using Testcontainers.MariaDb;

namespace TcfOss.DatabaseManager.MariaDb.IntegrationTests.DefinitionMapping;

// ReSharper disable once UnusedType.Global
// ReSharper disable once UnusedMember.Global
public class ChangeComputerTests_MariaDb_11_08(ITestOutputHelper testOutputHelper)
    : ChangeComputerTestsMaBase(testOutputHelper)
{
    protected override MariaDbBuilder Configure()
    {
        return new MariaDbBuilder(FixtureImages.MariaDb_11_08)
            .WithStandardOptions();
    }
}
