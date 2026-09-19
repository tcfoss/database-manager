using TcfOss.DatabaseManager.Core.IntegrationTests;
using TcfOss.DatabaseManager.MySql.IntegrationTests;
using TcfOss.DatabaseManager.MySql.IntegrationTests.DatabaseFixtures;
using Testcontainers.MariaDb;
using Xunit.Sdk;

namespace TcfOss.DatabaseManager.MariaDb.IntegrationTests.DatabaseFixtures;

public class MariaDbFixture_10_11(IMessageSink messageSink)
    : DbFixture<MariaDbBuilder, MariaDbContainer>(messageSink)
{
    protected override ushort Port => MariaDbBuilder.MariaDbPort;

    protected override MariaDbBuilder Configure()
    {
        return new MariaDbBuilder(FixtureImages.MariaDb_10_11)
            .WithStandardOptions();
    }
}
