using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.IntegrationTests;
using TcfOss.DatabaseManager.MariaDb.IntegrationTests.DatabaseFixtures;
using TcfOss.DatabaseManager.MySql.IntegrationTests.DownloadSchema;
using Testcontainers.MariaDb;
using Xunit.Sdk;

namespace TcfOss.DatabaseManager.MariaDb.IntegrationTests.DownloadSchema;

// ReSharper disable ClassNeverInstantiated.Global
public class DownloadSchemaFixture_MariaDb_11_08_02 : DownloadSchemaFixture<MariaDbBuilder, MariaDbContainer>
{
    protected override SqlDialect Dialect => SqlDialect.MariaDb;

    public DownloadSchemaFixture_MariaDb_11_08_02(IMessageSink messageSink)
    {
        DbFixture = new MariaDbFixture_11_08(messageSink);
        FsProjectFixture = new FsProjectFixture();
    }
}
