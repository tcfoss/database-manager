using TcfOss.DatabaseManager.Core.IntegrationTests;
using Testcontainers.MySql;
using Xunit.Sdk;

namespace TcfOss.DatabaseManager.MySql.IntegrationTests.DatabaseFixtures;

public sealed class MySqlFixture_09_07(IMessageSink messageSink)
    : DbFixture<MySqlBuilder, MySqlContainer>(messageSink)
{
    protected override ushort Port => MySqlBuilder.MySqlPort;

    protected override MySqlBuilder Configure()
    {
        return new MySqlBuilder(FixtureImages.MySql_09_07)
            .WithStandardOptions();
    }
}
