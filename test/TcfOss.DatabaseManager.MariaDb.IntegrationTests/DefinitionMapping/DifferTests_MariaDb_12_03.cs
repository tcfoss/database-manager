using TcfOss.DatabaseManager.MariaDb.IntegrationTests.DatabaseFixtures;
using TcfOss.DatabaseManager.MySql.IntegrationTests.DefinitionMapping;
using Testcontainers.MariaDb;
using Xunit.Sdk;

namespace TcfOss.DatabaseManager.MariaDb.IntegrationTests.DefinitionMapping;

// ReSharper disable once UnusedMember.Global
public class DifferTests_MariaDb_12_03 : DifferTests<MariaDbBuilder, MariaDbContainer, DifferTests_MariaDb_12_03.ThisFixture>
{
    protected override string CharacterSet => "utf8mb4";
    protected override string Collation => "utf8mb4_uca1400_ai_ci";
    protected override string OnDeleteRestrict => "";
    protected override string IntWidth => "(11)";
    protected override string FunctionParameterDirection => "IN ";
    protected override string CteDeclName => "my_cte";

    public DifferTests_MariaDb_12_03(ThisFixture fixture)
    {
        Fixture = fixture;
    }

    public class ThisFixture : DifferFixtureMaBase<MariaDbBuilder, MariaDbContainer>
    {
        public ThisFixture(IMessageSink messageSink)
        {
            DbFixture = new MariaDbFixture_12_03(messageSink);
        }
    }

}
