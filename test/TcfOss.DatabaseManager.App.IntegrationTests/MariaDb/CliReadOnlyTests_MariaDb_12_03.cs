using TcfOss.DatabaseManager.App.IntegrationTests.MySqlFamily;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;

namespace TcfOss.DatabaseManager.App.IntegrationTests.MariaDb;

// ReSharper disable once UnusedMember.Global
public class CliReadOnlyTests_MariaDb_12_03 : CliReadOnlyTests<CliReadOnlyFixture_MariaDb_12_03>
{
    protected override SqlDialect Dialect => SqlDialect.MariaDb;
    protected override string IfConditionBegin => "";
    protected override string IfConditionEnd => "";
    protected override uint? DefaultIntWidth => 11;
    protected override string DefaultParameterDirection => "IN ";
    protected override string DefaultCollation => "utf8mb4_uca1400_ai_ci";

    public CliReadOnlyTests_MariaDb_12_03(CliReadOnlyFixture_MariaDb_12_03 fixture, ITestOutputHelper testOutputHelper)
    {
        Fixture = fixture;
        TestOutputHelper = testOutputHelper;
    }
}
