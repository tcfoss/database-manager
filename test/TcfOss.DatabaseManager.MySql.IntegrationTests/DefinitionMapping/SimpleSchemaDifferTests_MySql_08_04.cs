using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.MySql.IntegrationTests.DatabaseFixtures;
using Testcontainers.MySql;
using Xunit.Sdk;

namespace TcfOss.DatabaseManager.MySql.IntegrationTests.DefinitionMapping;

// ReSharper disable once UnusedMember.Global
public sealed class SimpleSchemaDifferTests_MySql_08_04
    : SimpleSchemaDifferTests<MySqlBuilder, MySqlContainer, SimpleSchemaDifferTests_MySql_08_04.ThisFixture>
{
    public SimpleSchemaDifferTests_MySql_08_04(ThisFixture fixture)
    {
        Fixture = fixture;
    }

    public sealed class ThisFixture : SimpleSchemaDifferFixture<MySqlBuilder, MySqlContainer>
    {
        protected override SqlDialect Dialect => SqlDialect.MySql;

        public ThisFixture(IMessageSink messageSink)
        {
            DbFixture = new MySqlFixture_08_04(messageSink);
        }
    }

}
