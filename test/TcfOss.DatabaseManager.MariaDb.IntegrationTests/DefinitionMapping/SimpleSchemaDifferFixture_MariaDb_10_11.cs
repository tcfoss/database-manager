using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.MariaDb.IntegrationTests.DatabaseFixtures;
using TcfOss.DatabaseManager.MySql.IntegrationTests.DefinitionMapping;
using Testcontainers.MariaDb;
using Xunit.Sdk;

namespace TcfOss.DatabaseManager.MariaDb.IntegrationTests.DefinitionMapping;

public sealed class SimpleSchemaDifferFixture_MariaDb_10_11
    : SimpleSchemaDifferFixture<MariaDbBuilder, MariaDbContainer>
{
    protected override SqlDialect Dialect => SqlDialect.MariaDb;

    public SimpleSchemaDifferFixture_MariaDb_10_11(IMessageSink messageSink)
    {
        DbFixture = new MariaDbFixture_10_11_13(messageSink);
    }
}
