using TcfOss.DatabaseManager.MySql.IntegrationTests.DefinitionMapping;
using Testcontainers.MariaDb;

namespace TcfOss.DatabaseManager.MariaDb.IntegrationTests.DefinitionMapping;

// ReSharper disable once UnusedMember.Global
public sealed class SimpleSchemaDifferTests_MariaDb_10_11
    : SimpleSchemaDifferTests<MariaDbBuilder, MariaDbContainer, SimpleSchemaDifferFixture_MariaDb_10_11>
{
    public SimpleSchemaDifferTests_MariaDb_10_11(SimpleSchemaDifferFixture_MariaDb_10_11 fixture)
    {
        Fixture = fixture;
    }
}
