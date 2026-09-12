using TcfOss.DatabaseManager.MariaDb.IntegrationTests.DatabaseFixtures;
using Testcontainers.MariaDb;
using Xunit.Sdk;

namespace TcfOss.DatabaseManager.MariaDb.IntegrationTests.DefinitionMapping;

// ReSharper disable ClassNeverInstantiated.Global
public class DifferFixture_MariaDb_10_11_13 : DifferFixtureMaBase<MariaDbBuilder, MariaDbContainer>
{
    public DifferFixture_MariaDb_10_11_13(IMessageSink messageSink)
    {
        DbFixture = new MariaDbFixture_10_11_13(messageSink);
    }
}
