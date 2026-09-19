using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.MariaDb.IntegrationTests.DatabaseFixtures;
using TcfOss.DatabaseManager.MySql.IntegrationTests.DefinitionMapping;
using Testcontainers.MariaDb;
using Xunit.Sdk;

namespace TcfOss.DatabaseManager.MariaDb.IntegrationTests.DefinitionMapping;

public sealed class SimpleSchemaDifferFixture_MariaDb_11_08
    : SimpleSchemaDifferFixture<MariaDbBuilder, MariaDbContainer>
{
    protected override SqlDialect Dialect => SqlDialect.MariaDb;

    public SimpleSchemaDifferFixture_MariaDb_11_08(IMessageSink messageSink)
    {
        DbFixture = new MariaDbFixture_11_08_02(messageSink);
    }
}
