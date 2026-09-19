using TcfOss.DatabaseManager.App.IntegrationTests.MySqlFamily;
using TcfOss.DatabaseManager.MySql.IntegrationTests.DatabaseFixtures;
using Testcontainers.MySql;
using Xunit.Sdk;

namespace TcfOss.DatabaseManager.App.IntegrationTests.MySql;

// ReSharper disable ClassNeverInstantiated.Global
public class CliReadOnlyFixture_MySql_08_04 : CliReadOnlyFixture<MySqlBuilder, MySqlContainer>
{
    public CliReadOnlyFixture_MySql_08_04(IMessageSink messageSink)
    {
        DbFixture = new MySqlFixture_08_04(messageSink);
    }
}
