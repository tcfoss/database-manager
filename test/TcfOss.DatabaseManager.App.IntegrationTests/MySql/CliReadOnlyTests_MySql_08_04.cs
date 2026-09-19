using TcfOss.DatabaseManager.App.IntegrationTests.MySqlFamily;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;

namespace TcfOss.DatabaseManager.App.IntegrationTests.MySql;

// ReSharper disable once UnusedMember.Global
public class CliReadOnlyTests_MySql_08_04 : CliReadOnlyTests<CliReadOnlyFixture_MySql_08_04>
{
    protected override SqlDialect Dialect => SqlDialect.MySql;
    protected override string IfConditionBegin => "(";
    protected override string IfConditionEnd => ")";

    public CliReadOnlyTests_MySql_08_04(CliReadOnlyFixture_MySql_08_04 fixture, ITestOutputHelper testOutputHelper)
    {
        Fixture = fixture;
        TestOutputHelper = testOutputHelper;
    }
}
