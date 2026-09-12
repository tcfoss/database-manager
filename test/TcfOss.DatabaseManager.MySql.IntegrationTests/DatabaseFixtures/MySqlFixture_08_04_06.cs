using Testcontainers.MySql;
using Xunit.Sdk;

namespace TcfOss.DatabaseManager.MySql.IntegrationTests.DatabaseFixtures;

public sealed class MySqlFixture_08_04_06(IMessageSink messageSink)
    : DbFixture<MySqlBuilder, MySqlContainer>(messageSink)
{
    protected override ushort Port => MySqlBuilder.MySqlPort;

    protected override MySqlBuilder Configure()
    {
        return new MySqlBuilder("mysql:8.4.6")
            .WithStandardOptions();
    }
}
