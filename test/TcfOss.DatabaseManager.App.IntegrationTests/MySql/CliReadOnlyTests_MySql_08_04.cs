using TcfOss.DatabaseManager.App.IntegrationTests.MySqlFamily;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.MySql.IntegrationTests.DatabaseFixtures;
using Testcontainers.MySql;
using Xunit.Sdk;

namespace TcfOss.DatabaseManager.App.IntegrationTests.MySql;

// ReSharper disable once UnusedMember.Global
public class CliReadOnlyTests_MySql_08_04 : CliReadOnlyTests<CliReadOnlyTests_MySql_08_04.ThisFixture>
{
    protected override SqlDialect Dialect => SqlDialect.MySql;
    protected override string IfConditionBegin => "(";
    protected override string IfConditionEnd => ")";

    public CliReadOnlyTests_MySql_08_04(ThisFixture fixture, ITestOutputHelper testOutputHelper)
    {
        Fixture = fixture;
        TestOutputHelper = testOutputHelper;
    }

    public class ThisFixture : CliReadOnlyFixture<MySqlBuilder, MySqlContainer>
    {
        public ThisFixture(IMessageSink messageSink)
        {
            DbFixture = new MySqlFixture_08_04(messageSink);
        }
    }
}
