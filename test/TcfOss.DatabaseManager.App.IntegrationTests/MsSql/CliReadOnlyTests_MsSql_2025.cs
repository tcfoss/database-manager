namespace TcfOss.DatabaseManager.App.IntegrationTests.MsSql;

// ReSharper disable once UnusedMember.Global
public class CliReadOnlyTests_MsSql_2025
    : CliReadOnlyTests_MsSql<CliReadOnlyFixture_MsSql_2025>
{
    protected override int MajorVersion => 17;

    public CliReadOnlyTests_MsSql_2025(CliReadOnlyFixture_MsSql_2025 fixture, ITestOutputHelper testOutputHelper)
    {
        Fixture = fixture;
        TestOutputHelper = testOutputHelper;
    }
}
