using TcfOss.DatabaseManager.MySql.IntegrationTests.DatabaseFixtures;
using Testcontainers.MySql;
using Xunit.Sdk;

namespace TcfOss.DatabaseManager.MySql.IntegrationTests.DefinitionMapping;

// ReSharper disable ClassNeverInstantiated.Global
public class DifferFixture_MySql_08_04_06 : DifferFixture<MySqlBuilder, MySqlContainer>
{
    protected override bool RemoveSlashesBeforeQuotesGenerationExpression => true;
    public DifferFixture_MySql_08_04_06(IMessageSink messageSink)
    {
        DbFixture = new MySqlFixture_08_04_06(messageSink);
    }
}
