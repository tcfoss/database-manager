using TcfOss.DatabaseManager.App.IntegrationTests.MySqlFamily;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;

namespace TcfOss.DatabaseManager.App.IntegrationTests.MariaDb;

// ReSharper disable once UnusedMember.Global
public class CliReadOnlyTests_MariaDb_NoDatabase(
    CliReadOnlyFixture_NoDatabase fixture,
    ITestOutputHelper testOutputHelper)
    : CliReadOnlyTests_NoDatabase(fixture, testOutputHelper)
{
    protected override SqlDialect Dialect => SqlDialect.MariaDb;
    protected override uint? DefaultIntWidth => 11;
    protected override string DefaultCollation => "utf8mb4_uca1400_ai_ci";
    protected override string IfConditionBegin => "";
    protected override string IfConditionEnd => "";
}
