namespace TcfOss.DatabaseManager.App.IntegrationTests.MsSql;

// ReSharper disable once UnusedMember.Global
public class CliReadOnlyTests_MsSql_2022
    : CliReadOnlyTests_MsSql<CliReadOnlyFixture_MsSql_2022>
{
    public CliReadOnlyTests_MsSql_2022(CliReadOnlyFixture_MsSql_2022 fixture, ITestOutputHelper testOutputHelper)
    {
        Fixture = fixture;
        TestOutputHelper = testOutputHelper;
    }
}
