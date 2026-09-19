using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.IntegrationTests;
using TcfOss.DatabaseManager.MySql.IntegrationTests.DatabaseFixtures;
using Testcontainers.MySql;
using Xunit.Sdk;

namespace TcfOss.DatabaseManager.MySql.IntegrationTests.DownloadSchema;

// ReSharper disable ClassNeverInstantiated.Global
public class DownloadSchemaFixture_MySql_08_04_06 : DownloadSchemaFixture<MySqlBuilder, MySqlContainer>
{
    protected override bool RemoveSlashesBeforeQuotesGenerationExpression => true;
    protected override SqlDialect Dialect => SqlDialect.MySql;

    public DownloadSchemaFixture_MySql_08_04_06(IMessageSink messageSink)
    {
        DbFixture = new MySqlFixture_08_04(messageSink);
        FsProjectFixture = new FsProjectFixture();
    }
}
