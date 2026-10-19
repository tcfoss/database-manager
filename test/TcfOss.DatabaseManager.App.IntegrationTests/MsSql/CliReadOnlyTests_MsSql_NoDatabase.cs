namespace TcfOss.DatabaseManager.App.IntegrationTests.MsSql;

// ReSharper disable once UnusedMember.Global
public class CliReadOnlyTests_MsSql_NoDatabase
    : CliReadOnlyTests_MsSql<CliReadOnlyFixture_MsSql_FakeDb>
{
    public CliReadOnlyTests_MsSql_NoDatabase(CliReadOnlyFixture_MsSql_FakeDb fixture, ITestOutputHelper testOutputHelper)
    {
        Fixture = fixture;
        TestOutputHelper = testOutputHelper;
    }

    protected override bool ConnectionAvailable => false;
}
