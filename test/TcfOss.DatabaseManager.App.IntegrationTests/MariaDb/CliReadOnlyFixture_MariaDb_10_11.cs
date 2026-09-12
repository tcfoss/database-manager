using TcfOss.DatabaseManager.MariaDb.IntegrationTests.DatabaseFixtures;
using Testcontainers.MariaDb;
using Xunit.Sdk;

namespace TcfOss.DatabaseManager.App.IntegrationTests.MariaDb;

// ReSharper disable ClassNeverInstantiated.Global
public class CliReadOnlyFixture_MariaDb_10_11 : CliReadOnlyFixture<MariaDbBuilder, MariaDbContainer>
{
    public CliReadOnlyFixture_MariaDb_10_11(IMessageSink messageSink)
    {
        DbFixture = new MariaDbFixture_10_11_13(messageSink);
    }

}
