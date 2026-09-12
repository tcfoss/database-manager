using TcfOss.DatabaseManager.Core.Configuration.Attributes;

namespace TcfOss.DatabaseManager.App.IntegrationTests.MariaDb;

// ReSharper disable once UnusedMember.Global
public class CliReadOnlyTests_MariaDb_10_11
    : CliReadOnlyTests<CliReadOnlyFixture_MariaDb_10_11>
{
    protected override SqlDialect Dialect => SqlDialect.MariaDb;
    protected override string IfConditionBegin => "";
    protected override string IfConditionEnd => "";
    protected override uint? DefaultIntWidth => 11;
    protected override string DefaultParameterDirection => "IN ";
    protected override string DefaultCharset => "latin1";
    protected override string DefaultCollation => "latin1_swedish_ci";

    public CliReadOnlyTests_MariaDb_10_11(CliReadOnlyFixture_MariaDb_10_11 fixture, ITestOutputHelper testOutputHelper)
    {
        Fixture = fixture;
        TestOutputHelper = testOutputHelper;
    }
}
