using Testcontainers.MsSql;
using Xunit.Sdk;

namespace TcfOss.DatabaseManager.MsSql.IntegrationTests.DatabaseFixtures;

public sealed class MsSqlFixture_2022(IMessageSink messageSink)
    : DbFixture<MsSqlBuilder, MsSqlContainer>(messageSink)
{
    protected override MsSqlBuilder Configure()
    {
        return new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
            .WithStandardOptions();
    }
}
