using TcfOss.DatabaseManager.MsSql.IntegrationTests.DatabaseFixtures;
using Testcontainers.MsSql;
using Xunit.Sdk;

namespace TcfOss.DatabaseManager.App.IntegrationTests.MsSql;

// ReSharper disable ClassNeverInstantiated.Global
public class CliReadOnlyFixture_MsSql_2025 : CliReadOnlyFixture_MsSql<MsSqlBuilder, MsSqlContainer>
{
    public CliReadOnlyFixture_MsSql_2025(IMessageSink messageSink)
    {
        DbFixture = new MsSqlFixture_2025(messageSink);
    }
}
