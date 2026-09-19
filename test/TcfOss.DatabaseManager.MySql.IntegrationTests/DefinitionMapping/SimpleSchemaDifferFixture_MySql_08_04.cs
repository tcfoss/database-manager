using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.MySql.IntegrationTests.DatabaseFixtures;
using Testcontainers.MySql;
using Xunit.Sdk;

namespace TcfOss.DatabaseManager.MySql.IntegrationTests.DefinitionMapping;

public sealed class SimpleSchemaDifferFixture_MySql_08_04
    : SimpleSchemaDifferFixture<MySqlBuilder, MySqlContainer>
{
    protected override SqlDialect Dialect => SqlDialect.MySql;

    public SimpleSchemaDifferFixture_MySql_08_04(IMessageSink messageSink)
    {
        DbFixture = new MySqlFixture_08_04(messageSink);
    }
}
