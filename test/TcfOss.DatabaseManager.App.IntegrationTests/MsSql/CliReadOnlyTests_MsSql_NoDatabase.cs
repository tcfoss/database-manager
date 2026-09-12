namespace TcfOss.DatabaseManager.App.IntegrationTests.MsSql;

#pragma warning disable IDE0060 // Remove unused parameter

// ReSharper disable once UnusedMember.Global
public class CliReadOnlyTests_MsSql_NoDatabase
    : CliReadOnlyTests_MsSql<CliReadOnlyFixture_MsSql_FakeDb>
{
    // ReSharper disable once UnusedParameter.Local
    public CliReadOnlyTests_MsSql_NoDatabase(CliReadOnlyFixture_MsSql_FakeDb fixture, ITestOutputHelper testOutputHelper)
    {
        TestOutputHelper = testOutputHelper;
    }
}
