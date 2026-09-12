using TcfOss.DatabaseManager.MySql.IntegrationTests.DownloadSchema;
using Testcontainers.MariaDb;

namespace TcfOss.DatabaseManager.MariaDb.IntegrationTests.DownloadSchema;

// ReSharper disable once UnusedMember.Global
public class DownloadSchemaTests_MariaDb_11_08_02 : DownloadSchemaTests<MariaDbBuilder, MariaDbContainer, DownloadSchemaFixture_MariaDb_11_08_02>
{
    public DownloadSchemaTests_MariaDb_11_08_02(DownloadSchemaFixture_MariaDb_11_08_02 fixture)
    {
        Fixture = fixture;
    }

    protected override string DefaultCharacterSetAndCollation => "CHARACTER SET utf8mb4 COLLATE utf8mb4_uca1400_ai_ci";
    protected override string DefaultFunctionParameterDirection => "IN ";
    protected override string DefaultIntegerWidthString => "(11)";
}
