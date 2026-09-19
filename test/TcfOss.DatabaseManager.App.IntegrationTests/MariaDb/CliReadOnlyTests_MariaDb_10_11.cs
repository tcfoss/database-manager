using TcfOss.DatabaseManager.App.IntegrationTests.MySqlFamily;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.MariaDb.IntegrationTests.DatabaseFixtures;
using Testcontainers.MariaDb;
using Xunit.Sdk;

namespace TcfOss.DatabaseManager.App.IntegrationTests.MariaDb;

// ReSharper disable once UnusedMember.Global
public class CliReadOnlyTests_MariaDb_10_11
    : CliReadOnlyTests<CliReadOnlyTests_MariaDb_10_11.ThisFixture>
{
    protected override SqlDialect Dialect => SqlDialect.MariaDb;
    protected override string IfConditionBegin => "";
    protected override string IfConditionEnd => "";
    protected override uint? DefaultIntWidth => 11;
    protected override string DefaultParameterDirection => "IN ";
    protected override string DefaultCharset => "latin1";
    protected override string DefaultCollation => "latin1_swedish_ci";

    public CliReadOnlyTests_MariaDb_10_11(ThisFixture fixture, ITestOutputHelper testOutputHelper)
    {
        Fixture = fixture;
        TestOutputHelper = testOutputHelper;
    }

    public class ThisFixture : CliReadOnlyFixture<MariaDbBuilder, MariaDbContainer>
    {
        public ThisFixture(IMessageSink messageSink)
        {
            DbFixture = new MariaDbFixture_10_11(messageSink);
        }
    }
}
