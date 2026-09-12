using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.MySql.IntegrationTests.DefinitionMapping;
using Testcontainers.MariaDb;

namespace TcfOss.DatabaseManager.MariaDb.IntegrationTests.DefinitionMapping;

public abstract class ChangeComputerTestsMaBase(ITestOutputHelper testOutputHelper)
    : ChangeComputerTests<MariaDbBuilder, MariaDbContainer>(testOutputHelper)
{
    protected override SqlDialect Dialect => SqlDialect.MariaDb;
}
