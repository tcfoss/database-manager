using TcfOss.DatabaseManager.App.IntegrationTests.MySqlFamily;
using TcfOss.DatabaseManager.MariaDb.IntegrationTests.DatabaseFixtures;
using Testcontainers.MariaDb;
using Xunit.Sdk;

namespace TcfOss.DatabaseManager.App.IntegrationTests.MariaDb;

// ReSharper disable ClassNeverInstantiated.Global
public class CliReadOnlyFixture_MariaDb_12_03 : CliReadOnlyFixture<MariaDbBuilder, MariaDbContainer>
{
    public CliReadOnlyFixture_MariaDb_12_03(IMessageSink messageSink)
    {
        DbFixture = new MariaDbFixture_12_03(messageSink);
    }

}
