using TcfOss.DatabaseManager.App.IntegrationTests.MySqlFamily;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;

namespace TcfOss.DatabaseManager.App.IntegrationTests.MySql;

// ReSharper disable once UnusedMember.Global
public class CliReadWriteTests_MySql_NoDatabase(ITestOutputHelper testOutputHelper)
    : CliReadWriteTests_NoDatabase(testOutputHelper)
{
    protected override SqlDialect Dialect => SqlDialect.MySql;
}
