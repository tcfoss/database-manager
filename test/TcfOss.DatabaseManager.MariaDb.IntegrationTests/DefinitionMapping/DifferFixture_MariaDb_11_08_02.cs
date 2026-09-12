using TcfOss.DatabaseManager.MariaDb.IntegrationTests.DatabaseFixtures;
using Testcontainers.MariaDb;
using Xunit.Sdk;

namespace TcfOss.DatabaseManager.MariaDb.IntegrationTests.DefinitionMapping;

// ReSharper disable ClassNeverInstantiated.Global
public class DifferFixture_MariaDb_11_08_02 : DifferFixtureMaBase<MariaDbBuilder, MariaDbContainer>
{
    public DifferFixture_MariaDb_11_08_02(IMessageSink messageSink)
    {
        DbFixture = new MariaDbFixture_11_08_02(messageSink);
    }
}
