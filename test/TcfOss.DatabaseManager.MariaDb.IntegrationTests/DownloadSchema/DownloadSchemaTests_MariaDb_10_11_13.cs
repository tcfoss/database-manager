using TcfOss.DatabaseManager.MySql.IntegrationTests.DownloadSchema;
using Testcontainers.MariaDb;

namespace TcfOss.DatabaseManager.MariaDb.IntegrationTests.DownloadSchema;

// ReSharper disable once UnusedMember.Global
public class DownloadSchemaTests_MariaDb_10_11_13 : DownloadSchemaTests<MariaDbBuilder, MariaDbContainer, DownloadSchemaFixture_MariaDb_10_11_13>
{
    public DownloadSchemaTests_MariaDb_10_11_13(DownloadSchemaFixture_MariaDb_10_11_13 fixture)
    {
        Fixture = fixture;
    }

    protected override string DefaultCharacterSetAndCollation => "CHARACTER SET latin1 COLLATE latin1_swedish_ci";
    protected override string DefaultFunctionParameterDirection => "IN ";
    protected override string DefaultIntegerWidthString => "(11)";
}
