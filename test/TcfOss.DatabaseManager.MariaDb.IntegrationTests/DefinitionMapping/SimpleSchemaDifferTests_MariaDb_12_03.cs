using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.MariaDb.IntegrationTests.DatabaseFixtures;
using TcfOss.DatabaseManager.MySql.IntegrationTests.DefinitionMapping;
using Testcontainers.MariaDb;
using Xunit.Sdk;

namespace TcfOss.DatabaseManager.MariaDb.IntegrationTests.DefinitionMapping;

// ReSharper disable once UnusedMember.Global
public sealed class SimpleSchemaDifferTests_MariaDb_12_03
    : SimpleSchemaDifferTests<MariaDbBuilder, MariaDbContainer, SimpleSchemaDifferTests_MariaDb_12_03.ThisFixture>
{
    public SimpleSchemaDifferTests_MariaDb_12_03(ThisFixture fixture)
    {
        Fixture = fixture;
    }

    public sealed class ThisFixture
        : SimpleSchemaDifferFixture<MariaDbBuilder, MariaDbContainer>
    {
        protected override SqlDialect Dialect => SqlDialect.MariaDb;

        public ThisFixture(IMessageSink messageSink)
        {
            DbFixture = new MariaDbFixture_12_03(messageSink);
        }
    }

}
