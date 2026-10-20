using TcfOss.DatabaseManager.App.IntegrationTests.MySqlFamily;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;

namespace TcfOss.DatabaseManager.App.IntegrationTests.MySql;

// ReSharper disable once UnusedMember.Global
public class CliReadOnlyTests_MySql_NoDatabase(
    CliReadOnlyFixture_NoDatabase fixture)
    : CliReadOnlyTests_NoDatabase(fixture)
{
    protected override SqlDialect Dialect => SqlDialect.MySql;
}
