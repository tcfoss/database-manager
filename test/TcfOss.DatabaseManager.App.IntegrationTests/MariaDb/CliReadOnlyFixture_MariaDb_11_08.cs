using TcfOss.DatabaseManager.MariaDb.IntegrationTests.DatabaseFixtures;
using Testcontainers.MariaDb;
using Xunit.Sdk;

namespace TcfOss.DatabaseManager.App.IntegrationTests.MariaDb;

// ReSharper disable ClassNeverInstantiated.Global
public class CliReadOnlyFixture_MariaDb_11_08 : CliReadOnlyFixture<MariaDbBuilder, MariaDbContainer>
{
    public CliReadOnlyFixture_MariaDb_11_08(IMessageSink messageSink)
    {
        DbFixture = new MariaDbFixture_11_08_02(messageSink);
    }

}
