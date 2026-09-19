using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.IntegrationTests;
using TcfOss.DatabaseManager.MariaDb.IntegrationTests.DatabaseFixtures;
using TcfOss.DatabaseManager.MySql.IntegrationTests.DownloadSchema;
using Testcontainers.MariaDb;
using Xunit.Sdk;

namespace TcfOss.DatabaseManager.MariaDb.IntegrationTests.DownloadSchema;

// ReSharper disable once UnusedMember.Global
public class DownloadSchemaTests_MariaDb_10_11 : DownloadSchemaTests<MariaDbBuilder, MariaDbContainer, DownloadSchemaTests_MariaDb_10_11.ThisFixture>
{
    public DownloadSchemaTests_MariaDb_10_11(ThisFixture fixture)
    {
        Fixture = fixture;
    }

    protected override string DefaultCharacterSetAndCollation => "CHARACTER SET latin1 COLLATE latin1_swedish_ci";
    protected override string DefaultFunctionParameterDirection => "IN ";
    protected override string DefaultIntegerWidthString => "(11)";

    public class ThisFixture : DownloadSchemaFixture<MariaDbBuilder, MariaDbContainer>
    {
        protected override SqlDialect Dialect => SqlDialect.MariaDb;

        public ThisFixture(IMessageSink messageSink)
        {
            DbFixture = new MariaDbFixture_10_11(messageSink);
            FsProjectFixture = new FsProjectFixture();
        }
    }

}
