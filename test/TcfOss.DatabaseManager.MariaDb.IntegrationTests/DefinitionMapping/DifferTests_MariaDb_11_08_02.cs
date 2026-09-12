using TcfOss.DatabaseManager.MySql.IntegrationTests.DefinitionMapping;
using Testcontainers.MariaDb;

namespace TcfOss.DatabaseManager.MariaDb.IntegrationTests.DefinitionMapping;

// ReSharper disable once UnusedMember.Global
public class DifferTests_MariaDb_11_08_02 : DifferTests<MariaDbBuilder, MariaDbContainer, DifferFixture_MariaDb_11_08_02>
{
    protected override string CharacterSet => "utf8mb4";
    protected override string Collation => "utf8mb4_uca1400_ai_ci";
    protected override string OnDeleteRestrict => "";
    protected override string IntWidth => "(11)";
    protected override string FunctionParameterDirection => "IN ";
    protected override string CteDeclName => "my_cte";

    public DifferTests_MariaDb_11_08_02(DifferFixture_MariaDb_11_08_02 fixture)
    {
        Fixture = fixture;
    }
}
