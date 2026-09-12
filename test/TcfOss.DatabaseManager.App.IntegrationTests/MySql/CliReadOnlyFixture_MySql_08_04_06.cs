using TcfOss.DatabaseManager.MySql.IntegrationTests.DatabaseFixtures;
using Testcontainers.MySql;
using Xunit.Sdk;

namespace TcfOss.DatabaseManager.App.IntegrationTests.MySql;

// ReSharper disable ClassNeverInstantiated.Global
public class CliReadOnlyFixture_MySql_08_04_06 : CliReadOnlyFixture<MySqlBuilder, MySqlContainer>
{
    public CliReadOnlyFixture_MySql_08_04_06(IMessageSink messageSink)
    {
        DbFixture = new MySqlFixture_08_04_06(messageSink);
    }
}
