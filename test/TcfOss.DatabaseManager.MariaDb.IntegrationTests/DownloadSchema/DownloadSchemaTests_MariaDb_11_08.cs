using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.IntegrationTests;
using TcfOss.DatabaseManager.MariaDb.IntegrationTests.DatabaseFixtures;
using TcfOss.DatabaseManager.MySql.IntegrationTests.DownloadSchema;
using Testcontainers.MariaDb;
using Xunit.Sdk;

namespace TcfOss.DatabaseManager.MariaDb.IntegrationTests.DownloadSchema;

// ReSharper disable once UnusedMember.Global
public class DownloadSchemaTests_MariaDb_11_08 : DownloadSchemaTests<MariaDbBuilder, MariaDbContainer, DownloadSchemaTests_MariaDb_11_08.ThisFixture>
{
    public DownloadSchemaTests_MariaDb_11_08(ThisFixture fixture)
    {
        Fixture = fixture;
    }

    protected override string DefaultCharacterSetAndCollation => "CHARACTER SET utf8mb4 COLLATE utf8mb4_uca1400_ai_ci";
    protected override string DefaultFunctionParameterDirection => "IN ";
    protected override string DefaultIntegerWidthString => "(11)";

    public class ThisFixture : DownloadSchemaFixture<MariaDbBuilder, MariaDbContainer>
    {
        protected override SqlDialect Dialect => SqlDialect.MariaDb;

        public ThisFixture(IMessageSink messageSink)
        {
            DbFixture = new MariaDbFixture_11_08(messageSink);
            FsProjectFixture = new FsProjectFixture();
        }
    }

}
