using Testcontainers.MySql;

namespace TcfOss.DatabaseManager.MySql.IntegrationTests.DownloadSchema;

// ReSharper disable once UnusedMember.Global
public class DownloadSchemaTests_MySql_08_04_06 : DownloadSchemaTests<MySqlBuilder, MySqlContainer, DownloadSchemaFixture_MySql_08_04_06>
{
    protected override string IfClauseOpen => "(";
    protected override string IfClauseClose => ")";

    public DownloadSchemaTests_MySql_08_04_06(DownloadSchemaFixture_MySql_08_04_06 fixture)
    {
        Fixture = fixture;
    }
}
