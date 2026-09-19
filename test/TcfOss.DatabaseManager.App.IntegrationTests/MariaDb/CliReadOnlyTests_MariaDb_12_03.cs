using TcfOss.DatabaseManager.App.IntegrationTests.MySqlFamily;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.MariaDb.IntegrationTests.DatabaseFixtures;
using Testcontainers.MariaDb;
using Xunit.Sdk;

namespace TcfOss.DatabaseManager.App.IntegrationTests.MariaDb;

// ReSharper disable once UnusedMember.Global
public class CliReadOnlyTests_MariaDb_12_03 : CliReadOnlyTests<CliReadOnlyTests_MariaDb_12_03.ThisFixture>
{
    protected override SqlDialect Dialect => SqlDialect.MariaDb;
    protected override string IfConditionBegin => "";
    protected override string IfConditionEnd => "";
    protected override uint? DefaultIntWidth => 11;
    protected override string DefaultParameterDirection => "IN ";
    protected override string DefaultCollation => "utf8mb4_uca1400_ai_ci";

    public CliReadOnlyTests_MariaDb_12_03(ThisFixture fixture, ITestOutputHelper testOutputHelper)
    {
        Fixture = fixture;
        TestOutputHelper = testOutputHelper;
    }

    public class ThisFixture : CliReadOnlyFixture<MariaDbBuilder, MariaDbContainer>
    {
        public ThisFixture(IMessageSink messageSink)
        {
            DbFixture = new MariaDbFixture_12_03(messageSink);
        }
    }
}
