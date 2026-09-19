using TcfOss.DatabaseManager.MySql.IntegrationTests.DefinitionMapping;
using Testcontainers.MariaDb;

namespace TcfOss.DatabaseManager.MariaDb.IntegrationTests.DefinitionMapping;

// ReSharper disable once UnusedMember.Global
public sealed class SimpleSchemaDifferTests_MariaDb_11_08
    : SimpleSchemaDifferTests<MariaDbBuilder, MariaDbContainer, SimpleSchemaDifferFixture_MariaDb_11_08>
{
    public SimpleSchemaDifferTests_MariaDb_11_08(SimpleSchemaDifferFixture_MariaDb_11_08 fixture)
    {
        Fixture = fixture;
    }
}
