using Testcontainers.MsSql;
using Xunit.Sdk;

namespace TcfOss.DatabaseManager.MsSql.IntegrationTests.DatabaseFixtures;

public sealed class MsSqlFixture_2025(IMessageSink messageSink)
    : DbFixture<MsSqlBuilder, MsSqlContainer>(messageSink)
{
    protected override MsSqlBuilder Configure()
    {
        return new MsSqlBuilder("mcr.microsoft.com/mssql/server:2025-CU9-ubuntu-22.04")
            .WithStandardOptions();
    }
}
