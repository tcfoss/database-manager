using TcfOss.DatabaseManager.MsSql.IntegrationTests.DatabaseFixtures;
using Testcontainers.MsSql;
using Xunit.Sdk;

namespace TcfOss.DatabaseManager.App.IntegrationTests.MsSql;

// ReSharper disable ClassNeverInstantiated.Global
public class CliReadOnlyFixture_MsSql_2022 : CliReadOnlyFixture_MsSql<MsSqlBuilder, MsSqlContainer>
{
    public CliReadOnlyFixture_MsSql_2022(IMessageSink messageSink)
    {
        DbFixture = new MsSqlFixture_2022(messageSink);
    }
}
