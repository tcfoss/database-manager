using TcfOss.DatabaseManager.MySql.IntegrationTests.DatabaseFixtures;
using Testcontainers.MySql;
using Xunit.Sdk;

namespace TcfOss.DatabaseManager.MySql.IntegrationTests.DefinitionMapping;

// ReSharper disable once UnusedMember.Global
public class DifferTests_MySql_09_07 : DifferTests<MySqlBuilder, MySqlContainer, DifferTests_MySql_09_07.ThisFixture>
{
    protected override string CharacterSet => "utf8mb4";
    protected override string Collation => "utf8mb4_0900_ai_ci";
    protected override string OnDeleteRestrict => " ON DELETE RESTRICT";
    protected override string IntWidth => "";

    public DifferTests_MySql_09_07(ThisFixture fixture)
    {
        Fixture = fixture;
    }

    public class ThisFixture : DifferFixture<MySqlBuilder, MySqlContainer>
    {
        protected override bool RemoveSlashesBeforeQuotesGenerationExpression => true;
        public ThisFixture(IMessageSink messageSink)
        {
            DbFixture = new MySqlFixture_09_07(messageSink);
        }
    }

}
