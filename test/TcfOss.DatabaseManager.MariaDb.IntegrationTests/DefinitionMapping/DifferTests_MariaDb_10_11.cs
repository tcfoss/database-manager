using TcfOss.DatabaseManager.MySql.IntegrationTests.DefinitionMapping;
using Testcontainers.MariaDb;

namespace TcfOss.DatabaseManager.MariaDb.IntegrationTests.DefinitionMapping;

// ReSharper disable once UnusedMember.Global
public class DifferTests_MariaDb_10_11 : DifferTests<MariaDbBuilder, MariaDbContainer, DifferFixture_MariaDb_10_11>
{
    protected override string CharacterSet => "latin1";
    protected override string Collation => "latin1_swedish_ci";
    protected override string OnDeleteRestrict => "";
    protected override string IntWidth => "(11)";
    protected override string FunctionParameterDirection => "IN ";
    protected override string CteDeclName => "my_cte";

    public DifferTests_MariaDb_10_11(DifferFixture_MariaDb_10_11 fixture)
    {
        Fixture = fixture;
    }
}
