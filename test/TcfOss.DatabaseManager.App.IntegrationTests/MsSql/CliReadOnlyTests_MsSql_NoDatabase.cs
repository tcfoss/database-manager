namespace TcfOss.DatabaseManager.App.IntegrationTests.MsSql;

// ReSharper disable once UnusedMember.Global
public class CliReadOnlyTests_MsSql_NoDatabase
    : CliReadOnlyTests_MsSql<CliReadOnlyFixture_MsSql_FakeDb>
{
    public static new bool SkipTests => false;

    public CliReadOnlyTests_MsSql_NoDatabase(CliReadOnlyFixture_MsSql_FakeDb fixture)
    {
        Fixture = fixture;
    }

    protected override bool ConnectionAvailable => false;
}
