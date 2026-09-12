using TcfOss.DatabaseManager.MySql.IntegrationTests;
using TcfOss.DatabaseManager.MySql.IntegrationTests.DatabaseFixtures;
using Testcontainers.MariaDb;
using Xunit.Sdk;

namespace TcfOss.DatabaseManager.MariaDb.IntegrationTests.DatabaseFixtures;

public sealed class MariaDbFixture_11_08_02(IMessageSink messageSink)
    : DbFixture<MariaDbBuilder, MariaDbContainer>(messageSink)
{
    protected override ushort Port => MariaDbBuilder.MariaDbPort;

    protected override MariaDbBuilder Configure()
    {
        return new MariaDbBuilder("mariadb:11.8.2")
            .WithStandardOptions();
        // .WithImage("mariadb:11.8.2")
        // .WithPrivileged(true)
        // .WithPortBinding(3306, true)
        // .WithUsername("root")
        // .WithPassword("mypassword")
        // .WithDatabase("information_schema")
        // .WithEnvironment("MYSQL_ROOT_PASSWORD", "mypassword")

        // .WithResourceMapping(
        // InitialSourceDirectory,
        // "/InitialSchemas")
        // .WithCommand("mariadb < /root/library_schema.sql")
        // .WithResourceMapping(y, "/docker-entrypoint-initdb.d")
        // .WithResourceMapping(x, "/docker-entrypoint-initdb.d")
        // .WithWaitStrategy(Wait.ForUnixContainer().UntilDatabaseIsAvailable(DbProviderFactory));
        // .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted("mariadb < /root/library_schema.sql"))
    }
}

