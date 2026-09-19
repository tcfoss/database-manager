using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.IntegrationTests;
using TcfOss.DatabaseManager.MySql.IntegrationTests.DatabaseFixtures;
using Testcontainers.MySql;
using Xunit.Sdk;

namespace TcfOss.DatabaseManager.MySql.IntegrationTests.DownloadSchema;

// ReSharper disable once UnusedMember.Global
public class DownloadSchemaTests_MySql_08_04 : DownloadSchemaTests<MySqlBuilder, MySqlContainer, DownloadSchemaTests_MySql_08_04.ThisFixture>
{
    protected override string IfClauseOpen => "(";
    protected override string IfClauseClose => ")";

    public DownloadSchemaTests_MySql_08_04(ThisFixture fixture)
    {
        Fixture = fixture;
    }


    public class ThisFixture : DownloadSchemaFixture<MySqlBuilder, MySqlContainer>
    {
        protected override bool RemoveSlashesBeforeQuotesGenerationExpression => true;
        protected override SqlDialect Dialect => SqlDialect.MySql;

        public ThisFixture(IMessageSink messageSink)
        {
            DbFixture = new MySqlFixture_08_04(messageSink);
            FsProjectFixture = new FsProjectFixture();
        }
    }

}
