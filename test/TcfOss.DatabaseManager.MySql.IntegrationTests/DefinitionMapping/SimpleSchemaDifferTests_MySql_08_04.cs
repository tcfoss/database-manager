using Testcontainers.MySql;

namespace TcfOss.DatabaseManager.MySql.IntegrationTests.DefinitionMapping;

// ReSharper disable once UnusedMember.Global
public sealed class SimpleSchemaDifferTests_MySql_08_04
    : SimpleSchemaDifferTests<MySqlBuilder, MySqlContainer, SimpleSchemaDifferFixture_MySql_08_04>
{
    public SimpleSchemaDifferTests_MySql_08_04(SimpleSchemaDifferFixture_MySql_08_04 fixture)
    {
        Fixture = fixture;
    }
}
