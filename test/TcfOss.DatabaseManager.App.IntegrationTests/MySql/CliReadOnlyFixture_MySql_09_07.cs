using TcfOss.DatabaseManager.App.IntegrationTests.MySqlFamily;
using TcfOss.DatabaseManager.MySql.IntegrationTests.DatabaseFixtures;
using Testcontainers.MySql;
using Xunit.Sdk;

namespace TcfOss.DatabaseManager.App.IntegrationTests.MySql;

// ReSharper disable ClassNeverInstantiated.Global
public class CliReadOnlyFixture_MySql_09_07 : CliReadOnlyFixture<MySqlBuilder, MySqlContainer>
{
    public CliReadOnlyFixture_MySql_09_07(IMessageSink messageSink)
    {
        DbFixture = new MySqlFixture_09_07(messageSink);
    }
}
